using Godot;
using System.Collections.Generic;
public partial class Endrun : Control
{
    public enum UpgradeType { MaxHealth, Heal, BulletDamage, Arena, FloatingItemCount }
    private Button button1;
    private Button button2;
    private Button button3;
    private Button rerollButton;
    private Button button4;
    private UpgradeType[] currentOffers = new UpgradeType[3];
    private static readonly UpgradeType[] AllUpgrades = {
        UpgradeType.MaxHealth,
        UpgradeType.Heal,
        UpgradeType.BulletDamage,
        UpgradeType.Arena,
        UpgradeType.FloatingItemCount
    };
    public override void _Ready()
    {
        // Adjust these paths to match your scene tree
        button1 = GetNode<Button>("Control/Button");
        button2 = GetNode<Button>("Control/Button2");
        button3 = GetNode<Button>("Control/Button3");
        button4 = GetNode<Button>("Control/Button4");
        GD.Print("button1: " + button1);
        GD.Print("button2: " + button2);
        GD.Print("button3: " + button3);
        GD.Print("button4: " + button4);
        rerollButton = GetNode<Button>("Control/Reroll"); // add a Reroll button in your scene
        button1.Pressed += () => ApplyUpgrade(0);
        button2.Pressed += () => ApplyUpgrade(1);
        button3.Pressed += () => ApplyUpgrade(2);
        button4.Pressed += () => Continue();
        rerollButton.Pressed += Reroll;
        Reroll();
        GD.Print("button1 text after reroll: " + button1?.Text);
    }
    private void Reroll()
    {
        var pool = new List<UpgradeType>(AllUpgrades);
        for (int i = 0; i < 3; i++)
        {
            int idx = (int)GD.RandRange(0, pool.Count - 1);
            currentOffers[i] = pool[idx];
            pool.RemoveAt(idx);
        }
        button1.Text = GetUpgradeLabel(currentOffers[0]);
        button2.Text = GetUpgradeLabel(currentOffers[1]);
        button3.Text = GetUpgradeLabel(currentOffers[2]);
    }
    private void ApplyUpgrade(int index)
    {
        switch (currentOffers[index])
        {
            case UpgradeType.MaxHealth:
                Player.Instance?.IncreaseMaxHealth(25f);
                break;
            case UpgradeType.Heal:
                Player.Instance?.Heal(50f);
                break;
            case UpgradeType.BulletDamage:
                Bullet.Damage += 10;
                break;
            case UpgradeType.Arena:
                if (Arena.Instance != null)
                {
                    Arena.Instance.ArenaWidth += 2;
                    Arena.Instance.ArenaHeight += 2;
                }
                break;
            case UpgradeType.FloatingItemCount:
                if (GameControl.Instance != null)
                    GameControl.Instance.MaxHealingPots += 1;
                break;
        }
        Visible = false;
    }
    private string GetUpgradeLabel(UpgradeType type) => type switch
    {
        UpgradeType.MaxHealth      => "MAX HEALTH +25",
        UpgradeType.Heal           => "HEAL +50 HP",
        UpgradeType.BulletDamage   => "BULLET DAMAGE +10",
        UpgradeType.Arena          => "ARENA SIZE UP",
        UpgradeType.FloatingItemCount => "FLOATING ITEM +1",
        _ => "???"
    };

    private void Continue()
    {
        if (GameControl.Instance != null)
            GameControl.Instance.DifficultyMultiplier *= 1.5f;

        foreach (Node enemy in GetTree().GetNodesInGroup("enemy"))
            enemy.QueueFree();

        Visible = false;
        GameControl.Instance.isPlaying = true;
        GameControl.Instance?.CallDeferred(nameof(GameControl.UnfreezeLayers));
        GameControl.Instance?.CallDeferred(nameof(GameControl.QueueFreePortal));
    }
}