using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// ─────────────────────────────────────────────────────────────────────────────
// AnalyticsDashboard — OLAP dashboard controller (scene-node edition)
//
// All UI layout, colours, and fonts live in Scenes/analytics.tscn.
// This script only handles:
//   • Wiring button signals
//   • Populating OptionButton items
//   • Running OLAP queries (via OlapManager)
//   • Updating label text & chart data
//   • Dynamically appending table rows (variable-length content)
//
// Every node is fetched with GetNode<T>("%UniqueName") — unique names are
// marked in the .tscn with  unique_name_in_owner = true.
// ─────────────────────────────────────────────────────────────────────────────

public partial class AnalyticsDashboard : Control
{
    // ── Shared toolbar ────────────────────────────────────────────────────────
    private Button       _syncBtn;
    private Button       _exportBtn;
    private Button       _backBtn;
    private OptionButton _itemSelector;
    private OptionButton _dateSelector;
    private OptionButton _olapSelector;
    private Button       _applyBtn;
    private Label        _statusLabel;

    // ── Trade OLAP tab ────────────────────────────────────────────────────────
    private Label         _sumTradesVal;
    private Label         _sumVolumeVal;
    private Label         _sumAvgPriceVal;
    private Label         _sumTopItemVal;
    private Label         _tradeChartTitle;
    private PriceChart    _tradeChart;
    private Label         _tradeTableTitle;
    private VBoxContainer _tradeTableContainer;

    // ── Score Trends tab ──────────────────────────────────────────────────────
    private Label         _scTotalRunsVal;
    private Label         _scMaxScoreVal;
    private Label         _scAvgScoreVal;
    private Label         _scMinScoreVal;
    private Label         _scTop1NameVal;
    private Label         _scTop1ScoreVal;
    private Label         _scoreChartTitle;
    private PriceChart    _scoreChart;
    private Label         _scoreDailyTitle;
    private PriceChart    _scoreDailyChart;
    private VBoxContainer _scoreTableContainer;

    // ── State ─────────────────────────────────────────────────────────────────
    private OlapManager _olap;
    private string      _selectedItem   = "all";
    private int         _selectedDays   = 0;
    private int         _selectedOlapOp = 0;

    private static readonly string[] OlapOpNames =
    {
        "Roll-Up by Item",
        "Roll-Up by Day",
        "Drill-Down (Item)",
        "Slice (by Item)",
        "Slice (by Date)",
        "Dice (Item + Date)",
    };

    private static readonly int[]    DateDayOptions   = { 0, 7, 30, 90 };
    private static readonly string[] DateOptionLabels = { "All Time", "Last 7 Days", "Last 30 Days", "Last 90 Days" };

    // ─────────────────────────────────────────────────────────────────────────
    // _Ready — grab nodes, wire signals, populate dropdowns, load data
    // ─────────────────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        // ── Fetch all scene nodes ─────────────────────────────────────────────

        // Toolbar
        _syncBtn   = GetNode<Button>("%SyncButton");
        _exportBtn = GetNode<Button>("%ExportButton");
        _backBtn   = GetNode<Button>("%BackButton");
        _applyBtn  = GetNode<Button>("%ApplyButton");
        _statusLabel  = GetNode<Label>("%StatusLabel");
        _itemSelector = GetNode<OptionButton>("%ItemSelector");
        _dateSelector = GetNode<OptionButton>("%DateSelector");
        _olapSelector = GetNode<OptionButton>("%OlapSelector");

        // Trade tab
        _sumTradesVal        = GetNode<Label>("%SumTradesVal");
        _sumVolumeVal        = GetNode<Label>("%SumVolumeVal");
        _sumAvgPriceVal      = GetNode<Label>("%SumAvgPriceVal");
        _sumTopItemVal       = GetNode<Label>("%SumTopItemVal");
        _tradeChartTitle     = GetNode<Label>("%TradeChartTitle");
        _tradeChart          = GetNode<PriceChart>("%TradeChart");
        _tradeTableTitle     = GetNode<Label>("%TradeTableTitle");
        _tradeTableContainer = GetNode<VBoxContainer>("%TradeTableContainer");

