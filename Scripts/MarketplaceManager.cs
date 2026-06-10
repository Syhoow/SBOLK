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
        public double ExpiresAt;   
    }

    public class TradeRecord
    {
        public int    Price;
        public double Timestamp;
        public string BuyerUid;
        public string SellerUid;
    }

    public string MyUid => _myUid;

    public event Action DataChanged;
    public event Action<bool, string> BuyCompleted;

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
    private float _pollTimer = 0f;
    private const float PollInterval = 30f;

    private bool   _buyInProgress      = false;
    private string _pendingBuyListingId = "";


    public override void _Ready()
    {
        Instance = this;
        EnsureFirebaseNodes();
        LoadCache();
        TryLoadFromCloud();
    }

    public override void _Process(double delta)
    {
        double nowUtc = DateTimeToUnix(DateTime.UtcNow);
        bool anyExpired = false;
        for (int i = Listings.Count - 1; i >= 0; i--)
        {
            if (Listings[i].ExpiresAt > 0 && nowUtc >= Listings[i].ExpiresAt)
            {
                Listings.RemoveAt(i);
                anyExpired = true;
            }
        }
        if (anyExpired)
        {
            SaveCache();
            DataChanged?.Invoke();
        }

        if (!IsLoggedIn()) return;
        _pollTimer += (float)delta;
        if (_pollTimer >= PollInterval)
        {
            _pollTimer = 0f;
            TryLoadFromCloud();
        }
    }

    public static double DateTimeToUnix(DateTime dt)
        => (dt.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;


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
        _buyInProgress = false;
        _pendingBuyListingId = "";
        Listings.Clear();
        PriceHistory.Clear();
        DataChanged?.Invoke();
    }


    public void TryLoadFromCloud()
    {
        EnsureFirebaseNodes();
        if (_database == null || _auth == null) return;

        if (!IsLoggedIn()) { RequestAuth(); return; }

        _myUid = GetCurrentUid();

        var listRef = (Node)_database.Call("get_once_database_reference", ListingsPath);
        Connect(listRef, "once_successful", nameof(OnListingsLoaded));
        Connect(listRef, "once_failed",     nameof(OnListingsFailed));
        listRef.Call("once", "");

        var histRef = (Node)_database.Call("get_once_database_reference", HistoryPath);
        Connect(histRef, "once_successful", nameof(OnHistoryLoaded));
        Connect(histRef, "once_failed",     nameof(OnHistoryFailed));
        histRef.Call("once", "");

        if (!string.IsNullOrEmpty(_myUid))
            ClaimPendingPayouts();
    }


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
                    Price     = td.ContainsKey("price")      ? ParseInt((Variant)td["price"], 0)            : 0,
                    Timestamp = td.ContainsKey("ts")         ? ParseDouble((Variant)td["ts"], 0)            : 0,
                    BuyerUid  = td.ContainsKey("buyer_uid")  ? ((Variant)td["buyer_uid"]).AsString()        : "",
                    SellerUid = td.ContainsKey("seller_uid") ? ((Variant)td["seller_uid"]).AsString()       : "",
                });
            }
            PriceHistory[itemId] = trades;
        }
        DataChanged?.Invoke();
    }

    private void OnHistoryFailed() { }


    public bool PostListing(string itemId, string itemName, int price, int quantity)
    {
        if (CosmeticManager.Instance == null) return false;
        if (CosmeticManager.Instance.GetOwnedCount(itemId) < quantity) return false;
        if (price <= 0 || quantity <= 0) return false;

        var uid = GetCurrentUid();
        if (string.IsNullOrEmpty(uid)) return false;

        double now = DateTimeToUnix(DateTime.UtcNow);
        var id = uid + "_" + itemId + "_" + (long)now;
        var listing = new Listing
        {
            Id          = id,
            ItemId      = itemId,
            ItemName    = itemName,
            SellerUid   = uid,
            SellerEmail = GetDisplayName(),
            Price       = price,
            Quantity    = quantity,
            CreatedAt   = now,
            ExpiresAt   = now + 86400   
        };

        CosmeticManager.Instance.OwnedCounts[itemId] =
            CosmeticManager.Instance.GetOwnedCount(itemId) - quantity;
        CosmeticManager.Instance.QueueSave();

        Listings.Add(listing);
        SaveCache();
        DataChanged?.Invoke();

        EnsureFirebaseNodes();
        if (_database != null && IsLoggedIn())
        {
            var refNode = (Node)_database.Call("get_once_database_reference", ListingsPath + "/" + id);
            refNode.Call("update", "", ListingToDict(listing));
        }

        return true;
    }

    public bool CancelListing(string listingId)
    {
        if (CosmeticManager.Instance == null) return false;

        var listing = Listings.Find(l => l.Id == listingId);
        if (listing == null) return false;

        var uid = GetCurrentUid();
        if (string.IsNullOrEmpty(uid)) return false;
        if (listing.SellerUid != uid) return false;  

        CosmeticManager.Instance.OwnedCounts[listing.ItemId] =
            CosmeticManager.Instance.GetOwnedCount(listing.ItemId) + listing.Quantity;
        CosmeticManager.Instance.QueueSave();

        Listings.Remove(listing);
        SaveCache();
        DataChanged?.Invoke();

        EnsureFirebaseNodes();
        if (_database != null && IsLoggedIn())
        {
            var listRef = (Node)_database.Call("get_once_database_reference",
                ListingsPath + "/" + listingId);
            listRef.Call("update", "", new Godot.Collections.Dictionary
            {
                { "quantity",  0    },
                { "sold",      true },
                { "cancelled", true }
            });
        }

        return true;
    }


    public bool BuyListing(string listingId)
    {
        if (_buyInProgress) return false;
        if (CosmeticManager.Instance == null) return false;

        var listing = Listings.Find(l => l.Id == listingId);
        if (listing == null) return false;

        var uid = GetCurrentUid();
        if (string.IsNullOrEmpty(uid)) return false;
        if (listing.SellerUid == uid) return false;
        if (CosmeticManager.Instance.Coins < listing.Price) return false;
        if (listing.Quantity <= 0) return false;

        EnsureFirebaseNodes();
        if (_database == null || !IsLoggedIn()) return false;

        _buyInProgress       = true;
        _pendingBuyListingId = listingId;

        var refNode = (Node)_database.Call("get_once_database_reference",
            ListingsPath + "/" + listingId);
        Connect(refNode, "once_successful", nameof(OnBuyListingVerified));
        Connect(refNode, "once_failed",     nameof(OnBuyListingVerifyFailed));
        refNode.Call("once", "");

        return true;
    }

    private void OnBuyListingVerifyFailed()
    {
        _buyInProgress       = false;
        _pendingBuyListingId = "";
        BuyCompleted?.Invoke(false, "Could not verify listing — please refresh and try again.");
    }

    private void OnBuyListingVerified(Godot.Collections.Dictionary snapshot)
    {
        string listingId     = _pendingBuyListingId;
        _pendingBuyListingId = "";
        _buyInProgress       = false;

        if (snapshot == null || snapshot.Count == 0)
        {
            RemoveStaleListingLocally(listingId);
            BuyCompleted?.Invoke(false, "This listing no longer exists. The market has been refreshed.");
            TryLoadFromCloud();
            return;
        }

        bool sold = snapshot.ContainsKey("sold")
            && ((Variant)snapshot["sold"]).VariantType == Variant.Type.Bool
            && ((Variant)snapshot["sold"]).AsBool();

        bool cancelled = snapshot.ContainsKey("cancelled")
            && ((Variant)snapshot["cancelled"]).VariantType == Variant.Type.Bool
            && ((Variant)snapshot["cancelled"]).AsBool();

        int cloudQty = snapshot.ContainsKey("quantity")
            ? ParseInt((Variant)snapshot["quantity"], 0)
            : 0;

        if (sold || cancelled || cloudQty <= 0)
        {
            RemoveStaleListingLocally(listingId);
            BuyCompleted?.Invoke(false, "This listing was already cancelled or sold. The market has been refreshed.");
            TryLoadFromCloud();
            return;
        }

        var listing = Listings.Find(l => l.Id == listingId);
        if (listing == null)
        {
            BuyCompleted?.Invoke(false, "Listing not found — please refresh.");
            return;
        }

        var uid = GetCurrentUid();
        if (string.IsNullOrEmpty(uid))          { BuyCompleted?.Invoke(false, "Not logged in.");           return; }
        if (listing.SellerUid == uid)            { BuyCompleted?.Invoke(false, "Cannot buy your own listing."); return; }
        if (CosmeticManager.Instance == null)   { BuyCompleted?.Invoke(false, "Inventory unavailable.");    return; }
        if (CosmeticManager.Instance.Coins < listing.Price)
        {
            BuyCompleted?.Invoke(false, "Not enough coins.");
            return;
        }

        int newQty = listing.Quantity - 1;
        EnsureFirebaseNodes();
        if (_database != null && IsLoggedIn())
        {
            var listRef = (Node)_database.Call("get_once_database_reference",
                ListingsPath + "/" + listingId);
            listRef.Call("update", "", new Godot.Collections.Dictionary
            {
                { "quantity", newQty          },
                { "sold",     newQty <= 0     }
            });
        }

        CosmeticManager.Instance.Coins -= listing.Price;
        CosmeticManager.Instance.OwnedCounts[listing.ItemId] =
            CosmeticManager.Instance.GetOwnedCount(listing.ItemId) + 1;
        CosmeticManager.Instance.QueueSave();

        listing.Quantity = newQty;
        if (newQty <= 0)
            Listings.Remove(listing);

        RecordTrade(listing.ItemId, listing.Price, uid, listing.SellerUid);
        SaveCache();
        DataChanged?.Invoke();

        if (_database != null && IsLoggedIn())
        {
            var payoutId  = uid + "_" + (long)Time.GetUnixTimeFromSystem();
            var payoutRef = (Node)_database.Call("get_once_database_reference",
                PayoutsPath + "/" + listing.SellerUid + "/" + payoutId);
            payoutRef.Call("update", "", new Godot.Collections.Dictionary
            {
                { "amount",    listing.Price                },
                { "itemId",    listing.ItemId               },
                { "itemName",  listing.ItemName             },
                { "buyerUid",  uid                          },
                { "timestamp", Time.GetUnixTimeFromSystem() }
            });

            PushHistoryToCloud(listing.ItemId);
        }

        BuyCompleted?.Invoke(true, "Purchased!");
    }

    private void RemoveStaleListingLocally(string listingId)
    {
        var stale = Listings.Find(l => l.Id == listingId);
        if (stale == null) return;
        Listings.Remove(stale);
        SaveCache();
        DataChanged?.Invoke();
    }

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

            bool alreadyClaimed = payout.ContainsKey("claimed") &&
                ((Variant)payout["claimed"]).VariantType == Variant.Type.Bool &&
                ((Variant)payout["claimed"]).AsBool();
            if (alreadyClaimed) continue;

            int amount = payout.ContainsKey("amount") ? ParseInt((Variant)payout["amount"], 0) : 0;
            if (amount <= 0) continue;

            totalEarned += amount;

            var payoutRef = (Node)_database.Call("get_once_database_reference",
                PayoutsPath + "/" + _myUid + "/" + key.ToString());
            payoutRef.Call("update", "", new Godot.Collections.Dictionary
            {
                { "claimed",   true                        },
                { "claimedAt", Time.GetUnixTimeFromSystem() }
            });
        }

        if (totalEarned <= 0) return;

        if (CosmeticManager.Instance != null)
        {
            CosmeticManager.Instance.Coins += totalEarned;
            DataChanged?.Invoke();

            GD.Print($"MarketplaceManager: claimed {totalEarned} coins from sales.");

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

    private void RecordTrade(string itemId, int price, string buyerUid = "", string sellerUid = "")
    {
        if (!PriceHistory.ContainsKey(itemId))
            PriceHistory[itemId] = new List<TradeRecord>();

        PriceHistory[itemId].Add(new TradeRecord
        {
            Price      = price,
            Timestamp  = Time.GetUnixTimeFromSystem(),
            BuyerUid   = buyerUid,
            SellerUid  = sellerUid
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
                { "price",       record.Price      },
                { "ts",          record.Timestamp  },
                { "buyer_uid",   record.BuyerUid  ?? "" },
                { "seller_uid",  record.SellerUid ?? "" }
            });
        }
        var refNode = (Node)_database.Call("get_once_database_reference",
            HistoryPath + "/" + itemId);
        refNode.Call("update", "", new Godot.Collections.Dictionary { { "trades", trades } });
    }

    public List<TradeRecord> GetPriceHistory(string itemId)
        => PriceHistory.TryGetValue(itemId, out var list) ? list : new List<TradeRecord>();

    private void Connect(Node node, string signal, string method)
    {
        var c = new Callable(this, method);
        if (!node.IsConnected(signal, c))
            node.Connect(signal, c);
    }


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
                {
                    { "price",      t.Price          },
                    { "ts",         t.Timestamp      },
                    { "buyer_uid",  t.BuyerUid  ?? "" },
                    { "seller_uid", t.SellerUid ?? "" }
                });
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
                        Price     = td.ContainsKey("price")      ? ParseInt((Variant)td["price"], 0)      : 0,
                        Timestamp = td.ContainsKey("ts")         ? ParseDouble((Variant)td["ts"], 0)      : 0,
                        BuyerUid  = td.ContainsKey("buyer_uid")  ? ((Variant)td["buyer_uid"]).AsString()  : "",
                        SellerUid = td.ContainsKey("seller_uid") ? ((Variant)td["seller_uid"]).AsString() : "",
                    });
                }
                PriceHistory[itemId] = trades;
            }
        }

        DataChanged?.Invoke();
    }


    private static Listing ParseListing(string id, Godot.Collections.Dictionary d)
    {
        if (d == null) return null;
        double createdAt = d.ContainsKey("createdAt") ? ParseDouble((Variant)d["createdAt"], 0) : 0;
        double expiresAt = d.ContainsKey("expiresAt") ? ParseDouble((Variant)d["expiresAt"], 0)
                                                       : (createdAt > 0 ? createdAt + 86400 : 0);
        return new Listing
        {
            Id          = id,
            ItemId      = d.ContainsKey("itemId")     ? ((Variant)d["itemId"]).AsString()      : "",
            ItemName    = d.ContainsKey("itemName")    ? ((Variant)d["itemName"]).AsString()    : "",
            SellerUid   = d.ContainsKey("sellerUid")  ? ((Variant)d["sellerUid"]).AsString()   : "",
            SellerEmail = d.ContainsKey("sellerEmail") ? ((Variant)d["sellerEmail"]).AsString() : "?",
            Price       = d.ContainsKey("price")       ? ParseInt((Variant)d["price"], 0)       : 0,
            Quantity    = d.ContainsKey("quantity")    ? ParseInt((Variant)d["quantity"], 0)    : 0,
            CreatedAt   = createdAt,
            ExpiresAt   = expiresAt,
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
            { "createdAt",   l.CreatedAt   },
            { "expiresAt",   l.ExpiresAt   }
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
