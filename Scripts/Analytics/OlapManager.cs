using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

// ─────────────────────────────────────────────────────────────────────────────
// OlapManager — Star-Schema ETL Engine & OLAP Query Layer
//
// Firebase OLAP Schema (mirrors a Star Schema in NoSQL):
//
//   OLTP Sources (read-only):
//     leaderboards/global/{uid}           → {email, score, updatedAt}
//     marketplace/priceHistory/{itemId}   → {trades:[{price,ts}]}
//     marketplace/listings/{id}           → {itemId, price, qty, ...}
//
//   OLAP Targets (written by ETL):
//     olap/dim_items/{itemId}             → Dimension: item metadata
//     olap/dim_time/{dateKey}             → Dimension: date hierarchy (Y→Q→M→D)
//     olap/dim_players/{uid}              → Dimension: player info
//     olap/fact_trades/{tradeId}          → Fact: one row per trade event
//     olap/fact_scores/{uid}              → Fact: one row per player best score
//     olap/aggregates/by_item/{itemId}    → Roll-up: aggregated per item
//     olap/aggregates/by_day/{dateKey}    → Roll-up: aggregated per day
//     olap/meta/last_etl                  → ETL run metadata
//
// OLAP Operations implemented:
//   Roll-up   : GROUP BY item / day / month / quarter / year (full hierarchy)
//   Drill-down: expand aggregated row → individual trade facts
//   Slice     : filter on ONE dimension (item OR date range)
//   Dice      : filter on MULTIPLE dimensions (item AND date range)
// ─────────────────────────────────────────────────────────────────────────────

public partial class OlapManager : Node
{
    public static OlapManager Instance { get; private set; }

    // ── Signals ──────────────────────────────────────────────────────────────
    [Signal] public delegate void EtlCompletedEventHandler(int tradesFact, int scoresFact);
    [Signal] public delegate void OlapDataLoadedEventHandler();
    [Signal] public delegate void StatusUpdatedEventHandler(string message);

    // ── OLAP Schema: Dimension Tables ────────────────────────────────────────

    public class DimItem
    {
        public string ItemKey;
        public string ItemName;
        public string Category = "cosmetic";
    }

    public class DimTime
    {
        public string TimeKey;    // "YYYY-MM-DD"
        public int    Year;
        public int    Month;
        public int    Day;
        public int    Quarter;
        public int    WeekOfYear;
        public string MonthName;
    }

    public class DimPlayer
    {
        public string PlayerKey;
        public string DisplayName;
    }

    // ── OLAP Schema: Fact Tables ─────────────────────────────────────────────

    public class FactTrade
    {
        public string TradeKey;
        public string ItemKey;     // FK → dim_items
        public string TimeKey;     // FK → dim_time
        public string PlayerKey;   // FK → dim_players
        public int    Price;
        public int    Quantity;
        public int    TotalValue;  // Price × Quantity
        public double Timestamp;
    }

    public class FactScore
    {
        public string ScoreKey;
        public string PlayerKey;   // FK → dim_players
        public string TimeKey;     // FK → dim_time
        public string Email;
        public int    Score;
        public double Timestamp;
    }

    // ── OLAP Schema: Aggregates (materialized Roll-ups) ──────────────────────

    public class TradeAggregate
    {
        public string Key;
        public string Label;
        public int    TotalTrades;
        public int    TotalVolume;
        public float  AvgPrice;
        public int    MinPrice;
        public int    MaxPrice;
    }

    public class ScoreAggregate
    {
        public int   TotalRuns;
        public float AvgScore;
        public int   MaxScore;
        public int   MinScore;
    }

    // ── Analytics Models ─────────────────────────────────────────────────────

    // Price volatility: Coefficient of Variation (StdDev / AvgPrice) per item
    public class VolatilityRecord
    {
        public string ItemKey;
        public string ItemName;
        public float  AvgPrice;
        public float  StdDev;
        public float  CoeffVariation; // Higher = more volatile pricing
        public int    TotalTrades;
        public int    MinPrice;
        public int    MaxPrice;
    }

    // Anomalous trade: price deviates from median by ≥ threshold factor
    public class AnomalyRecord
    {
        public string ItemKey;
        public string ItemName;
        public int    TradePrice;
        public float  MedianPrice;
        public float  Ratio;      // TradePrice / MedianPrice
        public string DateKey;
    }

    // OLAP schema row counts — displayed in ETL Monitor tab
    public class SchemaStats
    {
        public int FactTradeCount;
        public int FactScoreCount;
        public int DimItemCount;
        public int DimTimeCount;
        public int DimPlayerCount;
        public int UniqueItemsTraded;
        public int UniqueDaysTraded;
        public int ActiveListings;
        public int UniqueSellers;
    }

    // Per-seller listing stats from live OLTP data
    public class SellerRecord
    {
        public string SellerEmail;
        public int    ListingCount;
        public int    TotalVolume;
        public float  AvgPrice;
    }

    // ── In-Memory OLAP Store ─────────────────────────────────────────────────

    public List<FactTrade>               FactTrades  { get; private set; } = new();
    public List<FactScore>               FactScores  { get; private set; } = new();
    public Dictionary<string, DimItem>   DimItems    { get; private set; } = new();
    public Dictionary<string, DimTime>   DimTimes    { get; private set; } = new();
    public Dictionary<string, DimPlayer> DimPlayers  { get; private set; } = new();

    // ── Firebase references ──────────────────────────────────────────────────

    private Node _firebase;
    private Node _auth;
    private Node _database;
    private bool _etlRunning;

    // ── ETL state ────────────────────────────────────────────────────────────

    private Godot.Collections.Dictionary _oltpHistory;
    private Godot.Collections.Dictionary _oltpLeaderboard;
    private Godot.Collections.Dictionary _oltpRuns;
    private bool _historyLoaded;
    private bool _leaderboardLoaded;
    private bool _runsLoaded;

    // ── Firebase OLAP paths ──────────────────────────────────────────────────

    private const string DimItemsPath   = "olap/dim_items";
    private const string DimTimePath    = "olap/dim_time";
    private const string DimPlayersPath = "olap/dim_players";
    private const string FactTradesPath = "olap/fact_trades";
    private const string FactScoresPath = "olap/fact_scores";
    private const string AggByItemPath  = "olap/aggregates/by_item";
    private const string AggByDayPath   = "olap/aggregates/by_day";
    private const string MetaPath       = "olap/meta";