        // Score tab
        _scTotalRunsVal      = GetNode<Label>("%ScTotalRunsVal");
        _scMaxScoreVal       = GetNode<Label>("%ScMaxScoreVal");
        _scAvgScoreVal       = GetNode<Label>("%ScAvgScoreVal");
        _scMinScoreVal       = GetNode<Label>("%ScMinScoreVal");
        _scTop1NameVal       = GetNode<Label>("%ScTop1NameVal");
        _scTop1ScoreVal      = GetNode<Label>("%ScTop1ScoreVal");
        _scoreChartTitle     = GetNode<Label>("%ScoreChartTitle");
        _scoreChart          = GetNode<PriceChart>("%ScoreChart");
        _scoreDailyTitle     = GetNode<Label>("%ScoreDailyTitle");
        _scoreDailyChart     = GetNode<PriceChart>("%ScoreDailyChart");
        _scoreTableContainer = GetNode<VBoxContainer>("%ScoreTableContainer");

        // ── Populate static dropdowns ─────────────────────────────────────────

        _dateSelector.Clear();
        foreach (var lbl in DateOptionLabels) _dateSelector.AddItem(lbl);

        _olapSelector.Clear();
        foreach (var op in OlapOpNames) _olapSelector.AddItem(op);

        // ── Wire signals ──────────────────────────────────────────────────────

        _syncBtn.Pressed   += OnSyncPressed;
        _exportBtn.Pressed += OnExportPressed;
        _backBtn.Pressed   += () => GetTree().ChangeSceneToFile("res://Scenes/titlescreen.tscn");
        _applyBtn.Pressed  += OnApplyPressed;

        // ── Bootstrap OlapManager + initial data ──────────────────────────────

        _olap = new OlapManager();
        AddChild(_olap);
        _olap.StatusUpdated  += msg => _statusLabel.Text = msg;
        _olap.OlapDataLoaded += OnDataReady;
        _olap.EtlCompleted   += (_, _) => OnDataReady();

        // Seed immediately from in-memory marketplace (no Firebase round-trip)
        _olap.SeedFromMarketplaceManager();
        PopulateItemSelector();
        RunCurrentOlapOp();
        RefreshScoreTab();

        // Then overlay with persisted OLAP store from Firebase
        _olap.LoadFromOlapStore();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Callbacks — data ready
    // ─────────────────────────────────────────────────────────────────────────

