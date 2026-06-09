using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// ─────────────────────────────────────────────────────────────────────────────
// AnalyticsDashboard — OLAP admin panel controller
//
// Tabs:
//   0  Trade OLAP        — OLAP operations on marketplace trade facts
//   1  Score Trends      — Player leaderboard and score analytics
//   2  Marketplace Health — Live listing monitor, anomaly detection, seller LB
//   3  ETL / Schema       — Star-schema stats, time hierarchy, ETL metadata
//
// All UI layout lives in Scenes/analytics.tscn.
// This script handles: data fetching, query dispatch, and dynamic table fills.
// ─────────────────────────────────────────────────────────────────────────────

public partial class AnalyticsDashboard : Control
{
    private const string AdminEmail = "admin123@gmail.com";

    // ── Shared toolbar ────────────────────────────────────────────────────────
    private Button       _syncBtn;
    private Button       _exportBtn;
    private Button       _exportScoresBtn;
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

    // ── Marketplace Health tab ────────────────────────────────────────────────
    private VBoxContainer _mktHealthContainer;

    // ── ETL / Schema Monitor tab ──────────────────────────────────────────────
    private VBoxContainer _etlMonitorContainer;

    // ── State ─────────────────────────────────────────────────────────────────
    private OlapManager _olap;
    private string      _selectedItem   = "all";
    private int         _selectedDays   = 0;
    private int         _selectedOlapOp = 0;

    private static readonly string[] OlapOpNames =
    {
        "Roll-Up by Item",
        "Roll-Up by Day",
        "Roll-Up by Month",
        "Roll-Up by Quarter",
        "Roll-Up by Year",
        "Drill-Down (Item)",
        "Slice (by Item)",
        "Slice (by Date)",
        "Dice (Item + Date)",
    };

    private static readonly int[]    DateDayOptions   = { 0, 7, 30, 90 };
    private static readonly string[] DateOptionLabels = { "All Time", "Last 7 Days", "Last 30 Days", "Last 90 Days" };

    // ─────────────────────────────────────────────────────────────────────────
    // _Ready
    // ─────────────────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        if (!IsCurrentUserAdmin())
        {
            GetTree().ChangeSceneToFile("res://Scenes/titlescreen.tscn");
            return;
        }

        // ── Fetch toolbar nodes ───────────────────────────────────────────────
        _syncBtn        = GetNode<Button>("%SyncButton");
        _exportBtn      = GetNode<Button>("%ExportButton");
        _exportScoresBtn= GetNode<Button>("%ExportScoresButton");
        _backBtn        = GetNode<Button>("%BackButton");
        _applyBtn       = GetNode<Button>("%ApplyButton");
        _statusLabel    = GetNode<Label>("%StatusLabel");
        _itemSelector   = GetNode<OptionButton>("%ItemSelector");
        _dateSelector   = GetNode<OptionButton>("%DateSelector");
        _olapSelector   = GetNode<OptionButton>("%OlapSelector");

        // ── Trade OLAP tab nodes ──────────────────────────────────────────────
        _sumTradesVal        = GetNode<Label>("%SumTradesVal");
        _sumVolumeVal        = GetNode<Label>("%SumVolumeVal");
        _sumAvgPriceVal      = GetNode<Label>("%SumAvgPriceVal");
        _sumTopItemVal       = GetNode<Label>("%SumTopItemVal");
        _tradeChartTitle     = GetNode<Label>("%TradeChartTitle");
        _tradeChart          = GetNode<PriceChart>("%TradeChart");
        _tradeTableTitle     = GetNode<Label>("%TradeTableTitle");
        _tradeTableContainer = GetNode<VBoxContainer>("%TradeTableContainer");

        // ── Score Trends tab nodes ────────────────────────────────────────────
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

        // ── Marketplace Health + ETL Monitor tab containers ───────────────────
        _mktHealthContainer  = GetNode<VBoxContainer>("%MktHealthContainer");
        _etlMonitorContainer = GetNode<VBoxContainer>("%EtlMonitorContainer");

