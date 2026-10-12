// Copyright (C) 2026 Francesco Valerio Buccoli. Sarda Cloud Community.
// Licensed under the MIT License. See LICENSE in the repository root.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management.Automation;
using System.Threading.Tasks;
using SardaCloud.PurviewExplorer.Catalog;
using SardaCloud.PurviewExplorer.Data;
using Xunit;

namespace SardaCloud.PurviewExplorer.Tests
{
    public class JsonTests
    {
        [Fact]
        public void RoundTripsAllValueKinds()
        {
            var map = new JsonMap
            {
                ["s"] = "quote \" backslash \\ newline \n tab \t unicode àé€ 😀 control \u0001",
                ["i"] = 42L,
                ["neg"] = -7L,
                ["d"] = 3.25,
                ["big"] = 1e300,
                ["t"] = true,
                ["f"] = false,
                ["n"] = null,
                ["list"] = new List<object> { 1L, "two", new JsonMap { ["three"] = 3L }, new List<object>() },
                ["empty"] = new JsonMap(),
            };
            foreach (var indented in new[] { true, false })
            {
                var parsed = (JsonMap)Json.Parse(Json.Serialize(map, indented));
                Assert.Equal(map["s"], parsed["s"]);
                Assert.Equal(42L, parsed["i"]);
                Assert.Equal(-7L, parsed["neg"]);
                Assert.Equal(3.25, parsed["d"]);
                Assert.Equal(1e300, parsed["big"]);
                Assert.Equal(true, parsed["t"]);
                Assert.Equal(false, parsed["f"]);
                Assert.True(parsed.ContainsKey("n"));
                Assert.Null(parsed["n"]);
                var list = (List<object>)parsed["list"];
                Assert.Equal(4, list.Count);
                Assert.Equal(3L, ((JsonMap)list[2])["three"]);
                Assert.Empty((List<object>)list[3]);
                Assert.Equal(0, ((JsonMap)parsed["empty"]).Count);
                Assert.Equal(map.Keys, parsed.Keys);
            }
        }

        [Fact]
        public void ParsesEscapesAndBom()
        {
            var v = (JsonMap)Json.Parse("\uFEFF { \"a\" : \"\\u00e8\\/\\r\" , \"B\":[ ] } ");
            Assert.Equal("è/\r", v["a"]);
            Assert.Equal("è/\r", v["A"]); // keys are case-insensitive
            Assert.Empty((List<object>)v["b"]);
        }

        [Theory]
        [InlineData("")]
        [InlineData("{")]
        [InlineData("{\"a\":}")]
        [InlineData("[1,]")]
        [InlineData("tru")]
        [InlineData("\"unterminated")]
        [InlineData("{} extra")]
        [InlineData("{'a':1}")]
        public void RejectsInvalidJson(string text)
        {
            Assert.Throws<JsonException>(() => Json.Parse(text));
        }

        [Fact]
        public void RejectsExcessiveNesting()
        {
            var deep = new string('[', 1000) + new string(']', 1000);
            Assert.Throws<JsonException>(() => Json.Parse(deep));
        }

        [Fact]
        public void JsonMapPreservesOrderAndSupportsInsert()
        {
            var m = new JsonMap { ["b"] = 1L, ["a"] = 2L };
            m.Insert(0, "Case", "x");
            m["B"] = 3L;
            Assert.Equal(new[] { "Case", "b", "a" }, m.Keys.ToArray());
            Assert.Equal(3L, m["b"]);
            Assert.True(m.Remove("A"));
            Assert.Equal(new[] { "Case", "b" }, m.Keys.ToArray());
            Assert.Equal(3L, m["b"]);
        }
    }

