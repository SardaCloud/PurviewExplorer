// Copyright (C) 2026 Francesco Valerio Buccoli. Sarda Cloud Community.
// Licensed under the MIT License. See LICENSE in the repository root.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using SardaCloud.PurviewExplorer.Catalog;

namespace SardaCloud.PurviewExplorer.Data
{
    /// <summary>
    /// Application state: the data store, the live session and the load queue.
    /// Must be used from the UI thread; events are raised on the UI thread.
    /// </summary>
    public sealed class Workspace
    {
        private readonly LinkedList<string> _queue = new LinkedList<string>();

        // State each queued collection had before it was queued, restored if the queue is cancelled.
        private readonly Dictionary<string, LoadState> _stateBeforeQueue = new Dictionary<string, LoadState>(StringComparer.OrdinalIgnoreCase);

        private TaskCompletionSource<bool> _idle;
        private TaskCompletionSource<bool> _connectDone;
        private bool _processing;
        private int _generation;
        private int _batchTotal;
        private int _batchDone;
        private string _activity;

        public DataStore Store { get; private set; } = new DataStore();
        public IPurviewSession Session { get; private set; }
        public bool IsConnected => Session?.IsConnected == true;
        public bool IsConnecting { get; private set; }
        public bool IsLoading => _processing;
        public bool IsBusy => _processing || IsConnecting;
        public string Activity => _activity;
        public int BatchTotal => _batchTotal;
        public int BatchDone => _batchDone;

        /// <summary>The whole store was replaced (connected, snapshot opened...).</summary>
        public event EventHandler StoreReplaced;

        /// <summary>A collection's state or items changed.</summary>
        public event EventHandler<string> CollectionChanged;

        /// <summary>Connection, busy state or activity text changed.</summary>
        public event EventHandler StateChanged;

        public async Task ConnectAsync(ConnectOptions options)
        {
            if (IsBusy) throw new InvalidOperationException("Another operation is in progress.");
            if (Session != null) await DisconnectAsync();

            IsConnecting = true;
            _connectDone = new TaskCompletionSource<bool>();
            SetActivity("Connecting...");
            try
            {
                var progress = new Progress<string>(SetActivity);
                var session = await PurviewSession.ConnectAsync(options, PurviewCatalog.CmdletNames, progress);
                AttachSession(session, new SourceInfo
                {
                    Kind = SourceKind.Live,
                    Connected = true,
                    Organization = session.Organization,
                    Account = session.Account,
                    TenantId = session.TenantId,
                    Environment = ConnectOptions.EnvironmentName(session.Environment),
                    ModuleVersion = session.Module?.Version?.ToString(),
                    SearchOnlySession = session.IsSearchOnlySession,
                    CreatedUtc = DateTime.UtcNow,
                });
            }
            finally
            {
                IsConnecting = false;
                SetActivity(null);
                _connectDone.TrySetResult(true);
            }
        }

        /// <summary>Completes when a connection attempt in progress has finished (successfully or not).</summary>
        public Task WhenConnectFinishedAsync() => IsConnecting && _connectDone != null ? _connectDone.Task : Task.CompletedTask;

        /// <summary>Uses an already connected session (also used by tests with a fake session).</summary>
        internal void AttachSession(IPurviewSession session, SourceInfo source)
        {
            Session = session;
            ReplaceStore(new DataStore(source));
        }

        public async Task DisconnectAsync()
        {
            CancelQueue();
            var session = Session;
            Session = null;
            Store.Source.Connected = false;
            if (session != null)
            {
                SetActivity("Disconnecting...");
                try
                {
                    await session.DisconnectAsync();
                }
                catch (Exception ex) when (!(ex is OutOfMemoryException))
                {
                    Trace.WriteLine("Disconnect failed: " + ex);
                }
                finally
                {
                    session.Dispose();
                }
            }
            SetActivity(null);
        }

        /// <summary>Shows a snapshot (or demo) instead of live data. Call <see cref="DisconnectAsync"/> first if connected.</summary>
        public void ShowStore(DataStore store)
        {
            CancelQueue();
            ReplaceStore(store);
        }