    // ─────────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────────────

    public override void _Ready()
    {
        Instance = this;
        EnsureFirebase();
    }

    private void EnsureFirebase()
    {
        _firebase = GetNodeOrNull<Node>("/root/Firebase");
        _auth     = _firebase?.GetNodeOrNull<Node>("Auth");
        _database = _firebase?.GetNodeOrNull<Node>("Database");
    }

    private bool IsLoggedIn()
    {
        if (_auth == null) return false;
        var v = (Variant)_auth.Call("is_logged_in");
        return v.VariantType == Variant.Type.Bool && v.AsBool();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ETL — Extract, Transform, Load
    // Reads OLTP Firebase paths → transforms into Star Schema facts/dims →
    // writes back to olap/ Firebase paths + updates in-memory OLAP store.
    // ─────────────────────────────────────────────────────────────────────────

    public void RunEtl()
    {
        if (_etlRunning) return;
        EnsureFirebase();

        if (_database == null)
        {
            EmitSignal(SignalName.StatusUpdated, "ETL Error: Firebase not available.");
            return;
        }

        _etlRunning        = true;
        _historyLoaded     = false;
        _leaderboardLoaded = false;
        _runsLoaded        = false;

        EmitSignal(SignalName.StatusUpdated, "ETL: Extracting OLTP data from Firebase...");

        // Extract Step 1: marketplace price history → trade facts
        var histRef = (Node)_database.Call("get_once_database_reference", "marketplace/priceHistory");
        SafeConnect(histRef, "once_successful", nameof(OnOltpHistoryLoaded));
        SafeConnect(histRef, "once_failed",     nameof(OnOltpHistoryFailed));
        histRef.Call("once", "");

        // Extract Step 2: leaderboard best scores → used for ranking / leaderboard display
        var lbRef = (Node)_database.Call("get_once_database_reference", "leaderboards/global");
        SafeConnect(lbRef, "once_successful", nameof(OnOltpLeaderboardLoaded));
        SafeConnect(lbRef, "once_failed",     nameof(OnOltpLeaderboardFailed));
        lbRef.Call("once", "");

        // Extract Step 3: run history → every individual session (powers daily avg chart)
        var runsRef = (Node)_database.Call("get_once_database_reference", "leaderboards/runs");
        SafeConnect(runsRef, "once_successful", nameof(OnOltpRunsLoaded));
        SafeConnect(runsRef, "once_failed",     nameof(OnOltpRunsFailed));
        runsRef.Call("once", "");
    }

    // ── ETL: Extract callbacks ────────────────────────────────────────────────

    private void OnOltpHistoryLoaded(Godot.Collections.Dictionary snapshot)
    {
        _oltpHistory   = snapshot;
        _historyLoaded = true;
        TryRunTransform();
    }

    private void OnOltpHistoryFailed()
    {
        _oltpHistory   = null;
        _historyLoaded = true;
        EmitSignal(SignalName.StatusUpdated, "ETL Warning: Could not read trade history.");
        TryRunTransform();
    }

    private void OnOltpLeaderboardLoaded(Godot.Collections.Dictionary snapshot)
    {
        _oltpLeaderboard   = snapshot;
        _leaderboardLoaded = true;
        TryRunTransform();
    }

    private void OnOltpLeaderboardFailed()
    {
        _oltpLeaderboard   = null;
        _leaderboardLoaded = true;
        EmitSignal(SignalName.StatusUpdated, "ETL Warning: Could not read leaderboard.");
        TryRunTransform();
    }

    private void OnOltpRunsLoaded(Godot.Collections.Dictionary snapshot)
    {
        _oltpRuns   = snapshot;
        _runsLoaded = true;
        TryRunTransform();
    }

    private void OnOltpRunsFailed()
    {
        _oltpRuns   = null;
        _runsLoaded = true;
        EmitSignal(SignalName.StatusUpdated, "ETL Note: No run history yet (leaderboards/runs empty).");
        TryRunTransform();
    }

    // ── ETL: Transform + Load ─────────────────────────────────────────────────

    private void TryRunTransform()
    {
        if (!_historyLoaded || !_leaderboardLoaded || !_runsLoaded) return;

        EmitSignal(SignalName.StatusUpdated, "ETL: Transforming into Star Schema...");

        FactTrades.Clear();
        FactScores.Clear();
        DimItems.Clear();
        DimTimes.Clear();
        DimPlayers.Clear();

        // ── Transform: trade facts ────────────────────────────────────────────
        if (_oltpHistory != null)
        {
            foreach (var itemKey in _oltpHistory.Keys)
            {
                string itemId  = itemKey.ToString();
                var    itemVar = (Variant)_oltpHistory[itemKey];
                if (itemVar.VariantType != Variant.Type.Dictionary) continue;
                var itemDict = itemVar.AsGodotDictionary();

                string itemName = GetItemName(itemId);
                if (!DimItems.ContainsKey(itemId))
                    DimItems[itemId] = new DimItem { ItemKey = itemId, ItemName = itemName, Category = "cosmetic" };

                if (!itemDict.ContainsKey("trades")) continue;
                var tradesVar = (Variant)itemDict["trades"];
                if (tradesVar.VariantType != Variant.Type.Array) continue;

                int tradeIndex = 0;
                foreach (var t in tradesVar.AsGodotArray())
                {
                    var tv = (Variant)t;
                    if (tv.VariantType != Variant.Type.Dictionary) continue;
                    var td = tv.AsGodotDictionary();

                    int    price    = ParseInt(td, "price", 0);
                    double ts       = ParseDouble(td, "ts", 0);
                    string timeKey  = TimestampToDateKey(ts);
                    EnsureDimTime(timeKey, ts);

                    FactTrades.Add(new FactTrade
                    {
                        TradeKey   = $"{itemId}_{(long)ts}_{tradeIndex++}",
                        ItemKey    = itemId,
                        TimeKey    = timeKey,
                        PlayerKey  = "",
                        Price      = price,
                        Quantity   = 1,
                        TotalValue = price,
                        Timestamp  = ts
                    });
                }
            }
        }

        // ── Transform: score facts ────────────────────────────────────────────
        // Prefer leaderboards/runs (per-session records) for richer daily chart data.
        // Fall back to leaderboards/global (best-score per player) when no runs exist yet.
        bool hasRuns = _oltpRuns != null && _oltpRuns.Count > 0;

        if (hasRuns)
        {
            double nowTs = DateTimeToUnix(DateTime.UtcNow);
            foreach (var runId in _oltpRuns.Keys)
            {
                string key    = runId.ToString();
                var    runVar = (Variant)_oltpRuns[runId];
                if (runVar.VariantType != Variant.Type.Dictionary) continue;
                var run = runVar.AsGodotDictionary();

                string email      = run.ContainsKey("email")     ? ((Variant)run["email"]).AsString()             : "anonymous";
                int    score      = run.ContainsKey("score")     ? ParseIntV((Variant)run["score"], 0)            : 0;
                double ts         = run.ContainsKey("timestamp") ? ParseDoubleV((Variant)run["timestamp"], nowTs) : nowTs;
                // Derive a stable player key from the run id (format: {uid}_{unixTs})
                string playerKey  = key.Contains('_') ? key[..key.LastIndexOf('_')] : key;

                if (!DimPlayers.ContainsKey(playerKey))
                    DimPlayers[playerKey] = new DimPlayer { PlayerKey = playerKey, DisplayName = email };

                string timeKey = TimestampToDateKey(ts);
                EnsureDimTime(timeKey, ts);

                FactScores.Add(new FactScore
                {
                    ScoreKey  = key,        // unique per run
                    PlayerKey = playerKey,
                    TimeKey   = timeKey,
                    Email     = email,
                    Score     = score,
                    Timestamp = ts
                });
            }
        }
        else if (_oltpLeaderboard != null)
        {
            // Fallback: one best-score entry per player from leaderboards/global
            double nowTs = DateTimeToUnix(DateTime.UtcNow);
            foreach (var uid in _oltpLeaderboard.Keys)
            {
                string playerKey = uid.ToString();
                var    entryVar  = (Variant)_oltpLeaderboard[uid];
                if (entryVar.VariantType != Variant.Type.Dictionary) continue;
                var entry = entryVar.AsGodotDictionary();

                string email   = entry.ContainsKey("email")     ? ((Variant)entry["email"]).AsString()             : "anonymous";
                int    score   = entry.ContainsKey("score")     ? ParseIntV((Variant)entry["score"], 0)            : 0;
                double ts      = entry.ContainsKey("updatedAt") ? ParseDoubleV((Variant)entry["updatedAt"], nowTs) : nowTs;

                if (!DimPlayers.ContainsKey(playerKey))
                    DimPlayers[playerKey] = new DimPlayer { PlayerKey = playerKey, DisplayName = email };

                string timeKey = TimestampToDateKey(ts);
                EnsureDimTime(timeKey, ts);

                FactScores.Add(new FactScore
                {
                    ScoreKey  = playerKey,
                    PlayerKey = playerKey,
                    TimeKey   = timeKey,
                    Email     = email,
                    Score     = score,
                    Timestamp = ts
                });
            }
        }

        EmitSignal(SignalName.StatusUpdated,
            $"ETL: Transformed {FactTrades.Count} trade facts, {FactScores.Count} score facts. Pushing to Firebase...");

        PushOlapToFirebase();
    }

    // ── ETL: Load — push to Firebase ─────────────────────────────────────────

    private void PushOlapToFirebase()
    {
        if (_database == null) { FinishEtl(); return; }

        foreach (var kv in DimItems)
        {
            var r = (Node)_database.Call("get_once_database_reference", DimItemsPath + "/" + kv.Key);
            r.Call("update", "", new Godot.Collections.Dictionary
            {
                { "item_key",  kv.Value.ItemKey  },
                { "item_name", kv.Value.ItemName },
                { "category",  kv.Value.Category }
            });
        }

        foreach (var kv in DimTimes)
        {
            var r = (Node)_database.Call("get_once_database_reference", DimTimePath + "/" + kv.Key);
            r.Call("update", "", new Godot.Collections.Dictionary
            {
                { "time_key",   kv.Value.TimeKey    },
                { "year",       kv.Value.Year       },
                { "month",      kv.Value.Month      },
                { "day",        kv.Value.Day        },
                { "quarter",    kv.Value.Quarter    },
                { "week",       kv.Value.WeekOfYear },
                { "month_name", kv.Value.MonthName  }
            });
        }

        foreach (var kv in DimPlayers)
        {
            var r = (Node)_database.Call("get_once_database_reference", DimPlayersPath + "/" + kv.Key);
            r.Call("update", "", new Godot.Collections.Dictionary
            {
                { "player_key",   kv.Value.PlayerKey   },
                { "display_name", kv.Value.DisplayName }
            });
        }

        foreach (var ft in FactTrades)
        {
            var r = (Node)_database.Call("get_once_database_reference", FactTradesPath + "/" + ft.TradeKey);
            r.Call("update", "", new Godot.Collections.Dictionary
            {
                { "trade_key",   ft.TradeKey   },
                { "item_key",    ft.ItemKey    },
                { "time_key",    ft.TimeKey    },
                { "player_key",  ft.PlayerKey  },
                { "price",       ft.Price      },
                { "quantity",    ft.Quantity   },
                { "total_value", ft.TotalValue },
                { "timestamp",   ft.Timestamp  }
            });
        }

        foreach (var fs in FactScores)
        {
            var r = (Node)_database.Call("get_once_database_reference", FactScoresPath + "/" + fs.ScoreKey);
            r.Call("update", "", new Godot.Collections.Dictionary
            {
                { "score_key",  fs.ScoreKey  },
                { "player_key", fs.PlayerKey },
                { "time_key",   fs.TimeKey   },
                { "email",      fs.Email     },
                { "score",      fs.Score     },
                { "timestamp",  fs.Timestamp }
            });
        }

        PushAggregates();

        var metaRef = (Node)_database.Call("get_once_database_reference", MetaPath);
        metaRef.Call("update", "", new Godot.Collections.Dictionary
        {
            { "last_etl_ts",      Time.GetUnixTimeFromSystem() },
            { "trade_fact_count", FactTrades.Count             },
            { "score_fact_count", FactScores.Count             },
            { "dim_item_count",   DimItems.Count               },
            { "dim_time_count",   DimTimes.Count               },
            { "dim_player_count", DimPlayers.Count             }
        });

        FinishEtl();
    }

    private void PushAggregates()
    {
        foreach (var agg in RollUpByItem())
        {
            var r = (Node)_database.Call("get_once_database_reference", AggByItemPath + "/" + agg.Key);
            r.Call("update", "", new Godot.Collections.Dictionary
            {
                { "item_key",     agg.Key        },
                { "item_name",    agg.Label       },
                { "total_trades", agg.TotalTrades },
                { "total_volume", agg.TotalVolume },
                { "avg_price",    agg.AvgPrice    },
                { "min_price",    agg.MinPrice    },
                { "max_price",    agg.MaxPrice    }
            });
        }

        foreach (var agg in RollUpByDay())
        {
            string safeKey = agg.Key.Replace("-", "");
            var r = (Node)_database.Call("get_once_database_reference", AggByDayPath + "/" + safeKey);
            r.Call("update", "", new Godot.Collections.Dictionary
            {
                { "date_key",     agg.Key        },
                { "total_trades", agg.TotalTrades },
                { "total_volume", agg.TotalVolume },
                { "avg_price",    agg.AvgPrice    }
            });
        }
    }

    private void FinishEtl()
    {
        _etlRunning = false;
        EmitSignal(SignalName.StatusUpdated,
            $"ETL complete: {FactTrades.Count} trades, {FactScores.Count} scores synced to OLAP store.");
        EmitSignal(SignalName.EtlCompleted, FactTrades.Count, FactScores.Count);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Load OLAP data from Firebase (read already-computed OLAP store)
    // ─────────────────────────────────────────────────────────────────────────

    public void LoadFromOlapStore()
    {
        EnsureFirebase();
        if (_database == null) return;

        EmitSignal(SignalName.StatusUpdated, "Loading OLAP store from Firebase...");

        // Reset merge-score state for this load cycle
        _cachedScoresDone  = false;
        _globalScoresDone  = false;
        _cachedScoresSnap  = null;
        _globalScoresSnap  = null;

        // Trades + dimensions from OLAP cache
        var ftRef = (Node)_database.Call("get_once_database_reference", FactTradesPath);
        SafeConnect(ftRef, "once_successful", nameof(OnFactTradesLoaded));
        SafeConnect(ftRef, "once_failed",     nameof(OnFactTradesFailed));
        ftRef.Call("once", "");

        var diRef = (Node)_database.Call("get_once_database_reference", DimItemsPath);
        SafeConnect(diRef, "once_successful", nameof(OnDimItemsLoaded));
        SafeConnect(diRef, "once_failed",     nameof(OnDimItemsFailed));
        diRef.Call("once", "");

        // Scores: load OLAP cache AND live leaderboard so all players appear
        // even if ETL hasn't been re-run since they joined.
        var fsRef = (Node)_database.Call("get_once_database_reference", FactScoresPath);
        SafeConnect(fsRef, "once_successful", nameof(OnCachedScoresLoaded));
        SafeConnect(fsRef, "once_failed",     nameof(OnCachedScoresFailed));
        fsRef.Call("once", "");

        var lbRef = (Node)_database.Call("get_once_database_reference", "leaderboards/global");
        SafeConnect(lbRef, "once_successful", nameof(OnGlobalScoresLoaded));
        SafeConnect(lbRef, "once_failed",     nameof(OnGlobalScoresFailed));
        lbRef.Call("once", "");
    }

    private int _loadPending = 3;   // fact_trades + dim_items + TryMergeScores (handles cache + global)

    // Score merge state
    private Godot.Collections.Dictionary _cachedScoresSnap;
    private Godot.Collections.Dictionary _globalScoresSnap;
    private bool _cachedScoresDone;
    private bool _globalScoresDone;

    private void OnCachedScoresLoaded(Godot.Collections.Dictionary snap)
    {
        _cachedScoresSnap = snap;
        _cachedScoresDone = true;
        TryMergeScores();
    }

    private void OnCachedScoresFailed()
    {
        _cachedScoresSnap = null;
        _cachedScoresDone = true;
        TryMergeScores();
    }

    private void OnGlobalScoresLoaded(Godot.Collections.Dictionary snap)
    {
        _globalScoresSnap = snap;
        _globalScoresDone = true;
        TryMergeScores();
    }

    private void OnGlobalScoresFailed()
    {
        _globalScoresSnap = null;
        _globalScoresDone = true;
        TryMergeScores();
    }

    // Merges OLAP-cached scores with live leaderboard so all players
    // appear even without a fresh ETL sync.
    private void TryMergeScores()
    {
        if (!_cachedScoresDone || !_globalScoresDone) return;

        FactScores.Clear();
        double nowTs = DateTimeToUnix(DateTime.UtcNow);

        // Step 1: load what's already in the OLAP cache
        if (_cachedScoresSnap != null)
        {
            foreach (var k in _cachedScoresSnap.Keys)
            {
                var v = (Variant)_cachedScoresSnap[k];
                if (v.VariantType != Variant.Type.Dictionary) continue;
                var d = v.AsGodotDictionary();
                FactScores.Add(new FactScore
                {
                    ScoreKey  = d.ContainsKey("score_key")  ? ((Variant)d["score_key"]).AsString()    : k.ToString(),
                    PlayerKey = d.ContainsKey("player_key") ? ((Variant)d["player_key"]).AsString()   : k.ToString(),
                    TimeKey   = d.ContainsKey("time_key")   ? ((Variant)d["time_key"]).AsString()     : "",
                    Email     = d.ContainsKey("email")      ? ((Variant)d["email"]).AsString()         : "",
                    Score     = d.ContainsKey("score")      ? ParseIntV((Variant)d["score"], 0)       : 0,
                    Timestamp = d.ContainsKey("timestamp")  ? ParseDoubleV((Variant)d["timestamp"],0) : 0,
                });
            }
        }

        // Step 2: merge in any players from leaderboards/global not yet cached
        if (_globalScoresSnap != null)
        {
            var cachedPlayers = new HashSet<string>(FactScores.Select(fs => fs.PlayerKey));
            foreach (var uid in _globalScoresSnap.Keys)
            {
                string playerKey = uid.ToString();
                if (cachedPlayers.Contains(playerKey)) continue;   // already have this player

                var entryVar = (Variant)_globalScoresSnap[uid];
                if (entryVar.VariantType != Variant.Type.Dictionary) continue;
                var entry = entryVar.AsGodotDictionary();

                string email   = entry.ContainsKey("email")     ? ((Variant)entry["email"]).AsString()             : "anonymous";
                int    score   = entry.ContainsKey("score")     ? ParseIntV((Variant)entry["score"], 0)            : 0;
                double ts      = entry.ContainsKey("updatedAt") ? ParseDoubleV((Variant)entry["updatedAt"], nowTs) : nowTs;
                string timeKey = TimestampToDateKey(ts);
                EnsureDimTime(timeKey, ts);

                FactScores.Add(new FactScore
                {
                    ScoreKey  = playerKey,
                    PlayerKey = playerKey,
                    TimeKey   = timeKey,
                    Email     = email,
                    Score     = score,
                    Timestamp = ts
                });
            }
        }

        CheckAllLoaded();
    }

    private void OnFactTradesLoaded(Godot.Collections.Dictionary snap)
    {
        FactTrades.Clear();
        if (snap != null)
        {
            foreach (var k in snap.Keys)
            {
                var v = (Variant)snap[k];
                if (v.VariantType != Variant.Type.Dictionary) continue;
                var d = v.AsGodotDictionary();
                FactTrades.Add(new FactTrade
                {
                    TradeKey   = d.ContainsKey("trade_key")   ? ((Variant)d["trade_key"]).AsString()    : k.ToString(),
                    ItemKey    = d.ContainsKey("item_key")    ? ((Variant)d["item_key"]).AsString()      : "",
                    TimeKey    = d.ContainsKey("time_key")    ? ((Variant)d["time_key"]).AsString()      : "",
                    PlayerKey  = d.ContainsKey("player_key")  ? ((Variant)d["player_key"]).AsString()    : "",
                    Price      = d.ContainsKey("price")       ? ParseIntV((Variant)d["price"], 0)        : 0,
                    Quantity   = d.ContainsKey("quantity")    ? ParseIntV((Variant)d["quantity"], 1)     : 1,
                    TotalValue = d.ContainsKey("total_value") ? ParseIntV((Variant)d["total_value"], 0)  : 0,
                    Timestamp  = d.ContainsKey("timestamp")   ? ParseDoubleV((Variant)d["timestamp"], 0) : 0,
                });
            }
        }
        CheckAllLoaded();
    }

    private void OnFactTradesFailed() { CheckAllLoaded(); }

    private void OnFactScoresLoaded(Godot.Collections.Dictionary snap)
    {
        FactScores.Clear();
        if (snap != null)
        {
            foreach (var k in snap.Keys)
            {
                var v = (Variant)snap[k];
                if (v.VariantType != Variant.Type.Dictionary) continue;
                var d = v.AsGodotDictionary();
                FactScores.Add(new FactScore
                {
                    ScoreKey  = d.ContainsKey("score_key")  ? ((Variant)d["score_key"]).AsString()    : k.ToString(),
                    PlayerKey = d.ContainsKey("player_key") ? ((Variant)d["player_key"]).AsString()    : "",
                    TimeKey   = d.ContainsKey("time_key")   ? ((Variant)d["time_key"]).AsString()      : "",
                    Email     = d.ContainsKey("email")      ? ((Variant)d["email"]).AsString()          : "",
                    Score     = d.ContainsKey("score")      ? ParseIntV((Variant)d["score"], 0)        : 0,
                    Timestamp = d.ContainsKey("timestamp")  ? ParseDoubleV((Variant)d["timestamp"], 0) : 0,
                });
            }
        }
        CheckAllLoaded();
    }

    private void OnFactScoresFailed() { CheckAllLoaded(); }

    private void OnDimItemsLoaded(Godot.Collections.Dictionary snap)
    {
        DimItems.Clear();
        if (snap != null)
        {
            foreach (var k in snap.Keys)
            {
                var    v   = (Variant)snap[k];
                if (v.VariantType != Variant.Type.Dictionary) continue;
                var    d   = v.AsGodotDictionary();
                string key = k.ToString();
                DimItems[key] = new DimItem
                {
                    ItemKey  = key,
                    ItemName = d.ContainsKey("item_name") ? ((Variant)d["item_name"]).AsString() : key,
                    Category = d.ContainsKey("category")  ? ((Variant)d["category"]).AsString()  : "cosmetic",
                };
            }
        }
        CheckAllLoaded();
    }

    private void OnDimItemsFailed() { CheckAllLoaded(); }

    private void CheckAllLoaded()
    {
        _loadPending--;
        if (_loadPending > 0) return;
        _loadPending = 3;   // fact_trades, dim_items, TryBuildLiveScores
        EmitSignal(SignalName.StatusUpdated,
            $"OLAP store loaded: {FactTrades.Count} trade facts, {FactScores.Count} score facts.");
        EmitSignal(SignalName.OlapDataLoaded);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // OLAP Operations — Roll-Up (full time hierarchy: Year → Quarter → Month → Day)
    // ─────────────────────────────────────────────────────────────────────────

    // Roll-Up: GROUP BY item  (collapses all time detail into per-item totals)
    public List<TradeAggregate> RollUpByItem()
    {
        var groups = new Dictionary<string, List<FactTrade>>();
        foreach (var ft in FactTrades)
        {
            if (!groups.ContainsKey(ft.ItemKey)) groups[ft.ItemKey] = new List<FactTrade>();
            groups[ft.ItemKey].Add(ft);
        }
        var result = new List<TradeAggregate>();
        foreach (var kv in groups)
        {
            string name = DimItems.ContainsKey(kv.Key) ? DimItems[kv.Key].ItemName : kv.Key;
            result.Add(BuildAggregate(kv.Key, name, kv.Value));
        }
        result.Sort((a, b) => b.TotalVolume.CompareTo(a.TotalVolume));
        return result;
    }

    // Roll-Up: GROUP BY day  (finest time grain — YYYY-MM-DD)
    public List<TradeAggregate> RollUpByDay()
    {
        var groups = new Dictionary<string, List<FactTrade>>();
        foreach (var ft in FactTrades)
        {
            string key = string.IsNullOrEmpty(ft.TimeKey) ? "unknown" : ft.TimeKey;
            if (!groups.ContainsKey(key)) groups[key] = new List<FactTrade>();
            groups[key].Add(ft);
        }
        var result = new List<TradeAggregate>();
        foreach (var kv in groups)
            result.Add(BuildAggregate(kv.Key, kv.Key, kv.Value));
        result.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.Ordinal));
        return result;
    }

    // Roll-Up: GROUP BY month  (YYYY-MM — one level above day)
    public List<TradeAggregate> RollUpByMonth()
    {
        var groups = new Dictionary<string, List<FactTrade>>();
        foreach (var ft in FactTrades)
        {
            string key = string.IsNullOrEmpty(ft.TimeKey) || ft.TimeKey.Length < 7
                ? "unknown"
                : ft.TimeKey.Substring(0, 7);
            if (!groups.ContainsKey(key)) groups[key] = new List<FactTrade>();
            groups[key].Add(ft);
        }
        var result = new List<TradeAggregate>();
        foreach (var kv in groups)
        {
            string label = kv.Key == "unknown" ? "unknown" : FormatMonthLabel(kv.Key);
            result.Add(BuildAggregate(kv.Key, label, kv.Value));
        }
        result.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.Ordinal));
        return result;
    }

    // Roll-Up: GROUP BY quarter  (YYYY-Q# — one level above month)
    public List<TradeAggregate> RollUpByQuarter()
    {
        var groups = new Dictionary<string, List<FactTrade>>();
        foreach (var ft in FactTrades)
        {
            string qKey = "unknown";
            if (!string.IsNullOrEmpty(ft.TimeKey) && DimTimes.ContainsKey(ft.TimeKey))
            {
                var dt = DimTimes[ft.TimeKey];
                qKey = $"{dt.Year}-Q{dt.Quarter}";
            }
            if (!groups.ContainsKey(qKey)) groups[qKey] = new List<FactTrade>();
            groups[qKey].Add(ft);
        }
        var result = new List<TradeAggregate>();
        foreach (var kv in groups)
            result.Add(BuildAggregate(kv.Key, kv.Key, kv.Value));
        result.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.Ordinal));
        return result;
    }

    // Roll-Up: GROUP BY year  (coarsest time grain — top of hierarchy)
    public List<TradeAggregate> RollUpByYear()
    {
        var groups = new Dictionary<string, List<FactTrade>>();
        foreach (var ft in FactTrades)
        {
            string yKey = string.IsNullOrEmpty(ft.TimeKey) || ft.TimeKey.Length < 4
                ? "unknown"
                : ft.TimeKey.Substring(0, 4);
            if (!groups.ContainsKey(yKey)) groups[yKey] = new List<FactTrade>();
            groups[yKey].Add(ft);
        }
        var result = new List<TradeAggregate>();
        foreach (var kv in groups)
            result.Add(BuildAggregate(kv.Key, kv.Key, kv.Value));
        result.Sort((a, b) => string.Compare(a.Key, b.Key, StringComparison.Ordinal));
        return result;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // OLAP Operations — Drill-Down
    // ─────────────────────────────────────────────────────────────────────────

    // Drill-Down: item aggregate → individual trade facts
    public List<FactTrade> DrillDownByItem(string itemKey)
        => FactTrades.Where(ft => ft.ItemKey == itemKey).OrderByDescending(ft => ft.Timestamp).ToList();

    // Drill-Down: day aggregate → individual trade facts
    public List<FactTrade> DrillDownByDay(string timeKey)
        => FactTrades.Where(ft => ft.TimeKey == timeKey).OrderByDescending(ft => ft.Timestamp).ToList();

    // ─────────────────────────────────────────────────────────────────────────
    // OLAP Operations — Slice (filter on ONE dimension)
    // ─────────────────────────────────────────────────────────────────────────

    // Slice: WHERE item_key = x
    public List<FactTrade> SliceByItem(string itemKey)
    {
        if (itemKey == "all" || string.IsNullOrEmpty(itemKey))
            return FactTrades.OrderByDescending(ft => ft.Timestamp).ToList();
        return FactTrades.Where(ft => ft.ItemKey == itemKey).OrderByDescending(ft => ft.Timestamp).ToList();
    }

    // Slice: WHERE timestamp BETWEEN startTs AND endTs
    public List<FactTrade> SliceByDate(double startTs, double endTs)
        => FactTrades.Where(ft => ft.Timestamp >= startTs && ft.Timestamp <= endTs)
                     .OrderByDescending(ft => ft.Timestamp).ToList();

    // ─────────────────────────────────────────────────────────────────────────
    // OLAP Operations — Dice (filter on MULTIPLE dimensions)
    // ─────────────────────────────────────────────────────────────────────────

    // Dice: WHERE item_key = x AND timestamp BETWEEN startTs AND endTs
    public List<FactTrade> DiceByItemAndDate(string itemKey, double startTs, double endTs)
    {
        var q = FactTrades.AsEnumerable();
        if (!string.IsNullOrEmpty(itemKey) && itemKey != "all") q = q.Where(ft => ft.ItemKey == itemKey);
        if (startTs > 0)                                         q = q.Where(ft => ft.Timestamp >= startTs);
        if (endTs > 0 && endTs < double.MaxValue)               q = q.Where(ft => ft.Timestamp <= endTs);
        return q.OrderByDescending(ft => ft.Timestamp).ToList();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Analytics — Score Summary
    // ─────────────────────────────────────────────────────────────────────────

    public ScoreAggregate GetScoreSummary()
    {
        if (FactScores.Count == 0)
            return new ScoreAggregate { TotalRuns = 0, AvgScore = 0, MaxScore = 0, MinScore = 0 };
        return new ScoreAggregate
        {
            TotalRuns = FactScores.Count,
            AvgScore  = (float)FactScores.Average(fs => fs.Score),
            MaxScore  = FactScores.Max(fs => fs.Score),
            MinScore  = FactScores.Min(fs => fs.Score),
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Analytics — Price Volatility (Coefficient of Variation per item)
    // CV = StdDev / AvgPrice — higher means prices swing more between trades
    // ─────────────────────────────────────────────────────────────────────────

    public List<VolatilityRecord> GetVolatilityMetrics()
    {
        var result = new List<VolatilityRecord>();
        var groups = new Dictionary<string, List<FactTrade>>();
        foreach (var ft in FactTrades)
        {
            if (!groups.ContainsKey(ft.ItemKey)) groups[ft.ItemKey] = new List<FactTrade>();
            groups[ft.ItemKey].Add(ft);
        }
        foreach (var kv in groups)
        {
            if (kv.Value.Count < 2) continue;
            string name = DimItems.ContainsKey(kv.Key) ? DimItems[kv.Key].ItemName : kv.Key;
            float  avg  = (float)kv.Value.Average(t => t.Price);
            float  var2 = (float)kv.Value.Average(t => Math.Pow(t.Price - avg, 2));
            float  sd   = (float)Math.Sqrt(var2);
            result.Add(new VolatilityRecord
            {
                ItemKey        = kv.Key,
                ItemName       = name,
                AvgPrice       = avg,
                StdDev         = sd,
                CoeffVariation = avg > 0 ? sd / avg : 0f,
                TotalTrades    = kv.Value.Count,
                MinPrice       = kv.Value.Min(t => t.Price),
                MaxPrice       = kv.Value.Max(t => t.Price),
            });
        }
        result.Sort((a, b) => b.CoeffVariation.CompareTo(a.CoeffVariation));
        return result;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Analytics — Price Anomaly Detection
    // Flags trades whose price is >= threshold × or <= 1/threshold × the
    // per-item median.  Default threshold = 2.0 (2× above or below median).
    // ─────────────────────────────────────────────────────────────────────────

    public List<AnomalyRecord> DetectPriceAnomalies(float threshold = 2.0f)
    {
        var anomalies = new List<AnomalyRecord>();
        var groups    = new Dictionary<string, List<FactTrade>>();
        foreach (var ft in FactTrades)
        {
            if (!groups.ContainsKey(ft.ItemKey)) groups[ft.ItemKey] = new List<FactTrade>();
            groups[ft.ItemKey].Add(ft);
        }
        foreach (var kv in groups)
        {
            if (kv.Value.Count < 3) continue;
            var   sorted = kv.Value.Select(t => t.Price).OrderBy(p => p).ToList();
            float median = sorted.Count % 2 == 0
                ? (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2f
                : sorted[sorted.Count / 2];
            if (median <= 0) continue;
            string name = DimItems.ContainsKey(kv.Key) ? DimItems[kv.Key].ItemName : kv.Key;
            foreach (var ft in kv.Value)
            {
                float ratio = ft.Price / median;
                if (ratio >= threshold || ratio <= (1f / threshold))
                {
                    anomalies.Add(new AnomalyRecord
                    {
                        ItemKey    = kv.Key,
                        ItemName   = name,
                        TradePrice = ft.Price,
                        MedianPrice= median,
                        Ratio      = ratio,
                        DateKey    = ft.TimeKey,
                    });
                }
            }
        }
        anomalies.Sort((a, b) => b.Ratio.CompareTo(a.Ratio));
        return anomalies.Take(50).ToList();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Analytics — OLAP Star Schema Statistics (ETL Monitor tab)
    // ─────────────────────────────────────────────────────────────────────────

    public SchemaStats GetSchemaStats()
    {
        int activeListings = 0;
        var sellerSet      = new HashSet<string>();
        if (MarketplaceManager.Instance != null)
        {
            activeListings = MarketplaceManager.Instance.Listings.Count;
            foreach (var l in MarketplaceManager.Instance.Listings)
                if (!string.IsNullOrEmpty(l.SellerUid)) sellerSet.Add(l.SellerUid);
        }
        return new SchemaStats
        {
            FactTradeCount    = FactTrades.Count,
            FactScoreCount    = FactScores.Count,
            DimItemCount      = DimItems.Count,
            DimTimeCount      = DimTimes.Count,
            DimPlayerCount    = DimPlayers.Count,
            UniqueItemsTraded = FactTrades.Select(t => t.ItemKey).Distinct().Count(),
            UniqueDaysTraded  = FactTrades.Select(t => t.TimeKey).Distinct().Count(),
            ActiveListings    = activeListings,
            UniqueSellers     = sellerSet.Count,
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Analytics — Seller Leaderboard (live OLTP marketplace data)
    // ─────────────────────────────────────────────────────────────────────────

    public List<SellerRecord> GetSellerLeaderboard()
    {
        if (MarketplaceManager.Instance == null) return new List<SellerRecord>();
        var groups = new Dictionary<string, (string email, int count, int volume, List<int> prices)>();
        foreach (var l in MarketplaceManager.Instance.Listings)
        {
            string key   = string.IsNullOrEmpty(l.SellerUid) ? "unknown" : l.SellerUid;
            string email = l.SellerEmail ?? "unknown";
            if (!groups.ContainsKey(key)) groups[key] = (email, 0, 0, new List<int>());
            var g = groups[key];
            g.prices.Add(l.Price);
            groups[key] = (email, g.count + 1, g.volume + l.Price * l.Quantity, g.prices);
        }
        var result = new List<SellerRecord>();
        foreach (var kv in groups)
        {
            result.Add(new SellerRecord
            {
                SellerEmail  = kv.Value.email,
                ListingCount = kv.Value.count,
                TotalVolume  = kv.Value.volume,
                AvgPrice     = kv.Value.prices.Count > 0 ? (float)kv.Value.prices.Average() : 0f,
            });
        }
        result.Sort((a, b) => b.TotalVolume.CompareTo(a.TotalVolume));
        return result;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CSV Export — with professional metadata headers
    // ─────────────────────────────────────────────────────────────────────────

    public string ExportTradesToCsv(List<FactTrade> trades = null)
    {
        var rows = trades ?? FactTrades;
        var sb   = new StringBuilder();
        sb.AppendLine("trade_key,item_key,item_name,date,price,quantity,total_value,timestamp");
        foreach (var ft in rows)
        {
            string itemName = DimItems.ContainsKey(ft.ItemKey) ? DimItems[ft.ItemKey].ItemName : ft.ItemKey;
            string date     = ft.Timestamp > 0
                ? DateTimeOffset.FromUnixTimeSeconds((long)ft.Timestamp).ToString("yyyy-MM-dd")
                : ft.TimeKey;
            sb.AppendLine($"{Escape(ft.TradeKey)},{Escape(ft.ItemKey)},{Escape(itemName)}" +
                          $",{date},{ft.Price},{ft.Quantity},{ft.TotalValue},{ft.Timestamp}");
        }
        return sb.ToString();
    }

    public string ExportScoresToCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("score_key,email,date,score");
        foreach (var fs in FactScores.OrderByDescending(s => s.Score))
        {
            string date = fs.Timestamp > 0
                ? DateTimeOffset.FromUnixTimeSeconds((long)fs.Timestamp).ToString("yyyy-MM-dd")
                : fs.TimeKey;
            sb.AppendLine($"{Escape(fs.ScoreKey)},{Escape(fs.Email)},{date},{fs.Score}");
        }
        return sb.ToString();
    }

    // Full analytical report: metadata header + trades section + scores section
    public string ExportFullReportCsv(string reportTitle, string filterDesc)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# SBOLK OLAP Analytics Report");
        sb.AppendLine($"# Report     : {reportTitle}");
        sb.AppendLine($"# Filters    : {filterDesc}");
        sb.AppendLine($"# Generated  : {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"# Fact Trades: {FactTrades.Count}   Fact Scores: {FactScores.Count}");
        sb.AppendLine($"# Dim Items  : {DimItems.Count}   Dim Time: {DimTimes.Count}   Dim Players: {DimPlayers.Count}");
        sb.AppendLine("#");
        sb.AppendLine("# SECTION: TRADE FACTS");
        sb.Append(ExportTradesToCsv());
        sb.AppendLine("#");
        sb.AppendLine("# SECTION: SCORE FACTS");
        sb.Append(ExportScoresToCsv());
        return sb.ToString();
    }

    // Scores-only analytical report
    public string ExportScoresReportCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# SBOLK Score Analytics Report");
        sb.AppendLine($"# Generated  : {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"# Total Players: {FactScores.Count}");
        sb.AppendLine("#");
        sb.Append(ExportScoresToCsv());
        return sb.ToString();
    }

    public void SaveCsvToFile(string csvContent, string filename)
    {
        string path = $"user://{filename}";
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file == null) return;
        file.StoreString(csvContent);
        GD.Print($"OlapManager: CSV saved to {path}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Seed in-memory OLAP from MarketplaceManager (no Firebase round-trip)
    // ─────────────────────────────────────────────────────────────────────────

    public void SeedFromMarketplaceManager()
    {
        if (MarketplaceManager.Instance == null) return;

        FactTrades.Clear();
        DimItems.Clear();
        DimTimes.Clear();

        foreach (var kv in MarketplaceManager.Instance.PriceHistory)
        {
            string itemId   = kv.Key;
            string itemName = GetItemName(itemId);
            if (!DimItems.ContainsKey(itemId))
                DimItems[itemId] = new DimItem { ItemKey = itemId, ItemName = itemName, Category = "cosmetic" };

            int idx = 0;
            foreach (var record in kv.Value)
            {
                string timeKey = TimestampToDateKey(record.Timestamp);
                EnsureDimTime(timeKey, record.Timestamp);
                FactTrades.Add(new FactTrade
                {
                    TradeKey   = $"{itemId}_{(long)record.Timestamp}_{idx++}",
                    ItemKey    = itemId,
                    TimeKey    = timeKey,
                    PlayerKey  = "",
                    Price      = record.Price,
                    Quantity   = 1,
                    TotalValue = record.Price,
                    Timestamp  = record.Timestamp
                });
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private utilities
    // ─────────────────────────────────────────────────────────────────────────

    private static TradeAggregate BuildAggregate(string key, string label, List<FactTrade> trades)
    {
        int   total = trades.Sum(t => t.TotalValue);
        float avg   = trades.Count > 0 ? (float)trades.Average(t => t.Price) : 0f;
        int   minP  = trades.Count > 0 ? trades.Min(t => t.Price) : 0;
        int   maxP  = trades.Count > 0 ? trades.Max(t => t.Price) : 0;
        return new TradeAggregate
        {
            Key         = key,
            Label       = label,
            TotalTrades = trades.Count,
            TotalVolume = total,
            AvgPrice    = avg,
            MinPrice    = minP,
            MaxPrice    = maxP,
        };
    }

    private void EnsureDimTime(string timeKey, double ts)
    {
        if (DimTimes.ContainsKey(timeKey)) return;
        var dt = ts > 0
            ? DateTimeOffset.FromUnixTimeSeconds((long)ts).UtcDateTime
            : DateTime.UtcNow;
        DimTimes[timeKey] = new DimTime
        {
            TimeKey    = timeKey,
            Year       = dt.Year,
            Month      = dt.Month,
            Day        = dt.Day,
            Quarter    = (dt.Month - 1) / 3 + 1,
            WeekOfYear = System.Globalization.ISOWeek.GetWeekOfYear(dt),
            MonthName  = dt.ToString("MMMM"),
        };
    }

    private static string TimestampToDateKey(double ts)
        => ts <= 0 ? DateTime.UtcNow.ToString("yyyy-MM-dd")
                   : DateTimeOffset.FromUnixTimeSeconds((long)ts).ToString("yyyy-MM-dd");

    private static double DateTimeToUnix(DateTime dt)
        => (dt.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

    private static string FormatMonthLabel(string yyyyMM)
        => DateTime.TryParse(yyyyMM + "-01", out var dt) ? dt.ToString("MMM yyyy") : yyyyMM;

    private static string GetItemName(string itemId)
    {
        if (CosmeticManager.Instance == null) return itemId;
        foreach (var c in CosmeticManager.Instance.AllCosmetics)
            if (c.Id == itemId) return c.Name;
        return itemId;
    }

    private static string Escape(string s)
        => s.Contains(',') || s.Contains('"') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;

    private void SafeConnect(Node node, string signal, string method)
    {
        var c = new Callable(this, method);
        if (!node.IsConnected(signal, c)) node.Connect(signal, c);
    }

    private static int ParseInt(Godot.Collections.Dictionary d, string k, int def)
        => d.ContainsKey(k) ? ParseIntV((Variant)d[k], def) : def;

    private static double ParseDouble(Godot.Collections.Dictionary d, string k, double def)
        => d.ContainsKey(k) ? ParseDoubleV((Variant)d[k], def) : def;

    private static int ParseIntV(Variant v, int def) => v.VariantType switch
    {
        Variant.Type.Int    => (int)v.AsInt64(),
        Variant.Type.Float  => (int)v.AsDouble(),
        Variant.Type.String => int.TryParse(v.AsString(), out var p) ? p : def,
        _                   => def
    };

    private static double ParseDoubleV(Variant v, double def) => v.VariantType switch
    {
        Variant.Type.Int    => (double)v.AsInt64(),
        Variant.Type.Float  => v.AsDouble(),
        Variant.Type.String => double.TryParse(v.AsString(), out var p) ? p : def,
        _                   => def
    };
}