    public class CatalogTests
    {
        [Fact]
        public void KeysAreUniqueAndResolvable()
        {
            var keys = PurviewCatalog.AllCollections.Select(c => c.Key).ToList();
            Assert.Equal(keys.Count, keys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            foreach (var key in keys) Assert.Same(PurviewCatalog.Find(key), PurviewCatalog.Find(key.ToUpperInvariant()));
            Assert.True(PurviewCatalog.Solutions.Count >= 10);
            Assert.All(PurviewCatalog.Solutions, s => Assert.NotEmpty(s.Collections));
        }

        [Fact]
        public void OnlyReadOnlyCmdletsAreUsed()
        {
            Assert.All(PurviewCatalog.AllCollections, c => Assert.StartsWith("Get-", c.Cmdlet));
        }

        [Fact]
        public void RelationsAndForEachReferencesExist()
        {
            foreach (var c in PurviewCatalog.AllCollections)
            {
                foreach (var r in c.Relations) Assert.NotNull(PurviewCatalog.Find(r.CollectionKey));
                if (c.ForEachCollections != null)
                {
                    Assert.NotNull(c.ForEachParameter);
                    foreach (var p in c.ForEachCollections) Assert.NotNull(PurviewCatalog.Find(p));
                }
            }
        }

        [Fact]
        public void CommandLineShowsParameters()
        {
            Assert.Equal("Get-Label -IncludeDetailedLabelActions", PurviewCatalog.Find("InformationProtection.SensitivityLabels").CommandLine);
            Assert.Equal("Get-ComplianceCase -CaseType AdvancedEdiscovery", PurviewCatalog.Find("eDiscovery.PremiumCases").CommandLine);
            Assert.Equal("Get-CaseHoldPolicy -Case <each Case>", PurviewCatalog.Find("eDiscovery.CaseHolds").CommandLine);
            Assert.Equal("'a b'", CollectionDef.QuoteArgument("a b"));
            Assert.Equal("'it''s'", CollectionDef.QuoteArgument("it's"));
        }
    }

    public class SnapshotTests
    {
        [Fact]
        public void DemoSnapshotCoversEveryCollection()
        {
            var store = Snapshot.LoadDemo();
            Assert.Equal(SourceKind.Demo, store.Source.Kind);
            Assert.Equal("contoso.onmicrosoft.com", store.Source.Organization);
            foreach (var def in PurviewCatalog.AllCollections)
            {
                Assert.True(store.Contains(def.Key), "Demo data is missing " + def.Key);
                var r = store.Get(def.Key);
                Assert.True(r.State == LoadState.Loaded || r.State == LoadState.Unavailable, def.Key);
                if (r.State == LoadState.Loaded) Assert.NotEmpty(r.Items);
            }
            Assert.DoesNotContain(store.Results, r => PurviewCatalog.Find(r.Key) == null);
        }

