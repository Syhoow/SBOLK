using Godot;
using Godot.Collections;
using System;

public partial class CosmeticManager : Node
{
    public static CosmeticManager Instance;
    public event Action DataChanged;

    public struct Cosmetic
    {
        public string Id;
        public string Name;
        public string Type;
        public int Price;
        public Texture2D Icon;
    }

    public Cosmetic[] AllCosmetics = new[]
    {
        new Cosmetic { Id = "hat_crown",  Name = "Crown",  Type = "hat", Price = 100 },
        new Cosmetic { Id = "hat_cap",    Name = "Cap",    Type = "hat", Price = 50  },
        new Cosmetic { Id = "hat_wizard", Name = "Wizard", Type = "hat", Price = 150 },
        new Cosmetic { Id = "hat_1",  Name = "hat1",  Type = "hat", Price = 100 },
        new Cosmetic { Id = "hat_2",    Name = "hat2",    Type = "hat", Price = 50  },
        new Cosmetic { Id = "hat_3", Name = "hat3", Type = "hat", Price = 150 },
        new Cosmetic { Id = "hat_4",  Name = "hat4",  Type = "hat", Price = 100 },
        new Cosmetic { Id = "hat_5",    Name = "hat5",    Type = "hat", Price = 50  },
        new Cosmetic { Id = "hat_6", Name = "hat6", Type = "hat", Price = 150 },
        new Cosmetic { Id = "hat_7",  Name = "hat7",  Type = "hat", Price = 100 },
        new Cosmetic { Id = "hat_8",    Name = "hat8",    Type = "hat", Price = 50  },
        new Cosmetic { Id = "hat_9", Name = "hat9", Type = "hat", Price = 150 },
    };

    public int Coins = 1000;
    public Array<string> OwnedIds = new();
    public string EquippedHat = "";

    // Offer system
    public Array<int> CurrentOfferIndices = new();
    public double OfferTimeRemaining = 0;
    public const double OfferDuration = 3600.0;
    private const int OfferCount = 3;

    private const string CachePathPrefix = "user://cosmetics_";
    private const string PlayerRootPath = "players";
    private const string ShopCatalogPath = "shop";

    private Node _firebase;
    private Node _auth;
    private Node _database;
    private bool _waitingForAuth;
    private bool _loadedFromCloud;
    private bool _dirty;
    private string _currentUid = "";
    private double _offerExpiresAt;

    public override void _Ready()
    {
        Instance = this;
        EnsureFirebaseNodes();
        if (IsLoggedIn())
        {
            UpdateCurrentUidFromAuth();
        }
        LoadCache();
        if (CurrentOfferIndices.Count == 0)
        {
            GenerateOffers();
        }

        TryLoadFromCloud();
    }

    public override void _Process(double delta)
    {
        if (OfferTimeRemaining > 0)
        {
            OfferTimeRemaining -= delta;
            if (OfferTimeRemaining <= 0)
                GenerateOffers();
        }
    }