    private void OnDataReady()
    {
        PopulateItemSelector();
        RunCurrentOlapOp();
        RefreshScoreTab();
        RefreshTradeSummary();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Toolbar button handlers
    // ─────────────────────────────────────────────────────────────────────────

    private void OnSyncPressed()
    {
        _syncBtn.Disabled = true;
        _syncBtn.Text     = "Syncing...";
        _olap.RunEtl();
        GetTree().CreateTimer(6.0).Timeout += () =>
        {
            if (IsInstanceValid(_syncBtn))
            {
                _syncBtn.Disabled = false;
                _syncBtn.Text     = "Sync ETL";
            }
        };
    }

    private void OnApplyPressed()
    {
        _selectedItem   = GetSelectedItemKey();
        _selectedDays   = DateDayOptions[Mathf.Clamp(_dateSelector.Selected, 0, DateDayOptions.Length - 1)];
        _selectedOlapOp = _olapSelector.Selected;
        RunCurrentOlapOp();
        RefreshTradeSummary();
    }

    private void OnExportPressed()
    {
        string csv      = BuildExportCsv();
        string filename = $"sbolk_olap_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        _olap.SaveCsvToFile(csv, filename);
        _statusLabel.Text = $"Exported → user://{filename}";
    }

    // ─────────────────────────────────────────────────────────────────────────
    // OLAP dispatch (Trade OLAP tab)
    // ─────────────────────────────────────────────────────────────────────────

    private void RunCurrentOlapOp()
    {
        _selectedItem   = GetSelectedItemKey();
        _selectedDays   = DateDayOptions[Mathf.Clamp(_dateSelector.Selected, 0, DateDayOptions.Length - 1)];
        _selectedOlapOp = _olapSelector.Selected;

        double now     = Time.GetUnixTimeFromSystem();
        double startTs = _selectedDays > 0 ? now - (_selectedDays * 86400.0) : 0;

        switch (_selectedOlapOp)
        {
            case 0: RunRollUpByItem();                              break;
            case 1: RunRollUpByDay();                               break;
            case 2: RunDrillDownByItem();                           break;
            case 3: RunSliceByItem();                               break;
            case 4: RunSliceByDate(startTs, now);                   break;
            case 5: RunDice(_selectedItem, startTs, now);           break;
            default: RunRollUpByItem();                             break;
        }
    }

    // ── Roll-Up by Item ───────────────────────────────────────────────────────

    private void RunRollUpByItem()
    {
        _tradeTableTitle.Text = "Roll-Up — Trades aggregated by Item";
        var aggs = _olap.RollUpByItem();

        RebuildTable(_tradeTableContainer,
            new[] { "Item", "Trades", "Volume", "Avg", "Min", "Max" },
            aggs.Select(a => new[]
            {
                a.Label, a.TotalTrades.ToString(),
                $"{a.TotalVolume}c", $"{a.AvgPrice:F1}c",
                $"{a.MinPrice}c",   $"{a.MaxPrice}c"
            }));

        UpdateTradeChart(_selectedItem);
    }

    // ── Roll-Up by Day ────────────────────────────────────────────────────────

    private void RunRollUpByDay()
    {
        _tradeTableTitle.Text = "Roll-Up — Trades aggregated by Day";
        var aggs = _olap.RollUpByDay();

        RebuildTable(_tradeTableContainer,
            new[] { "Date", "Trades", "Volume", "Avg Price" },
            aggs.Select(a => new[]
            {
                a.Label, a.TotalTrades.ToString(),
                $"{a.TotalVolume}c", $"{a.AvgPrice:F1}c"
            }));

        _tradeChartTitle.Text = "Avg Price per Day (Roll-Up)";
        _tradeChart.SetPrices(aggs.Select(a => a.AvgPrice).ToList());
    }

    // ── Drill-Down by Item ────────────────────────────────────────────────────

    private void RunDrillDownByItem()
    {
        string ik = NormaliseItemKey(_selectedItem);
        _tradeTableTitle.Text = $"Drill-Down — Trades for: {(string.IsNullOrEmpty(ik) ? "All" : ItemLabel(ik))}";

        var trades = string.IsNullOrEmpty(ik)
            ? _olap.FactTrades.OrderByDescending(t => t.Timestamp).ToList()
            : _olap.DrillDownByItem(ik);

        ShowTradeDetailTable(trades);
        UpdateTradeChart(ik);
    }

    // ── Slice by Item ─────────────────────────────────────────────────────────

    private void RunSliceByItem()
    {
        string ik = NormaliseItemKey(_selectedItem);
        _tradeTableTitle.Text = $"Slice — WHERE item = \"{ItemLabel(ik)}\"";
        ShowTradeDetailTable(_olap.SliceByItem(ik));
        UpdateTradeChart(ik);
    }

    // ── Slice by Date ─────────────────────────────────────────────────────────

    private void RunSliceByDate(double startTs, double endTs)
    {
        string period = _selectedDays > 0 ? $"last {_selectedDays} days" : "all time";
        _tradeTableTitle.Text = $"Slice — WHERE date = {period}";
        ShowTradeDetailTable(_olap.SliceByDate(startTs > 0 ? startTs : 0, endTs));
        UpdateTradeChart(NormaliseItemKey(_selectedItem));
    }

    // ── Dice: Item AND Date ───────────────────────────────────────────────────

    private void RunDice(string rawItem, double startTs, double endTs)
    {
        string ik     = NormaliseItemKey(rawItem);
        string nm     = string.IsNullOrEmpty(ik) ? "All" : ItemLabel(ik);
        string period = _selectedDays > 0 ? $"last {_selectedDays} days" : "all time";
        _tradeTableTitle.Text = $"Dice — item=\"{nm}\" AND date={period}";

        var trades = _olap.DiceByItemAndDate(ik, startTs > 0 ? startTs : 0,
                                                  endTs   > 0 ? endTs   : double.MaxValue);
        ShowTradeDetailTable(trades);
        UpdateTradeChart(ik);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Score Trends tab — refresh all panels
    // ─────────────────────────────────────────────────────────────────────────

    private void RefreshScoreTab()
    {
        RefreshScoreSummary();
        RefreshScoreChronologicalChart();
        RefreshScoreDailyChart();
        RefreshScoreTable();
    }

    private void RefreshScoreSummary()
    {
        var s = _olap.GetScoreSummary();
        _scTotalRunsVal.Text = s.TotalRuns.ToString();
        _scMaxScoreVal.Text  = s.MaxScore.ToString();
        _scAvgScoreVal.Text  = $"{s.AvgScore:F0}";
        _scMinScoreVal.Text  = s.TotalRuns > 0 ? s.MinScore.ToString() : "—";

        if (_olap.FactScores.Count > 0)
        {
            var top = _olap.FactScores.OrderByDescending(fs => fs.Score).First();
            _scTop1NameVal.Text  = TruncateName(top.Email, 22);
            _scTop1ScoreVal.Text = top.Score.ToString();
        }
        else
        {
            _scTop1NameVal.Text  = "—";
            _scTop1ScoreVal.Text = "—";
        }
    }

    // Chart 1 — individual scores in chronological order
    private void RefreshScoreChronologicalChart()
    {
        var scores = _olap.FactScores
            .OrderBy(fs => fs.Timestamp)
            .Select(fs => (float)fs.Score)
            .ToList();

        _scoreChart.SetPrices(scores);
        _scoreChartTitle.Text = $"Score Trend — All Runs ({scores.Count} entries)";
    }

    // Chart 2 — daily avg score roll-up (one point per calendar day)
    private void RefreshScoreDailyChart()
    {
        var daily = _olap.FactScores
            .GroupBy(fs => string.IsNullOrEmpty(fs.TimeKey) ? "unknown" : fs.TimeKey)
            .OrderBy(g => g.Key)
            .Select(g => (float)g.Average(fs => fs.Score))
            .ToList();

        _scoreDailyChart.SetPrices(daily);
        _scoreDailyTitle.Text = $"Daily Avg Score — {daily.Count} day(s) of data";
    }

    // Score table — Top-10 leaderboard + daily score roll-up
    private void RefreshScoreTable()
    {
        ClearContainer(_scoreTableContainer);

        // ── Top-10 Leaderboard ────────────────────────────────────────────────
        _scoreTableContainer.AddChild(SectionTitle("Top-10 Leaderboard"));
        _scoreTableContainer.AddChild(new HSeparator());
        _scoreTableContainer.AddChild(TableRow(new[] { "#", "Player", "Score", "Date" }, isHeader: true));
        _scoreTableContainer.AddChild(new HSeparator());

        var top10 = _olap.FactScores
            .OrderByDescending(fs => fs.Score)
            .Take(10)
            .ToList();

        if (top10.Count == 0)
        {
            _scoreTableContainer.AddChild(EmptyRow("No score data yet — play a run then Sync ETL."));
        }
        else
        {
            for (int i = 0; i < top10.Count; i++)
            {
                var fs   = top10[i];
                string date   = FormatDate(fs.Timestamp, fs.TimeKey);
                string rank   = i == 0 ? "1st" : i == 1 ? "2nd" : i == 2 ? "3rd" : $"{i + 1}th";
                var    row    = TableRow(new[] { rank, TruncateName(fs.Email, 20), fs.Score.ToString(), date },
                                        isHeader: false, alternate: i % 2 == 1);

                // Gold / silver / bronze tint
                if (i < 3)
                {
                    var tint = i == 0 ? new Color(1f, 0.88f, 0.2f)
                             : i == 1 ? new Color(0.82f, 0.84f, 0.9f)
                             :          new Color(0.9f, 0.55f, 0.3f);
                    foreach (Node child in row.GetChildren())
                        if (child is Label lbl) lbl.AddThemeColorOverride("font_color", tint);
                }

                _scoreTableContainer.AddChild(row);
            }
        }

        // ── Daily Score Roll-Up ───────────────────────────────────────────────
        _scoreTableContainer.AddChild(new HSeparator());
        _scoreTableContainer.AddChild(SectionTitle("Daily Score Roll-Up by Day"));
        _scoreTableContainer.AddChild(new HSeparator());
        _scoreTableContainer.AddChild(TableRow(new[] { "Date", "Runs", "Avg Score", "Max Score" }, isHeader: true));
        _scoreTableContainer.AddChild(new HSeparator());

        var dailyGroups = _olap.FactScores
            .GroupBy(fs => string.IsNullOrEmpty(fs.TimeKey) ? "unknown" : fs.TimeKey)
            .OrderByDescending(g => g.Key)
            .ToList();

        if (dailyGroups.Count == 0)
        {
            _scoreTableContainer.AddChild(EmptyRow("No daily data."));
        }
        else
        {
            int idx = 0;
            foreach (var g in dailyGroups)
            {
                _scoreTableContainer.AddChild(TableRow(
                    new[] { g.Key, g.Count().ToString(),
                            $"{g.Average(fs => fs.Score):F0}",
                            g.Max(fs => fs.Score).ToString() },
                    isHeader: false, alternate: idx++ % 2 == 1));
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Shared helpers
    // ─────────────────────────────────────────────────────────────────────────

    private void ShowTradeDetailTable(List<OlapManager.FactTrade> trades)
    {
        RebuildTable(_tradeTableContainer,
            new[] { "Item", "Date", "Price", "Qty", "Total" },
            trades.Take(200).Select(ft =>
            {
                string name = _olap.DimItems.ContainsKey(ft.ItemKey)
                    ? _olap.DimItems[ft.ItemKey].ItemName : ft.ItemKey;
                return new[] { name, FormatDate(ft.Timestamp, ft.TimeKey),
                               $"{ft.Price}c", ft.Quantity.ToString(), $"{ft.TotalValue}c" };
            }));
    }

    private void UpdateTradeChart(string itemKey)
    {
        var prices = new List<float>();

        if (string.IsNullOrEmpty(itemKey))
        {
            foreach (var agg in _olap.RollUpByDay()) prices.Add(agg.AvgPrice);
            _tradeChartTitle.Text = "Avg Trade Price — All Items Over Time";
        }
        else
        {
            foreach (var t in _olap.SliceByItem(itemKey).OrderBy(t => t.Timestamp))
                prices.Add(t.Price);
            _tradeChartTitle.Text = $"Price History — {ItemLabel(itemKey)}";
        }

        _tradeChart.SetPrices(prices);
    }

    private void RefreshTradeSummary()
    {
        var byItem = _olap.RollUpByItem();
        int  total  = _olap.FactTrades.Count;
        int  volume = _olap.FactTrades.Sum(t => t.TotalValue);
        float avg   = total > 0 ? (float)_olap.FactTrades.Average(t => t.Price) : 0f;

        _sumTradesVal.Text   = total.ToString();
        _sumVolumeVal.Text   = $"{volume}c";
        _sumAvgPriceVal.Text = $"{avg:F1}c";
        _sumTopItemVal.Text  = byItem.Count > 0 ? byItem[0].Label : "—";
    }

    // ── ItemSelector ──────────────────────────────────────────────────────────

    private void PopulateItemSelector()
    {
        _itemSelector.Clear();
        _itemSelector.AddItem("All Items");
        _itemSelector.SetItemMetadata(0, "all");

        var byItem = _olap.RollUpByItem();
        var seen   = new HashSet<string>();

        foreach (var agg in byItem)
        {
            seen.Add(agg.Key);
            _itemSelector.AddItem(agg.Label);
            _itemSelector.SetItemMetadata(_itemSelector.ItemCount - 1, agg.Key);
        }

        foreach (var kv in _olap.DimItems)
        {
            if (seen.Contains(kv.Key)) continue;
            _itemSelector.AddItem(kv.Value.ItemName);
            _itemSelector.SetItemMetadata(_itemSelector.ItemCount - 1, kv.Key);
        }
    }

    private string GetSelectedItemKey()
    {
        if (_itemSelector.Selected < 0) return "all";
        return _itemSelector.GetItemMetadata(_itemSelector.Selected).AsString();
    }

    // ── Dynamic table builder (rows only — headers are part of each call) ─────

    private void RebuildTable(VBoxContainer container, string[] headers, IEnumerable<string[]> rows)
    {
        ClearContainer(container);
        container.AddChild(TableRow(headers, isHeader: true));
        container.AddChild(new HSeparator());

        int count = 0;
        foreach (var row in rows)
        {
            container.AddChild(TableRow(row, isHeader: false, alternate: count % 2 == 1));
            count++;
        }

        if (count == 0)
            container.AddChild(EmptyRow("No data matching the current filters."));
    }

    private static HBoxContainer TableRow(string[] cells, bool isHeader, bool alternate = false)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);

        if (alternate)
        {
            var style = new StyleBoxFlat { BgColor = new Color(1, 1, 1, 0.03f) };
            row.AddThemeStyleboxOverride("panel", style);
        }

        foreach (var cell in cells)
        {
            var lbl = new Label { Text = cell };
            lbl.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            lbl.AddThemeFontSizeOverride("font_size", isHeader ? 10 : 9);
            if (isHeader)
                lbl.AddThemeColorOverride("font_color", new Color(0.55f, 0.85f, 1f));
            row.AddChild(lbl);
        }

        return row;
    }

    private static Label SectionTitle(string text)
    {
        var lbl = new Label { Text = text };
        lbl.AddThemeFontSizeOverride("font_size", 11);
        lbl.AddThemeColorOverride("font_color", new Color(0.45f, 1f, 0.65f));
        return lbl;
    }

    private static Label EmptyRow(string message)
    {
        var lbl = new Label { Text = message };
        lbl.AddThemeFontSizeOverride("font_size", 9);
        lbl.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
        return lbl;
    }

    private static void ClearContainer(VBoxContainer container)
    {
        foreach (Node child in container.GetChildren()) child.QueueFree();
    }

    // ── Export CSV ────────────────────────────────────────────────────────────

    private string BuildExportCsv()
    {
        double now     = Time.GetUnixTimeFromSystem();
        double startTs = _selectedDays > 0 ? now - (_selectedDays * 86400.0) : 0;
        int    op      = _olapSelector.Selected;

        if (op == 0 || op == 1)
        {
            var aggs = op == 0 ? _olap.RollUpByItem() : _olap.RollUpByDay();
            var sb   = new System.Text.StringBuilder();
            sb.AppendLine("key,label,total_trades,total_volume,avg_price,min_price,max_price");
            foreach (var a in aggs)
                sb.AppendLine($"{a.Key},{a.Label},{a.TotalTrades},{a.TotalVolume},{a.AvgPrice:F2},{a.MinPrice},{a.MaxPrice}");
            return sb.ToString();
        }

        string ik = NormaliseItemKey(_selectedItem);
        List<OlapManager.FactTrade> trades;
        if      (op == 3) trades = _olap.SliceByItem(ik);
        else if (op == 4) trades = _olap.SliceByDate(startTs, now);
        else              trades = _olap.DiceByItemAndDate(ik, startTs > 0 ? startTs : 0, now);

        return _olap.ExportTradesToCsv(trades);
    }

    // ── Small utilities ───────────────────────────────────────────────────────

    private string ItemLabel(string itemKey)
    {
        if (string.IsNullOrEmpty(itemKey) || itemKey == "all") return "All Items";
        if (_olap.DimItems.ContainsKey(itemKey)) return _olap.DimItems[itemKey].ItemName;
        if (CosmeticManager.Instance != null)
            foreach (var c in CosmeticManager.Instance.AllCosmetics)
                if (c.Id == itemKey) return c.Name;
        return itemKey;
    }

    private static string NormaliseItemKey(string key)
        => key == "all" ? "" : key;

    private static string TruncateName(string s, int max)
        => s.Length > max ? s[..max] + "…" : s;

    private static string FormatDate(double ts, string fallback)
        => ts > 0
            ? DateTimeOffset.FromUnixTimeSeconds((long)ts).ToString("yyyy-MM-dd")
            : (fallback ?? "—");
}