        [Fact]
        public void SaveAndLoadRoundTrip()
        {
            var store = new DataStore(new SourceInfo { Kind = SourceKind.Live, Organization = "fabrikam.onmicrosoft.com", Account = "admin@fabrikam.com", TenantId = Guid.Empty.ToString() });
            store.Set(new CollectionResult("DataLossPrevention.Policies", LoadState.Loaded)
            {
                Items = { new JsonMap { ["Name"] = "Policy A", ["Priority"] = 0L, ["Workload"] = new List<object> { "Exchange", "Teams" } } },
                RetrievedUtc = new DateTime(2026, 10, 12, 18, 0, 0, DateTimeKind.Utc),
                DurationSeconds = 1.234,
                Message = "partial error",
                Warnings = { "a warning" },
            });
            store.Set(new CollectionResult("Audit.Configuration", LoadState.Unavailable) { Message = "not available" });
            store.Set(new CollectionResult("Alerts.AlertPolicies", LoadState.Loading)); // in-flight: not saved

            var path = Path.Combine(Path.GetTempPath(), "pv-test-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                Snapshot.Save(store, path);
                var loaded = Snapshot.Load(path);
                Assert.Equal(SourceKind.Snapshot, loaded.Source.Kind);
                Assert.Equal("fabrikam.onmicrosoft.com", loaded.Source.Organization);
                Assert.Equal(path, loaded.Source.FilePath, StringComparer.OrdinalIgnoreCase);
                var dlp = loaded.Get("DataLossPrevention.Policies");
                Assert.Equal(LoadState.Loaded, dlp.State);
                Assert.Equal("Policy A", dlp.Items.Single()["Name"]);
                Assert.Equal(new object[] { "Exchange", "Teams" }, ((List<object>)dlp.Items.Single()["Workload"]).ToArray());
                Assert.Equal(new DateTime(2026, 10, 12, 18, 0, 0, DateTimeKind.Utc), dlp.RetrievedUtc);
                Assert.Equal(1.23, dlp.DurationSeconds);
                Assert.Equal("partial error", dlp.Message);
                Assert.Equal(new[] { "a warning" }, dlp.Warnings);
                Assert.Equal(LoadState.Unavailable, loaded.Get("Audit.Configuration").State);
                Assert.False(loaded.Contains("Alerts.AlertPolicies"));
                Assert.Equal("Get-DlpCompliancePolicy", dlp.CommandLine);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void RejectsForeignOrNewerFiles()
        {
            Assert.Throws<InvalidDataException>(() => Snapshot.Parse("{\"hello\":1}", SourceKind.Snapshot));
            Assert.Throws<InvalidDataException>(() => Snapshot.Parse("[]", SourceKind.Snapshot));
            Assert.Throws<InvalidDataException>(() => Snapshot.Parse("{\"format\":\"PurviewExplorer.Snapshot\",\"formatVersion\":99}", SourceKind.Snapshot));
        }

        [Fact]
        public void OpensSnapshotsSavedByPurviewConfigurationViewer()
        {
            const string saved010 = "{\"format\":\"PurviewViewer.Snapshot\",\"formatVersion\":1,\"source\":{\"kind\":\"Live\",\"organization\":\"fabrikam.onmicrosoft.com\"}," +
                                    "\"collections\":{\"Permissions.RoleGroups\":{\"state\":\"Loaded\",\"items\":[{\"Name\":\"ComplianceAdministrator\"}]}}}";
            var store = Snapshot.Parse(saved010, SourceKind.Snapshot);
            Assert.Equal("fabrikam.onmicrosoft.com", store.Source.Organization);
            Assert.Equal("ComplianceAdministrator", store.Get("Permissions.RoleGroups").Items.Single()["Name"]);
            Assert.Contains("\"format\": \"PurviewExplorer.Snapshot\"", Snapshot.Serialize(store));
        }
    }

    public class ValueFormatterTests
    {
        [Fact]
        public void FormatsScalarsAndLists()
        {
            Assert.Equal("", ValueFormatter.ToShortText(null));
            Assert.Equal("True", ValueFormatter.ToShortText(true));
            Assert.Equal("2.5", ValueFormatter.ToShortText(2.5));
            Assert.Equal("a; b; 3", ValueFormatter.ToShortText(new List<object> { "a", "b", 3L }));
            Assert.Equal("line1 line2", ValueFormatter.ToShortText("line1\r\n  line2"));
            Assert.Equal("[{\"k\":1}]", ValueFormatter.ToShortText(new List<object> { new JsonMap { ["k"] = 1L } }));
            Assert.EndsWith("…", ValueFormatter.ToShortText(new string('x', 5000)));
        }

        [Fact]
        public void PrettyPrintsEmbeddedJsonAndXml()
        {
            var json = ValueFormatter.ToLongText("{\"Type\":\"encrypt\",\"Settings\":[{\"Key\":\"a\",\"Value\":\"b\"}]}");
            Assert.Contains("\r\n  \"Type\": \"encrypt\"", json);
            var xml = ValueFormatter.ToLongText("<a><b x=\"1\"/></a>");
            Assert.Equal("<a>\r\n  <b x=\"1\" />\r\n</a>", xml);
            Assert.Equal("[color, #FF0000]", ValueFormatter.ToLongText("[color, #FF0000]"));
            Assert.Equal("one\r\ntwo", ValueFormatter.ToLongText(new List<object> { "one", "two" }));
        }
    }

    public class ItemHelperTests
    {
        private static readonly DataStore Demo = Snapshot.LoadDemo();

        [Fact]
        public void OrdersSublabelsAfterTheirParent()
        {
            var def = PurviewCatalog.Find("InformationProtection.SensitivityLabels");
            var items = Demo.Get(def.Key).Items;
            var shuffled = items.OrderByDescending(i => i["Name"] as string).ToList();
            var ordered = ItemHelper.Order(def, shuffled);
            var names = ordered.Select(i => (string)i["Name"]).ToList();
            Assert.Equal(new[] { "Public", "General", "Confidential", "Confidential-AllEmployees", "Confidential-SpecificPeople", "Confidential-Partners", "HighlyConfidential" },
                names.Take(7));
            var parents = ItemHelper.BuildParentLookup(def, items);
            var child = items.First(i => (string)i["Name"] == "Confidential-Partners");
            Assert.Equal("Confidential", ItemHelper.Title(parents[child]));
            Assert.Equal(1, ItemHelper.Depth(child, parents));
            Assert.Contains(ItemHelper.ParentColumn, ItemHelper.DisplayColumns(def, items));
        }

        [Fact]
        public void FindsRulesOfAPolicy()
        {
            var policies = PurviewCatalog.Find("DataLossPrevention.Policies");
            var policy = Demo.Get(policies.Key).Items.First(i => (string)i["Name"] == "U.S. Financial Data");
            var rules = ItemHelper.FindRelated(policy, Demo.Get("DataLossPrevention.Rules").Items, policies.Relations.Single());
            Assert.Equal(2, rules.Count);

            var cases = PurviewCatalog.Find("eDiscovery.Cases");
            var fabrikam = Demo.Get(cases.Key).Items.First(i => (string)i["Name"] == "Contoso v. Fabrikam");
            var holds = ItemHelper.FindRelated(fabrikam, Demo.Get("eDiscovery.CaseHolds").Items, cases.Relations.Single());
            Assert.Equal("Fabrikam custodians", holds.Single()["Name"]);
        }

        [Fact]
        public void DisplayColumnsSkipMissingProperties()
        {
            var def = PurviewCatalog.Find("DataLossPrevention.Policies");
            var items = new[] { new JsonMap { ["Name"] = "x", ["Mode"] = "Enable", ["Other"] = 1L } };
            Assert.Equal(new[] { "Name", "Mode" }, ItemHelper.DisplayColumns(def, items));

            var unknown = new CollectionDef { Columns = new[] { "Nope" } };
            Assert.Equal(new[] { "Name", "Mode", "Other" }, ItemHelper.DisplayColumns(unknown, items));
        }

        [Fact]
        public void TitlePrefersDisplayName()
        {
            Assert.Equal("Shown", ItemHelper.Title(new JsonMap { ["Name"] = "internal", ["DisplayName"] = "Shown" }));
            Assert.Equal("internal", ItemHelper.Title(new JsonMap { ["Name"] = "internal", ["DisplayName"] = "" }));
            Assert.Equal("filter", ItemHelper.Title(new JsonMap { ["FilterName"] = "filter" }));
        }

        [Fact]
        public void ComparerIsNumericAware()
        {
            var values = new List<string> { "10", "9", "", "abc", "1.5" };
            values.Sort(ValueComparer.Instance);
            Assert.Equal(new[] { "1.5", "9", "10", "abc", "" }, values);
        }
    }

    public class PsConverterTests
    {
        private sealed class Typed
        {
            public override string ToString() => "contoso.com/Users/Megan";
        }

        [Fact]
        public void ConvertsPowerShellObjects()
        {
            var nested = new PSObject();
            nested.Properties.Add(new PSNoteProperty("name", "Credit Card Number"));
            nested.Properties.Add(new PSNoteProperty("mincount", 1));

            var obj = new PSObject();
            obj.Properties.Add(new PSNoteProperty("Name", "Rule"));
            obj.Properties.Add(new PSNoteProperty("RunspaceId", Guid.NewGuid()));
            obj.Properties.Add(new PSNoteProperty("Priority", 3));
            obj.Properties.Add(new PSNoteProperty("Enabled", true));
            obj.Properties.Add(new PSNoteProperty("When", new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc)));
            obj.Properties.Add(new PSNoteProperty("Locations", new ArrayList { "All", PSObject.AsPSObject("Exchange") }));
            obj.Properties.Add(new PSNoteProperty("Sit", new object[] { nested }));
            obj.Properties.Add(new PSNoteProperty("Owner", PSObject.AsPSObject(new Typed())));
            obj.Properties.Add(new PSNoteProperty("Table", new Hashtable { ["k"] = "v" }));
            obj.Properties.Add(new PSNoteProperty("Kind", DayOfWeek.Monday));
            obj.Properties.Add(new PSNoteProperty("Nothing", null));

            var item = PsConverter.ToItem(obj);
            Assert.False(item.ContainsKey("RunspaceId"));
            Assert.Equal("Rule", item["Name"]);
            Assert.Equal(3L, item["Priority"]);
            Assert.Equal(true, item["Enabled"]);
            Assert.Equal("2026-01-02T03:04:05Z", item["When"]);
            Assert.Equal(new object[] { "All", "Exchange" }, ((List<object>)item["Locations"]).ToArray());
            var sit = (JsonMap)((List<object>)item["Sit"]).Single();
            Assert.Equal("Credit Card Number", sit["name"]);
            Assert.Equal(1L, sit["mincount"]);
            Assert.Equal("contoso.com/Users/Megan", item["Owner"]);
            Assert.Equal("v", ((JsonMap)item["Table"])["k"]);
            Assert.Equal("Monday", item["Kind"]);
            Assert.Null(item["Nothing"]);
        }

        [Fact]
        public void TypedDeserializedObjectsWithoutUsefulToStringAreExpanded()
        {
            var o = new PSObject();
            o.TypeNames.Insert(0, "Deserialized.Microsoft.Exchange.Something");
            o.Properties.Add(new PSNoteProperty("Value", 5));
            var converted = PsConverter.ConvertValue(o, 1);
            Assert.Equal(5L, ((JsonMap)converted)["Value"]);
        }

        public sealed class Inner
        {
            public string A { get; set; } = "alpha";
            public int B { get; set; } = 2;
        }

        public sealed class Named
        {
            public string Id { get; set; } = "x";
            public override string ToString() => "contoso.com/Configuration/Policy A";
        }

        public sealed class Outer
        {
            public string Name { get; set; } = "outer";
            public Inner In { get; set; } = new Inner();
            public Named Ref { get; set; } = new Named();
        }

        [Fact]
        public void RealDeserializedObjectsAreExpandedUnlessToStringIsMeaningful()
        {
            var deserialized = PSSerializer.Deserialize(PSSerializer.Serialize(new Outer(), 2));
            var item = PsConverter.ToItem((PSObject)deserialized);
            Assert.Equal("outer", item["Name"]);
            var inner = Assert.IsType<JsonMap>(item["In"]);
            Assert.Equal("alpha", inner["A"]);
            Assert.Equal(2L, inner["B"]);
            Assert.Equal("contoso.com/Configuration/Policy A", item["Ref"]);
        }

        [Fact]
        public void ScalarOutputBecomesValueProperty()
        {
            var item = PsConverter.ToItem(PSObject.AsPSObject("hello"));
            Assert.Equal("hello", item["Value"]);
        }
    }

