using Godot;
using Godot.Collections;
using System;

public partial class CosmeticManager : Node
{
    public static CosmeticManager Instance;
    public event Action DataChanged;

    public enum Rarity { Common, Rare, Legendary }

    public struct Cosmetic
    {
        public string Id;
        public string Name;
        public string Type;
        public int Price;
        public Rarity Rarity;
        public Texture2D Icon;
    }

    public Cosmetic[] AllCosmetics = new[]
    {
        new Cosmetic { Id = "hat_crown",  Name = "Crown of Eternity", Type = "hat", Price = 50,  Rarity = Rarity.Legendary },
        new Cosmetic { Id = "hat_cap",    Name = "Cap",               Type = "hat", Price = 75,  Rarity = Rarity.Rare },
        new Cosmetic { Id = "hat_wizard", Name = "Wizard Hat",        Type = "hat", Price = 100, Rarity = Rarity.Rare },
        new Cosmetic { Id = "hat_1",  Name = "Red Top Hat",   Type = "hat", Price = 50, Rarity = Rarity.Common },
        new Cosmetic { Id = "hat_2",  Name = "Green Cap",     Type = "hat", Price = 50, Rarity = Rarity.Common },
        new Cosmetic { Id = "hat_3",  Name = "Orange Wizard", Type = "hat", Price = 50, Rarity = Rarity.Common },
        new Cosmetic { Id = "hat_4",  Name = "Teal Top Hat",  Type = "hat", Price = 50, Rarity = Rarity.Common },
        new Cosmetic { Id = "hat_5",  Name = "Pink Crown",    Type = "hat", Price = 50, Rarity = Rarity.Common },
        new Cosmetic { Id = "hat_6",  Name = "Lime Cap",      Type = "hat", Price = 50, Rarity = Rarity.Common },
        new Cosmetic { Id = "hat_7",  Name = "Gold Wizard",   Type = "hat", Price = 50, Rarity = Rarity.Common },
        new Cosmetic { Id = "hat_8",  Name = "Blue Top Hat",  Type = "hat", Price = 50, Rarity = Rarity.Common },
        new Cosmetic { Id = "hat_9",  Name = "Purple Crown",  Type = "hat", Price = 50, Rarity = Rarity.Common },
    };

    public const int MaxStack = 10;

    public int Coins = 1000;
    public Dictionary OwnedCounts = new();
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
        // Drive timer from computer clock so it counts down correctly even when offline
        double nowUtc = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        if (_offerExpiresAt > 0)
        {
            OfferTimeRemaining = _offerExpiresAt - nowUtc;
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

    public void SetData(int coins, Dictionary ownedCounts, string equippedHat)
    {
        Coins = coins;
        OwnedCounts = ownedCounts;
        EquippedHat = equippedHat;
        QueueSave();
        DataChanged?.Invoke();
    }

    public bool Purchase(string id)
    {
        var cosmetic = FindCosmetic(id);
        if (cosmetic == null) return false;

        var currentCount = GetOwnedCount(id);
        if (currentCount >= MaxStack) return false;

        if (Coins < cosmetic.Value.Price) return false;
        Coins -= cosmetic.Value.Price;

        OwnedCounts[id] = currentCount + 1;
        QueueSave();
        DataChanged?.Invoke();
        return true;
    }

    public void Equip(string id)
    {
        if (GetOwnedCount(id) <= 0)
        {
            return;
        }
        EquippedHat = id;
        QueueSave();
        DataChanged?.Invoke();
    }

    public void Unequip(string id)
    {
        if (EquippedHat != id)
        {
            return;
        }
        EquippedHat = "";
        QueueSave();
        DataChanged?.Invoke();
    }

    public int GetOwnedCount(string id)
    {
        if (OwnedCounts.ContainsKey(id))
        {
            return ParseIntVariant((Variant)OwnedCounts[id], 0);
        }
        return 0;
    }

    private Cosmetic? FindCosmetic(string id)
    {
        foreach (var c in AllCosmetics)
        {
            if (c.Id == id)
            {
                return c;
            }
        }
        return null;
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
        OwnedCounts = new Dictionary();
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

            if (snapshot.ContainsKey("ownedCounts"))
            {
                OwnedCounts = ParseOwnedCountsVariant((Variant)snapshot["ownedCounts"], OwnedCounts);
            }
            else if (snapshot.ContainsKey("owned"))
            {
                OwnedCounts = ConvertLegacyOwned(ParseStringArrayVariant((Variant)snapshot["owned"], new Array<string>()));
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
            ApplyOfferTimer();
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

    public void QueueSave()
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
            { "ownedCounts", OwnedCounts },
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
        if (data.ContainsKey("ownedCounts"))
        {
            OwnedCounts = ParseOwnedCountsVariant((Variant)data["ownedCounts"], OwnedCounts);
        }
        else if (data.ContainsKey("owned"))
        {
            OwnedCounts = ConvertLegacyOwned(ParseStringArrayVariant((Variant)data["owned"], new Array<string>()));
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

        var uid = _currentUid;
        if (string.IsNullOrEmpty(uid))
        {
            uid = GetUidFromAuth();
            if (!string.IsNullOrEmpty(uid))
                _currentUid = uid;
        }
        if (string.IsNullOrEmpty(uid))
        {
            return;
        }
        var cachePath = GetCachePath(uid);
        var data = new Dictionary
        {
            { "coins", Coins },
            { "ownedCounts", OwnedCounts },
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

    // Returns the per-item icon if it exists in Assets/Icons/{itemId}.png,
    // otherwise generates a unique colored placeholder so every item looks distinct.
    public static Texture2D LoadItemIcon(string itemId)
    {
        if (!string.IsNullOrEmpty(itemId))
        {
            var path = $"res://Assets/Icons/{itemId}.png";
            if (ResourceLoader.Exists(path))
                return GD.Load<Texture2D>(path);
        }
        return MakePlaceholderIcon(itemId);
    }

    private static Texture2D MakePlaceholderIcon(string itemId)
    {
        int hash = string.IsNullOrEmpty(itemId) ? 12345 : Math.Abs(itemId.GetHashCode());
        float r = (hash * 13 % 200 + 55) / 255f;
        float g = (hash * 29 % 200 + 55) / 255f;
        float b = (hash * 47 % 200 + 55) / 255f;
        var img = Image.Create(32, 32, false, Image.Format.Rgba8);
        img.Fill(new Color(r, g, b, 1f));
        return ImageTexture.CreateFromImage(img);
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

    private static Dictionary ParseOwnedCountsVariant(Variant value, Dictionary fallback)
    {
        if (value.VariantType != Variant.Type.Dictionary)
        {
            return fallback;
        }

        var result = new Dictionary();
        var dict = value.AsGodotDictionary();
        foreach (var key in dict.Keys)
        {
            var keyStr = key.ToString();
            if (string.IsNullOrEmpty(keyStr))
            {
                continue;
            }

            var count = ParseIntVariant((Variant)dict[key], 0);
            if (count <= 0)
            {
                continue;
            }
            if (count > MaxStack)
            {
                count = MaxStack;
            }

            result[keyStr] = count;
        }

        return result;
    }

    private static Dictionary ConvertLegacyOwned(Array<string> ownedIds)
    {
        var result = new Dictionary();
        foreach (var id in ownedIds)
        {
            if (string.IsNullOrEmpty(id))
            {
                continue;
            }

            var current = 0;
            if (result.ContainsKey(id))
            {
                current = ParseIntVariant((Variant)result[id], 0);
            }

            current += 1;
            if (current > MaxStack)
            {
                current = MaxStack;
            }
            result[id] = current;
        }

        return result;
    }
}