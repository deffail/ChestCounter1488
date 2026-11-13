using System.Diagnostics;
using System.Drawing;
using System.Runtime.Versioning;
using System.Windows.Forms;

namespace ChestCounter;

[SupportedOSPlatform("windows")]
public sealed partial class StatsForm : Form
{
    private readonly Monitor _monitor;
    private Label _summary, _odds;
    private NumericUpDown _oneIn, _next, _initial;
    private DataGridView _history;
    private ComboBox _reward;
    private CheckBox _claimed;
    private bool _loadingReward;
    private DropReward Reward => (DropReward)_reward.SelectedItem!;
    private MonitorState? _state;
    private bool _editingInitial;

    public StatsForm(Monitor monitor, Store store, MonitorState? state)
    {
        _monitor = monitor;
        BuildUi(monitor, store, state);
        _oneIn.ValueChanged += (_, _) =>
        {
            if (!_loadingReward) monitor.Change(c => c.RewardOdds[Reward.Id] = (int)_oneIn.Value);
            UpdateOdds();
        };
        _next.ValueChanged += (_, _) => UpdateOdds();
        _reward.SelectedIndexChanged += (_, _) =>
        {
            monitor.Change(c => c.SelectedReward = Reward.Id);
            LoadReward();
        };
        _claimed.CheckedChanged += (_, _) =>
        {
            if (_loadingReward) return;
            var key = _state?.CurrentKey ?? "";
            monitor.Change(c =>
            {
                if (!c.ClaimedRewards.TryGetValue(key, out var ids)) c.ClaimedRewards[key] = ids = new();
                ids.Remove(Reward.Id);
                if (_claimed.Checked && !Reward.Repeatable) ids.Add(Reward.Id);
            });
            UpdateOdds();
        };
        monitor.Changed += OnChanged;
        FormClosed += (_, _) =>
        {
            monitor.Changed -= OnChanged;
            monitor.Change(c => { c.DropNextChests = (int)_next.Value; });
        };
        LoadReward();
        if (state != null) Render(state);
        else UpdateOdds();
        Shown += (_, _) => monitor.RequestPublish();
    }

    private void OnChanged(MonitorState state)
    {
        if (!IsHandleCreated || IsDisposed) return;
        try { BeginInvoke(new Action(() => { if (!IsDisposed) Render(state); })); }
        catch (InvalidOperationException) { }
    }

    private void Render(MonitorState state)
    {
        var profileChanged = _state?.CurrentKey != state.CurrentKey;
        var historyChanged = _state == null || profileChanged || !_state.Desktop.History.SequenceEqual(state.Desktop.History);
        _state = state;
        if (profileChanged) LoadReward();
        var d = state.Desktop;
        var name = state.Profiles.FirstOrDefault(p => p.Key == state.CurrentKey)?.Name ?? "—";
        _summary.Text = L.T($"Профиль {name} · локальная статистика", $"Profile {name} · local statistics");
        _totalValue.Text = d.Total.ToString("N0", L.Culture);
        _matchValue.Text = state.View.Current ?? "—";
        _matchCaption.Text = state.View.CurrentLive ? L.T("текущий · из реплея", "current · from replay") : L.T("последний завершённый", "last completed");
        _sessionValue.Text = d.SessionChests.ToString("N0", L.Culture);
        _sessionDetail.Text = L.T($"завершено матчей: {d.SessionMatches}", $"completed matches: {d.SessionMatches}");
        _averageValue.Text = d.History.Count == 0 ? "—" : d.Average.ToString("0.0", L.Culture);
        _averageDetail.Text = L.T($"по {d.History.Count} матчам истории", $"across {d.History.Count} matches");
        _historyTitle.Text = d.History.Count == 0 ? L.T("Здесь появятся завершённые матчи", "Completed matches will appear here") : L.T($"История · {d.History.Count} матчей", $"History · {d.History.Count} matches");
        _overlay.Text = _monitor.OverlayVisible ? L.T("Оверлей: вкл.", "Overlay: on") : L.T("Оверлей: выкл.", "Overlay: off");
        if (profileChanged || !_editingInitial) { _initial.Value = d.Initial; _editingInitial = false; }
        if (historyChanged) { _history.DataSource = d.History.ToList(); _history.ClearSelection(); }
        UpdateOdds();
    }

