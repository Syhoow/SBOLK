using Godot;
using System;
using System.Collections.Generic;

public partial class MarketplaceManager : Node
{
    public static MarketplaceManager Instance;

    public class Listing
    {
        public string Id;
        public string ItemId;
        public string ItemName;
        public string SellerUid;
        public string SellerEmail;
        public int    Price;
        public int    Quantity;
        public double CreatedAt;
    }

    public class TradeRecord
    {
        public int    Price;
        public double Timestamp;
    }

    public event Action DataChanged;

    public List<Listing>                          Listings     = new();
    public Dictionary<string, List<TradeRecord>>  PriceHistory = new();

    private const string ListingsPath    = "marketplace/listings";
    private const string HistoryPath     = "marketplace/priceHistory";
    private const string PayoutsPath     = "marketplace/pendingPayouts";
    private const string PlayersPath     = "players";
    private const string CachePath       = "user://marketplace_cache.json";

    private Node   _firebase;
    private Node   _auth;
    private Node   _database;
    private bool   _waitingForAuth;
    private bool   _claimingPayouts;
    private string _myUid = "";

    // ─── Lifecycle ───────────────────────────────────────────────────────────

    public override void _Ready()
    {
        Instance = this;
        EnsureFirebaseNodes();
        LoadCache();
        TryLoadFromCloud();
    }

    // ─── Firebase plumbing ───────────────────────────────────────────────────

    private void EnsureFirebaseNodes()
    {
        _firebase = GetNodeOrNull<Node>("/root/Firebase");
        _auth     = _firebase?.GetNodeOrNull<Node>("Auth");
        _database = _firebase?.GetNodeOrNull<Node>("Database");

        if (_auth == null) return;
        var loggedOut = new Callable(this, nameof(OnLoggedOut));
        if (!_auth.IsConnected("logged_out", loggedOut))
            _auth.Connect("logged_out", loggedOut);
    }

    private bool IsLoggedIn()
    {
        if (_auth == null) return false;
        var v = (Variant)_auth.Call("is_logged_in");
        return v.VariantType == Variant.Type.Bool && v.AsBool();
    }

    private string GetCurrentUid()
    {
        if (_auth == null) return "";
        var authVar = _auth.Get("auth");
        if (authVar.VariantType != Variant.Type.Dictionary) return "";
        var d = authVar.AsGodotDictionary();
        if (d.ContainsKey("localid")) return ((Variant)d["localid"]).AsString();
        if (d.ContainsKey("localId")) return ((Variant)d["localId"]).AsString();
        return "";
    }

    private string GetDisplayName()
    {
        var uid = GetCurrentUid();
        if (!string.IsNullOrEmpty(uid))
            return "Player #" + uid.Substring(0, Math.Min(6, uid.Length)).ToUpper();
        return "Player";
    }

    private void RequestAuth()
    {
        if (_waitingForAuth) return;
        _waitingForAuth = true;

        var callable = new Callable(this, nameof(OnAuthRequest));
        if (!_auth.IsConnected("auth_request", callable))
            _auth.Connect("auth_request", callable);

        var hasAuthFileVar = (Variant)_auth.Call("check_auth_file");
        var hasAuthFile = hasAuthFileVar.VariantType == Variant.Type.Bool && hasAuthFileVar.AsBool();
        if (!hasAuthFile)
            _auth.Call("login_anonymous");
    }

    private void OnAuthRequest(long resultCode, Variant _resultContent)
    {
        _waitingForAuth = false;
        if (resultCode == 1)
        {
            EnsureFirebaseNodes();
            _myUid = GetCurrentUid();
            TryLoadFromCloud();
        }
    }

    private void OnLoggedOut()
    {
        _myUid = "";
        _claimingPayouts = false;
        Listings.Clear();
        PriceHistory.Clear();
        DataChanged?.Invoke();
    }

    // ─── Cloud load ──────────────────────────────────────────────────────────

