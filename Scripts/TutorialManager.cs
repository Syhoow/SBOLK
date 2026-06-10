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
        "The Game Is Simple",
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
        "Survive waves of enemies, fill the Goal Bar, and enter the Portal to get stronger each wave.",
        "Use  W A S D  to move around the arena.\nKeep moving, standing still is deadly!",
        "Your weapon always aims at your mouse cursor.",
        "Right-click to DASH (you can't with 0 Goal Bar) and get brief invincibility.\nWarning: each dash costs Goal Bar progress. So don't spam it",
        "Enemies are spawning! Defeat them and collect their drops to fill the Goal Bar and grow your score.",
        "The bar at the top is the Goal Bar.\nFill it with enemy drops and a portal appears when it's full.",
        "Walk into the Portal to end the wave and open the Upgrade Shop.",
        "Spend some money ($) to upgrade before the next wave.\nUpgrades: Health, Damage, Heal, Weapons & more.\nREROLL for new offers. CONTINUE when ready.",
        "Each wave is harder so more HP, more damage, bigger Goal Bar.\nKeep upgrading to survive and compete for the leaderboard.",
        "Score converts to Coins when you die.\nSpend Coins in the SHOP for cosmetic hats. Good luck!"
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

    private static readonly TutStep[] PauseOnShowSteps = { TutStep.GoalBar };

    private void ShowStep(TutStep step)
    {
        _stepDone = false;

        if (step == TutStep.Done)
        {
            _panel.Visible = false;
            Engine.TimeScale = 1f;
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

        if (System.Array.IndexOf(PauseOnShowSteps, step) >= 0)
            Engine.TimeScale = 0f;
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
        if (System.Array.IndexOf(PauseOnShowSteps, _step) >= 0)
            Engine.TimeScale = 1f;
        _step += 1;
        ShowStep(_step);
    }
}
