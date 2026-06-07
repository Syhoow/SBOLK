using Godot;

public partial class TutorialManager : CanvasLayer
{
    public static TutorialManager Instance;

    private enum TutStep
    {
        Welcome = 0,
        Movement,
        AimShoot,
        Dash,
        EnemiesDrops,
        GoalBar,
        Portal,
        UpgradeShop,
        NextWaves,
        CoinsScore,
        Done
    }

    private TutStep _step = TutStep.Welcome;
    private bool _stepDone = false;

    public bool EnemiesAllowed => _step >= TutStep.EnemiesDrops;

    private const int TotalSteps = 10;

    private PanelContainer _panel;
    private Label _stepCounter;
    private Label _titleLabel;
    private Label _bodyLabel;
    private Label _actionHintLabel;
    private Button _nextButton;

    private static readonly string[] StepTitles = {
        "Welcome, Rookie!",
        "Movement",
        "Aim & Shoot",
        "Dash",
        "Enemies",
        "The Goal Bar",
        "The Portal",
        "Upgrade Shop",
        "Endless Waves",
        "Score & Coins",
    };

    private static readonly string[] StepBodies = {
        "This is a top-down arena shooter.\n\nEnemies spawn from all directions and chase you down. Your goal is to defeat them, collect drops to fill the Goal Bar, and survive the portal runs to get stronger each wave.",
        "Use  W A S D  (or arrow keys) to move around the arena.\n\nStay mobile — standing still makes you a very easy target!",
        "Your weapon always aims toward your mouse cursor.",
        "Right-click to DASH in your movement direction.\n\nWhile dashing you are briefly invisible and invincible — perfect for dodging attacks or closing in on enemies.\n\nWarning: Each dash costs Goal Points from your progress bar, so don't spam it carelessly!",
        "Enemies are now spawning! Defeat them and collect their drops.\n\nEach drop gives you points toward filling the Goal Bar, and your score increases with every drop collected.",
        "See the progress bar at the top of the screen? That is the Goal Bar.\n\nFill it by collecting enemy drops. Once it is completely full, a glowing Portal will appear somewhere inside the arena.\n\n↑  Watch the Goal Bar at the top of the screen",
        "When the Goal Bar is full, a Portal spawns on the map.\n\nWalk into it to end the wave and open the Upgrade Shop. The portal disappears as soon as you enter — don't miss it!\n\nFill the Goal Bar and step through the Portal to continue...",
        "Spend your Gold ($) here to power up for the next wave!\n\nAvailable upgrades:\n  •  Max Health — survive more hits\n  •  Heal — restore HP right now\n  •  Bullet Damage — kill enemies faster\n  •  Arena Size X / Y — expand your play area\n  •  Potions — extra heal drops on the map\n  •  New Weapon — Pistol  →  Rifle  →  SMG\n\nPress REROLL for different offers (costs Gold).\nPress CONTINUE when you are ready for the next wave.",
        "Each new wave is harder than the last.\n\nEnemies gain more HP, deal more damage, and the Goal Bar target increases. You must keep upgrading every wave to stay competitive.\n\nYour score grows with every drop collected — compete for the top spot on the global leaderboard!",
        "When you die your run ends and your score converts to permanent Coins:\n\n  100 points  =  10 coins\n\nCoins carry across all runs. Spend them in the SHOP on the Title Screen to unlock cosmetic hats for your character.\n\nGood luck out there — stay mobile, aim true, and survive!"
    };

    private static readonly string[] StepActionHints = {
        "",
        "→  Try moving now to continue...",
        "",
        "",
        "→  Pick up an enemy drop to continue...",
        "",
        "→  Fill the Goal Bar and enter the Portal to continue...",
        "",
        "",
        ""
    };

    private static readonly string[] StepButtonTexts = {
        "NEXT  →",
        "",
        "NEXT  →",
        "NEXT  →",
        "",
        "NEXT  →",
        "",
        "NEXT  →",
        "NEXT  →",
        "GOT IT!  ✓"
    };

    public override void _Ready()
    {
        Instance = this;

        _panel           = GetNode<PanelContainer>("Panel");
        _stepCounter     = GetNode<Label>("Panel/VBox/TopRow/StepCounter");
        _titleLabel      = GetNode<Label>("Panel/VBox/TitleLabel");
        _bodyLabel       = GetNode<Label>("Panel/VBox/BodyLabel");
        _actionHintLabel = GetNode<Label>("Panel/VBox/ActionHintLabel");
        _nextButton      = GetNode<Button>("Panel/VBox/BottomRow/NextButton");
        _nextButton.Pressed += OnNextPressed;

        ShowStep(_step);
    }

    public override void _Process(double delta)
    {
        if (_step == TutStep.Done || _stepDone) return;
        CheckAutoAdvance();
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }

    private void ShowStep(TutStep step)
    {
        _stepDone = false;

        if (step == TutStep.Done)
        {
            _panel.Visible = false;
            return;
        }

        int i = (int)step;
        _panel.Visible = true;
        _stepCounter.Text    = $"TUTORIAL  •  Step {i + 1} of {TotalSteps}";
        _titleLabel.Text     = StepTitles[i];
        _bodyLabel.Text      = StepBodies[i];

        string hint = StepActionHints[i];
        _actionHintLabel.Text    = hint;
        _actionHintLabel.Visible = !string.IsNullOrEmpty(hint);

        string btnText = StepButtonTexts[i];
        _nextButton.Text    = btnText;
        _nextButton.Visible = !string.IsNullOrEmpty(btnText);
    }

    private void CheckAutoAdvance()
    {
        switch (_step)
        {
            case TutStep.Movement:
                if (Player.Instance != null && Player.Instance.Velocity.Length() > 25f)
                    Advance();
                break;

            case TutStep.EnemiesDrops:
                if (GameControl.Instance != null && GameControl.Instance.score > 0)
                    Advance();
                break;

            case TutStep.Portal:
                if (GameControl.Instance != null && GameControl.Instance.goalReached)
                    _actionHintLabel.Text = "→  Portal has spawned! Walk into it now...";
                break;
        }
    }

    public void OnShopOpened()
    {
        if (_step <= TutStep.Portal)
        {
            _step = TutStep.UpgradeShop;
            ShowStep(_step);
        }
    }

    private void OnNextPressed()
    {
        Advance();
    }

    private void Advance()
    {
        if (_stepDone) return;
        _stepDone = true;
        _step += 1;
        ShowStep(_step);
    }
}
