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
        public void SessionTypeIsSavedAndRead()
        {
            string Saved(bool? searchOnly) => Snapshot.Serialize(new DataStore(new SourceInfo { Kind = SourceKind.Live, Organization = "adatum.onmicrosoft.com", SearchOnlySession = searchOnly }));

            Assert.Contains("\"sessionType\": \"SearchOnly\"", Saved(true));
            Assert.Contains("\"sessionType\": \"Standard\"", Saved(false));
            Assert.True(Snapshot.Parse(Saved(true), SourceKind.Snapshot).Source.SearchOnlySession);
            Assert.False(Snapshot.Parse(Saved(false), SourceKind.Snapshot).Source.SearchOnlySession);
            Assert.Null(Snapshot.Parse(Saved(null), SourceKind.Snapshot).Source.SearchOnlySession);
            Assert.Null(Snapshot.LoadDemo().Source.SearchOnlySession);
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

    public class PathPrivacyTests
    {
        [Fact]
        public void ReplacesTheUsersFoldersWithPlaceholders()
        {
            var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var text = $"ExchangeOnlineManagement 3.10.1: {docs}\\WindowsPowerShell\\Modules\\x.psd1\r\nTemp: {profile.ToUpperInvariant()}\\AppData\\Local\\Temp";
            var redacted = PathPrivacy.Redact(text);
            Assert.Contains("<Documents>\\WindowsPowerShell\\Modules\\x.psd1", redacted);
            Assert.Contains("<LocalAppData>\\Temp", redacted);
            Assert.DoesNotContain(Environment.UserName, redacted, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("C:\\Program Files\\WindowsPowerShell\\Modules", PathPrivacy.Redact("C:\\Program Files\\WindowsPowerShell\\Modules"));
        }

        [Fact]
        public void PrerequisiteReportContainsNoUserName()
        {
            var report = PurviewSession.CheckPrerequisites(out _);
            Assert.DoesNotContain("\\Users\\" + Environment.UserName, report, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(" process on ", report);
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
                Assert.DoesNotContain("search-only", ws.Store.Get("Permissions.RoleGroups").Message);
            });
        }

        [Fact]
        public void UnavailableInASearchOnlySessionSaysSo()
        {
            Run(async () =>
            {
                var ws = new Workspace();
                ws.AttachSession(new FakeSession { CommandProbe = false }, new SourceInfo { Kind = SourceKind.Live, Connected = true, SearchOnlySession = true });
                ws.Enqueue(new[] { "eDiscovery.Searches" });
                await ws.WhenIdleAsync();
                var r = ws.Store.Get("eDiscovery.Searches");
                Assert.Equal(LoadState.Unavailable, r.State);
                Assert.Contains("search-only session", r.Message);
                Assert.Contains("Get-ComplianceSearch", r.Message);
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

    public class ConnectOptionsTests
    {
        private static readonly string[] Commands = { "Get-Label", "Get-DlpCompliancePolicy" };

        private static List<KeyValuePair<string, object>> Parameters(ConnectOptions o, bool disableWam = false, Func<string, bool> supported = null, bool searchOnly = false) =>
            ConnectOptions.ConnectParameters(o, Commands, disableWam, supported ?? (_ => true), searchOnly);

        private static object Value(List<KeyValuePair<string, object>> list, string name) =>
            list.Where(p => p.Key == name).Select(p => p.Value).SingleOrDefault();

        [Fact]
        public void OwnOrganizationInCommercialUsesTheModuleDefaults()
        {
            var p = Parameters(new ConnectOptions { UserPrincipalName = " admin@contoso.com " });
            Assert.Equal(new[] { "ShowBanner", "CommandName", "UserPrincipalName", "ErrorAction" }, p.Select(x => x.Key));
            Assert.Equal("admin@contoso.com", Value(p, "UserPrincipalName"));
            Assert.Same(Commands, Value(p, "CommandName"));
        }

        [Theory]
        [InlineData(CloudEnvironment.Commercial, null, "https://login.microsoftonline.com/adatum.onmicrosoft.com")]
        [InlineData(CloudEnvironment.GccHigh, "https://ps.compliance.protection.office365.us/powershell-liveid/", "https://login.microsoftonline.us/adatum.onmicrosoft.com")]
        [InlineData(CloudEnvironment.DoD, "https://l5.ps.compliance.protection.office365.us/powershell-liveid/", "https://login.microsoftonline.us/adatum.onmicrosoft.com")]
        [InlineData(CloudEnvironment.China, "https://ps.compliance.protection.partner.outlook.cn/powershell-liveid", "https://login.chinacloudapi.cn/adatum.onmicrosoft.com")]
        public void OtherOrganizationSignsInThereAndMapsTheSession(CloudEnvironment environment, string connectionUri, string authorizationUri)
        {
            var o = new ConnectOptions { Environment = environment, UserPrincipalName = "alex@contoso.com", DelegatedOrganization = " adatum.onmicrosoft.com " };
            Assert.True(o.IsDelegated);
            var p = Parameters(o);
            Assert.Equal(connectionUri, Value(p, "ConnectionUri"));
            Assert.Equal(authorizationUri, Value(p, "AzureADAuthorizationEndpointUri"));
            Assert.Equal("adatum.onmicrosoft.com", Value(p, "DelegatedOrganization"));
            Assert.Equal("alex@contoso.com", Value(p, "UserPrincipalName"));
            Assert.Single(p, x => x.Key == "AzureADAuthorizationEndpointUri");
        }

        [Theory]
        [InlineData(CloudEnvironment.GccHigh, "https://login.microsoftonline.us/organizations")]
        [InlineData(CloudEnvironment.DoD, "https://login.microsoftonline.us/organizations")]
        [InlineData(CloudEnvironment.China, "https://login.chinacloudapi.cn/organizations")]
        public void OwnOrganizationInSovereignCloudsUsesTheirEndpoints(CloudEnvironment environment, string authorizationUri)
        {
            var p = Parameters(new ConnectOptions { Environment = environment });
            Assert.NotNull(Value(p, "ConnectionUri"));
            Assert.Equal(authorizationUri, Value(p, "AzureADAuthorizationEndpointUri"));
            Assert.Null(Value(p, "DelegatedOrganization"));
        }

        [Fact]
        public void CertificateIgnoresTheOtherOrganizationField()
        {
            var o = new ConnectOptions
            {
                Mode = AuthMode.Certificate,
                AppId = "36ee4c6c-0812-40a2-b820-b22ebd02bce3",
                CertificateThumbprint = new string('A', 40),
                Organization = "fabrikam.onmicrosoft.com",
                DelegatedOrganization = "adatum.onmicrosoft.com",
                UserPrincipalName = "admin@contoso.com",
            };
            Assert.False(o.IsDelegated);
            var p = Parameters(o, disableWam: true);
            Assert.Equal("fabrikam.onmicrosoft.com", Value(p, "Organization"));
            Assert.Null(Value(p, "DelegatedOrganization"));
            Assert.Null(Value(p, "AzureADAuthorizationEndpointUri"));
            Assert.Null(Value(p, "UserPrincipalName"));
            Assert.Null(Value(p, "DisableWAM"));
        }

        [Fact]
        public void OptionalParametersFollowTheInstalledModule()
        {
            var o = new ConnectOptions { DelegatedOrganization = "adatum.onmicrosoft.com" };
            var p = Parameters(o, disableWam: true, supported: name => name != "DisableWAM" && name != "ShowBanner");
            Assert.Equal(new[] { "CommandName", "AzureADAuthorizationEndpointUri", "DelegatedOrganization", "ErrorAction" }, p.Select(x => x.Key));
            Assert.True((bool)Value(Parameters(o, disableWam: true), "DisableWAM"));
        }

        [Fact]
        public void SearchOnlySessionIsRequestedOnlyForOtherOrganizations()
        {
            var guest = new ConnectOptions { UserPrincipalName = "alex@contoso.com", DelegatedOrganization = "adatum.onmicrosoft.com" };
            var p = Parameters(guest, disableWam: true, searchOnly: true);
            Assert.Equal(new[] { "ShowBanner", "CommandName", "AzureADAuthorizationEndpointUri", "DelegatedOrganization", "UserPrincipalName", "DisableWAM", "EnableSearchOnlySession", "ErrorAction" },
                p.Select(x => x.Key));
            Assert.True((bool)Value(p, "EnableSearchOnlySession"));

            Assert.Null(Value(Parameters(guest), "EnableSearchOnlySession"));
            Assert.Null(Value(Parameters(guest, searchOnly: true, supported: name => name != "EnableSearchOnlySession"), "EnableSearchOnlySession"));
            Assert.Null(Value(Parameters(new ConnectOptions { UserPrincipalName = "admin@contoso.com" }, searchOnly: true), "EnableSearchOnlySession"));
            var cert = new ConnectOptions { Mode = AuthMode.Certificate, AppId = Guid.NewGuid().ToString(), CertificateThumbprint = new string('A', 40), Organization = "fabrikam.onmicrosoft.com" };
            Assert.Null(Value(Parameters(cert, searchOnly: true), "EnableSearchOnlySession"));
        }

        [Fact]
        public void TargetOrganizationIsTheDomainTheOptionsName()
        {
            Assert.Equal("adatum.onmicrosoft.com", new ConnectOptions { DelegatedOrganization = " adatum.onmicrosoft.com " }.TargetOrganization);
            Assert.Equal("fabrikam.onmicrosoft.com", new ConnectOptions { Mode = AuthMode.Certificate, Organization = "fabrikam.onmicrosoft.com", DelegatedOrganization = "adatum.onmicrosoft.com" }.TargetOrganization);
            Assert.Null(new ConnectOptions { UserPrincipalName = "admin@contoso.com" }.TargetOrganization);
            Assert.Null(new ConnectOptions { Mode = AuthMode.Certificate }.TargetOrganization);
        }

        [Fact]
        public void TenantCheckRefusesASessionOfAnotherTenant()
        {
            const string adatum = "7AC9C85C-6F6C-4CE6-8CC4-7F6FE24083E4";
            Assert.Null(ConnectOptions.CheckTenant(adatum, adatum.ToLowerInvariant(), "adatum.onmicrosoft.com"));
            Assert.Null(ConnectOptions.CheckTenant(null, adatum, "adatum.onmicrosoft.com"));
            Assert.Null(ConnectOptions.CheckTenant(adatum, null, "adatum.onmicrosoft.com"));
            var message = ConnectOptions.CheckTenant(adatum, "11111111-2222-3333-4444-555555555555", "adatum.onmicrosoft.com");
            Assert.Contains("11111111-2222-3333-4444-555555555555", message);
            Assert.Contains("adatum.onmicrosoft.com is tenant " + adatum, message);
            Assert.Contains("disconnected", message);
        }

        [Theory]
        [InlineData("contoso.onmicrosoft.com", true)]
        [InlineData(" adatum.onmicrosoft.us ", true)]
        [InlineData("tailspin.partner.onmschina.cn", true)]
        [InlineData("my-org.onmicrosoft.com", true)]
        [InlineData("contoso", false)]
        [InlineData("admin@contoso.onmicrosoft.com", false)]
        [InlineData("https://contoso.onmicrosoft.com", false)]
        [InlineData("contoso..onmicrosoft.com", false)]
        [InlineData("-contoso.onmicrosoft.com", false)]
        [InlineData("contoso.onmicrosoft.com/", false)]
        [InlineData("2a1b3c4d-0000-1111-2222-333344445555", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void RecognizesOrganizationDomains(string value, bool expected)
        {
            Assert.Equal(expected, ConnectOptions.IsOrganizationName(value));
        }

        [Fact]
        public void ValidationPointsAtTheFieldToCorrect()
        {
            string Field(ConnectOptions o) { o.Validate(out var f); return f; }

            Assert.Null(new ConnectOptions().Validate(out _));
            Assert.Null(new ConnectOptions { UserPrincipalName = "alex@contoso.com", DelegatedOrganization = "adatum.onmicrosoft.com" }.Validate(out _));
            Assert.Equal("UserPrincipalName", Field(new ConnectOptions { UserPrincipalName = "alex" }));
            Assert.Equal("DelegatedOrganization", Field(new ConnectOptions { DelegatedOrganization = "adatum" }));
            Assert.Equal("DelegatedOrganization", Field(new ConnectOptions { DelegatedOrganization = "alex@adatum.onmicrosoft.com" }));
            Assert.Contains("adatum.onmicrosoft.com", new ConnectOptions { DelegatedOrganization = "adatum" }.Validate(out _));

            var cert = new ConnectOptions
            {
                Mode = AuthMode.Certificate,
                AppId = "36ee4c6c-0812-40a2-b820-b22ebd02bce3",
                CertificateThumbprint = new string('A', 40),
                Organization = "fabrikam.onmicrosoft.com",
                DelegatedOrganization = "not checked for certificates",
            };
            Assert.Null(cert.Validate(out _));
            cert.Organization = "fabrikam";
            Assert.Equal("Organization", Field(cert));
            cert.CertificateThumbprint = "1234";
            Assert.Equal("CertificateThumbprint", Field(cert));
            cert.AppId = "app";
            Assert.Equal("AppId", Field(cert));
        }

        private static JsonMap Info(string organization, string delegated, string upn, string tenant) => new JsonMap
        {
            ["State"] = "Connected",
            ["Organization"] = organization,
            ["DelegatedOrganization"] = delegated,
            ["UserPrincipalName"] = upn,
            ["TenantID"] = tenant,
        };

        [Fact]
        public void IdentityOfAnOtherOrganizationIsThatOrganization()
        {
            var o = new ConnectOptions { UserPrincipalName = "alex@contoso.com", DelegatedOrganization = "adatum.onmicrosoft.com" };
            var id = ConnectOptions.ResolveIdentity(Info("", "adatum.onmicrosoft.com", "alex@contoso.com", "11111111-2222-3333-4444-555555555555"), o);
            Assert.Equal("adatum.onmicrosoft.com", id.Organization);
            Assert.Equal("alex@contoso.com", id.Account);
            Assert.Equal("11111111-2222-3333-4444-555555555555", id.TenantId);

            // Without connection information, the options still name the other organization, not the account's domain.
            id = ConnectOptions.ResolveIdentity(null, o);
            Assert.Equal("adatum.onmicrosoft.com", id.Organization);
            Assert.Equal("alex@contoso.com", id.Account);
            Assert.Null(id.TenantId);
        }

        [Fact]
        public void IdentityOfTheOwnOrganization()
        {
            var o = new ConnectOptions { UserPrincipalName = "admin@contoso.com" };
            Assert.Equal("contoso.com", ConnectOptions.ResolveIdentity(Info(null, null, "admin@contoso.com", null), o).Organization);
            Assert.Equal("contoso.onmicrosoft.com", ConnectOptions.ResolveIdentity(Info("contoso.onmicrosoft.com", "", "admin@contoso.com", null), o).Organization);
            var none = ConnectOptions.ResolveIdentity(null, new ConnectOptions());
            Assert.Null(none.Organization);
            Assert.Null(none.Account);
        }

        [Fact]
        public void IdentityOfACertificateConnection()
        {
            var o = new ConnectOptions { Mode = AuthMode.Certificate, AppId = "36ee4c6c-0812-40a2-b820-b22ebd02bce3", Organization = "fabrikam.onmicrosoft.com", DelegatedOrganization = "adatum.onmicrosoft.com" };
            var id = ConnectOptions.ResolveIdentity(Info(null, null, null, null), o);
            Assert.Equal("fabrikam.onmicrosoft.com", id.Organization);
            Assert.Equal("App 36ee4c6c-0812-40a2-b820-b22ebd02bce3", id.Account);
        }
    }

    public class ConnectionErrorsTests
    {
        private static readonly ConnectOptions Guest = new ConnectOptions { UserPrincipalName = "alex@contoso.com", DelegatedOrganization = "adatum.onmicrosoft.com" };
        private static readonly ConnectOptions Own = new ConnectOptions { UserPrincipalName = "admin@contoso.com" };

        // Messages as ExchangeOnlineManagement 3.10.1 reports them.
        [Theory]
        [InlineData("Object reference not set to an instance of an object.", ConnectFailure.ServiceSetupFailed)]
        [InlineData("UnAuthorized", ConnectFailure.Unauthorized)]
        [InlineData("No cmdlet assigned to the user have this feature enabled.", ConnectFailure.NoRole)]
        [InlineData("Unexpected character encountered while parsing value: <. Path '', line 0, position 0.", ConnectFailure.UnknownOrganization)]
        [InlineData("User canceled authentication.", ConnectFailure.Cancelled)]
        [InlineData("A window handle must be configured. See https://aka.ms/msal-net-wam#parent-window-handles", ConnectFailure.WebAccountManager)]
        [InlineData("", ConnectFailure.Other)]
        [InlineData(null, ConnectFailure.Other)]
        public void ClassifiesModuleMessages(string message, ConnectFailure expected)
        {
            Assert.Equal(expected, ConnectionErrors.Classify(message));
        }

        [Fact]
        public void GuestServiceFailureNamesTheOrganizationAndKeepsTheModuleText()
        {
            const string raw = "Object reference not set to an instance of an object.";
            var text = ConnectionErrors.Describe(raw, Guest);
            Assert.Contains("adatum.onmicrosoft.com", text);
            Assert.Contains("guest accounts", text);
            Assert.EndsWith("What the module reported:\r\n    " + raw, text);

            var withFallback = ConnectionErrors.Describe(raw, Guest, searchOnlyError: "The search-only session timed out.");
            Assert.Contains("search-only", withFallback);
            Assert.Contains("The search-only session timed out.", withFallback);

            var oldModule = ConnectionErrors.Describe(raw, Guest, searchOnlySupported: false);
            Assert.Contains("3.9.0", oldModule);
            Assert.Contains("Update-Module ExchangeOnlineManagement", oldModule);

            Assert.DoesNotContain("guest", ConnectionErrors.Describe(raw, Own));
        }

        [Fact]
        public void ExplainsRolesAccessAndUnknownOrganizations()
        {
            var noRole = ConnectionErrors.Describe("No cmdlet assigned to the user have this feature enabled.", Guest);
            Assert.Contains("no Microsoft Purview role in adatum.onmicrosoft.com", noRole);
            Assert.Contains("Privileged Identity Management", noRole);
            Assert.Contains("in your organization", ConnectionErrors.Describe("No cmdlet assigned to the user have this feature enabled.", Own));

            var denied = ConnectionErrors.Describe("UnAuthorized", Guest);
            Assert.Contains("adatum.onmicrosoft.com", denied);
            Assert.Contains("GDAP", denied);
            Assert.DoesNotContain("GDAP", ConnectionErrors.Describe("UnAuthorized", Own));

            Assert.Contains("primary .onmicrosoft.com domain", ConnectionErrors.Describe("Unexpected character encountered while parsing value: <. Path '', line 0, position 0.", Guest));
        }

        [Fact]
        public void WebAccountManagerFailureAsksForARestartWithTheBrowser()
        {
            const string raw = "A window handle must be configured. See https://aka.ms/msal-net-wam#parent-window-handles";
            var text = ConnectionErrors.Describe(raw, Own);
            Assert.Contains("Close Purview Explorer, open it again", text);
            Assert.Contains("Sign in with a browser window instead of Web Account Manager", text);
            Assert.EndsWith("What the module reported:\r\n    " + raw, text);
        }

        [Fact]
        public void CancelledAndUnknownErrors()
        {
            Assert.Equal("The sign-in was cancelled.", ConnectionErrors.Describe("User canceled authentication.", Guest));
            Assert.Equal("The connection was cancelled.", ConnectionErrors.Describe(null, Guest));
            Assert.Equal("Something new went wrong.", ConnectionErrors.Describe("Something new went wrong.", Guest));
            var both = ConnectionErrors.Describe("Something new went wrong.", Guest, searchOnlyError: "And this too.");
            Assert.StartsWith("Something new went wrong.", both);
            Assert.Contains("A search-only session also failed:\r\n    And this too.", both);
        }
    }

    public class TenantLookupTests
    {
        [Theory]
        [InlineData(CloudEnvironment.Commercial, "https://login.microsoftonline.com/adatum.onmicrosoft.com/v2.0/.well-known/openid-configuration")]
        [InlineData(CloudEnvironment.GccHigh, "https://login.microsoftonline.us/adatum.onmicrosoft.com/v2.0/.well-known/openid-configuration")]
        [InlineData(CloudEnvironment.China, "https://login.chinacloudapi.cn/adatum.onmicrosoft.com/v2.0/.well-known/openid-configuration")]
        public void AsksTheEnvironmentsSignInService(CloudEnvironment environment, string expected)
        {
            Assert.Equal(expected, TenantLookup.MetadataUrl(environment, " adatum.onmicrosoft.com "));
        }

        [Fact]
        public void ReadsTheTenantIdFromTheMetadata()
        {
            const string body = "{\"token_endpoint\":\"https://login.microsoftonline.com/7AC9C85C-6F6C-4CE6-8CC4-7F6FE24083E4/oauth2/v2.0/token\"," +
                                "\"issuer\":\"https://login.microsoftonline.com/7ac9c85c-6f6c-4ce6-8cc4-7f6fe24083e4/v2.0\",\"cloud_instance_name\":\"microsoftonline.com\"}";
            var r = TenantLookup.Interpret(200, body);
            Assert.Equal(TenantLookupStatus.Found, r.Status);
            Assert.Equal("7ac9c85c-6f6c-4ce6-8cc4-7f6fe24083e4", r.TenantId);
        }

        [Fact]
        public void RecognizesAnUnknownOrganization()
        {
            const string body = "{\"error\":\"invalid_tenant\",\"error_description\":\"AADSTS90002: Tenant 'nobody.onmicrosoft.com' not found.\",\"error_codes\":[90002]}";
            Assert.Equal(TenantLookupStatus.NotFound, TenantLookup.Interpret(400, body).Status);
            Assert.Equal(TenantLookupStatus.NotFound, TenantLookup.Interpret(400, "{\"error_codes\":[90002]}").Status);
        }

        [Fact]
        public void AnythingElseIsUnknownSoTheConnectionGoesAhead()
        {
            var r = TenantLookup.Interpret(500, "<html>busy</html>");
            Assert.Equal(TenantLookupStatus.Unknown, r.Status);
            Assert.Equal("HTTP 500", r.Message);
            Assert.Equal(TenantLookupStatus.Unknown, TenantLookup.Interpret(200, "{\"issuer\":\"x\"}").Status);
            Assert.Equal(TenantLookupStatus.Unknown, TenantLookup.Interpret(200, "{\"token_endpoint\":\"https://login.microsoftonline.com/common/oauth2/v2.0/token\"}").Status);
            Assert.Contains("AADSTS50000", TenantLookup.Interpret(400, "{\"error\":\"other\",\"error_description\":\"AADSTS50000: Something.\\r\\nTrace ID: 1\"}").Message);
        }
    }

    public class SettingsTests
    {
        [Fact]
        public void SearchOnlyOrganizationsArePreferredForTwoWeeks()
        {
            var s = new AppSettings();
            var when = new DateTime(2026, 10, 14, 8, 0, 0, DateTimeKind.Utc);
            s.RecordSessionType(" adatum.onmicrosoft.com ", searchOnly: true, when);
            s.RecordSessionType("", searchOnly: true, when);
            Assert.Single(s.SearchOnlyOrganizations);
            Assert.True(s.PrefersSearchOnly("ADATUM.onmicrosoft.com", when.AddDays(13)));
            Assert.False(s.PrefersSearchOnly("adatum.onmicrosoft.com", when.AddDays(15)));
            Assert.False(s.PrefersSearchOnly("fabrikam.onmicrosoft.com", when));
            Assert.False(s.PrefersSearchOnly(null, when));

            s.RecordSessionType("adatum.onmicrosoft.com", searchOnly: false, when.AddDays(1));
            Assert.False(s.PrefersSearchOnly("adatum.onmicrosoft.com", when.AddDays(1)));
            Assert.Empty(s.SearchOnlyOrganizations);
        }

        [Fact]
        public void SearchOnlyOrganizationsRoundTripAndExpire()
        {
            var s = new AppSettings();
            Assert.Null(s.ToJson()["searchOnlyOrganizations"]);
            var recent = DateTime.UtcNow.AddDays(-1);
            s.RecordSessionType("adatum.onmicrosoft.com", true, recent);
            s.RecordSessionType("tailspin.onmicrosoft.com", true, DateTime.UtcNow.AddDays(-20));

            var loaded = AppSettings.FromJson(Json.Parse(Json.Serialize(s.ToJson(), true)) as JsonMap);
            Assert.Equal(new[] { "adatum.onmicrosoft.com" }, loaded.SearchOnlyOrganizations.Keys);
            Assert.True(loaded.PrefersSearchOnly("adatum.onmicrosoft.com", DateTime.UtcNow));
            Assert.True(Math.Abs((loaded.SearchOnlyOrganizations["adatum.onmicrosoft.com"] - recent).TotalSeconds) < 1);

            var broken = Json.Parse("{\"searchOnlyOrganizations\":{\"adatum.onmicrosoft.com\":\"not a date\",\"fabrikam.onmicrosoft.com\":42}}") as JsonMap;
            Assert.Empty(AppSettings.FromJson(broken).SearchOnlyOrganizations);
        }

        [Fact]
        public void RecentOrganizationsAreMostRecentFirstWithoutDuplicates()
        {
            var s = new AppSettings();
            s.RememberOrganization("adatum.onmicrosoft.com");
            s.RememberOrganization(" fabrikam.onmicrosoft.com ");
            s.RememberOrganization("");
            s.RememberOrganization(null);
            s.RememberOrganization("ADATUM.onmicrosoft.com");
            Assert.Equal(new[] { "ADATUM.onmicrosoft.com", "fabrikam.onmicrosoft.com" }, s.RecentOrganizations);

            for (int i = 0; i < 20; i++) s.RememberOrganization($"tenant{i}.onmicrosoft.com");
            Assert.Equal(AppSettings.MaxRecentOrganizations, s.RecentOrganizations.Count);
            Assert.Equal("tenant19.onmicrosoft.com", s.RecentOrganizations[0]);
            Assert.Equal("tenant10.onmicrosoft.com", s.RecentOrganizations.Last());
        }

        [Fact]
        public void RoundTripsThroughJson()
        {
            var s = new AppSettings
            {
                AuthMode = AuthMode.Interactive,
                Environment = CloudEnvironment.GccHigh,
                UserPrincipalName = "alex@contoso.com",
                DelegatedOrganization = "adatum.onmicrosoft.com",
                Organization = "fabrikam.onmicrosoft.com",
                Bounds = new[] { 1, 2, 300, 400 },
            };
            s.RememberOrganization("fabrikam.onmicrosoft.com");
            s.RememberOrganization("adatum.onmicrosoft.com");

            var loaded = AppSettings.FromJson(Json.Parse(Json.Serialize(s.ToJson(), true)) as JsonMap);
            Assert.Equal(CloudEnvironment.GccHigh, loaded.Environment);
            Assert.Equal("alex@contoso.com", loaded.UserPrincipalName);
            Assert.Equal("adatum.onmicrosoft.com", loaded.DelegatedOrganization);
            Assert.Equal("fabrikam.onmicrosoft.com", loaded.Organization);
            Assert.Equal(new[] { "adatum.onmicrosoft.com", "fabrikam.onmicrosoft.com" }, loaded.RecentOrganizations);
            Assert.Equal(new[] { 1, 2, 300, 400 }, loaded.Bounds);
        }

        [Fact]
        public void SettingsFromEarlierVersionsSeedTheRecentList()
        {
            var old = Json.Parse("{\"authMode\":\"Certificate\",\"organization\":\"fabrikam.onmicrosoft.com\"}") as JsonMap;
            var s = AppSettings.FromJson(old);
            Assert.Equal(AuthMode.Certificate, s.AuthMode);
            Assert.Null(s.DelegatedOrganization);
            Assert.Equal(new[] { "fabrikam.onmicrosoft.com" }, s.RecentOrganizations);
            Assert.Empty(AppSettings.FromJson(new JsonMap()).RecentOrganizations);
            Assert.Empty(AppSettings.FromJson(null).RecentOrganizations);
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
                Assert.Contains("DelegatedOrganization", report);
                if (ModuleLocator.FindBest().Version >= new Version(3, 9, 0)) Assert.Contains("EnableSearchOnlySession", report);
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

        [Fact]
        public async System.Threading.Tasks.Task UnknownOrganizationFailsBeforeSigningIn()
        {
            const string nobody = "pe-test-nx-4f9a2c.onmicrosoft.com";
            if (ModuleLocator.FindBest() == null) return;
            // Only when the lookup can reach Microsoft Entra ID; otherwise connecting would go on to a sign-in prompt.
            if (TenantLookup.Resolve(CloudEnvironment.Commercial, nobody).Status != TenantLookupStatus.NotFound) return;

            var options = new ConnectOptions { UserPrincipalName = "alex@contoso.com", DelegatedOrganization = nobody };
            var progress = new List<string>();
            var ex = await Assert.ThrowsAsync<PurviewException>(() =>
                PurviewSession.ConnectAsync(options, PurviewCatalog.CmdletNames, new SyncProgress(progress.Add)));
            Assert.Contains("No organization has the domain " + nobody, ex.Message);
            Assert.Contains("Looking up " + nobody + "...", progress);
            Assert.DoesNotContain("Starting Windows PowerShell...", progress);
            Assert.DoesNotContain("Signing in to Security & Compliance PowerShell...", progress);
        }

        [Fact]
        public void LooksUpAKnownOrganization()
        {
            var r = TenantLookup.Resolve(CloudEnvironment.Commercial, "microsoft.onmicrosoft.com");
            if (r.Status == TenantLookupStatus.Unknown) return; // no internet access here
            Assert.Equal(TenantLookupStatus.Found, r.Status);
            Assert.Equal("72f988bf-86f1-41af-91ab-2d7cd011db47", r.TenantId);
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
