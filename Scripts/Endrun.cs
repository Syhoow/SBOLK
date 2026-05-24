using Godot;
using System.Collections.Generic;

public partial class Endrun : Control
{
    public enum UpgradeType { MaxHealth, Heal, BulletDamage, ArenaX, ArenaY, FloatingItemCount }

    private struct ShopOffer
    {
        public UpgradeType Type;
        public float Amount;
        public int Price;
        public string Label;
    }

    private float _displayedMoney = 0f;
    private float _targetMoney = 0f;
    private bool _moneyAnimating = false;

    // Shop buttons
    private Button button1, button2, button3;
    private Button continueButton;
    private Button rerollButton;
    private Button newGunButton;

    // Labels
    private RichTextLabel moneyLabel;
    private Label enemyCountLabel;
    private Label playerHealthLabel;
    private Label playerDamageLabel;
    private Label potCountLabel;
    private Label potHealLabel;
    private Label enemyHPLabel;
    private Label enemyDamageLabel;
    private Label goalLabel;
    private Label arenaXLabel;
    private Label arenaYLabel;

    private Texture2D pistolIcon;
    private Texture2D rifleIcon;
    private Texture2D smgIcon;

    // Shop state
    private int rerollCost = 4;
    private int rerollCostWave = 4;
    private bool[] sold = new bool[3];
    private ShopOffer[] currentOffers = new ShopOffer[3];

    // Gun upgrade state
    private int gunsOwned = 0;
    private int currentGunIndex = 0;
    private static readonly WeaponType[] GunProgression = {
        WeaponType.Pistol,
        WeaponType.Rifle,
        WeaponType.Smg
    };

    private static readonly UpgradeType[] AllUpgrades = {
        UpgradeType.MaxHealth,
        UpgradeType.Heal,
        UpgradeType.BulletDamage,
        UpgradeType.ArenaX,
        UpgradeType.ArenaY,
        UpgradeType.FloatingItemCount
    };

    public override void _Ready()
    {

        pistolIcon = GD.Load<Texture2D>("res://Assets/Pistolpixel.png");
        rifleIcon  = GD.Load<Texture2D>("res://Assets/shotgunpixel.png");
        smgIcon    = GD.Load<Texture2D>("res://Assets/smgpixel.png");
        // Shop buttons
        button1      = GetNode<Button>("Control/Button");
        button2      = GetNode<Button>("Control/Button2");
        button3      = GetNode<Button>("Control/Button3");
        continueButton = GetNode<Button>("Control/Button4");
        rerollButton = GetNode<Button>("Control/Reroll");
        newGunButton = GetNode<Button>("Control/NewGun");

        // Labels
        moneyLabel       = GetNode<RichTextLabel>("Control/Money");
        enemyCountLabel  = GetNode<Label>("EnemyCount");
        playerHealthLabel = GetNode<Label>("PlayerHealth");
        playerDamageLabel = GetNode<Label>("PlayerDamage");
        potCountLabel    = GetNode<Label>("PotCount");
        potHealLabel     = GetNode<Label>("HP");
        enemyHPLabel     = GetNode<Label>("EnemyHP");
        goalLabel        = GetNode<Label>("Goal");
        arenaXLabel      = GetNode<Label>("ArenaX");
        arenaYLabel      = GetNode<Label>("ArenaY");

        // Connect buttons
        button1.Pressed      += () => ApplyUpgrade(0);
        button2.Pressed      += () => ApplyUpgrade(1);
        button3.Pressed      += () => ApplyUpgrade(2);
        continueButton.Pressed += () => Continue();
        rerollButton.Pressed += Reroll;
        newGunButton.Pressed += BuyNewGun;
    }

    public override void _Process(double delta)
    {
        if (GameControl.Instance == null) return;

        if (_moneyAnimating)
        {
            float remaining = _targetMoney - _displayedMoney;
            float speed = Mathf.Max(1f, remaining * 5f);
            _displayedMoney = Mathf.MoveToward(_displayedMoney, _targetMoney, speed * (float)delta);
            if (Mathf.IsEqualApprox(_displayedMoney, _targetMoney))
                _moneyAnimating = false;
            moneyLabel.Text = $"${(int)_displayedMoney}";
        }
        else
        {
            moneyLabel.Text = $"${GameControl.Instance.money}";
        }
        enemyCountLabel.Text  = $"COUNT: {GameControl.Instance.WaveEnemyCount}";
        enemyHPLabel.Text     = $"HP: {GameControl.Instance.WaveEnemyHP}";
        goalLabel.Text        = $"{GameControl.Instance.WaveGoal}";
        arenaXLabel.Text      = $"X: {Arena.Instance?.ArenaWidth}";
        arenaYLabel.Text      = $"Y: {Arena.Instance?.ArenaHeight}";
        playerHealthLabel.Text = $"Max HP: {Player.Instance?.maxHealth}";
        playerDamageLabel.Text = $"DMG: {Bullet.Damage}";
        potCountLabel.Text    = $"Potions: {GameControl.Instance.MaxHealingPots}";
        potHealLabel.Text     = $"HP: {Player.Instance?.health}"; // change 30 to your actual pot heal amount
    }