    private void UpdateOdds()
    {
        if (_loadingReward) return;
        var n = (int)_oneIn.Value;
        var count = _state?.Desktop.Total ?? 0;
        var p = DropMath.Probability(n, count);
        var next = DropMath.Probability(n, (long)_next.Value);
        var claimed = !Reward.Repeatable && _claimed.Checked;
        _chanceValue.Text = claimed ? "✓" : FormatChance(p);
        _chanceCaption.Text = claimed ? L.T("награда уже получена", "reward already received") : L.T($"за {count:N0} сундуков", $"in {count:N0} chests");
        _nextValue.Text = claimed ? "0%" : FormatChance(next);
        _nextCaption.Text = L.T($"за следующие {_next.Value:N0}", $"in the next {_next.Value:N0}");
        if (!Reward.Repeatable && _claimed.Checked)
        {
            _odds.Text = L.T("Награда отмечена как полученная. Повторный дроп: 0%.\n" +
                "Если станет доступна новая награда, снимите отметку вручную.\nАвтоматического сброса по неделям нет.",
                "Reward marked as received. Repeat drop: 0%.\n" +
                "If a new reward becomes available, uncheck it manually.\nThere is no automatic weekly reset.");
            return;
        }
        var expected = Reward.ExpectedRewards(n, count, false);
        _odds.Text = L.T(
            $"В среднем до первого дропа: {n:N0} сундуков\n" +
            $"Ожидаемых наград: {expected:0.###}" + (Reward.Repeatable ? " · повторяемая\n\n" : " · максимум 1\n\n") +
            "Сундуков для заданной вероятности\n" +
            $"50%: {DropMath.ChestsFor(n, .5):N0}   ·   90%: {DropMath.ChestsFor(n, .9):N0}   ·   99%: {DropMath.ChestsFor(n, .99):N0}",
            $"Expected chests to first drop: {n:N0}\n" +
            $"Expected rewards: {expected:0.###}" + (Reward.Repeatable ? " · repeatable\n\n" : " · at most 1\n\n") +
            "Chests needed for a given probability\n" +
            $"50%: {DropMath.ChestsFor(n, .5):N0}   ·   90%: {DropMath.ChestsFor(n, .9):N0}   ·   99%: {DropMath.ChestsFor(n, .99):N0}");
    }

    private void LoadReward()
    {
        _loadingReward = true;
        _oneIn.Value = Math.Clamp(_monitor.Config.RewardOdds.GetValueOrDefault(Reward.Id,
            Reward.Id == "custom" ? _monitor.Config.DropOneIn : Reward.OneIn), 1, 1000000000);
        _claimed.Enabled = !Reward.Repeatable;
        _claimed.Checked = !Reward.Repeatable && _monitor.Config.ClaimedRewards.TryGetValue(_state?.CurrentKey ?? "", out var ids) && ids.Contains(Reward.Id);
        _loadingReward = false;
        UpdateOdds();
    }

    private static string FormatChance(double p) => p > 0 && p < .00001
        ? "<" + .00001.ToString("P3", L.Culture)
        : p < 1 && p >= .9995 ? ">" + .999.ToString("P1", L.Culture)
        : p.ToString(p < .01 && p > 0 ? "P3" : "P1", L.Culture);
    private static NumericUpDown Number(int max, int min, int value) => new()
    { Minimum = min, Maximum = max, Value = value, Width = 118, ThousandsSeparator = true, BackColor = Theme.Field, ForeColor = Theme.Ink };
}