    public void TryLoadFromCloud()
    {
        EnsureFirebaseNodes();
        if (_database == null || _auth == null) return;

        if (!IsLoggedIn()) { RequestAuth(); return; }

        _myUid = GetCurrentUid();

        // Load all listings
        var listRef = (Node)_database.Call("get_once_database_reference", ListingsPath);
        Connect(listRef, "once_successful", nameof(OnListingsLoaded));
        Connect(listRef, "once_failed",     nameof(OnListingsFailed));
        listRef.Call("once", "");

        // Load price history
        var histRef = (Node)_database.Call("get_once_database_reference", HistoryPath);
        Connect(histRef, "once_successful", nameof(OnHistoryLoaded));
        Connect(histRef, "once_failed",     nameof(OnHistoryFailed));
        histRef.Call("once", "");

        // Claim any pending payouts owed to me
        if (!string.IsNullOrEmpty(_myUid))
            ClaimPendingPayouts();
    }

    // ─── Listings ────────────────────────────────────────────────────────────

    private void OnListingsLoaded(Godot.Collections.Dictionary snapshot)
    {
        Listings.Clear();
        if (snapshot != null)
        {
            foreach (var key in snapshot.Keys)
            {
                var v = (Variant)snapshot[key];
                if (v.VariantType != Variant.Type.Dictionary) continue;
                var d = v.AsGodotDictionary();
                bool sold = d.ContainsKey("sold") && ((Variant)d["sold"]).VariantType == Variant.Type.Bool && ((Variant)d["sold"]).AsBool();
                if (sold) continue;
                var l = ParseListing(key.ToString(), d);
                if (l != null && l.Quantity > 0) Listings.Add(l);
            }
        }
        SaveCache();
        DataChanged?.Invoke();
    }

    private void OnListingsFailed() { }

    // ─── Price history ───────────────────────────────────────────────────────

    private void OnHistoryLoaded(Godot.Collections.Dictionary snapshot)
    {
        PriceHistory.Clear();
        if (snapshot == null) return;

        foreach (var itemKey in snapshot.Keys)
        {
            var itemId  = itemKey.ToString();
            var itemVar = (Variant)snapshot[itemKey];
            if (itemVar.VariantType != Variant.Type.Dictionary) continue;
            var itemDict = itemVar.AsGodotDictionary();
            if (!itemDict.ContainsKey("trades")) continue;
            var tradesVar = (Variant)itemDict["trades"];
            if (tradesVar.VariantType != Variant.Type.Array) continue;

            var trades = new List<TradeRecord>();
            foreach (var t in tradesVar.AsGodotArray())
            {
                var tv = (Variant)t;
                if (tv.VariantType != Variant.Type.Dictionary) continue;
                var td = tv.AsGodotDictionary();
                trades.Add(new TradeRecord
                {
                    Price     = td.ContainsKey("price") ? ParseInt((Variant)td["price"], 0) : 0,
                    Timestamp = td.ContainsKey("ts")    ? ParseDouble((Variant)td["ts"], 0) : 0,
                });
            }
            PriceHistory[itemId] = trades;
        }
        DataChanged?.Invoke();
    }

    private void OnHistoryFailed() { }

    // ─── Post listing ────────────────────────────────────────────────────────

    public bool PostListing(string itemId, string itemName, int price, int quantity)
    {
        if (CosmeticManager.Instance == null) return false;
        if (CosmeticManager.Instance.GetOwnedCount(itemId) < quantity) return false;
        if (price <= 0 || quantity <= 0) return false;

        var uid = GetCurrentUid();
        if (string.IsNullOrEmpty(uid)) return false;

        var id = uid + "_" + itemId + "_" + (long)Time.GetUnixTimeFromSystem();
        var listing = new Listing
        {
            Id          = id,
            ItemId      = itemId,
            ItemName    = itemName,
            SellerUid   = uid,
            SellerEmail = GetDisplayName(),
            Price       = price,
            Quantity    = quantity,
            CreatedAt   = Time.GetUnixTimeFromSystem()
        };

        // Deduct item from seller inventory immediately
        CosmeticManager.Instance.OwnedCounts[itemId] =
            CosmeticManager.Instance.GetOwnedCount(itemId) - quantity;

        Listings.Add(listing);
        SaveCache();
        DataChanged?.Invoke();

        // Push listing to Firebase
        EnsureFirebaseNodes();
        if (_database != null && IsLoggedIn())
        {
            var refNode = (Node)_database.Call("get_once_database_reference", ListingsPath + "/" + id);
            refNode.Call("update", "", ListingToDict(listing));
        }

        return true;
    }

