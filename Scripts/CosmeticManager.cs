using Godot;
using Godot.Collections;

public partial class CosmeticManager : Node
{
    public static CosmeticManager Instance;

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

    public override void _Ready()
    {
        Instance = this;
        GenerateOffers();
        // Call your database load here once connected
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
    }

    public bool Purchase(string id)
    {
        foreach (var c in AllCosmetics)
        {
            if (c.Id == id && !OwnedIds.Contains(id) && Coins >= c.Price)
            {
                Coins -= c.Price;
                OwnedIds.Add(id);
                // TODO: push to database
                return true;
            }
        }
        return false;
    }

    public void Equip(string id)
    {
        EquippedHat = id;
        // TODO: push to database
    }
}