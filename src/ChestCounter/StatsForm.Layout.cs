using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace ChestCounter;

public sealed partial class StatsForm
{
    private static readonly Color SurfaceColor = Color.FromArgb(22, 23, 30);
    private static readonly Color FieldColor = Color.FromArgb(31, 32, 41);
    private Label _totalValue, _matchValue, _matchCaption, _sessionValue, _sessionDetail, _averageValue, _averageDetail;
    private Label _chanceValue, _chanceCaption, _nextValue, _nextCaption, _historyTitle;
    private Button _overlay;

    private void BuildUi(Monitor monitor, Store store, MonitorState? state)
    {
        Text = L.T("ChestCounter · Статистика", "ChestCounter · Statistics");
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96, 96);
        ClientSize = new Size(920, 650);
        MinimumSize = new Size(860, 660);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Color.FromArgb(14, 15, 20);
        ForeColor = Theme.Ink;
        Font = Theme.Text(10);
        Icon = AppIcon.Load();
        DoubleBuffered = true;

        var root = Grid(1, 5);
        root.Padding = new Padding(22, 12, 22, 12);
        foreach (var h in new[] { 66, 112, 48 }) root.RowStyles.Add(new RowStyle(SizeType.Absolute, h));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        Controls.Add(root);
        var header = Grid(2, 2);
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 135));
        header.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        header.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        header.Controls.Add(Label("CHESTCOUNTER", 17, Theme.Ink, true), 0, 0);
        _summary = Label("", 9, Theme.Muted);
        header.Controls.Add(_summary, 0, 1);
        _overlay = ActionButton(L.T("Оверлей", "Overlay"));
        _overlay.Dock = DockStyle.Fill;
        _overlay.Click += (_, _) => monitor.ToggleOverlay();
        header.Controls.Add(_overlay, 1, 0);
        root.Controls.Add(header, 0, 0);

        var metrics = Grid(4, 1);
        for (var i = 0; i < 4; i++) metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        metrics.Controls.Add(Metric(L.T("ВСЕГО СУНДУКОВ", "TOTAL CHESTS"), out _totalValue, out _, Theme.Accent), 0, 0);
        metrics.Controls.Add(Metric(L.T("МАТЧ", "MATCH"), out _matchValue, out _matchCaption, Theme.Ink), 1, 0);
        metrics.Controls.Add(Metric(L.T("ЗА СЕССИЮ", "THIS SESSION"), out _sessionValue, out _sessionDetail, Theme.Ink), 2, 0);
        metrics.Controls.Add(Metric(L.T("СРЕДНЕЕ ЗА МАТЧ", "AVERAGE / MATCH"), out _averageValue, out _averageDetail, Theme.Ink), 3, 0);
        root.Controls.Add(metrics, 0, 1);

        var tabs = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 7, 0, 4) };
        var rewardsTab = ActionButton(L.T("Награды и шансы", "Rewards & odds"));
        var historyTab = ActionButton(L.T("История матчей", "Match history"));
        rewardsTab.Width = historyTab.Width = 170;
        tabs.Controls.Add(rewardsTab); tabs.Controls.Add(historyTab);
        root.Controls.Add(tabs, 0, 2);
        var pages = new Panel { Dock = DockStyle.Fill };
        root.Controls.Add(pages, 0, 3);
        var rewardsPage = Grid(2, 1);
        rewardsPage.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 296));
        rewardsPage.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var historyPage = new Surface { Dock = DockStyle.Fill, Padding = new Padding(18) };
        pages.Controls.Add(historyPage); pages.Controls.Add(rewardsPage);
        void SelectPage(bool rewards)
        {
            rewardsPage.Visible = rewards; historyPage.Visible = !rewards;
            rewardsTab.BackColor = rewards ? Theme.ActiveBg : FieldColor;
            historyTab.BackColor = rewards ? FieldColor : Theme.ActiveBg;
            rewardsTab.ForeColor = rewards ? Theme.Accent : Theme.Muted;
            historyTab.ForeColor = rewards ? Theme.Muted : Theme.Accent;
        }
        rewardsTab.Click += (_, _) => SelectPage(true); historyTab.Click += (_, _) => SelectPage(false);
        SelectPage(true);

        var settings = new Surface { Dock = DockStyle.Fill, Padding = new Padding(18), Margin = new Padding(0, 0, 12, 0) };
        rewardsPage.Controls.Add(settings, 0, 0);
        var fields = Grid(1, 7);
        foreach (var h in new[] { 28, 38, 56, 56, 36, 38 }) fields.RowStyles.Add(new RowStyle(SizeType.Absolute, h));
        fields.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        settings.Controls.Add(fields);
        fields.Controls.Add(Label(L.T("Выбери награду", "Choose a reward"), 11, Theme.Ink, true), 0, 0);
        _reward = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top,
            FlatStyle = FlatStyle.Flat, BackColor = FieldColor, ForeColor = Theme.Ink, IntegralHeight = false, DropDownHeight = 260 };
        _reward.Items.AddRange(DropReward.All);
        _reward.DrawMode = DrawMode.OwnerDrawFixed;
        _reward.ItemHeight = 23;
        _reward.DrawItem += (_, e) =>
        {
            using var fill = new SolidBrush((e.State & DrawItemState.Selected) != 0 ? Theme.ActiveBg : FieldColor);
            e.Graphics.FillRectangle(fill, e.Bounds);
            var item = e.Index >= 0 ? _reward.Items[e.Index]?.ToString() : _reward.SelectedItem?.ToString();
            TextRenderer.DrawText(e.Graphics, item ?? "", _reward.Font, Rectangle.Inflate(e.Bounds, -5, 0), Theme.Ink,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            e.DrawFocusRectangle();
        };
        _reward.SelectedItem = DropReward.All.FirstOrDefault(r => r.Id == monitor.Config.SelectedReward) ?? DropReward.All[3];
        fields.Controls.Add(_reward, 0, 1);
        _oneIn = Number(1000000000, 1, Math.Clamp(monitor.Config.DropOneIn, 1, 1000000000));
        fields.Controls.Add(Field(L.T("Шанс за сундук · 1 из", "Chance per chest · 1 in"), _oneIn), 0, 2);
        _next = Number(1000000000, 0, Math.Clamp(monitor.Config.DropNextChests, 0, 1000000000));
        fields.Controls.Add(Field(L.T("Сколько ещё сундуков?", "How many more chests?"), _next), 0, 3);
        _claimed = new CheckBox { Text = L.T("Уже получено", "Already received"), Dock = DockStyle.Fill,
            FlatStyle = FlatStyle.Flat, ForeColor = Theme.Ink, Cursor = Cursors.Hand, AutoSize = true };
        fields.Controls.Add(_claimed, 0, 4);
        var reset = ActionButton(L.T("Вернуть исходный шанс", "Restore default odds"));
        reset.Dock = DockStyle.Top;
        reset.Click += (_, _) => _oneIn.Value = Reward.OneIn;
        fields.Controls.Add(reset, 0, 5);

        var result = new Surface { Dock = DockStyle.Fill, Padding = new Padding(20), Margin = Padding.Empty };
        rewardsPage.Controls.Add(result, 1, 0);
        var results = Grid(1, 4);
        results.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        results.RowStyles.Add(new RowStyle(SizeType.Absolute, 93));
        results.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        results.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        result.Controls.Add(results);
        results.Controls.Add(Label(L.T("Вероятность хотя бы одной награды", "Chance of at least one reward"), 11, Theme.Ink, true), 0, 0);
        var chances = Grid(2, 1);
        chances.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); chances.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        chances.Controls.Add(Chance(out _chanceValue, out _chanceCaption), 0, 0);
        chances.Controls.Add(Chance(out _nextValue, out _nextCaption), 1, 0);
        results.Controls.Add(chances, 0, 1);
        _odds = Label("", 10, Theme.Ink);
        results.Controls.Add(_odds, 0, 2);
        results.Controls.Add(Label(L.T("Модель, не гарантия. Шансы со скриншота; попытки независимы. Каждый учтённый сундук должен подходить для награды.",
            "A model, not a guarantee. User-supplied odds, independent trials. Every counted chest must be eligible for this reward."), 8.5f, Theme.Muted), 0, 3);

        var historyLayout = Grid(1, 3);
        historyLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
        historyLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        historyLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        historyPage.Controls.Add(historyLayout);
        _historyTitle = Label("", 11, Theme.Ink, true);
        historyLayout.Controls.Add(_historyTitle, 0, 0);
        _history = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
            RowHeadersVisible = false, AutoGenerateColumns = false, BackgroundColor = SurfaceColor, BorderStyle = BorderStyle.None,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, EnableHeadersVisualStyles = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None, GridColor = FieldColor, ColumnHeadersHeight = 34,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing };
        _history.RowTemplate.Height = 34;
        _history.DefaultCellStyle.BackColor = SurfaceColor; _history.DefaultCellStyle.ForeColor = Theme.Ink;
        _history.DefaultCellStyle.SelectionBackColor = Theme.ActiveBg; _history.DefaultCellStyle.SelectionForeColor = Theme.Ink;
        _history.DefaultCellStyle.Padding = new Padding(8, 0, 8, 0);
        _history.ColumnHeadersDefaultCellStyle.BackColor = SurfaceColor; _history.ColumnHeadersDefaultCellStyle.ForeColor = Theme.Muted;
        _history.ColumnHeadersDefaultCellStyle.SelectionBackColor = SurfaceColor; _history.ColumnHeadersDefaultCellStyle.Font = Theme.Text(9);
        foreach (var (name, title) in new[] { ("Date", L.T("Дата", "Date")), ("Chests", L.T("Сундуки", "Chests")), ("Placement", L.T("Место", "Place")), ("Mode", L.T("Режим", "Mode")) })
            _history.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = name, HeaderText = title, SortMode = DataGridViewColumnSortMode.NotSortable });
        _history.Columns[0].DefaultCellStyle.Format = "dd.MM.yyyy HH:mm";
        _history.Columns[0].FillWeight = 140; _history.Columns[2].DefaultCellStyle.NullValue = "—";
        historyLayout.Controls.Add(_history, 0, 1);
        var correction = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 12, 0, 0) };
        correction.Controls.Add(new Label { Text = L.T("До установки", "Before installation"), AutoSize = true, ForeColor = Theme.Muted, Margin = new Padding(0, 6, 10, 0) });
        _initial = Number(int.MaxValue, 0, state?.Desktop.Initial ?? monitor.InitialChests);
        _initial.Width = 120; correction.Controls.Add(_initial);
        var save = ActionButton(L.T("Применить", "Apply")); save.Width = 110;
        save.Click += (_, _) => { monitor.SetInitialChests((int)_initial.Value, _state?.CurrentKey); _editingInitial = false; };
        _initial.ValueChanged += (_, _) => _editingInitial = true; correction.Controls.Add(save);
        var report = ActionButton(L.T("HTML-отчёт ↗", "HTML report ↗")); report.Width = 132;
        report.Click += (_, _) => { try { Process.Start(new ProcessStartInfo(store.ReportPath) { UseShellExecute = true }); } catch (Exception ex) { MessageBox.Show(this, ex.Message, Text); } };
        correction.Controls.Add(report); historyLayout.Controls.Add(correction, 0, 2);
        var note = Label(L.T("Из реплеев · данные могут появиться только после матча · всё хранится на этом ПК",
            "From replays · counts may arrive only after the match · stored on this PC"), 8.5f, Theme.Muted);
        note.TextAlign = ContentAlignment.MiddleLeft; root.Controls.Add(note, 0, 4);
    }

    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Theme.Chrome(Handle); }
    private static TableLayoutPanel Grid(int columns, int rows) => new() { Dock = DockStyle.Fill, ColumnCount = columns, RowCount = rows, Margin = Padding.Empty, BackColor = Color.Transparent };
    private static Label Label(string text, float size, Color color, bool bold = false) => new() { Text = text, Dock = DockStyle.Fill, AutoEllipsis = true,
        ForeColor = color, BackColor = Color.Transparent, Font = Theme.Text(size, bold ? FontStyle.Bold : FontStyle.Regular), Margin = Padding.Empty };
    private static Control Metric(string title, out Label value, out Label detail, Color color)
    {
        var card = new Surface { Dock = DockStyle.Fill, Padding = new Padding(14, 12, 10, 10), Margin = new Padding(0, 0, 10, 0) };
        var grid = Grid(1, 3);
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 20)); grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 19));
        value = Label("—", 25, color, true); detail = Label("", 8, Theme.Muted);
        grid.Controls.Add(Label(title, 8, Theme.Muted, true), 0, 0); grid.Controls.Add(value, 0, 1); grid.Controls.Add(detail, 0, 2); card.Controls.Add(grid); return card;
    }
    private static Control Chance(out Label value, out Label caption)
    {
        var grid = Grid(1, 2); grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 55)); grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        value = Label("—", 30, Theme.Accent, true); caption = Label("", 9, Theme.Muted);
        grid.Controls.Add(value, 0, 0); grid.Controls.Add(caption, 0, 1); return grid;
    }
    private static Control Field(string title, NumericUpDown input)
    {
        var grid = Grid(1, 2); grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 23)); grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        grid.Controls.Add(Label(title, 9, Theme.Muted), 0, 0); input.Dock = DockStyle.Top; input.BackColor = FieldColor; input.BorderStyle = BorderStyle.FixedSingle; grid.Controls.Add(input, 0, 1); return grid;
    }
    private static Button ActionButton(string text)
    {
        var b = new Button { Text = text, Height = 32, Width = 150, FlatStyle = FlatStyle.Flat, BackColor = FieldColor, ForeColor = Theme.Ink,
            Cursor = Cursors.Hand, Margin = new Padding(0, 0, 8, 0), Font = Theme.Text(9, FontStyle.Bold) };
        b.FlatAppearance.BorderSize = 0; b.FlatAppearance.MouseOverBackColor = Theme.ActiveBg; return b;
    }
    private sealed class Surface : Panel
    {
        public Surface() { DoubleBuffered = true; BackColor = SurfaceColor; ResizeRedraw = true; }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Color.FromArgb(14, 15, 20)); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = new GraphicsPath(); const int d = 20; var r = new Rectangle(0, 0, Width - 1, Height - 1);
            if (r.Width < d || r.Height < d) return;
            path.AddArc(r.Left, r.Top, d, d, 180, 90); path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90); path.CloseFigure();
            using var fill = new SolidBrush(SurfaceColor); using var edge = new Pen(Color.FromArgb(37, 38, 48));
            e.Graphics.FillPath(fill, path); e.Graphics.DrawPath(edge, path);
        }
    }
}