    private ShopOffer GenerateOffer(UpgradeType type)
    {
        var counts = GameControl.Instance?.purchaseCounts;
        ShopOffer offer = new ShopOffer { Type = type };

        switch (type)
        {
            case UpgradeType.MaxHealth:
            {
                float[] amounts = { 10f, 25f, 50f };
                int[] prices    = {  30,  60, 120 };
                int idx = (int)GD.RandRange(0, amounts.Length - 1);
                string key = $"{type}_{amounts[idx]}";
                int timesBought = (counts != null && counts.ContainsKey(key)) ? counts[key] : 0;
                offer.Amount = amounts[idx];
                offer.Price  = (int)(prices[idx] * (1f + timesBought * 0.5f));
                offer.Label  = $"MAX HEALTH +{offer.Amount}  [${offer.Price}]";
                break;
            }
            case UpgradeType.Heal:
            {
                float[] amounts = { 20f, 50f, 100f };
                int[] prices    = {  20,  45,   90 };
                int idx = (int)GD.RandRange(0, amounts.Length - 1);
                string key = $"{type}_{amounts[idx]}";
                int timesBought = (counts != null && counts.ContainsKey(key)) ? counts[key] : 0;
                offer.Amount = amounts[idx];
                offer.Price  = (int)(prices[idx] * (1f + timesBought * 0.5f));
                offer.Label  = $"HEAL +{offer.Amount} HP  [${offer.Price}]";
                break;
            }
            case UpgradeType.BulletDamage:
            {
                float[] amounts = { 10f, 20f, 50f };
                int[] prices    = {  25,  50, 110 };
                int idx = (int)GD.RandRange(0, amounts.Length - 1);
                string key = $"{type}_{amounts[idx]}";
                int timesBought = (counts != null && counts.ContainsKey(key)) ? counts[key] : 0;
                offer.Amount = amounts[idx];
                offer.Price  = (int)(prices[idx] * (1f + timesBought * 0.5f));
                offer.Label  = $"BULLET DMG +{offer.Amount}  [${offer.Price}]";
                break;
            }
            case UpgradeType.ArenaX:
            {
                float[] amounts = { 1f, 2f, 5f };
                int[] prices    = { 20,  40,  75 };
                int idx = (int)GD.RandRange(0, amounts.Length - 1);
                string key = $"{type}_{amounts[idx]}";
                int timesBought = (counts != null && counts.ContainsKey(key)) ? counts[key] : 0;
                offer.Amount = amounts[idx];
                offer.Price  = (int)(prices[idx] * (1f + timesBought * 0.5f));
                offer.Label  = $"ARENA SIZE X +{offer.Amount}  [${offer.Price}]";
                break;
            }
            case UpgradeType.ArenaY:
            {
                float[] amounts = { 1f, 2f, 5f };
                int[] prices    = { 20,  40,  75 };
                int idx = (int)GD.RandRange(0, amounts.Length - 1);
                string key = $"{type}_{amounts[idx]}";
                int timesBought = (counts != null && counts.ContainsKey(key)) ? counts[key] : 0;
                offer.Amount = amounts[idx];
                offer.Price  = (int)(prices[idx] * (1f + timesBought * 0.5f));
                offer.Label  = $"ARENA SIZE Y +{offer.Amount}  [${offer.Price}]";
                break;
            }
            case UpgradeType.FloatingItemCount:
            {
                string key = $"{type}_1";
                int timesBought = (counts != null && counts.ContainsKey(key)) ? counts[key] : 0;
                offer.Amount = 1f;
                offer.Price  = (int)(GD.RandRange(20, 100) * (1f + timesBought * 0.5f));
                offer.Label  = $"POTION +1  [${offer.Price}]";
                break;
            }
        }

        return offer;
    }

    private void Reroll()
    {
        if (GameControl.Instance == null) return;
        if (GameControl.Instance.money < rerollCost) return;
        GameControl.Instance.money -= rerollCost;
        rerollCost += 5;
        rerollButton.Text = $"REROLL [${rerollCost}]";

        sold = new bool[3];

        var pool = new List<UpgradeType>(AllUpgrades);
        for (int i = 0; i < 3; i++)
        {
            int idx = (int)GD.RandRange(0, pool.Count - 1);
            currentOffers[i] = GenerateOffer(pool[idx]);
            pool.RemoveAt(idx);
        }
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        Button[] buttons = { button1, button2, button3 };
        for (int i = 0; i < 3; i++)
        {
            if (sold[i])
            {
                buttons[i].Text = "SOLD";
                buttons[i].Disabled = true;
            }
            else
            {
                buttons[i].Text = currentOffers[i].Label;
                buttons[i].Disabled = false;
            }
        }
    }