    // ─── Buy listing (real transaction) ──────────────────────────────────────

    public bool BuyListing(string listingId)
    {
        if (CosmeticManager.Instance == null) return false;

        var listing = Listings.Find(l => l.Id == listingId);
        if (listing == null) return false;

        var uid = GetCurrentUid();
        if (string.IsNullOrEmpty(uid)) return false;
        if (listing.SellerUid == uid) return false;          // can't buy own listing
        if (CosmeticManager.Instance.Coins < listing.Price) return false;
        if (listing.Quantity <= 0) return false;

        // ── Step 1: Deduct buyer coins & give item ──────────────────────────
        CosmeticManager.Instance.Coins -= listing.Price;
        CosmeticManager.Instance.OwnedCounts[listing.ItemId] =
            CosmeticManager.Instance.GetOwnedCount(listing.ItemId) + 1;
        CosmeticManager.Instance.QueueSave();   // persist coins + item to Firebase

        // ── Step 2: Remove listing locally ─────────────────────────────────
        int newQty = listing.Quantity - 1;
        listing.Quantity = newQty;
        if (newQty <= 0)
            Listings.Remove(listing);

        RecordTrade(listing.ItemId, listing.Price);
        SaveCache();
        DataChanged?.Invoke();

        // ── Step 3: Push everything to Firebase ─────────────────────────────
        EnsureFirebaseNodes();
        if (_database != null && IsLoggedIn())
        {
            // 3a. Update or zero-out the listing so it never re-appears on refresh
            var listRef = (Node)_database.Call("get_once_database_reference",
                ListingsPath + "/" + listingId);
            listRef.Call("update", "", new Godot.Collections.Dictionary
            {
                { "quantity", newQty },
                { "sold",     newQty <= 0 }
            });

            // 3b. Write pending payout so seller gets coins next login
            var payoutId  = uid + "_" + (long)Time.GetUnixTimeFromSystem();
            var payoutRef = (Node)_database.Call("get_once_database_reference",
                PayoutsPath + "/" + listing.SellerUid + "/" + payoutId);
            payoutRef.Call("update", "", new Godot.Collections.Dictionary
            {
                { "amount",    listing.Price               },
                { "itemId",    listing.ItemId              },
                { "itemName",  listing.ItemName            },
                { "buyerUid",  uid                         },
                { "timestamp", Time.GetUnixTimeFromSystem() }
            });

            // 3c. Push price history
            PushHistoryToCloud(listing.ItemId);
        }

        return true;
    }

    // ─── Claim payouts (seller receives coins) ────────────────────────────────

    private void ClaimPendingPayouts()
    {
        if (_claimingPayouts) return;
        if (string.IsNullOrEmpty(_myUid)) return;
        _claimingPayouts = true;

        var refNode = (Node)_database.Call("get_once_database_reference",
            PayoutsPath + "/" + _myUid);
        Connect(refNode, "once_successful", nameof(OnPayoutsLoaded));
        Connect(refNode, "once_failed",     nameof(OnPayoutsFailed));
        refNode.Call("once", "");
    }

    private void OnPayoutsFailed()
    {
        _claimingPayouts = false;
    }