        // ── Populate static dropdowns ─────────────────────────────────────────
        _dateSelector.Clear();
        foreach (var lbl in DateOptionLabels) _dateSelector.AddItem(lbl);

        _olapSelector.Clear();
        foreach (var op in OlapOpNames) _olapSelector.AddItem(op);

        // ── Wire signals ──────────────────────────────────────────────────────
        _syncBtn.Pressed        += OnSyncPressed;
        _exportBtn.Pressed      += OnExportPressed;
        _exportScoresBtn.Pressed+= OnExportScoresPressed;
        _backBtn.Pressed        += () => GetTree().ChangeSceneToFile("res://Scenes/titlescreen.tscn");
        _applyBtn.Pressed       += OnApplyPressed;

        // ── Bootstrap OlapManager ─────────────────────────────────────────────
        _olap = new OlapManager();
        AddChild(_olap);
        _olap.StatusUpdated  += msg => _statusLabel.Text = msg;
        _olap.OlapDataLoaded += OnDataReady;
        _olap.EtlCompleted   += (_, _) => OnDataReady();

        _olap.SeedFromMarketplaceManager();
        PopulateItemSelector();
        RunCurrentOlapOp();
        RefreshScoreTab();
        RefreshMarketplaceHealthTab();
        RefreshEtlMonitorTab();

