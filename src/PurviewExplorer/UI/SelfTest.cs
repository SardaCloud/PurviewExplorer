// Copyright (C) 2026 Francesco Valerio Buccoli. Sarda Cloud Community.
// Licensed under the MIT License. See LICENSE in the repository root.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using SardaCloud.PurviewExplorer.Catalog;
using SardaCloud.PurviewExplorer.Data;

namespace SardaCloud.PurviewExplorer.UI
{
    /// <summary>
    /// "/SelfTest:log.txt": drives the real UI with the demo data (every tab, collection, item list, properties dialog,
    /// filter and snapshot round trip), writes PASS/FAIL lines to the log and exits with 0 (all passed) or 1.
    /// </summary>
    internal sealed class SelfTest
    {
        private readonly List<string> _log = new List<string>();
        private int _failures;

        public static async Task<int> RunAsync(Workspace ws, TabControl tabs, string logPath)
        {
            var test = new SelfTest();
            try
            {
                await test.RunCoreAsync(ws, tabs);
            }
            catch (Exception ex)
            {
                test.Check(false, "unexpected exception: " + ex);
            }
            test._log.Add(test._failures == 0 ? "RESULT: PASS" : $"RESULT: FAIL ({test._failures} failures)");
            File.WriteAllLines(logPath, test._log);
            return test._failures == 0 ? 0 : 1;
        }

        private void Check(bool condition, string what)
        {
            _log.Add((condition ? "PASS " : "FAIL ") + what);
            if (!condition) _failures++;
        }

        private static async Task Pump()
        {
            Application.DoEvents();
            await Task.Delay(20);
            Application.DoEvents();
        }

        private async Task RunCoreAsync(Workspace ws, TabControl tabs)
        {
            ws.ShowStore(Snapshot.LoadDemo());
            await Pump();
            Check(tabs.TabCount == PurviewCatalog.Solutions.Count + 1, $"one tab per solution plus Summary ({tabs.TabCount})");

            for (int i = 0; i < tabs.TabCount; i++)
            {
                tabs.SelectedIndex = i;
                await Pump();
                if (!(tabs.SelectedTab.Tag is SolutionView view)) continue;
                Check(view.Visible, $"tab '{tabs.SelectedTab.Text}' is visible");

                foreach (var def in view.Solution.Collections)
                {
                    view.SelectCollection(def.Key);
                    await Pump();
                    var r = ws.Store.Get(def.Key);
                    Check(view.SelectedCollection == def, $"{def.Key}: selected");
                    if (r.State != LoadState.Loaded)
                    {
                        Check(!string.IsNullOrEmpty(view.MessageText), $"{def.Key}: {r.State} shows a message");
                        continue;
                    }
                    Check(view.ShownCount == r.Items.Count, $"{def.Key}: shows {view.ShownCount} of {r.Items.Count} items");

                    view.SelectItemAt(0);
                    await Pump();
                    Check(view.SelectedItem != null, $"{def.Key}: first item selected ({ItemHelper.Title(view.SelectedItem)})");

                    using (var dlg = new ItemPropertiesDialog(def, view.SelectedItem, ws.Store))
                    {
                        dlg.Show(tabs.FindForm());
                        await Pump();
                        var pages = dlg.Controls.OfType<Panel>().SelectMany(p => p.Controls.OfType<TabControl>()).FirstOrDefault();
                        Check(pages != null && pages.TabCount >= 2 + def.Relations.Count(rel => ws.Store.Get(rel.CollectionKey).State == LoadState.Loaded && rel.CollectionKey != def.Key),
                            $"{def.Key}: properties dialog has General, related and JSON pages ({pages?.TabCount})");
                        if (pages != null)
                        {
                            for (int p = 0; p < pages.TabCount; p++)
                            {
                                pages.SelectedIndex = p;
                                await Pump();
                            }
                        }
                        dlg.Close();
                    }

                    view.SetFilter("zz-no-such-text-zz");
                    Check(view.ShownCount == 0 && !string.IsNullOrEmpty(view.MessageText), $"{def.Key}: non-matching filter hides all items");
                    var firstTitle = ItemHelper.Title(r.Items[0]);
                    view.SetFilter(firstTitle);
                    Check(view.ShownCount >= 1, $"{def.Key}: filter '{firstTitle}' finds the item");
                    view.SetFilter("");
                    Check(view.ShownCount == r.Items.Count, $"{def.Key}: clearing the filter restores all items");

                    var csv = CsvExport.ToCsv(def, r.Items);
                    Check(!string.IsNullOrEmpty(csv), $"{def.Key}: CSV export produced {csv.Length} characters");
                }
            }

            var path = Path.Combine(Path.GetTempPath(), "PurviewExplorer-selftest-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                Snapshot.Save(ws.Store, path);
                var reloaded = Snapshot.Load(path);
                foreach (var def in PurviewCatalog.AllCollections)
                {
                    var a = ws.Store.Get(def.Key);
                    var b = reloaded.Get(def.Key);
                    Check(a.State == b.State && a.Items.Count == b.Items.Count, $"{def.Key}: snapshot round trip ({b.State}, {b.Items.Count} items)");
                }
                ws.ShowStore(reloaded);
                await Pump();
                Check(ws.Store.Source.Kind == SourceKind.Demo, "reloaded snapshot keeps the demo source kind");
            }
            finally
            {
                File.Delete(path);
            }

            tabs.SelectedIndex = 0;
            await Pump();
        }
    }
}