    public class CsvTests
    {
        [Fact]
        public void EscapesAndNeutralizesFormulas()
        {
            Assert.Equal("plain", CsvExport.Escape("plain"));
            Assert.Equal("\"a,b\"", CsvExport.Escape("a,b"));
            Assert.Equal("\"say \"\"hi\"\"\"", CsvExport.Escape("say \"hi\""));
            Assert.Equal("'=SUM(A1)", CsvExport.Escape("=SUM(A1)"));
            Assert.Equal("-5", CsvExport.Escape("-5"));
        }

        [Fact]
        public void ExportsPreferredColumnsFirstThenTheRest()
        {
            var def = PurviewCatalog.Find("DataLossPrevention.Policies");
            var items = new[] { new JsonMap { ["Zeta"] = "z", ["Name"] = "P1", ["Mode"] = "Enable" }, new JsonMap { ["Name"] = "P2", ["Extra"] = new List<object> { "a", "b" } } };
            var lines = CsvExport.ToCsv(def, items).Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal("Name,Mode,Zeta,Extra", lines[0]);
            Assert.Equal("P1,Enable,z,", lines[1]);
            Assert.Equal("P2,,,a; b", lines[2]);
        }
    }

    public class WorkspaceQueueTests
    {
        private sealed class FakeSession : IPurviewSession
        {
            public bool IsConnected { get; set; } = true;
            public ModuleInfo Module => null;
            public TaskCompletionSource<bool> Gate { get; set; }
            public bool? CommandProbe { get; set; } = true;
            public bool StopRequested { get; private set; }
            public List<string> Calls { get; } = new List<string>();
            public Func<string, List<KeyValuePair<string, object>>, InvokeResult> Handler { get; set; } = (c, a) => new InvokeResult();