    private void OnPayoutsLoaded(Godot.Collections.Dictionary snapshot)
    {
        _claimingPayouts = false;
        if (snapshot == null || snapshot.Count == 0) return;

        int totalEarned = 0;

        foreach (var key in snapshot.Keys)
        {
            var payoutVar = (Variant)snapshot[key];
            if (payoutVar.VariantType != Variant.Type.Dictionary) continue;
            var payout = payoutVar.AsGodotDictionary();

            // Skip already-claimed payouts
            bool alreadyClaimed = payout.ContainsKey("claimed") &&
                ((Variant)payout["claimed"]).VariantType == Variant.Type.Bool &&
                ((Variant)payout["claimed"]).AsBool();
            if (alreadyClaimed) continue;

            int amount = payout.ContainsKey("amount") ? ParseInt((Variant)payout["amount"], 0) : 0;
            if (amount <= 0) continue;

            totalEarned += amount;

            // Mark as claimed so it can never be claimed again
            var payoutRef = (Node)_database.Call("get_once_database_reference",
                PayoutsPath + "/" + _myUid + "/" + key.ToString());
            payoutRef.Call("update", "", new Godot.Collections.Dictionary
            {
                { "claimed",   true                       },
                { "claimedAt", Time.GetUnixTimeFromSystem() }
            });
        }

        if (totalEarned <= 0) return;

        // Add earned coins to seller
        if (CosmeticManager.Instance != null)
        {
            CosmeticManager.Instance.Coins += totalEarned;
            DataChanged?.Invoke();

            GD.Print($"MarketplaceManager: claimed {totalEarned} coins from sales.");

            // Push updated coin total to Firebase
            EnsureFirebaseNodes();
            if (_database != null && IsLoggedIn())
            {
                var shopRef = (Node)_database.Call("get_once_database_reference",
                    PlayersPath + "/" + _myUid + "/shop");
                shopRef.Call("update", "", new Godot.Collections.Dictionary
                {
                    { "coins",     CosmeticManager.Instance.Coins },
                    { "updatedAt", Time.GetUnixTimeFromSystem() }
                });
            }
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private void RecordTrade(string itemId, int price)
    {
        if (!PriceHistory.ContainsKey(itemId))
            PriceHistory[itemId] = new List<TradeRecord>();

        PriceHistory[itemId].Add(new TradeRecord
        {
            Price     = price,
            Timestamp = Time.GetUnixTimeFromSystem()
        });

        if (PriceHistory[itemId].Count > 50)
            PriceHistory[itemId].RemoveAt(0);
    }

    private void PushHistoryToCloud(string itemId)
    {
        if (_database == null || !PriceHistory.ContainsKey(itemId)) return;

        var trades = new Godot.Collections.Array();
        foreach (var record in PriceHistory[itemId])
        {
            trades.Add(new Godot.Collections.Dictionary
            {
                { "price", record.Price      },
                { "ts",    record.Timestamp  }
            });
        }
        var refNode = (Node)_database.Call("get_once_database_reference",
            HistoryPath + "/" + itemId);
        refNode.Call("update", "", new Godot.Collections.Dictionary { { "trades", trades } });
    }

    public List<TradeRecord> GetPriceHistory(string itemId)
        => PriceHistory.TryGetValue(itemId, out var list) ? list : new List<TradeRecord>();

    // ─── Safe signal connect (ignore duplicate) ───────────────────────────────

    private void Connect(Node node, string signal, string method)
    {
        var c = new Callable(this, method);
        if (!node.IsConnected(signal, c))
            node.Connect(signal, c);
    }

    // ─── Cache ───────────────────────────────────────────────────────────────

    private void SaveCache()
    {
        var listingsArr = new Godot.Collections.Array();
        foreach (var l in Listings)
            listingsArr.Add(ListingToDict(l));

        var histDict = new Godot.Collections.Dictionary();
        foreach (var kv in PriceHistory)
        {
            var tradesArr = new Godot.Collections.Array();
            foreach (var t in kv.Value)
                tradesArr.Add(new Godot.Collections.Dictionary
                    { { "price", t.Price }, { "ts", t.Timestamp } });
            histDict[kv.Key] = new Godot.Collections.Dictionary { { "trades", tradesArr } };
        }

        using var file = FileAccess.Open(CachePath, FileAccess.ModeFlags.Write);
        if (file == null) return;
        file.StoreString(Json.Stringify(new Godot.Collections.Dictionary
        {
            { "listings",     listingsArr },
            { "priceHistory", histDict    }
        }));
    }

    private void LoadCache()
    {
        if (!FileAccess.FileExists(CachePath)) return;
        using var file = FileAccess.Open(CachePath, FileAccess.ModeFlags.Read);
        if (file == null) return;

        var parsed = Json.ParseString(file.GetAsText());
        if (parsed.VariantType != Variant.Type.Dictionary) return;
        var data = parsed.AsGodotDictionary();

        if (data.ContainsKey("listings"))
        {
            Listings.Clear();
            foreach (var item in ((Variant)data["listings"]).AsGodotArray())
            {
                var iv = (Variant)item;
                if (iv.VariantType != Variant.Type.Dictionary) continue;
                var d = iv.AsGodotDictionary();
                var id = d.ContainsKey("id") ? ((Variant)d["id"]).AsString() : "";
                var l = ParseListing(id, d);
                if (l != null && l.Quantity > 0) Listings.Add(l);
            }
        }

        if (data.ContainsKey("priceHistory"))
        {
            PriceHistory.Clear();
            var hv = (Variant)data["priceHistory"];
            if (hv.VariantType != Variant.Type.Dictionary)
            {
                DataChanged?.Invoke();
                return;
            }
            foreach (var itemKey in hv.AsGodotDictionary().Keys)
            {
                var itemId  = itemKey.ToString();
                var itemVar = (Variant)hv.AsGodotDictionary()[itemKey];
                if (itemVar.VariantType != Variant.Type.Dictionary) continue;
                var itemDict = itemVar.AsGodotDictionary();
                if (!itemDict.ContainsKey("trades")) continue;
                var tv = (Variant)itemDict["trades"];
                if (tv.VariantType != Variant.Type.Array) continue;

                var trades = new List<TradeRecord>();
                foreach (var t in tv.AsGodotArray())
                {
                    var tvar = (Variant)t;
                    if (tvar.VariantType != Variant.Type.Dictionary) continue;
                    var td = tvar.AsGodotDictionary();
                    trades.Add(new TradeRecord
                    {
                        Price     = td.ContainsKey("price") ? ParseInt((Variant)td["price"], 0) : 0,
                        Timestamp = td.ContainsKey("ts")    ? ParseDouble((Variant)td["ts"], 0) : 0,
                    });
                }
                PriceHistory[itemId] = trades;
            }
        }

        DataChanged?.Invoke();
    }

    // ─── Parse helpers ────────────────────────────────────────────────────────

    private static Listing ParseListing(string id, Godot.Collections.Dictionary d)
    {
        if (d == null) return null;
        return new Listing
        {
            Id          = id,
            ItemId      = d.ContainsKey("itemId")      ? ((Variant)d["itemId"]).AsString()       : "",
            ItemName    = d.ContainsKey("itemName")     ? ((Variant)d["itemName"]).AsString()     : "",
            SellerUid   = d.ContainsKey("sellerUid")   ? ((Variant)d["sellerUid"]).AsString()    : "",
            SellerEmail = d.ContainsKey("sellerEmail")  ? ((Variant)d["sellerEmail"]).AsString()  : "?",
            Price       = d.ContainsKey("price")        ? ParseInt((Variant)d["price"], 0)        : 0,
            Quantity    = d.ContainsKey("quantity")     ? ParseInt((Variant)d["quantity"], 0)     : 0,
            CreatedAt   = d.ContainsKey("createdAt")    ? ParseDouble((Variant)d["createdAt"], 0) : 0,
        };
    }

    private static Godot.Collections.Dictionary ListingToDict(Listing l) =>
        new Godot.Collections.Dictionary
        {
            { "id",          l.Id          },
            { "itemId",      l.ItemId      },
            { "itemName",    l.ItemName    },
            { "sellerUid",   l.SellerUid   },
            { "sellerEmail", l.SellerEmail },
            { "price",       l.Price       },
            { "quantity",    l.Quantity    },
            { "createdAt",   l.CreatedAt   }
        };

    private static int ParseInt(Variant v, int fallback) => v.VariantType switch
    {
        Variant.Type.Int    => (int)v.AsInt64(),
        Variant.Type.Float  => (int)v.AsDouble(),
        Variant.Type.String => int.TryParse(v.AsString(), out var p) ? p : fallback,
        _                   => fallback
    };

    private static double ParseDouble(Variant v, double fallback) => v.VariantType switch
    {
        Variant.Type.Int    => v.AsInt64(),
        Variant.Type.Float  => v.AsDouble(),
        Variant.Type.String => double.TryParse(v.AsString(), out var p) ? p : fallback,
        _                   => fallback
    };
}