    public void GenerateOffers()
    {
        CurrentOfferIndices.Clear();

        var indices = new System.Collections.Generic.List<int>();
        for (int i = 0; i < AllCosmetics.Length; i++)
            indices.Add(i);

        // Fisher-Yates shuffle
        for (int i = indices.Count - 1; i > 0; i--)
        {
            int j = (int)GD.RandRange(0, i);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        int count = Mathf.Min(OfferCount, indices.Count);
        for (int i = 0; i < count; i++)
            CurrentOfferIndices.Add(indices[i]);

        OfferTimeRemaining = OfferDuration;
        _offerExpiresAt = Time.GetUnixTimeFromSystem() + OfferDuration;
        QueueSave();
        DataChanged?.Invoke();
    }

    public string GetTimeRemainingText()
    {
        int hours = (int)(OfferTimeRemaining / 3600);
        int minutes = (int)(OfferTimeRemaining / 60) % 60;
        int seconds = (int)(OfferTimeRemaining % 60);
        return hours > 0 ? $"{hours}h {minutes}m" : $"{minutes}m {seconds}s";
    }

    public void SetData(int coins, Array<string> ownedIds, string equippedHat)
    {
        Coins = coins;
        OwnedIds = ownedIds;
        EquippedHat = equippedHat;
        QueueSave();
        DataChanged?.Invoke();
    }

    public bool Purchase(string id)
    {
        foreach (var c in AllCosmetics)
        {
            if (c.Id == id && !OwnedIds.Contains(id) && Coins >= c.Price)
            {
                Coins -= c.Price;
                OwnedIds.Add(id);
                QueueSave();
                DataChanged?.Invoke();
                return true;
            }
        }
        return false;
    }

    public void Equip(string id)
    {
        EquippedHat = id;
        QueueSave();
        DataChanged?.Invoke();
    }

    private void EnsureFirebaseNodes()
    {
        _firebase = GetNodeOrNull<Node>("/root/Firebase");
        _auth = _firebase?.GetNodeOrNull<Node>("Auth");
        _database = _firebase?.GetNodeOrNull<Node>("Database");

        if (_auth != null)
        {
            var callable = new Callable(this, nameof(OnLoggedOut));
            if (!_auth.IsConnected("logged_out", callable))
            {
                _auth.Connect("logged_out", callable);
            }
        }
    }

    private void TryLoadFromCloud()
    {
        if (_database == null || _auth == null)
        {
            return;
        }

        if (!IsLoggedIn())
        {
            RequestAuth();
            return;
        }

        var uid = UpdateCurrentUidFromAuth();
        if (string.IsNullOrEmpty(uid))
        {
            return;
        }
        LoadPlayerData(uid);
        LoadCatalog();
    }

    private bool IsLoggedIn()
    {
        if (_auth == null)
        {
            return false;
        }
        var isLoggedInVar = (Variant)_auth.Call("is_logged_in");
        return isLoggedInVar.VariantType == Variant.Type.Bool && isLoggedInVar.AsBool();
    }

    private void RequestAuth()
    {
        if (_waitingForAuth)
        {
            return;
        }

        _waitingForAuth = true;
        var callable = new Callable(this, nameof(OnAuthRequest));
        if (!_auth.IsConnected("auth_request", callable))
        {
            _auth.Connect("auth_request", callable);
        }

        var hasAuthFileVar = (Variant)_auth.Call("check_auth_file");
        var hasAuthFile = hasAuthFileVar.VariantType == Variant.Type.Bool && hasAuthFileVar.AsBool();
        if (!hasAuthFile)
        {
            _auth.Call("login_anonymous");
        }
    }

    private void OnAuthRequest(long resultCode, Variant _resultContent)
    {
        _waitingForAuth = false;
        if (resultCode == 1)
        {
            EnsureFirebaseNodes();
            UpdateCurrentUidFromAuth();
            LoadCache();
            TryLoadFromCloud();
            TrySaveToCloud();
        }
    }

    private string UpdateCurrentUidFromAuth()
    {
        var uid = GetUidFromAuth();
        if (string.IsNullOrEmpty(uid))
        {
            return "";
        }

        if (_currentUid != uid)
        {
            _currentUid = uid;
            ResetSessionState();
        }
        return _currentUid;
    }

    private void OnLoggedOut()
    {
        _currentUid = "";
        _dirty = false;
        _loadedFromCloud = false;
        ResetSessionState();
    }

    private void ResetSessionState()
    {
        Coins = 1000;
        OwnedIds = new Array<string>();
        EquippedHat = "";
        CurrentOfferIndices = new Array<int>();
        OfferTimeRemaining = 0;
        _offerExpiresAt = 0;
        DataChanged?.Invoke();
    }

    private string GetUidFromAuth()
    {
        if (_auth == null)
        {
            return "";
        }

        var authVar = _auth.Get("auth");
        if (authVar.VariantType != Variant.Type.Dictionary)
        {
            return "";
        }

        var authDict = (Dictionary)authVar;
        if (authDict.ContainsKey("localid"))
        {
            return ((Variant)authDict["localid"]).AsString();
        }
        if (authDict.ContainsKey("localId"))
        {
            return ((Variant)authDict["localId"]).AsString();
        }

        return "";
    }

    private void LoadPlayerData(string uid)
    {
        var reference = (Node)_database.Call("get_once_database_reference", PlayerRootPath);
        var successCallable = new Callable(this, nameof(OnPlayerDataLoaded));
        var failedCallable = new Callable(this, nameof(OnPlayerDataFailed));

        if (!reference.IsConnected("once_successful", successCallable))
        {
            reference.Connect("once_successful", successCallable);
        }
        if (!reference.IsConnected("once_failed", failedCallable))
        {
            reference.Connect("once_failed", failedCallable);
        }

        reference.Call("once", uid + "/shop");
    }

    private void LoadCatalog()
    {
        var reference = (Node)_database.Call("get_once_database_reference", ShopCatalogPath);
        var successCallable = new Callable(this, nameof(OnCatalogLoaded));
        var failedCallable = new Callable(this, nameof(OnCatalogFailed));

        if (!reference.IsConnected("once_successful", successCallable))
        {
            reference.Connect("once_successful", successCallable);
        }
        if (!reference.IsConnected("once_failed", failedCallable))
        {
            reference.Connect("once_failed", failedCallable);
        }

        reference.Call("once", "catalog");
    }

    private void OnPlayerDataLoaded(Dictionary snapshot)
    {
        _loadedFromCloud = true;

        if (snapshot != null && snapshot.Count > 0)
        {
            if (snapshot.ContainsKey("coins"))
            {
                Coins = ParseIntVariant((Variant)snapshot["coins"], Coins);
            }

            if (snapshot.ContainsKey("owned"))
            {
                OwnedIds = ParseStringArrayVariant((Variant)snapshot["owned"], OwnedIds);
            }

            if (snapshot.ContainsKey("equippedHat"))
            {
                var equippedVar = (Variant)snapshot["equippedHat"];
                EquippedHat = equippedVar.VariantType == Variant.Type.String ? equippedVar.AsString() : EquippedHat;
            }

            if (snapshot.ContainsKey("offerIndices"))
            {
                CurrentOfferIndices = ParseIntArrayVariant((Variant)snapshot["offerIndices"], CurrentOfferIndices);
            }

            if (snapshot.ContainsKey("offerExpiresAt"))
            {
                _offerExpiresAt = ParseDoubleVariant((Variant)snapshot["offerExpiresAt"], 0);
            }
        }

        ApplyOfferTimer();
        SaveCache();
        DataChanged?.Invoke();
    }

    private void OnPlayerDataFailed()
    {
        _loadedFromCloud = false;
    }

    private void OnCatalogLoaded(Dictionary snapshot)
    {
        if (snapshot == null || snapshot.Count == 0)
        {
            return;
        }

        var list = new System.Collections.Generic.List<Cosmetic>();
        foreach (var key in snapshot.Keys)
        {
            var entryVar = (Variant)snapshot[key];
            if (entryVar.VariantType != Variant.Type.Dictionary)
            {
                continue;
            }

            var entry = entryVar.AsGodotDictionary();
            var cosmetic = new Cosmetic
            {
                Id = key.ToString(),
                Name = entry.ContainsKey("name") ? ((Variant)entry["name"]).AsString() : key.ToString(),
                Type = entry.ContainsKey("type") ? ((Variant)entry["type"]).AsString() : "",
                Price = entry.ContainsKey("price") ? ParseIntVariant((Variant)entry["price"], 0) : 0,
                Icon = null
            };
            list.Add(cosmetic);
        }

        if (list.Count > 0)
        {
            AllCosmetics = list.ToArray();
            GenerateOffers();
            DataChanged?.Invoke();
        }
    }

    private void OnCatalogFailed()
    {
    }

    private void ApplyOfferTimer()
    {
        var now = Time.GetUnixTimeFromSystem();
        if (_offerExpiresAt > now && CurrentOfferIndices.Count > 0)
        {
            OfferTimeRemaining = _offerExpiresAt - now;
            return;
        }

        GenerateOffers();
    }

    private void QueueSave()
    {
        _dirty = true;
        SaveCache();
        TrySaveToCloud();
    }

    private void TrySaveToCloud()
    {
        if (!_dirty)
        {
            return;
        }

        EnsureFirebaseNodes();
        if (_database == null || _auth == null)
        {
            return;
        }

        if (!IsLoggedIn())
        {
            RequestAuth();
            return;
        }

        var uid = string.IsNullOrEmpty(_currentUid) ? GetUidFromAuth() : _currentUid;
        if (string.IsNullOrEmpty(uid))
        {
            return;
        }

        _currentUid = uid;

        var payload = new Dictionary
        {
            { "coins", Coins },
            { "owned", OwnedIds },
            { "equippedHat", EquippedHat },
            { "offerIndices", CurrentOfferIndices },
            { "offerExpiresAt", _offerExpiresAt },
            { "updatedAt", Time.GetUnixTimeFromSystem() }
        };

        var reference = (Node)_database.Call("get_once_database_reference", PlayerRootPath);
        reference.Call("update", uid + "/shop", payload);
        _dirty = false;
    }

    private void LoadCache()
    {
        if (!IsLoggedIn())
        {
            return;
        }

        var uid = UpdateCurrentUidFromAuth();
        if (string.IsNullOrEmpty(uid))
        {
            return;
        }
        var cachePath = GetCachePath(uid);
        if (!FileAccess.FileExists(cachePath))
        {
            return;
        }

        using var file = FileAccess.Open(cachePath, FileAccess.ModeFlags.Read);
        if (file == null)
        {
            return;
        }

        var text = file.GetAsText();
        var parsed = Json.ParseString(text);
        if (parsed.VariantType != Variant.Type.Dictionary)
        {
            return;
        }

        var data = parsed.AsGodotDictionary();
        if (data.ContainsKey("coins"))
        {
            Coins = ParseIntVariant((Variant)data["coins"], Coins);
        }
        if (data.ContainsKey("owned"))
        {
            OwnedIds = ParseStringArrayVariant((Variant)data["owned"], OwnedIds);
        }
        if (data.ContainsKey("equippedHat"))
        {
            EquippedHat = ((Variant)data["equippedHat"]).AsString();
        }
        if (data.ContainsKey("offerIndices"))
        {
            CurrentOfferIndices = ParseIntArrayVariant((Variant)data["offerIndices"], CurrentOfferIndices);
        }
        if (data.ContainsKey("offerExpiresAt"))
        {
            _offerExpiresAt = ParseDoubleVariant((Variant)data["offerExpiresAt"], 0);
        }

        ApplyOfferTimer();
        DataChanged?.Invoke();
    }

    private void SaveCache()
    {
        if (!IsLoggedIn())
        {
            return;
        }

        var uid = UpdateCurrentUidFromAuth();
        if (string.IsNullOrEmpty(uid))
        {
            return;
        }
        var cachePath = GetCachePath(uid);
        var data = new Dictionary
        {
            { "coins", Coins },
            { "owned", OwnedIds },
            { "equippedHat", EquippedHat },
            { "offerIndices", CurrentOfferIndices },
            { "offerExpiresAt", _offerExpiresAt },
            { "updatedAt", Time.GetUnixTimeFromSystem() }
        };

        using var file = FileAccess.Open(cachePath, FileAccess.ModeFlags.Write);
        if (file == null)
        {
            return;
        }

        file.StoreString(Json.Stringify(data));
    }

    private static string GetCachePath(string uid)
    {
        return CachePathPrefix + uid + ".json";
    }

    private static int ParseIntVariant(Variant value, int fallback)
    {
        switch (value.VariantType)
        {
            case Variant.Type.Int:
                return (int)value.AsInt64();
            case Variant.Type.Float:
                return (int)value.AsDouble();
            case Variant.Type.String:
                if (int.TryParse(value.AsString(), out var parsed))
                {
                    return parsed;
                }
                break;
        }
        return fallback;
    }

    private static double ParseDoubleVariant(Variant value, double fallback)
    {
        switch (value.VariantType)
        {
            case Variant.Type.Int:
                return value.AsInt64();
            case Variant.Type.Float:
                return value.AsDouble();
            case Variant.Type.String:
                if (double.TryParse(value.AsString(), out var parsed))
                {
                    return parsed;
                }
                break;
        }
        return fallback;
    }

    private static Array<string> ParseStringArrayVariant(Variant value, Array<string> fallback)
    {
        if (value.VariantType != Variant.Type.Array)
        {
            return fallback;
        }

        var result = new Array<string>();
        var array = value.AsGodotArray();
        foreach (var item in array)
        {
            var itemVar = (Variant)item;
            if (itemVar.VariantType != Variant.Type.Nil)
            {
                result.Add(itemVar.AsString());
            }
        }
        return result;
    }

    private static Array<int> ParseIntArrayVariant(Variant value, Array<int> fallback)
    {
        if (value.VariantType != Variant.Type.Array)
        {
            return fallback;
        }

        var result = new Array<int>();
        var array = value.AsGodotArray();
        foreach (var item in array)
        {
            var itemVar = (Variant)item;
            result.Add(ParseIntVariant(itemVar, 0));
        }
        return result;
    }
}