            public Task<bool?> CommandExistsAsync(string name) => Task.FromResult(CommandProbe);

            public async Task<InvokeResult> InvokeAsync(string cmdlet, IEnumerable<KeyValuePair<string, object>> parameters)
            {
                var args = parameters.ToList();
                Calls.Add(cmdlet + string.Concat(args.Select(a => " -" + a.Key + ":" + a.Value)));
                if (Gate != null) await Gate.Task;
                return StopRequested ? new InvokeResult { Stopped = true } : Handler(cmdlet, args);
            }

            public void Stop()
            {
                StopRequested = true;
                Gate?.TrySetResult(true);
            }

            public Task DisconnectAsync()
            {
                IsConnected = false;
                return Task.CompletedTask;
            }

            public void Dispose() { }
        }

        private static InvokeResult Items(params JsonMap[] items)
        {
            var r = new InvokeResult();
            r.Items.AddRange(items);
            return r;
        }

        private static (Workspace, FakeSession) Connected(FakeSession fake = null)
        {
            var ws = new Workspace();
            fake = fake ?? new FakeSession();
            ws.AttachSession(fake, new SourceInfo { Kind = SourceKind.Live, Connected = true });
            return (ws, fake);
        }

        // Run without a SynchronizationContext so continuations run inline and the test is deterministic.
        private static void Run(Func<Task> body) => Task.Run(body).GetAwaiter().GetResult();

