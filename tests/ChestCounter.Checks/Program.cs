using ChestCounter;
using System.Drawing;
using System.Windows.Forms;

internal static class Program
{
    private static int _checks;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        _checks++;
    }

    [STAThread]
    private static void Main(string[] args)
    {
        L.Init("ru");
        Check(DropMath.Probability(2000, 0) == 0, "No trials");
        Check(DropMath.Probability(1, 1) == 1, "Guaranteed drop");
        Check(Math.Abs(DropMath.Probability(2000, 2000) - .63221254785) < 1e-9, "Known probability");
        Check(DropMath.ChestsFor(2000, .9) == 4605, "90 percent threshold");
        foreach (var n in new[] { 1, 2, 2000, 1000000000 })
        foreach (var p in new[] { .5, .9, .99 })
        {
            var k = DropMath.ChestsFor(n, p);
            Check(DropMath.Probability(n, k) >= p && DropMath.Probability(n, k - 1) < p, "Minimal threshold");
        }
        try { DropMath.Probability(0, 10); throw new Exception("Invalid odds accepted"); }
        catch (ArgumentOutOfRangeException) { _checks++; }
        Check(DropReward.All.Select(r => r.OneIn).SequenceEqual(new[] { 1562500, 137500, 7813, 568, 391, 272, 127, 2000 }), "User-supplied odds");
        var single = DropReward.All.First(r => r.Id == "pickaxe");
        var repeat = DropReward.All.First(r => r.Id == "gizmo");
        Check(single.ExpectedRewards(568, 10000, false) <= 1 && single.ExpectedRewards(568, 10000, true) == 0, "Single reward cap and claimed state");
        Check(repeat.ExpectedRewards(127, 254, true) == 2, "Gizmo remains repeatable");

        var start = ReportWriter.SessionStartUtc;
        var old = new MatchRecord { SessionId = "old", EpicId = "a", PlayerName = "Player", ChestsOpened = 10, MatchStartUtc = start.AddDays(-1) };
        var current = new MatchRecord { SessionId = "new", EpicId = "a", PlayerName = "Player", ChestsOpened = 20, MatchStartUtc = start };
        var live = new MatchRecord { ChestsOpened = 7, EpicId = "a" };
        var data = new TrackerData { Matches = new() { old, current, new() { EpicId = "b", ChestsOpened = 999 } } };
        var config = new Config { Profile = "a", InitialChests = new() { ["a"] = 1000 }, DropOneIn = 5000, DropNextChests = 350 };
        var filtered = Profiles.View(data, config);
        var stats = DesktopStats.Build(filtered.Matches, live, Profiles.InitialChests(data, config));
        Check(stats.Total == 1037 && stats.Average == 15 && stats.History.Count == 2, "Profile and correction isolation");
        Check(stats.SessionChests == 27 && stats.SessionMatches == 1, "Session excludes backlog and counts live once");
        Check(DesktopStats.Build(Array.Empty<MatchRecord>(), null, 50).Average == 0, "Empty history");
        Check(DesktopStats.Build(Enumerable.Repeat(old, 101), null, 0).History.Count == 101, "Full history");
        var view = OverlayModel.Build(filtered, null, true, 1000);
        Check(view.Current == "…" && view.CurrentLive, "Unavailable live is not zero");
        Check(OverlayModel.Build(filtered, null, false).Current == "20", "Last match fallback");
        var final = DesktopStats.Build(filtered.Matches.Append(live), null, 1000);
        Check(final.Total == stats.Total, "Live to completed without double count");

        var folder = Path.GetFullPath(args.Length > 0 ? args[0] : "check-output");
        Directory.CreateDirectory(folder);
        var store = new Store(Path.Combine(folder, "test-data"));
        store.SaveConfig(config);
        store.SaveData(data);
        var restored = store.LoadConfig()!;
        Check(restored.DropOneIn == 5000 && restored.DropNextChests == 350 && restored.InitialChests["a"] == 1000,
            "Calculator and correction persist");
        Check(store.LoadData().Matches.Count == 3, "History persists");
        File.WriteAllText(store.ConfigPath, "{\"Profile\":\"a\"}");
        Check(store.LoadConfig()!.DropOneIn == 2000, "Old config defaults");

        Application.SetHighDpiMode(HighDpiMode.DpiUnaware);
        Application.EnableVisualStyles();
        var tracker = new Tracker(store, config, data);
        var monitor = new ChestCounter.Monitor(store, config, data, tracker, store.Folder);
        var state = new MonitorState(OverlayModel.Build(filtered, live, true, 1000), Profiles.List(data), "a", null, false, ChestStats.Empty) { Desktop = stats };
        using (var form = new StatsForm(monitor, store, state))
        {
            form.Show();
            Application.DoEvents();
            var controls = Descendants(form).ToList();
            var rewardPicker = controls.OfType<ComboBox>().Single();
            var received = controls.OfType<CheckBox>().Single();
            received.Checked = true;
            Check(config.ClaimedRewards["a"].Contains("pickaxe"), "Claim saved for displayed profile");
            Check(controls.OfType<Label>().Any(l => l.Text.Contains("Повторный дроп: 0%")), "Claimed reward UI");
            rewardPicker.SelectedItem = repeat;
            Check(!received.Enabled && !received.Checked, "Repeatable reward cannot be exhausted");
            rewardPicker.SelectedItem = single;
            Check(received.Checked, "Claim survives reward switch");
            received.Checked = false;
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
            bitmap.Save(Path.Combine(folder, "stats-preview.png"));
            controls.OfType<Button>().Single(b => b.Text == "История матчей").PerformClick();
            Application.DoEvents();
            Check(controls.OfType<DataGridView>().Single().Visible, "History tab opens");
            using var historyBitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(historyBitmap, new Rectangle(Point.Empty, form.Size));
            historyBitmap.Save(Path.Combine(folder, "history-preview.png"));
            controls.OfType<Button>().Single(b => b.Text == "Награды и шансы").PerformClick();
            form.Size = form.MinimumSize;
            rewardPicker.SelectedItem = DropReward.All[0];
            Application.DoEvents();
            using var smallBitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(smallBitmap, new Rectangle(Point.Empty, form.Size));
            smallBitmap.Save(Path.Combine(folder, "small-preview.png"));
            form.Close();
        }
        NativeOverlay.Start(-20000, -20000, false, (_, _) => { }, _ => { });
        NativeOverlay.Update(state.View);
        Thread.Sleep(700);
        Check(NativeOverlay.Snapshot(Path.Combine(folder, "overlay-preview.png")), "Native overlay render");
        NativeOverlay.Update(state.View with { Rows = Array.Empty<OverlayRow>() });
        Thread.Sleep(700);
        Check(NativeOverlay.Snapshot(Path.Combine(folder, "overlay-compact.png")), "Compact overlay render");
        using (var fullOverlay = Image.FromFile(Path.Combine(folder, "overlay-preview.png")))
        using (var compactOverlay = Image.FromFile(Path.Combine(folder, "overlay-compact.png")))
            Check(fullOverlay.Height > compactOverlay.Height, "Overlay includes its statistics rows");
        NativeOverlay.Stop();
        Console.WriteLine($"PASS: {_checks} checks; previews: {folder}");
    }

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control c in parent.Controls)
        {
            yield return c;
            foreach (var child in Descendants(c)) yield return child;
        }
    }
}