    private void ApplyUpgrade(int index)
    {
        ShopOffer offer = currentOffers[index];

        if (GameControl.Instance == null) return;
        if (GameControl.Instance.money < offer.Price) return;
        GameControl.Instance.money -= offer.Price;

        string key = $"{offer.Type}_{offer.Amount}";
        if (!GameControl.Instance.purchaseCounts.ContainsKey(key))
            GameControl.Instance.purchaseCounts[key] = 0;
        GameControl.Instance.purchaseCounts[key]++;

        switch (offer.Type)
        {
            case UpgradeType.MaxHealth:
                Player.Instance?.IncreaseMaxHealth(offer.Amount);
                break;
            case UpgradeType.Heal:
                Player.Instance?.Heal(offer.Amount);
                break;
            case UpgradeType.BulletDamage:
                Bullet.Damage += (int)offer.Amount;
                break;
            case UpgradeType.ArenaX:
                if (Arena.Instance != null)
                {
                    Arena.Instance.ArenaWidth += (int)offer.Amount;
                    Arena.Instance.Rebuild();
                }
                break;
            case UpgradeType.ArenaY:
                if (Arena.Instance != null)
                {
                    Arena.Instance.ArenaHeight += (int)offer.Amount;
                    Arena.Instance.Rebuild();
                }
                break;
            case UpgradeType.FloatingItemCount:
                if (GameControl.Instance != null)
                    GameControl.Instance.MaxHealingPots += 1;
                break;
        }

        sold[index] = true;
        UpdateButtons();
    }

    private void BuyNewGun()
{
    if (GameControl.Instance == null) return;
    if (gunsOwned >= GunProgression.Length - 1) return;

    int[] prices = { 500, 5000 }; // Rifle = 500, Smg = 1000
    int price = prices[gunsOwned];

    if (GameControl.Instance.money < price) return;
    GameControl.Instance.money -= price;

    currentGunIndex++;
    var gun = Player.Instance?.GetNode<Gun>("Gun");
    gun?.UpgradeWeapon(GunProgression[currentGunIndex]);
    gunsOwned++;

    UpdateGunButton();
}

    private void UpdateGunButton()
    {
        int nextIndex = currentGunIndex + 1;
        if (nextIndex >= GunProgression.Length)
        {
            newGunButton.Text = "MAX GUN";
            newGunButton.Disabled = true;
            newGunButton.Icon = null;
            return;
        }

        int[] prices = { 500, 5000 };
        string nextGunName = GunProgression[nextIndex].ToString().ToUpper();
        newGunButton.Text = $"UPGRADE: [${prices[gunsOwned]}]";
        newGunButton.Disabled = false;

        // set icon based on next gun
        newGunButton.Icon = GunProgression[nextIndex] switch
        {
            WeaponType.Rifle => rifleIcon,
            WeaponType.Smg   => smgIcon,
            WeaponType.Pistol => pistolIcon,
            _ => null
        };
    }

    private void Continue()
    {
        GameControl.Instance.goal = GameControl.Instance.WaveGoal;
        GameControl.Instance.goalBar.MaxValue = GameControl.Instance.goal;
        GameControl.Instance.money = (int)(GameControl.Instance.money * 0.5f);
        Visible = false;
        Player.Instance.GlobalPosition = Arena.Instance.ToGlobal(
            new Vector2(Arena.Instance.ArenaWidth * 25f / 2, (Arena.Instance.ArenaHeight - 1) * 25f / 2)
        );
        GameControl.Instance.isPlaying = true;
        GameControl.Instance.CallDeferred(nameof(GameControl.UnfreezeLayers));
        GameControl.Instance.CallDeferred(nameof(GameControl.QueueFreePortal));
        Input.MouseMode = Input.MouseModeEnum.Captured;
        Input.MouseMode = Input.MouseModeEnum.Confined;
    }

    public void OpenShop()
    {
        _displayedMoney = 0f;
        _targetMoney = GameControl.Instance != null ? GameControl.Instance.money : 0f;
        _moneyAnimating = true;
        rerollCostWave = (int)(rerollCostWave * 1.5f);
        sold = new bool[3];
        rerollCost = rerollCostWave;
        rerollButton.Text = $"REROLL [${rerollCost}]";
        UpdateGunButton();

        var pool = new List<UpgradeType>(AllUpgrades);
        for (int i = 0; i < 3; i++)
        {
            int idx = (int)GD.RandRange(0, pool.Count - 1);
            currentOffers[i] = GenerateOffer(pool[idx]);
            pool.RemoveAt(idx);
        }
        UpdateButtons();
        Visible = true;
    }
}