        [Fact]
        public void LoadsQueuedCollections()
        {
            Run(async () =>
            {
                var (ws, fake) = Connected();
                fake.Handler = (c, a) => Items(new JsonMap { ["Name"] = c });
                ws.Enqueue(new[] { "DataLossPrevention.Policies", "DataLossPrevention.Rules" });
                await ws.WhenIdleAsync();
                Assert.Equal(LoadState.Loaded, ws.Store.Get("DataLossPrevention.Policies").State);
                Assert.Equal("Get-DlpComplianceRule", ws.Store.Get("DataLossPrevention.Rules").Items.Single()["Name"]);
                Assert.False(ws.IsLoading);
            });
        }

        [Fact]
        public void StopAfterRefreshAllRestoresPreviousStates()
        {
            Run(() =>
            {
                var (ws, fake) = Connected(new FakeSession { Gate = new TaskCompletionSource<bool>() });
                const string failed = "Audit.RetentionPolicies", unavailable = "Audit.Configuration", loaded = "Alerts.AlertPolicies";
                ws.Store.Set(new CollectionResult(failed, LoadState.Failed) { RetrievedUtc = DateTime.UtcNow, Message = "denied" });
                ws.Store.Set(new CollectionResult(unavailable, LoadState.Unavailable) { RetrievedUtc = DateTime.UtcNow });
                ws.Store.Set(new CollectionResult(loaded, LoadState.Loaded) { RetrievedUtc = DateTime.UtcNow, Items = { new JsonMap { ["Name"] = "a" } } });

                ws.Enqueue(new[] { failed, unavailable, loaded }, force: true);
                Assert.Equal(LoadState.Loading, ws.Store.Get(failed).State);
                Assert.Equal(LoadState.Queued, ws.Store.Get(unavailable).State);

                ws.CancelQueue();
                Assert.Equal(LoadState.Failed, ws.Store.Get(failed).State);
                Assert.Equal("denied", ws.Store.Get(failed).Message);
                Assert.Equal(LoadState.Unavailable, ws.Store.Get(unavailable).State);
                Assert.Equal(LoadState.Loaded, ws.Store.Get(loaded).State);
                Assert.Single(ws.Store.Get(loaded).Items);
                Assert.False(ws.IsLoading);
                return Task.CompletedTask;
            });
        }

        [Fact]
        public void DisconnectDuringInlineParentFetchLeavesNothingStuck()
        {
            Run(async () =>
            {
                var (ws, fake) = Connected(new FakeSession { Gate = new TaskCompletionSource<bool>() });
                ws.Enqueue(new[] { "eDiscovery.CaseHolds" });
                Assert.Equal(LoadState.Loading, ws.Store.Get("eDiscovery.Cases").State);

                await ws.DisconnectAsync();
                Assert.Null(ws.Session);
                Assert.False(ws.IsLoading);
                Assert.Equal(LoadState.NotLoaded, ws.Store.Get("eDiscovery.Cases").State);
                Assert.Equal(LoadState.NotLoaded, ws.Store.Get("eDiscovery.PremiumCases").State);
                Assert.Equal(LoadState.NotLoaded, ws.Store.Get("eDiscovery.CaseHolds").State);
                Assert.Single(fake.Calls);
            });
        }

