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
        public int Price;
        public int Quantity;
        public double CreatedAt;
    }

    public class TradeRecord
    {
        public int Price;
        public double Timestamp;
    }

    public event Action DataChanged;

    public List<Listing> Listings = new();
    public Dictionary<string, List<TradeRecord>> PriceHistory = new();

    private const string ListingsPath = "marketplace/listings";
    private const string HistoryPath = "marketplace/priceHistory";
    private const string CachePath = "user://marketplace_cache.json";

    private Node _firebase;
    private Node _auth;
    private Node _database;

    public override void _Ready()
    {
        Instance = this;
        EnsureFirebaseNodes();
        LoadCache();
        TryLoadFromCloud();
    }

    private void EnsureFirebaseNodes()
    {
        _firebase = GetNodeOrNull<Node>("/root/Firebase");
        _auth = _firebase?.GetNodeOrNull<Node>("Auth");
        _database = _firebase?.GetNodeOrNull<Node>("Database");
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
        var d = (Godot.Collections.Dictionary)authVar;
        if (d.ContainsKey("localid")) return ((Variant)d["localid"]).AsString();
        if (d.ContainsKey("localId")) return ((Variant)d["localId"]).AsString();
        return "";
    }

    private string GetCurrentEmail()
    {
        if (_auth == null) return "";
        var authVar = _auth.Get("auth");
        if (authVar.VariantType != Variant.Type.Dictionary) return "";
        var d = (Godot.Collections.Dictionary)authVar;
        if (d.ContainsKey("email")) return ((Variant)d["email"]).AsString();
        return "player";
    }

    public void TryLoadFromCloud()
    {
        EnsureFirebaseNodes();
        if (_database == null || !IsLoggedIn()) return;

        var refNode = (Node)_database.Call("get_once_database_reference", ListingsPath);
        var ok = new Callable(this, nameof(OnListingsLoaded));
        var fail = new Callable(this, nameof(OnListingsFailed));
        if (!refNode.IsConnected("once_successful", ok)) refNode.Connect("once_successful", ok);
        if (!refNode.IsConnected("once_failed", fail)) refNode.Connect("once_failed", fail);
        refNode.Call("once", "");

        var histRef = (Node)_database.Call("get_once_database_reference", HistoryPath);
        var histOk = new Callable(this, nameof(OnHistoryLoaded));
        var histFail = new Callable(this, nameof(OnHistoryFailed));
        if (!histRef.IsConnected("once_successful", histOk)) histRef.Connect("once_successful", histOk);
        if (!histRef.IsConnected("once_failed", histFail)) histRef.Connect("once_failed", histFail);
        histRef.Call("once", "");
    }

    private void OnListingsLoaded(Godot.Collections.Dictionary snapshot)
    {
        Listings.Clear();
        if (snapshot != null)
        {
            foreach (var key in snapshot.Keys)
            {
                var entryVar = (Variant)snapshot[key];
                if (entryVar.VariantType != Variant.Type.Dictionary) continue;
                var entry = entryVar.AsGodotDictionary();
                var listing = ParseListing(key.ToString(), entry);
                if (listing != null && listing.Quantity > 0)
                    Listings.Add(listing);
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
            var itemId = itemKey.ToString();
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
                    Price = td.ContainsKey("price") ? ParseInt((Variant)td["price"], 0) : 0,
                    Timestamp = td.ContainsKey("ts") ? ParseDouble((Variant)td["ts"], 0) : 0,
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

        var id = uid + "_" + itemId + "_" + (long)Time.GetUnixTimeFromSystem();
        var listing = new Listing
        {
            Id = id,
            ItemId = itemId,
            ItemName = itemName,
            SellerUid = uid,
            SellerEmail = GetCurrentEmail(),
            Price = price,
            Quantity = quantity,
            CreatedAt = Time.GetUnixTimeFromSystem()
        };

        CosmeticManager.Instance.OwnedCounts[itemId] =
            CosmeticManager.Instance.GetOwnedCount(itemId) - quantity;

        Listings.Add(listing);
        SaveCache();
        DataChanged?.Invoke();

        EnsureFirebaseNodes();
        if (_database != null && IsLoggedIn())
        {
            var payload = ListingToDict(listing);
            var refNode = (Node)_database.Call("get_once_database_reference", ListingsPath);
            refNode.Call("update", id, payload);
        }

        return true;
    }

    public bool BuyListing(string listingId)
    {
        if (CosmeticManager.Instance == null) return false;

        var listing = Listings.Find(l => l.Id == listingId);
        if (listing == null) return false;

        var uid = GetCurrentUid();
        if (listing.SellerUid == uid) return false;

        if (CosmeticManager.Instance.Coins < listing.Price) return false;
        if (listing.Quantity <= 0) return false;

        CosmeticManager.Instance.Coins -= listing.Price;
        var currentOwned = CosmeticManager.Instance.GetOwnedCount(listing.ItemId);
        CosmeticManager.Instance.OwnedCounts[listing.ItemId] = currentOwned + 1;

        listing.Quantity -= 1;
        if (listing.Quantity <= 0)
            Listings.Remove(listing);

        RecordTrade(listing.ItemId, listing.Price);
        SaveCache();
        DataChanged?.Invoke();

        EnsureFirebaseNodes();
        if (_database != null && IsLoggedIn())
        {
            if (listing.Quantity <= 0)
            {
                var refNode = (Node)_database.Call("get_once_database_reference", ListingsPath + "/" + listingId);
                refNode.Call("delete", "");
            }
            else
            {
                var refNode = (Node)_database.Call("get_once_database_reference", ListingsPath);
                var payload = new Godot.Collections.Dictionary { { "quantity", listing.Quantity } };
                refNode.Call("update", listingId + "/quantity", payload);
            }

            PushHistoryToCloud(listing.ItemId);
        }

        return true;
    }

    private void RecordTrade(string itemId, int price)
    {
        if (!PriceHistory.ContainsKey(itemId))
            PriceHistory[itemId] = new List<TradeRecord>();

        PriceHistory[itemId].Add(new TradeRecord
        {
            Price = price,
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
                { "price", record.Price },
                { "ts", record.Timestamp }
            });
        }
        var payload = new Godot.Collections.Dictionary { { "trades", trades } };
        var refNode = (Node)_database.Call("get_once_database_reference", HistoryPath);
        refNode.Call("update", itemId, payload);
    }

    public List<TradeRecord> GetPriceHistory(string itemId)
    {
        if (PriceHistory.ContainsKey(itemId))
            return PriceHistory[itemId];
        return new List<TradeRecord>();
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
                tradesArr.Add(new Godot.Collections.Dictionary { { "price", t.Price }, { "ts", t.Timestamp } });
            histDict[kv.Key] = new Godot.Collections.Dictionary { { "trades", tradesArr } };
        }

        var data = new Godot.Collections.Dictionary
        {
            { "listings", listingsArr },
            { "priceHistory", histDict }
        };

        using var file = FileAccess.Open(CachePath, FileAccess.ModeFlags.Write);
        if (file != null)
            file.StoreString(Json.Stringify(data));
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
            var arr = ((Variant)data["listings"]).AsGodotArray();
            foreach (var item in arr)
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
            var histVar = (Variant)data["priceHistory"];
            if (histVar.VariantType == Variant.Type.Dictionary)
            {
                var histDict = histVar.AsGodotDictionary();
                foreach (var itemKey in histDict.Keys)
                {
                    var itemId = itemKey.ToString();
                    var itemVar = (Variant)histDict[itemKey];
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
                            Price = td.ContainsKey("price") ? ParseInt((Variant)td["price"], 0) : 0,
                            Timestamp = td.ContainsKey("ts") ? ParseDouble((Variant)td["ts"], 0) : 0,
                        });
                    }
                    PriceHistory[itemId] = trades;
                }
            }
        }

        DataChanged?.Invoke();
    }

    private static Listing ParseListing(string id, Godot.Collections.Dictionary d)
    {
        if (d == null) return null;
        return new Listing
        {
            Id = id,
            ItemId = d.ContainsKey("itemId") ? ((Variant)d["itemId"]).AsString() : "",
            ItemName = d.ContainsKey("itemName") ? ((Variant)d["itemName"]).AsString() : "",
            SellerUid = d.ContainsKey("sellerUid") ? ((Variant)d["sellerUid"]).AsString() : "",
            SellerEmail = d.ContainsKey("sellerEmail") ? ((Variant)d["sellerEmail"]).AsString() : "?",
            Price = d.ContainsKey("price") ? ParseInt((Variant)d["price"], 0) : 0,
            Quantity = d.ContainsKey("quantity") ? ParseInt((Variant)d["quantity"], 0) : 0,
            CreatedAt = d.ContainsKey("createdAt") ? ParseDouble((Variant)d["createdAt"], 0) : 0,
        };
    }

    private static Godot.Collections.Dictionary ListingToDict(Listing l)
    {
        return new Godot.Collections.Dictionary
        {
            { "id", l.Id },
            { "itemId", l.ItemId },
            { "itemName", l.ItemName },
            { "sellerUid", l.SellerUid },
            { "sellerEmail", l.SellerEmail },
            { "price", l.Price },
            { "quantity", l.Quantity },
            { "createdAt", l.CreatedAt }
        };
    }

    private static int ParseInt(Variant v, int fallback)
    {
        return v.VariantType switch
        {
            Variant.Type.Int => (int)v.AsInt64(),
            Variant.Type.Float => (int)v.AsDouble(),
            Variant.Type.String => int.TryParse(v.AsString(), out var p) ? p : fallback,
            _ => fallback
        };
    }

    private static double ParseDouble(Variant v, double fallback)
    {
        return v.VariantType switch
        {
            Variant.Type.Int => v.AsInt64(),
            Variant.Type.Float => v.AsDouble(),
            Variant.Type.String => double.TryParse(v.AsString(), out var p) ? p : fallback,
            _ => fallback
        };
    }
}