        private void ReplaceStore(DataStore store)
        {
            Store = store;
            _stateBeforeQueue.Clear();
            StoreReplaced?.Invoke(this, EventArgs.Empty);
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Queues collections for loading from the live session.</summary>
        public void Enqueue(IEnumerable<string> keys, bool force = false, bool front = false)
        {
            if (!IsConnected) return;
            var list = keys.ToList();
            if (front) list.Reverse();
            foreach (var key in list)
            {
                if (PurviewCatalog.Find(key) == null) continue;
                var r = Store.Get(key);
                if (r.State == LoadState.Loading) continue;
                if (!force && r.State != LoadState.NotLoaded && r.State != LoadState.Queued) continue;

                var existing = _queue.Find(key);
                if (existing != null)
                {
                    if (!front) continue;
                    _queue.Remove(existing);
                    _queue.AddFirst(key);
                    continue;
                }
                if (front) _queue.AddFirst(key);
                else _queue.AddLast(key);
                _batchTotal++;
                if (!_stateBeforeQueue.ContainsKey(key)) _stateBeforeQueue[key] = r.State;
                r.State = LoadState.Queued;
                CollectionChanged?.Invoke(this, key);
            }
            if (!_processing && _queue.Count > 0) _ = ProcessQueueAsync();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Stops the running command and clears the queue; queued collections get their previous state back.</summary>
        public void CancelQueue()
        {
            _generation++;
            var cancelled = _queue.ToList();
            _queue.Clear();
            foreach (var key in cancelled)
            {
                var r = Store.Get(key);
                if (r.State == LoadState.Queued)
                {
                    r.State = _stateBeforeQueue.TryGetValue(key, out var previous) ? previous : LoadState.NotLoaded;
                    CollectionChanged?.Invoke(this, key);
                }
                _stateBeforeQueue.Remove(key);
            }
            Session?.Stop();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public Task WhenIdleAsync()
        {
            if (!_processing) return Task.CompletedTask;
            if (_idle == null) _idle = new TaskCompletionSource<bool>();
            return _idle.Task;
        }

        private async Task ProcessQueueAsync()
        {
            _processing = true;
            StateChanged?.Invoke(this, EventArgs.Empty);
            var store = Store;
            var session = Session;
            try
            {
                while (_queue.Count > 0 && session != null && ReferenceEquals(session, Session) && session.IsConnected && ReferenceEquals(store, Store))
                {
                    int generation = _generation;
                    var key = _queue.First.Value;
                    _queue.RemoveFirst();
                    var def = PurviewCatalog.Find(key);
                    var current = store.Get(key);
                    var before = _stateBeforeQueue.TryGetValue(key, out var s) ? s : LoadState.NotLoaded;
                    _stateBeforeQueue.Remove(key);
                    current.State = LoadState.Loading;
                    SetActivity($"Loading {def.Solution.Name} \u2013 {def.Name}...");
                    CollectionChanged?.Invoke(this, key);

                    CollectionResult result;
                    try
                    {
                        result = await FetchAsync(def, store, session, generation);
                    }
                    catch (Exception ex) when (!(ex is OutOfMemoryException))
                    {
                        result = new CollectionResult(key, LoadState.Failed) { Message = ex.Message, CommandLine = def.CommandLine, RetrievedUtc = DateTime.UtcNow };
                    }
                    if (!ReferenceEquals(store, Store)) break;
                    if (Cancelled(generation, session))
                    {
                        // Stopped or disconnected: keep what was there before (e.g. the data a refresh was replacing).
                        current.State = before;
                        CollectionChanged?.Invoke(this, key);
                        continue;
                    }
                    store.Set(result);
                    _batchDone++;
                    CollectionChanged?.Invoke(this, key);
                    StateChanged?.Invoke(this, EventArgs.Empty);
                }
            }
            finally
            {
                _processing = false;
                _batchDone = 0;
                _batchTotal = 0;
                SetActivity(null);
                var idle = _idle;
                _idle = null;
                idle?.TrySetResult(true);
            }
        }

        private bool Cancelled(int generation, IPurviewSession session) =>
            generation != _generation || !ReferenceEquals(session, Session) || !session.IsConnected;

        private async Task<CollectionResult> FetchAsync(CollectionDef def, DataStore store, IPurviewSession session, int generation)
        {
            var sw = Stopwatch.StartNew();
            var result = new CollectionResult(def.Key) { CommandLine = def.CommandLine, RetrievedUtc = DateTime.UtcNow };

            var exists = await session.CommandExistsAsync(def.Cmdlet);
            if (Cancelled(generation, session)) return Stopped(result);
            if (exists == null)
            {
                result.State = LoadState.Failed;
                result.Message = $"Couldn't determine whether {def.Cmdlet} is available. Press F5 to try again.";
                return result;
            }
            if (exists == false)
            {
                result.State = LoadState.Unavailable;
                result.Message = store.Source.SearchOnlySession == true
                    ? $"{def.Cmdlet} isn't available in this session. This is a search-only session (the workaround for guest accounts), " +
                      "which may not offer it; your account might also not have a Purview role that includes it, or the feature " +
                      "isn't available in this tenant."
                    : $"{def.Cmdlet} isn't available in this session. Your account might not have a Purview role that includes it, " +
                      "or the feature isn't available in this tenant.";
                return result;
            }

            var errors = new List<string>();
            if (def.ForEachCollections != null)
            {
                var parents = new List<JsonMap>();
                int parentsLoaded = 0;
                foreach (var parentKey in def.ForEachCollections)
                {
                    var parentDef = PurviewCatalog.Find(parentKey);
                    var parent = store.Get(parentKey);
                    if (parent.State != LoadState.Loaded)
                    {
                        var original = parent;
                        var before = original.State == LoadState.Queued
                            ? (_stateBeforeQueue.TryGetValue(parentKey, out var s) ? s : LoadState.NotLoaded)
                            : original.State;
                        var node = _queue.Find(parentKey);
                        if (node != null)
                        {
                            _queue.Remove(node);
                            _stateBeforeQueue.Remove(parentKey);
                            _batchDone++;
                        }
                        original.State = LoadState.Loading;
                        CollectionChanged?.Invoke(this, parentKey);
                        try
                        {
                            parent = await FetchAsync(parentDef, store, session, generation);
                        }
                        catch (Exception ex) when (!(ex is OutOfMemoryException))
                        {
                            parent = new CollectionResult(parentKey, LoadState.Failed) { Message = ex.Message, CommandLine = parentDef.CommandLine, RetrievedUtc = DateTime.UtcNow };
                        }
                        if (!ReferenceEquals(store, Store)) return Stopped(result);
                        if (Cancelled(generation, session))
                        {
                            original.State = before;
                            CollectionChanged?.Invoke(this, parentKey);
                            return Stopped(result);
                        }
                        store.Set(parent);
                        CollectionChanged?.Invoke(this, parentKey);
                    }
                    if (parent.State == LoadState.Loaded)
                    {
                        parentsLoaded++;
                        parents.AddRange(parent.Items);
                    }
                    else
                    {
                        errors.Add($"Couldn't load {parentDef.Name}, so its items weren't included: {parent.Message ?? parent.State.ToString()}");
                    }
                }

                var label = def.ForEachLabel ?? def.ForEachParameter;
                foreach (var parent in parents)
                {
                    var value = ValueFormatter.ToFlatText(parent[def.ForEachProperty]).Trim();
                    if (value.Length == 0) continue;
                    var args = def.Parameters.Concat(new[] { new KeyValuePair<string, object>(def.ForEachParameter, value) });
                    var r = await session.InvokeAsync(def.Cmdlet, args);
                    if (r.Stopped || Cancelled(generation, session)) return Stopped(result);
                    foreach (var item in r.Items)
                    {
                        item.Insert(0, label, value);
                        result.Items.Add(item);
                    }
                    if (r.TerminatingError != null) errors.Add(value + ": " + r.TerminatingError);
                    errors.AddRange(r.Errors.Select(e => value + ": " + e));
                    result.Warnings.AddRange(r.Warnings);
                }
                result.State = parentsLoaded == 0 && result.Items.Count == 0 ? LoadState.Failed : LoadState.Loaded;
            }
            else
            {
                var r = await session.InvokeAsync(def.Cmdlet, def.Parameters);
                if (r.Stopped || Cancelled(generation, session)) return Stopped(result);
                result.Items = r.Items;
                result.Warnings = r.Warnings;
                if (r.TerminatingError != null) errors.Add(r.TerminatingError);
                errors.AddRange(r.Errors);
                result.State = r.Items.Count == 0 && errors.Count > 0 ? LoadState.Failed : LoadState.Loaded;
            }

            var distinct = errors.Where(e => !string.IsNullOrWhiteSpace(e)).Distinct().ToList();
            result.Message = distinct.Count > 0 ? string.Join("\r\n", distinct) : null;
            result.DurationSeconds = sw.Elapsed.TotalSeconds;
            return result;
        }

        private static CollectionResult Stopped(CollectionResult result)
        {
            result.State = LoadState.Failed;
            result.Items.Clear();
            result.Message = "Stopped before the command completed. Press F5 to load it again.";
            return result;
        }

        private void SetActivity(string text)
        {
            _activity = text;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