        [Fact]
        public void CaseHoldsReportFailedParents()
        {
            Run(async () =>
            {
                var (ws, fake) = Connected();
                fake.Handler = (c, a) => c == "Get-ComplianceCase"
                    ? new InvokeResult { TerminatingError = "Access denied" }
                    : Items(new JsonMap { ["Name"] = "hold" });
                ws.Enqueue(new[] { "eDiscovery.CaseHolds" });
                await ws.WhenIdleAsync();
                var holds = ws.Store.Get("eDiscovery.CaseHolds");
                Assert.Equal(LoadState.Failed, holds.State);
                Assert.Contains("Couldn't load Cases (Standard)", holds.Message);
                Assert.Contains("Couldn't load Cases (Premium)", holds.Message);
                Assert.Equal(LoadState.Failed, ws.Store.Get("eDiscovery.Cases").State);
            });
        }

        [Fact]
        public void CaseHoldsArePartialWhenOneParentFails()
        {
            Run(async () =>
            {
                var (ws, fake) = Connected();
                fake.Handler = (c, a) =>
                {
                    if (c == "Get-ComplianceCase")
                        return a.Any(p => p.Key == "CaseType") ? new InvokeResult { TerminatingError = "Access denied" } : Items(new JsonMap { ["Name"] = "Case 1" });
                    return Items(new JsonMap { ["Name"] = "Hold for " + a.Single(p => p.Key == "Case").Value });
                };
                ws.Enqueue(new[] { "eDiscovery.CaseHolds" });
                await ws.WhenIdleAsync();
                var holds = ws.Store.Get("eDiscovery.CaseHolds");
                Assert.Equal(LoadState.Loaded, holds.State);
                Assert.Equal("Case 1", holds.Items.Single()["Case"]);
                Assert.Equal("Hold for Case 1", holds.Items.Single()["Name"]);
                Assert.Contains("Couldn't load Cases (Premium)", holds.Message);
            });
        }

        [Fact]
        public void UndeterminedCommandProbeIsAFailureNotUnavailable()
        {
            Run(async () =>
            {
                var (ws, fake) = Connected(new FakeSession { CommandProbe = null });
                ws.Enqueue(new[] { "Permissions.RoleGroups" });
                await ws.WhenIdleAsync();
                Assert.Equal(LoadState.Failed, ws.Store.Get("Permissions.RoleGroups").State);
                fake.CommandProbe = false;
                ws.Enqueue(new[] { "Permissions.RoleGroups" }, force: true);
                await ws.WhenIdleAsync();
                Assert.Equal(LoadState.Unavailable, ws.Store.Get("Permissions.RoleGroups").State);
            });
        }
    }

    public class WorkspaceTests
    {
        [Fact]
        public void ShowStoreReplacesDataAndIgnoresLoadsWhenOffline()
        {
            var ws = new Workspace();
            int replaced = 0;
            ws.StoreReplaced += (s, e) => replaced++;
            var demo = Snapshot.LoadDemo();
            ws.ShowStore(demo);
            Assert.Equal(1, replaced);
            Assert.Same(demo, ws.Store);
            ws.Enqueue(PurviewCatalog.AllCollections.Select(c => c.Key), force: true);
            Assert.False(ws.IsLoading);
            Assert.Equal(LoadState.Loaded, ws.Store.Get("DataLossPrevention.Policies").State);
        }
    }

    [Trait("Category", "Integration")]
    public class CommandLineTests
    {
        [Fact]
        public void CheckPrerequisitesWritesAReportAndExitCode()
        {
            var exe = typeof(Snapshot).Assembly.Location;
            var report = Path.Combine(Path.GetTempPath(), "pe-prereq-" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                using (var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, "\"/CheckPrerequisites:" + report + "\"") { UseShellExecute = false }))
                {
                    Assert.True(p.WaitForExit(120000), "the check timed out");
                    var text = File.ReadAllText(report);
                    Assert.Contains("Purview Explorer", text);
                    Assert.Contains(".NET Framework: 4.8", text);
                    bool moduleInstalled = ModuleLocator.FindBest() != null;
                    Assert.Equal(moduleInstalled ? 0 : 1, p.ExitCode);
                    Assert.Contains(moduleInstalled ? "Import-Module: OK" : "NOT FOUND", text);
                }
            }
            finally
            {
                if (File.Exists(report)) File.Delete(report);
            }
        }
    }

