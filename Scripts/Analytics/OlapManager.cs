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
//     olap/dim_time/{dateKey}             → Dimension: date hierarchy
//     olap/dim_players/{uid}              → Dimension: player info
//     olap/fact_trades/{tradeId}          → Fact: one row per trade event
//     olap/fact_scores/{uid}              → Fact: one row per player best score
//     olap/aggregates/by_item/{itemId}    → Roll-up: aggregated per item
//     olap/aggregates/by_day/{dateKey}    → Roll-up: aggregated per day
//     olap/meta/last_etl                  → ETL run metadata
//
// OLAP Operations implemented:
//   Roll-up   : aggregate trades grouped by item or by day
//   Drill-down: expand from an item aggregate → individual trade facts
//   Slice     : filter fact_trades on a single dimension (item OR date)
//   Dice      : filter fact_trades on multiple dimensions (item AND date range)
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
        public string PlayerKey;   // FK → dim_players (buyer, if known)
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

    // ── In-Memory OLAP Store ─────────────────────────────────────────────────

    public List<FactTrade>              FactTrades  { get; private set; } = new();
    public List<FactScore>              FactScores  { get; private set; } = new();
    public Dictionary<string, DimItem>  DimItems    { get; private set; } = new();
    public Dictionary<string, DimTime>  DimTimes    { get; private set; } = new();
    public Dictionary<string, DimPlayer> DimPlayers { get; private set; } = new();

    // ── Firebase references ──────────────────────────────────────────────────

    private Node _firebase;
    private Node _auth;
    private Node _database;
    private bool _etlRunning;

    // ── ETL state ────────────────────────────────────────────────────────────

    private Godot.Collections.Dictionary _oltpHistory;
    private Godot.Collections.Dictionary _oltpLeaderboard;
    private bool _historyLoaded;
    private bool _leaderboardLoaded;

    // ── Firebase OLAP paths ──────────────────────────────────────────────────

    private const string OlapRoot      = "olap";
    private const string DimItemsPath  = "olap/dim_items";
    private const string DimTimePath   = "olap/dim_time";
    private const string DimPlayersPath= "olap/dim_players";
    private const string FactTradesPath= "olap/fact_trades";
    private const string FactScoresPath= "olap/fact_scores";
    private const string AggByItemPath = "olap/aggregates/by_item";
    private const string AggByDayPath  = "olap/aggregates/by_day";
    private const string MetaPath      = "olap/meta";

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

        _etlRunning      = true;
        _historyLoaded   = false;
        _leaderboardLoaded = false;

        EmitSignal(SignalName.StatusUpdated, "ETL: Extracting OLTP data from Firebase...");

        // Extract Step 1: read marketplace price history (trade facts source)
        var histRef = (Node)_database.Call("get_once_database_reference", "marketplace/priceHistory");
        SafeConnect(histRef, "once_successful", nameof(OnOltpHistoryLoaded));
        SafeConnect(histRef, "once_failed",     nameof(OnOltpHistoryFailed));
        histRef.Call("once", "");

        // Extract Step 2: read leaderboard (score facts source)
        var lbRef = (Node)_database.Call("get_once_database_reference", "leaderboards/global");
        SafeConnect(lbRef, "once_successful", nameof(OnOltpLeaderboardLoaded));
        SafeConnect(lbRef, "once_failed",     nameof(OnOltpLeaderboardFailed));
        lbRef.Call("once", "");
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

    // ── ETL: Transform + Load ─────────────────────────────────────────────────

    private void TryRunTransform()
    {
        if (!_historyLoaded || !_leaderboardLoaded) return;

        EmitSignal(SignalName.StatusUpdated, "ETL: Transforming into Star Schema...");

        FactTrades.Clear();
        FactScores.Clear();
        DimItems.Clear();
        DimTimes.Clear();
        DimPlayers.Clear();

        // ── Transform trade facts ─────────────────────────────────────────────
        if (_oltpHistory != null)
        {
            foreach (var itemKey in _oltpHistory.Keys)
            {
                string itemId   = itemKey.ToString();
                var    itemVar  = (Variant)_oltpHistory[itemKey];
                if (itemVar.VariantType != Variant.Type.Dictionary) continue;
                var itemDict = itemVar.AsGodotDictionary();

                // Upsert dim_items dimension
                string itemName = CosmeticManager.Instance != null
                    ? GetItemName(itemId)
                    : itemId;

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

                    int    price = ParseInt(td, "price", 0);
                    double ts    = ParseDouble(td, "ts", 0);

                    // Build time dimension key from Unix timestamp
                    string timeKey = TimestampToDateKey(ts);
                    EnsureDimTime(timeKey, ts);

                    // Build unique trade key
                    string tradeKey = $"{itemId}_{(long)ts}_{tradeIndex++}";

                    FactTrades.Add(new FactTrade
                    {
                        TradeKey   = tradeKey,
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

        // ── Transform score facts ─────────────────────────────────────────────
        if (_oltpLeaderboard != null)
        {
            double nowTs = DateTimeToUnix(DateTime.UtcNow);
            foreach (var uid in _oltpLeaderboard.Keys)
            {
                string playerKey = uid.ToString();
                var    entryVar  = (Variant)_oltpLeaderboard[uid];
                if (entryVar.VariantType != Variant.Type.Dictionary) continue;
                var entry = entryVar.AsGodotDictionary();

                string email = entry.ContainsKey("email")
                    ? ((Variant)entry["email"]).AsString()
                    : "anonymous";
                int    score = entry.ContainsKey("score")
                    ? ParseIntV((Variant)entry["score"], 0)
                    : 0;
                double ts    = entry.ContainsKey("updatedAt")
                    ? ParseDoubleV((Variant)entry["updatedAt"], nowTs)
                    : nowTs;

                // Upsert dim_players
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
            $"ETL: Transformed {FactTrades.Count} trade facts, {FactScores.Count} score facts. Loading to Firebase...");

        // ── Load Step: push OLAP schema to Firebase ───────────────────────────
        PushOlapToFirebase();
    }

    // ── ETL: Load — push OLAP to Firebase ────────────────────────────────────

    private void PushOlapToFirebase()
    {
        if (_database == null) { FinishEtl(); return; }

        // Push dim_items
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

        // Push dim_time
        foreach (var kv in DimTimes)
        {
            var r = (Node)_database.Call("get_once_database_reference", DimTimePath + "/" + kv.Key);
            r.Call("update", "", new Godot.Collections.Dictionary
            {
                { "time_key",    kv.Value.TimeKey    },
                { "year",        kv.Value.Year       },
                { "month",       kv.Value.Month      },
                { "day",         kv.Value.Day        },
                { "quarter",     kv.Value.Quarter    },
                { "week",        kv.Value.WeekOfYear },
                { "month_name",  kv.Value.MonthName  }
            });
        }

        // Push dim_players
        foreach (var kv in DimPlayers)
        {
            var r = (Node)_database.Call("get_once_database_reference", DimPlayersPath + "/" + kv.Key);
            r.Call("update", "", new Godot.Collections.Dictionary
            {
                { "player_key",   kv.Value.PlayerKey   },
                { "display_name", kv.Value.DisplayName }
            });
        }

        // Push fact_trades
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

        // Push fact_scores
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

        // Push aggregates (materialized roll-ups)
        PushAggregates();

        // Push ETL metadata
        var metaRef = (Node)_database.Call("get_once_database_reference", MetaPath);
        metaRef.Call("update", "", new Godot.Collections.Dictionary
        {
            { "last_etl_ts",      Time.GetUnixTimeFromSystem()  },
            { "trade_fact_count", FactTrades.Count              },
            { "score_fact_count", FactScores.Count              },
            { "dim_item_count",   DimItems.Count                },
            { "dim_time_count",   DimTimes.Count                },
            { "dim_player_count", DimPlayers.Count              }
        });

        FinishEtl();
    }

    private void PushAggregates()
    {
        // Roll-up by item → aggregates/by_item
        var byItem = RollUpByItem();
        foreach (var agg in byItem)
        {
            var r = (Node)_database.Call("get_once_database_reference", AggByItemPath + "/" + agg.Key);
            r.Call("update", "", new Godot.Collections.Dictionary
            {
                { "item_key",     agg.Key          },
                { "item_name",    agg.Label         },
                { "total_trades", agg.TotalTrades   },
                { "total_volume", agg.TotalVolume   },
                { "avg_price",    agg.AvgPrice       },
                { "min_price",    agg.MinPrice       },
                { "max_price",    agg.MaxPrice       }
            });
        }

        // Roll-up by day → aggregates/by_day
        var byDay = RollUpByDay();
        foreach (var agg in byDay)
        {
            string safeKey = agg.Key.Replace("-", "");
            var r = (Node)_database.Call("get_once_database_reference", AggByDayPath + "/" + safeKey);
            r.Call("update", "", new Godot.Collections.Dictionary
            {
                { "date_key",     agg.Key          },
                { "total_trades", agg.TotalTrades   },
                { "total_volume", agg.TotalVolume   },
                { "avg_price",    agg.AvgPrice       }
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

        var ftRef = (Node)_database.Call("get_once_database_reference", FactTradesPath);
        SafeConnect(ftRef, "once_successful", nameof(OnFactTradesLoaded));
        SafeConnect(ftRef, "once_failed",     nameof(OnFactTradesFailed));
        ftRef.Call("once", "");

        var fsRef = (Node)_database.Call("get_once_database_reference", FactScoresPath);
        SafeConnect(fsRef, "once_successful", nameof(OnFactScoresLoaded));
        SafeConnect(fsRef, "once_failed",     nameof(OnFactScoresFailed));
        fsRef.Call("once", "");

        var diRef = (Node)_database.Call("get_once_database_reference", DimItemsPath);
        SafeConnect(diRef, "once_successful", nameof(OnDimItemsLoaded));
        SafeConnect(diRef, "once_failed",     nameof(OnDimItemsFailed));
        diRef.Call("once", "");
    }

    private int _loadPending = 3;

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
                    TradeKey   = d.ContainsKey("trade_key")   ? ((Variant)d["trade_key"]).AsString()  : k.ToString(),
                    ItemKey    = d.ContainsKey("item_key")    ? ((Variant)d["item_key"]).AsString()    : "",
                    TimeKey    = d.ContainsKey("time_key")    ? ((Variant)d["time_key"]).AsString()    : "",
                    PlayerKey  = d.ContainsKey("player_key")  ? ((Variant)d["player_key"]).AsString()  : "",
                    Price      = d.ContainsKey("price")       ? ParseIntV((Variant)d["price"], 0)      : 0,
                    Quantity   = d.ContainsKey("quantity")    ? ParseIntV((Variant)d["quantity"], 1)   : 1,
                    TotalValue = d.ContainsKey("total_value") ? ParseIntV((Variant)d["total_value"], 0): 0,
                    Timestamp  = d.ContainsKey("timestamp")   ? ParseDoubleV((Variant)d["timestamp"],0): 0,
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
                    ScoreKey  = d.ContainsKey("score_key")  ? ((Variant)d["score_key"]).AsString()  : k.ToString(),
                    PlayerKey = d.ContainsKey("player_key") ? ((Variant)d["player_key"]).AsString()  : "",
                    TimeKey   = d.ContainsKey("time_key")   ? ((Variant)d["time_key"]).AsString()    : "",
                    Email     = d.ContainsKey("email")      ? ((Variant)d["email"]).AsString()        : "",
                    Score     = d.ContainsKey("score")      ? ParseIntV((Variant)d["score"], 0)      : 0,
                    Timestamp = d.ContainsKey("timestamp")  ? ParseDoubleV((Variant)d["timestamp"],0): 0,
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
                var v = (Variant)snap[k];
                if (v.VariantType != Variant.Type.Dictionary) continue;
                var d  = v.AsGodotDictionary();
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
        _loadPending = 3;
        EmitSignal(SignalName.StatusUpdated,
            $"OLAP store loaded: {FactTrades.Count} trade facts, {FactScores.Count} score facts.");
        EmitSignal(SignalName.OlapDataLoaded);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // OLAP Operations
    // ─────────────────────────────────────────────────────────────────────────

    // Roll-Up: aggregate fact_trades GROUP BY item — collapses detail into summary
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

    // Roll-Up: aggregate fact_trades GROUP BY day — collapses detail into daily summary
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

    // Drill-Down: from a rolled-up item key, expand to individual trade facts
    public List<FactTrade> DrillDownByItem(string itemKey)
    {
        return FactTrades
            .Where(ft => ft.ItemKey == itemKey)
            .OrderByDescending(ft => ft.Timestamp)
            .ToList();
    }

    // Drill-Down: from a rolled-up day key, expand to individual trade facts
    public List<FactTrade> DrillDownByDay(string timeKey)
    {
        return FactTrades
            .Where(ft => ft.TimeKey == timeKey)
            .OrderByDescending(ft => ft.Timestamp)
            .ToList();
    }

    // Slice: filter fact_trades on ONE dimension — item (WHERE item_key = x)
    public List<FactTrade> SliceByItem(string itemKey)
    {
        if (itemKey == "all" || string.IsNullOrEmpty(itemKey))
            return FactTrades.OrderByDescending(ft => ft.Timestamp).ToList();

        return FactTrades
            .Where(ft => ft.ItemKey == itemKey)
            .OrderByDescending(ft => ft.Timestamp)
            .ToList();
    }

    // Slice: filter fact_trades on ONE dimension — date range
    public List<FactTrade> SliceByDate(double startTs, double endTs)
    {
        return FactTrades
            .Where(ft => ft.Timestamp >= startTs && ft.Timestamp <= endTs)
            .OrderByDescending(ft => ft.Timestamp)
            .ToList();
    }

    // Dice: filter fact_trades on MULTIPLE dimensions — item AND date range
    public List<FactTrade> DiceByItemAndDate(string itemKey, double startTs, double endTs)
    {
        var query = FactTrades.AsEnumerable();

        if (!string.IsNullOrEmpty(itemKey) && itemKey != "all")
            query = query.Where(ft => ft.ItemKey == itemKey);

        if (startTs > 0)
            query = query.Where(ft => ft.Timestamp >= startTs);

        if (endTs > 0)
            query = query.Where(ft => ft.Timestamp <= endTs);

        return query.OrderByDescending(ft => ft.Timestamp).ToList();
    }

    // Score summary — aggregate over all fact_scores
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
    // CSV Export
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

    public void SaveCsvToFile(string csvContent, string filename)
    {
        string path = $"user://{filename}";
        using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file == null) return;
        file.StoreString(csvContent);
        GD.Print($"OlapManager: CSV saved to {path}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers — feed in-memory data from MarketplaceManager without ETL
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

                string tradeKey = $"{itemId}_{(long)record.Timestamp}_{idx++}";
                FactTrades.Add(new FactTrade
                {
                    TradeKey   = tradeKey,
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
        int total   = trades.Sum(t => t.TotalValue);
        float avg   = trades.Count > 0 ? (float)trades.Average(t => t.Price) : 0f;
        int minP    = trades.Count > 0 ? trades.Min(t => t.Price) : 0;
        int maxP    = trades.Count > 0 ? trades.Max(t => t.Price) : 0;

        return new TradeAggregate
        {
            Key          = key,
            Label        = label,
            TotalTrades  = trades.Count,
            TotalVolume  = total,
            AvgPrice     = avg,
            MinPrice     = minP,
            MaxPrice     = maxP,
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
    {
        if (ts <= 0) return DateTime.UtcNow.ToString("yyyy-MM-dd");
        return DateTimeOffset.FromUnixTimeSeconds((long)ts).ToString("yyyy-MM-dd");
    }

    private static double DateTimeToUnix(DateTime dt)
        => (dt.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

    private static string GetItemName(string itemId)
    {
        if (CosmeticManager.Instance == null) return itemId;
        foreach (var c in CosmeticManager.Instance.AllCosmetics)
            if (c.Id == itemId) return c.Name;
        return itemId;
    }

    private static string Escape(string s) =>
        s.Contains(',') || s.Contains('"') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;

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
        Variant.Type.Int   => (int)v.AsInt64(),
        Variant.Type.Float => (int)v.AsDouble(),
        Variant.Type.String => int.TryParse(v.AsString(), out var p) ? p : def,
        _ => def
    };

    private static double ParseDoubleV(Variant v, double def) => v.VariantType switch
    {
        Variant.Type.Int   => (double)v.AsInt64(),
        Variant.Type.Float => v.AsDouble(),
        Variant.Type.String => double.TryParse(v.AsString(), out var p) ? p : def,
        _ => def
    };
}