        _olap.LoadFromOlapStore();
    }

    private bool IsCurrentUserAdmin()
        => string.Equals(GameControl.CurrentUserEmail, AdminEmail, StringComparison.OrdinalIgnoreCase);

    // ─────────────────────────────────────────────────────────────────────────
    // Callbacks — data ready
    // ─────────────────────────────────────────────────────────────────────────

    private void OnDataReady()
    {
        PopulateItemSelector();
        RunCurrentOlapOp();
        RefreshScoreTab();
        RefreshTradeSummary();
        RefreshMarketplaceHealthTab();
        RefreshEtlMonitorTab();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Toolbar handlers
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
        string opName   = OlapOpNames[Mathf.Clamp(_olapSelector.Selected, 0, OlapOpNames.Length - 1)];
        string period   = DateOptionLabels[Mathf.Clamp(_dateSelector.Selected, 0, DateOptionLabels.Length - 1)];
        string item     = ItemLabel(GetSelectedItemKey());
        string filterDesc = $"Op={opName}, Period={period}, Item={item}";
        string csv      = BuildExportCsv(filterDesc);
        string filename = $"sbolk_trades_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        _olap.SaveCsvToFile(csv, filename);
        _statusLabel.Text = $"Exported → user://{filename}";
    }

    private void OnExportScoresPressed()
    {
        string csv      = _olap.ExportScoresReportCsv();
        string filename = $"sbolk_scores_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        _olap.SaveCsvToFile(csv, filename);
        _statusLabel.Text = $"Scores exported → user://{filename}";
    }

    // ─────────────────────────────────────────────────────────────────────────
    // OLAP dispatch — Trade OLAP tab (now 9 operations)
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
            case 0: RunRollUpByItem();                    break;
            case 1: RunRollUpByDay();                     break;
            case 2: RunRollUpByMonth();                   break;
            case 3: RunRollUpByQuarter();                 break;
            case 4: RunRollUpByYear();                    break;
            case 5: RunDrillDownByItem();                 break;
            case 6: RunSliceByItem();                     break;
            case 7: RunSliceByDate(startTs, now);         break;
            case 8: RunDice(_selectedItem, startTs, now); break;
            default: RunRollUpByItem();                   break;
        }
    }

    // ── Roll-Up: By Item ──────────────────────────────────────────────────────
    private void RunRollUpByItem()
    {
        _tradeTableTitle.Text = "Roll-Up — Trades aggregated by Item";
        var aggs = _olap.RollUpByItem();
        RebuildTable(_tradeTableContainer,
            new[] { "Item", "Trades", "Volume", "Avg", "Min", "Max" },
            aggs.Select(a => new[] { a.Label, a.TotalTrades.ToString(),
                $"{a.TotalVolume}c", $"{a.AvgPrice:F1}c", $"{a.MinPrice}c", $"{a.MaxPrice}c" }));
        UpdateTradeChart(_selectedItem);
    }

    // ── Roll-Up: By Day ───────────────────────────────────────────────────────
    private void RunRollUpByDay()
    {
        _tradeTableTitle.Text = "Roll-Up — Trades aggregated by Day";
        var aggs = _olap.RollUpByDay();
        RebuildTable(_tradeTableContainer,
            new[] { "Date", "Trades", "Volume", "Avg Price" },
            aggs.Select(a => new[] { a.Label, a.TotalTrades.ToString(),
                $"{a.TotalVolume}c", $"{a.AvgPrice:F1}c" }));
        _tradeChartTitle.Text = "Avg Price per Day (Roll-Up by Day)";
        _tradeChart.SetPrices(aggs.Select(a => a.AvgPrice).ToList());
    }

    // ── Roll-Up: By Month ─────────────────────────────────────────────────────
    private void RunRollUpByMonth()
    {
        _tradeTableTitle.Text = "Roll-Up — Trades aggregated by Month (hierarchy level: Month)";
        var aggs = _olap.RollUpByMonth();
        RebuildTable(_tradeTableContainer,
            new[] { "Month", "Trades", "Volume", "Avg", "Min", "Max" },
            aggs.Select(a => new[] { a.Label, a.TotalTrades.ToString(),
                $"{a.TotalVolume}c", $"{a.AvgPrice:F1}c", $"{a.MinPrice}c", $"{a.MaxPrice}c" }));
        _tradeChartTitle.Text = "Avg Price per Month (Roll-Up by Month)";
        _tradeChart.SetPrices(aggs.Select(a => a.AvgPrice).ToList());
    }

    // ── Roll-Up: By Quarter ───────────────────────────────────────────────────
    private void RunRollUpByQuarter()
    {
        _tradeTableTitle.Text = "Roll-Up — Trades aggregated by Quarter (hierarchy level: Quarter)";
        var aggs = _olap.RollUpByQuarter();
        RebuildTable(_tradeTableContainer,
            new[] { "Quarter", "Trades", "Volume", "Avg", "Min", "Max" },
            aggs.Select(a => new[] { a.Label, a.TotalTrades.ToString(),
                $"{a.TotalVolume}c", $"{a.AvgPrice:F1}c", $"{a.MinPrice}c", $"{a.MaxPrice}c" }));
        _tradeChartTitle.Text = "Avg Price per Quarter (Roll-Up by Quarter)";
        _tradeChart.SetPrices(aggs.Select(a => a.AvgPrice).ToList());
    }

    // ── Roll-Up: By Year ──────────────────────────────────────────────────────
    private void RunRollUpByYear()
    {
        _tradeTableTitle.Text = "Roll-Up — Trades aggregated by Year (hierarchy level: Year — coarsest)";
        var aggs = _olap.RollUpByYear();
        RebuildTable(_tradeTableContainer,
            new[] { "Year", "Trades", "Volume", "Avg", "Min", "Max" },
            aggs.Select(a => new[] { a.Label, a.TotalTrades.ToString(),
                $"{a.TotalVolume}c", $"{a.AvgPrice:F1}c", $"{a.MinPrice}c", $"{a.MaxPrice}c" }));
        _tradeChartTitle.Text = "Avg Price per Year (Roll-Up by Year)";
        _tradeChart.SetPrices(aggs.Select(a => a.AvgPrice).ToList());
    }

    // ── Drill-Down: By Item ───────────────────────────────────────────────────
    private void RunDrillDownByItem()
    {
        string ik = NormaliseItemKey(_selectedItem);
        _tradeTableTitle.Text = $"Drill-Down — Individual trades for: {(string.IsNullOrEmpty(ik) ? "All" : ItemLabel(ik))}";
        var trades = string.IsNullOrEmpty(ik)
            ? _olap.FactTrades.OrderByDescending(t => t.Timestamp).ToList()
            : _olap.DrillDownByItem(ik);
        ShowTradeDetailTable(trades);
        UpdateTradeChart(ik);
    }

    // ── Slice: By Item ────────────────────────────────────────────────────────
    private void RunSliceByItem()
    {
        string ik = NormaliseItemKey(_selectedItem);
        _tradeTableTitle.Text = $"Slice — WHERE item = \"{ItemLabel(ik)}\"";
        ShowTradeDetailTable(_olap.SliceByItem(ik));
        UpdateTradeChart(ik);
    }

    // ── Slice: By Date ────────────────────────────────────────────────────────
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
    // Score Trends tab
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

    private void RefreshScoreChronologicalChart()
    {
        var scores = _olap.FactScores.OrderBy(fs => fs.Timestamp).Select(fs => (float)fs.Score).ToList();
        _scoreChart.SetPrices(scores);
        _scoreChartTitle.Text = $"Score Trend — All Runs ({scores.Count} entries)";
    }

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

    private void RefreshScoreTable()
    {
        ClearContainer(_scoreTableContainer);

        _scoreTableContainer.AddChild(SectionTitle("Top-10 Leaderboard"));
        _scoreTableContainer.AddChild(new HSeparator());
        _scoreTableContainer.AddChild(TableRow(new[] { "#", "Player", "Score", "Date" }, isHeader: true));
        _scoreTableContainer.AddChild(new HSeparator());

        var top10 = _olap.FactScores.OrderByDescending(fs => fs.Score).Take(10).ToList();
        if (top10.Count == 0)
        {
            _scoreTableContainer.AddChild(EmptyRow("No score data yet — play a run then Sync ETL."));
        }
        else
        {
            for (int i = 0; i < top10.Count; i++)
            {
                var    fs   = top10[i];
                string date = FormatDate(fs.Timestamp, fs.TimeKey);
                string rank = i == 0 ? "1st" : i == 1 ? "2nd" : i == 2 ? "3rd" : $"{i + 1}th";
                var    row  = TableRow(new[] { rank, TruncateName(fs.Email, 20), fs.Score.ToString(), date },
                                       isHeader: false, alternate: i % 2 == 1);
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

        _scoreTableContainer.AddChild(new HSeparator());
        _scoreTableContainer.AddChild(SectionTitle("Daily Score Roll-Up by Day"));
        _scoreTableContainer.AddChild(new HSeparator());
        _scoreTableContainer.AddChild(TableRow(new[] { "Date", "Runs", "Avg Score", "Max Score" }, isHeader: true));
        _scoreTableContainer.AddChild(new HSeparator());

        var dailyGroups = _olap.FactScores
            .GroupBy(fs => string.IsNullOrEmpty(fs.TimeKey) ? "unknown" : fs.TimeKey)
            .OrderByDescending(g => g.Key).ToList();

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
                    new[] { g.Key, g.Count().ToString(), $"{g.Average(fs => fs.Score):F0}", g.Max(fs => fs.Score).ToString() },
                    isHeader: false, alternate: idx++ % 2 == 1));
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Marketplace Health tab
    // Shows: live listing stats, price anomaly flags, volatility ranking,
    //        seller leaderboard, price spread per item
    // ─────────────────────────────────────────────────────────────────────────

    private void RefreshMarketplaceHealthTab()
    {
        ClearContainer(_mktHealthContainer);

        var mm = MarketplaceManager.Instance;

        // ── Live Listings Summary ─────────────────────────────────────────────
        _mktHealthContainer.AddChild(SectionTitle("Live Marketplace Snapshot (OLTP)"));
        _mktHealthContainer.AddChild(new HSeparator());

        int  totalListings    = mm?.Listings.Count ?? 0;
        var  uniqueItems      = mm?.Listings.Select(l => l.ItemId).Distinct().ToList() ?? new List<string>();
        var  uniqueSellers    = mm?.Listings.Select(l => l.SellerUid).Distinct().ToList() ?? new List<string>();
        int  totalListedValue = mm?.Listings.Sum(l => l.Price * l.Quantity) ?? 0;
        int  avgListingPrice  = totalListings > 0 ? (mm?.Listings.Sum(l => l.Price) ?? 0) / totalListings : 0;

        var grid = MakeKvGrid(new[]
        {
            ("Active Listings:",  totalListings.ToString()),
            ("Unique Items:",     uniqueItems.Count.ToString()),
            ("Unique Sellers:",   uniqueSellers.Count.ToString()),
            ("Total Listed Value:", $"{totalListedValue}c"),
            ("Avg Listing Price:", $"{avgListingPrice}c"),
            ("Snapshot Time:",    DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " UTC"),
        });
        _mktHealthContainer.AddChild(grid);

        // ── Active Listings per Item ──────────────────────────────────────────
        _mktHealthContainer.AddChild(new HSeparator());
        _mktHealthContainer.AddChild(SectionTitle("Listings per Item"));
        _mktHealthContainer.AddChild(TableRow(new[] { "Item", "Listings", "Min Price", "Max Price", "Avg Price" }, isHeader: true));
        _mktHealthContainer.AddChild(new HSeparator());

        if (mm != null && mm.Listings.Count > 0)
        {
            var byItem = mm.Listings
                .GroupBy(l => l.ItemId)
                .Select(g => new
                {
                    Name    = g.First().ItemName ?? g.Key,
                    Count   = g.Count(),
                    MinP    = g.Min(l => l.Price),
                    MaxP    = g.Max(l => l.Price),
                    AvgP    = (float)g.Average(l => l.Price),
                })
                .OrderByDescending(x => x.Count)
                .ToList();

            int ri = 0;
            foreach (var r in byItem)
            {
                _mktHealthContainer.AddChild(TableRow(
                    new[] { r.Name, r.Count.ToString(), $"{r.MinP}c", $"{r.MaxP}c", $"{r.AvgP:F1}c" },
                    isHeader: false, alternate: ri++ % 2 == 1));
            }
        }
        else
        {
            _mktHealthContainer.AddChild(EmptyRow("No active listings in marketplace."));
        }

        // ── Seller Leaderboard ────────────────────────────────────────────────
        _mktHealthContainer.AddChild(new HSeparator());
        _mktHealthContainer.AddChild(SectionTitle("Top Sellers (by Listed Value)"));
        _mktHealthContainer.AddChild(TableRow(new[] { "#", "Seller", "Listings", "Total Value", "Avg Price" }, isHeader: true));
        _mktHealthContainer.AddChild(new HSeparator());

        var sellers = _olap.GetSellerLeaderboard();
        if (sellers.Count == 0)
        {
            _mktHealthContainer.AddChild(EmptyRow("No seller data available."));
        }
        else
        {
            for (int i = 0; i < Math.Min(sellers.Count, 15); i++)
            {
                var s = sellers[i];
                _mktHealthContainer.AddChild(TableRow(
                    new[] { $"{i + 1}", TruncateName(s.SellerEmail, 24),
                            s.ListingCount.ToString(), $"{s.TotalVolume}c", $"{s.AvgPrice:F1}c" },
                    isHeader: false, alternate: i % 2 == 1));
            }
        }

        // ── Price Anomaly Flags ───────────────────────────────────────────────
        _mktHealthContainer.AddChild(new HSeparator());
        _mktHealthContainer.AddChild(SectionTitle("Price Anomaly Flags (trades ≥2× or ≤0.5× median)"));
        _mktHealthContainer.AddChild(TableRow(new[] { "Item", "Trade Price", "Median", "Ratio×", "Date" }, isHeader: true));
        _mktHealthContainer.AddChild(new HSeparator());

        var anomalies = _olap.DetectPriceAnomalies(2.0f);
        if (anomalies.Count == 0)
        {
            _mktHealthContainer.AddChild(EmptyRow("No price anomalies detected — market looks healthy."));
        }
        else
        {
            int ai = 0;
            foreach (var an in anomalies.Take(20))
            {
                var row = TableRow(
                    new[] { an.ItemName, $"{an.TradePrice}c", $"{an.MedianPrice:F1}c",
                            $"{an.Ratio:F2}×", an.DateKey ?? "—" },
                    isHeader: false, alternate: ai++ % 2 == 1);

                // Highlight high-ratio anomalies in red
                if (an.Ratio >= 3f || an.Ratio <= 0.33f)
                {
                    foreach (Node child in row.GetChildren())
                        if (child is Label lbl) lbl.AddThemeColorOverride("font_color", new Color(1f, 0.35f, 0.35f));
                }
                _mktHealthContainer.AddChild(row);
            }
        }

        // ── Price Volatility Ranking ──────────────────────────────────────────
        _mktHealthContainer.AddChild(new HSeparator());
        _mktHealthContainer.AddChild(SectionTitle("Price Volatility Ranking (Coeff. of Variation — StdDev/Avg)"));
        _mktHealthContainer.AddChild(TableRow(new[] { "Item", "Trades", "Avg", "StdDev", "CV%", "Range" }, isHeader: true));
        _mktHealthContainer.AddChild(new HSeparator());

        var volatility = _olap.GetVolatilityMetrics();
        if (volatility.Count == 0)
        {
            _mktHealthContainer.AddChild(EmptyRow("Insufficient trade data for volatility analysis (need ≥2 trades per item)."));
        }
        else
        {
            int vi = 0;
            foreach (var v in volatility.Take(15))
            {
                var row = TableRow(
                    new[] { v.ItemName, v.TotalTrades.ToString(), $"{v.AvgPrice:F1}c",
                            $"{v.StdDev:F1}", $"{v.CoeffVariation * 100:F1}%",
                            $"{v.MinPrice}c–{v.MaxPrice}c" },
                    isHeader: false, alternate: vi++ % 2 == 1);

                // Flag highly volatile items in orange
                if (v.CoeffVariation > 0.4f)
                {
                    foreach (Node child in row.GetChildren())
                        if (child is Label lbl) lbl.AddThemeColorOverride("font_color", new Color(1f, 0.65f, 0.1f));
                }
                _mktHealthContainer.AddChild(row);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ETL / Schema Monitor tab
    // Shows: Star-Schema row counts, time hierarchy explorer, ETL metadata
    // ─────────────────────────────────────────────────────────────────────────

    private void RefreshEtlMonitorTab()
    {
        ClearContainer(_etlMonitorContainer);

        var stats = _olap.GetSchemaStats();

        // ── Star-Schema Row Counts ────────────────────────────────────────────
        _etlMonitorContainer.AddChild(SectionTitle("OLAP Star Schema — Row Counts"));
        _etlMonitorContainer.AddChild(new HSeparator());

        _etlMonitorContainer.AddChild(SectionTitle("  Fact Tables (measurable events):"));
        _etlMonitorContainer.AddChild(MakeKvGrid(new[]
        {
            ("fact_trades (trade events):", stats.FactTradeCount.ToString()),
            ("fact_scores (player scores):", stats.FactScoreCount.ToString()),
        }));

        _etlMonitorContainer.AddChild(SectionTitle("  Dimension Tables (context/lookup):"));
        _etlMonitorContainer.AddChild(MakeKvGrid(new[]
        {
            ("dim_items  (cosmetic items):", stats.DimItemCount.ToString()),
            ("dim_time   (date hierarchy):", stats.DimTimeCount.ToString()),
            ("dim_players (player profiles):", stats.DimPlayerCount.ToString()),
        }));

        _etlMonitorContainer.AddChild(SectionTitle("  Coverage:"));
        _etlMonitorContainer.AddChild(MakeKvGrid(new[]
        {
            ("Unique items traded:", stats.UniqueItemsTraded.ToString()),
            ("Unique calendar days:", stats.UniqueDaysTraded.ToString()),
            ("Active OLTP listings:", stats.ActiveListings.ToString()),
            ("Active OLTP sellers:", stats.UniqueSellers.ToString()),
        }));

        // ── Time Hierarchy Explorer ───────────────────────────────────────────
        _etlMonitorContainer.AddChild(new HSeparator());
        _etlMonitorContainer.AddChild(SectionTitle("Time Dimension Hierarchy — Year → Quarter → Month → Day"));
        _etlMonitorContainer.AddChild(SectionTitle("  Roll-Up by Year (coarsest grain):"));
        BuildHierarchyTable(_etlMonitorContainer, _olap.RollUpByYear(),
            new[] { "Year", "Trades", "Volume", "Avg Price" });

        _etlMonitorContainer.AddChild(SectionTitle("  Roll-Up by Quarter:"));
        BuildHierarchyTable(_etlMonitorContainer, _olap.RollUpByQuarter(),
            new[] { "Quarter", "Trades", "Volume", "Avg Price" });

        _etlMonitorContainer.AddChild(SectionTitle("  Roll-Up by Month:"));
        BuildHierarchyTable(_etlMonitorContainer, _olap.RollUpByMonth(),
            new[] { "Month", "Trades", "Volume", "Avg Price" });

        _etlMonitorContainer.AddChild(SectionTitle("  Roll-Up by Day (finest grain):"));
        BuildHierarchyTable(_etlMonitorContainer, _olap.RollUpByDay(),
            new[] { "Day", "Trades", "Volume", "Avg Price" }, maxRows: 14);

        // ── dim_items listing ─────────────────────────────────────────────────
        _etlMonitorContainer.AddChild(new HSeparator());
        _etlMonitorContainer.AddChild(SectionTitle("dim_items — Item Dimension Table"));
        _etlMonitorContainer.AddChild(TableRow(new[] { "item_key", "item_name", "category" }, isHeader: true));
        _etlMonitorContainer.AddChild(new HSeparator());

        if (_olap.DimItems.Count == 0)
        {
            _etlMonitorContainer.AddChild(EmptyRow("dim_items is empty — run Sync ETL or play with marketplace."));
        }
        else
        {
            int ii = 0;
            foreach (var kv in _olap.DimItems.Values)
            {
                _etlMonitorContainer.AddChild(TableRow(
                    new[] { kv.ItemKey, kv.ItemName, kv.Category },
                    isHeader: false, alternate: ii++ % 2 == 1));
            }
        }

        // ── dim_players listing ───────────────────────────────────────────────
        _etlMonitorContainer.AddChild(new HSeparator());
        _etlMonitorContainer.AddChild(SectionTitle("dim_players — Player Dimension Table"));
        _etlMonitorContainer.AddChild(TableRow(new[] { "player_key (truncated)", "display_name" }, isHeader: true));
        _etlMonitorContainer.AddChild(new HSeparator());

        if (_olap.DimPlayers.Count == 0)
        {
            _etlMonitorContainer.AddChild(EmptyRow("dim_players is empty — run Sync ETL."));
        }
        else
        {
            int pi = 0;
            foreach (var kv in _olap.DimPlayers.Values)
            {
                _etlMonitorContainer.AddChild(TableRow(
                    new[] { TruncateName(kv.PlayerKey, 16), kv.DisplayName },
                    isHeader: false, alternate: pi++ % 2 == 1));
            }
        }

        // ── ETL metadata ──────────────────────────────────────────────────────
        _etlMonitorContainer.AddChild(new HSeparator());
        _etlMonitorContainer.AddChild(SectionTitle("ETL Run Metadata"));
        _etlMonitorContainer.AddChild(MakeKvGrid(new[]
        {
            ("In-memory fact_trades:", _olap.FactTrades.Count.ToString()),
            ("In-memory fact_scores:", _olap.FactScores.Count.ToString()),
            ("In-memory dim_items:",   _olap.DimItems.Count.ToString()),
            ("In-memory dim_time:",    _olap.DimTimes.Count.ToString()),
            ("In-memory dim_players:", _olap.DimPlayers.Count.ToString()),
            ("Current time (UTC):",    DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")),
        }));
    }

    // Compact hierarchy sub-table helper
    private void BuildHierarchyTable(VBoxContainer container, List<OlapManager.TradeAggregate> aggs,
                                      string[] headers, int maxRows = 20)
    {
        container.AddChild(TableRow(headers, isHeader: true));
        if (aggs.Count == 0)
        {
            container.AddChild(EmptyRow("  No data."));
            return;
        }
        int ri = 0;
        foreach (var a in aggs.Take(maxRows))
        {
            container.AddChild(TableRow(
                new[] { a.Label, a.TotalTrades.ToString(), $"{a.TotalVolume}c", $"{a.AvgPrice:F1}c" },
                isHeader: false, alternate: ri++ % 2 == 1));
        }
        if (aggs.Count > maxRows)
            container.AddChild(EmptyRow($"  ... and {aggs.Count - maxRows} more rows."));
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

        var  byItem = _olap.RollUpByItem();
        var  seen   = new HashSet<string>();
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

    // ── CSV Export ────────────────────────────────────────────────────────────

    private string BuildExportCsv(string filterDesc)
    {
        double now     = Time.GetUnixTimeFromSystem();
        double startTs = _selectedDays > 0 ? now - (_selectedDays * 86400.0) : 0;
        int    op      = _olapSelector.Selected;

        // Roll-up operations export aggregated CSV
        if (op >= 0 && op <= 4)
        {
            List<OlapManager.TradeAggregate> aggs = op switch
            {
                0 => _olap.RollUpByItem(),
                1 => _olap.RollUpByDay(),
                2 => _olap.RollUpByMonth(),
                3 => _olap.RollUpByQuarter(),
                _ => _olap.RollUpByYear(),
            };
            string opName = OlapOpNames[op];
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("# SBOLK OLAP Analytics Report");
            sb.AppendLine($"# Report  : {opName}");
            sb.AppendLine($"# Filters : {filterDesc}");
            sb.AppendLine($"# Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine("#");
            sb.AppendLine("key,label,total_trades,total_volume,avg_price,min_price,max_price");
            foreach (var a in aggs)
                sb.AppendLine($"{a.Key},{a.Label},{a.TotalTrades},{a.TotalVolume},{a.AvgPrice:F2},{a.MinPrice},{a.MaxPrice}");
            return sb.ToString();
        }

        // Detail operations export individual trade rows
        string ik = NormaliseItemKey(_selectedItem);
        List<OlapManager.FactTrade> trades = op switch
        {
            6 => _olap.SliceByItem(ik),
            7 => _olap.SliceByDate(startTs, now),
            _ => _olap.DiceByItemAndDate(ik, startTs > 0 ? startTs : 0, now),
        };

        string title = OlapOpNames[Mathf.Clamp(op, 0, OlapOpNames.Length - 1)];
        return _olap.ExportFullReportCsv(title, filterDesc);
    }

    // ── Dynamic table builder ─────────────────────────────────────────────────

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
            if (isHeader) lbl.AddThemeColorOverride("font_color", new Color(0.55f, 0.85f, 1f));
            row.AddChild(lbl);
        }
        return row;
    }

    // Key-value grid (2-column GridContainer) for stat cards
    private static GridContainer MakeKvGrid(IEnumerable<(string key, string val)> pairs)
    {
        var grid = new GridContainer();
        grid.Columns = 2;
        grid.AddThemeConstantOverride("h_separation", 12);
        grid.AddThemeConstantOverride("v_separation", 1);
        foreach (var (k, v) in pairs)
        {
            var kLbl = new Label { Text = k };
            kLbl.AddThemeFontSizeOverride("font_size", 10);
            kLbl.AddThemeColorOverride("font_color", new Color(0.6f, 0.7f, 0.8f));
            var vLbl = new Label { Text = v };
            vLbl.AddThemeFontSizeOverride("font_size", 10);
            vLbl.AddThemeColorOverride("font_color", new Color(1f, 1f, 0.65f));
            grid.AddChild(kLbl);
            grid.AddChild(vLbl);
        }
        return grid;
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

    private static string NormaliseItemKey(string key) => key == "all" ? "" : key;

    private static string TruncateName(string s, int max)
        => s.Length > max ? s[..max] + "…" : s;

    private static string FormatDate(double ts, string fallback)
        => ts > 0
            ? DateTimeOffset.FromUnixTimeSeconds((long)ts).ToString("yyyy-MM-dd")
            : (fallback ?? "—");
}