    [Trait("Category", "Integration")]
    public class UiSelfTest
    {
        [Fact]
        public void DrivesEveryTabAndCollectionWithDemoData()
        {
            var exe = typeof(Snapshot).Assembly.Location;
            var log = Path.Combine(Path.GetTempPath(), "pv-selftest-" + Guid.NewGuid().ToString("N") + ".log");
            try
            {
                using (var p = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, "\"/SelfTest:" + log + "\"") { UseShellExecute = false }))
                {
                    Assert.True(p.WaitForExit(180000), "self-test timed out");
                    var lines = File.Exists(log) ? File.ReadAllLines(log) : new string[0];
                    var failures = lines.Where(l => l.StartsWith("FAIL")).ToList();
                    Assert.True(p.ExitCode == 0 && failures.Count == 0, "Exit code " + p.ExitCode + "\n" + string.Join("\n", failures.DefaultIfEmpty(lines.LastOrDefault() ?? "(no log)")));
                    Assert.Contains("RESULT: PASS", lines);
                    Assert.True(lines.Count(l => l.StartsWith("PASS")) > 300, "expected several hundred checks, got " + lines.Length);
                }
            }
            finally
            {
                if (File.Exists(log)) File.Delete(log);
            }
        }
    }

    [Trait("Category", "Integration")]
    public class HostedPowerShellTests
    {
        [Fact]
        public void ModuleLoadsInHostedRunspaceWhenInstalled()
        {
            if (ModuleLocator.FindBest() == null) return; // ExchangeOnlineManagement isn't installed on this machine

            // Simulate being started from PowerShell 7, whose module folders come first.
            var original = Environment.GetEnvironmentVariable("PSModulePath");
            Environment.SetEnvironmentVariable("PSModulePath",
                @"C:\Users\x\Documents\PowerShell\Modules;C:\Program Files\PowerShell\Modules;c:\program files\powershell\7\Modules;" + original);
            try
            {
                var report = PurviewSession.CheckPrerequisites(out var ok);
                Assert.True(ok, report);
                Assert.Contains("Import-Module: OK", report);
                Assert.Contains("DisableWAM", report);
            }
            finally
            {
                Environment.SetEnvironmentVariable("PSModulePath", original);
            }
        }

        [Fact]
        public async System.Threading.Tasks.Task ConnectReportsModuleErrors()
        {
            if (ModuleLocator.FindBest() == null) return;
            var options = new ConnectOptions
            {
                Mode = AuthMode.Certificate,
                AppId = Guid.NewGuid().ToString(),
                CertificateThumbprint = new string('A', 40), // not in any store: fails locally, before any network call
                Organization = "contoso.onmicrosoft.com",
            };
            var progress = new List<string>();
            var ex = await Assert.ThrowsAsync<PurviewException>(() =>
                PurviewSession.ConnectAsync(options, PurviewCatalog.CmdletNames, new SyncProgress(progress.Add)));
            Assert.False(string.IsNullOrWhiteSpace(ex.Message));
            Assert.Contains("Signing in to Security & Compliance PowerShell...", progress);
        }

        private sealed class SyncProgress : IProgress<string>
        {
            private readonly Action<string> _report;
            public SyncProgress(Action<string> report) => _report = report;
            public void Report(string value) => _report(value);
        }

        [Theory]
        [InlineData(@"C:\Program Files\PowerShell\7\Modules", true)]
        [InlineData(@"c:\program files\powershell\7\Modules\", true)]
        [InlineData(@"C:\Program Files\PowerShell\Modules", true)]
        [InlineData(@"C:\Users\x\OneDrive\Documents\PowerShell\Modules", true)]
        [InlineData(@"C:\Users\x\OneDrive\Documents\WindowsPowerShell\Modules", false)]
        [InlineData(@"C:\WINDOWS\system32\WindowsPowerShell\v1.0\Modules", false)]
        [InlineData(@"C:\Program Files (x86)\Microsoft SQL Server\150\Tools\PowerShell\Modules", false)]
        [InlineData(@"D:\Tools\Modules", false)]
        public void RecognizesPowerShell7ModuleFolders(string path, bool expected)
        {
            Assert.Equal(expected, PurviewSession.IsPowerShell7Path(path));
        }
    }
}
