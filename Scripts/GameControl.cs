using Godot;
using System.Collections.Generic;
public partial class GameControl : Node2D
{
    public static GameControl Instance;
    public static string CurrentUserEmail = "";
    [Export] public PackedScene EnemyScene;
    [Export] public PackedScene Marker;
    [Export] public PackedScene Portal;
    [Export] public PackedScene HealingPotScene;
    [Export] public float HealingPotInterval = 15f;
    [Export] private float healingPotTimer = 0f;
    [Export] public int MaxHealingPots = 2;
    [Export] public float SpawnInterval = 0.5f;
    [Export] public float MarkerWarningTime = 1.5f; // how long marker shows before enemy spawns
    [Export] public int MaxEnemies = 5;
    public int goal = 300;
    public int currentGoal = 0;
    private CharacterBody2D player;
    private Arena arena;
    public ProgressBar goalBar;
    public Label GoalLabel;
    public Label HealthLabel;
    public Label ScoreLabel;
    public CanvasLayer enemyLayer;
    public CanvasLayer arenaLayer;
    public CanvasLayer markerLayer;
    public CanvasLayer hudLayer;
    public CanvasLayer paused;
    public ColorRect gameover;
    public Button record;
    public Label CoinsEarnedLabel;
    public static bool IsTutorialMode = false;
    public bool isPlaying = true;
    public bool goalReached = false;
    private float spawnTimer = 0f;
    public float DifficultyMultiplier = 1f;
    public float EnemyMultiplier = 1f;
    public int WaveEnemyCount => (int)(5 * EnemyMultiplier);
    public int WaveEnemyHP => (int)(100f * EnemyMultiplier);
    public int WaveGoal => (int)(100f * DifficultyMultiplier);
    public int currentWave = 0;
    private int _activeEnemies = 0;
    public int money = 0;
    public int score = 0;
    public Dictionary<string, int> purchaseCounts = new Dictionary<string, int>();
    private bool _scoreSubmitted = false;
    private bool _waitingForScoreAuth = false;
    private bool _waitingForUserdata = false;
    private string _pendingScoreUid = "";
    private string _cachedEmail = "";
    private string _cachedUid = "";
    private int _pendingScoreValue = 0;
    private bool _waitingForBestScore = false;
    private string _pendingBestUid = "";
    private string _pendingBestEmail = "";
    private int _pendingBestScore = 0;
    private Node _pendingBestDatabase;
    
    public override void _Ready()
    {
        Instance = this;
        player = GetNode<CharacterBody2D>("/root/Main/EnemyLayer/Player");
        arena = GetNode<Arena>("/root/Main/ArenaLayer/Arena");
        goalBar = GetNode<ProgressBar>("HUD/Goal");
        enemyLayer = GetNode<CanvasLayer>("/root/Main/EnemyLayer");
        arenaLayer = GetNode<CanvasLayer>("/root/Main/ArenaLayer");
        markerLayer = GetNode<CanvasLayer>("/root/Main/MarkerLayer");
        paused = GetNode<CanvasLayer>("/root/Main/Paused");
        hudLayer = GetNode<CanvasLayer>("/root/Main/HUD");
        GoalLabel = GetNode<Label>("HUD/Label");
        HealthLabel = GetNode<Label>("HUD/Label2");
        gameover = GetNode<ColorRect>("CanvasLayer/ColorRect");
        record = GetNode<Button>("CanvasLayer/Button");
        ScoreLabel = GetNode<Label>("CanvasLayer/Label");
        gameover.Modulate = new Color(1, 1, 1, 0);

        CoinsEarnedLabel = new Label();
        CoinsEarnedLabel.Visible = false;
        CoinsEarnedLabel.HorizontalAlignment = HorizontalAlignment.Center;
        CoinsEarnedLabel.AnchorLeft = 0f;
        CoinsEarnedLabel.AnchorRight = 1f;
        CoinsEarnedLabel.AnchorTop = 0.5f;
        CoinsEarnedLabel.AnchorBottom = 0.5f;
        CoinsEarnedLabel.OffsetTop = 40f;
        CoinsEarnedLabel.OffsetBottom = 70f;
        CoinsEarnedLabel.AddThemeFontSizeOverride("font_size", 22);
        GetNode<CanvasLayer>("CanvasLayer").AddChild(CoinsEarnedLabel);

        player.GlobalPosition = arena.ToGlobal(new Vector2(arena.ArenaWidth * 25f / 2, (arena.ArenaHeight - 1) * 25f / 2));

        bool isTutorial = IsTutorialMode;
        IsTutorialMode = false;
        if (isTutorial)
        {
            var tutScene = GD.Load<PackedScene>("res://Scenes/tutorial.tscn");
            if (tutScene != null)
                AddChild(tutScene.Instantiate());
        }

        PrimeAuthCache();
    }

    public override void _Process(double delta)
    {
        spawnTimer += (float)delta;
        if(isPlaying)
        if(spawnTimer >= SpawnInterval)
        {
            spawnTimer = 0f;
            if(Player.Instance.health > 0)
            {
                if (TutorialManager.Instance == null || TutorialManager.Instance.EnemiesAllowed)
                    SpawnWithWarning();
            }
            
        }

        GoalLabel.Text = $"Goal: {currentGoal} / {goal}";
        HealthLabel.Text = $"Health: {Player.Instance?.GetHealth() ?? 0f} / {Player.Instance?.maxHealth ?? 0f}";
        if(currentGoal >= goal && !goalReached)
        {
            GD.Print("Goal reached! You win!");
            // Random position
            Vector2 min = Arena.Instance.GlobalPosition + new Vector2(35f, 35f); // avoid spawning on walls
            Vector2 max = Arena.Instance.GlobalPosition + new Vector2(
                Arena.Instance.ArenaWidth * 35f,
                (Arena.Instance.ArenaHeight - 1) * 35f
            );

            Vector2 spawnPos = new Vector2(
                (float)GD.RandRange(min.X, max.X),
                (float)GD.RandRange(min.Y, max.Y)
            );
            SpawnPortal(spawnPos);
            goalReached = true;
        }

        healingPotTimer += (float)delta;
        if (healingPotTimer >= HealingPotInterval)
        {
            healingPotTimer = 0f;
            SpawnHealingPot();
        }

        if(Input.IsActionJustPressed("pause") && isPlaying)
        {
            Engine.TimeScale = 0f;
            paused.Visible = true;
            FreezeLayers();
        }

    }

    private async void SpawnWithWarning()
    {
        // Reserve a slot atomically before anything async happens
        if (_activeEnemies >= WaveEnemyCount) return;
        _activeEnemies++;

        if (Arena.Instance == null) 
        {
            _activeEnemies--;
            return;
        }

        Vector2 min = Arena.Instance.GlobalPosition + new Vector2(35f, 35f);
        Vector2 max = Arena.Instance.GlobalPosition + new Vector2(
            Arena.Instance.ArenaWidth * 35f,
            (Arena.Instance.ArenaHeight - 1) * 35f
        );

        Vector2 spawnPos = new Vector2(
            (float)GD.RandRange(min.X, max.X),
            (float)GD.RandRange(min.Y, max.Y)
        );

        var marker = Marker.Instantiate();
        ((Node2D)marker).GlobalPosition = spawnPos;
        markerLayer.AddChild(marker);
        marker.AddToGroup("marker");

        await ToSignal(GetTree().CreateTimer(MarkerWarningTime), "timeout");

        if (!IsInstanceValid(this) || !isPlaying)
        {
            _activeEnemies--;
            if (IsInstanceValid(marker)) ((Node2D)marker).QueueFree();
            return;
        }

        var enemy = EnemyScene.Instantiate<Enemy>();
        var types = (Enemy.EnemyType[])System.Enum.GetValues(typeof(Enemy.EnemyType));
        enemy.Type = types[(int)GD.RandRange(0,types.Length - 1)];
        enemy.GlobalPosition = spawnPos;
        enemy.health = WaveEnemyHP;

        enemyLayer.AddChild(enemy);
        enemy.AddToGroup("enemy");

        enemy.TreeExited += () =>
        {
            _activeEnemies--;
            OnEnemyKilled(enemy);
        };

        if (IsInstanceValid(marker))
            ((Node2D)marker).QueueFree();
    }

    private void SpawnHealingPot()
    {
        if (HealingPotScene == null || Arena.Instance == null) return;
        if (GetTree().GetNodesInGroup("healing_pot").Count >= MaxHealingPots) return; // ← limit check
        int side = (int)GD.RandRange(0, 4);
        Vector2 arenaPos = Arena.Instance.GlobalPosition;
        float w = Arena.Instance.ArenaWidth * 35f;
        float h = Arena.Instance.ArenaHeight * 35f;
        float offset = (float)GD.RandRange(100f, 200f);
        Vector2 spawnPos = side switch
        {
            0 => new Vector2((float)GD.RandRange(arenaPos.X, arenaPos.X + w), arenaPos.Y - offset),
            1 => new Vector2((float)GD.RandRange(arenaPos.X, arenaPos.X + w), arenaPos.Y + h + offset),
            2 => new Vector2(arenaPos.X - offset, (float)GD.RandRange(arenaPos.Y, arenaPos.Y + h)),
            _ => new Vector2(arenaPos.X + w + offset, (float)GD.RandRange(arenaPos.Y, arenaPos.Y + h)),
        };
        var pot = HealingPotScene.Instantiate<Node2D>();
        pot.GlobalPosition = spawnPos;
        pot.AddToGroup("healing_pot"); // ← tag it
        arenaLayer.AddChild(pot);
    }

    private void OnEnemyKilled(Enemy enemy)
    {
    }

    public void OnDropCollected()
    {
        score += 10;
        currentGoal += 5;
        int goldEarned = (int)GD.RandRange(1, 5);
        money += goldEarned;
        goalBar.Value = currentGoal;
    }

    public void SpawnPortal(Vector2 position)
    {
        if (Portal == null) return;
        var portal = Portal.Instantiate<Node2D>();
        if (portal == null) return;
        portal.GlobalPosition = position;
        portal.AddToGroup("portal");   // ← tag the ROOT node, not just the Area2D
        arenaLayer.AddChild(portal);
        GD.Print("Portal spawned at: " + position);
        
    }

    public void QueueFreePortal()
    {
        var portals = GetTree().GetNodesInGroup("portal");
        GD.Print("Portals found: " + portals.Count);   // ← should print 1
        if (portals.Count == 0) return;
        var portal = (Node2D)portals[0];
        portal.Visible = false;
        portal.QueueFree();
        GD.Print("Portal removed");
        
    }

    public void FreezeLayers()
    {
        enemyLayer.ProcessMode = ProcessModeEnum.Disabled;
        arenaLayer.ProcessMode = ProcessModeEnum.Disabled;
    }

    public void UnfreezeLayers()
    {
        _activeEnemies = 0;
        isPlaying = true;
        enemyLayer.ProcessMode = ProcessModeEnum.Inherit;
        arenaLayer.ProcessMode = ProcessModeEnum.Inherit;
    }

    public void _on_button_pressed()
    {
        GetTree().ChangeSceneToFile("res://Scenes/titlescreen.tscn");
    }

    public void SubmitScoreToLeaderboard(int finalScore)
    {
        _pendingScoreValue = finalScore;
        SubmitScoreToLeaderboard();
    }

    public void SubmitScoreToLeaderboard()
    {
        if (_scoreSubmitted)
        {
            return;
        }

        var scoreToSubmit = _pendingScoreValue > 0 ? _pendingScoreValue : score;

        var firebase = GetNodeOrNull<Node>("/root/Firebase");
        var authNode = firebase?.GetNodeOrNull<Node>("Auth");
        var database = firebase?.GetNodeOrNull<Node>("Database");
        if (authNode == null || database == null)
        {
            return;
        }

        var isLoggedInVar = (Variant)authNode.Call("is_logged_in");
        var isLoggedIn = isLoggedInVar.VariantType == Variant.Type.Bool && isLoggedInVar.AsBool();
        if (!isLoggedIn)
        {
            if (!_waitingForScoreAuth)
            {
                _waitingForScoreAuth = true;
                var callable = new Callable(this, nameof(OnScoreAuthRequest));
                if (!authNode.IsConnected("auth_request", callable))
                {
                    authNode.Connect("auth_request", callable);
                }
            }

            var hasAuthFileVar = (Variant)authNode.Call("check_auth_file");
            var hasAuthFile = hasAuthFileVar.VariantType == Variant.Type.Bool && hasAuthFileVar.AsBool();
            if (!hasAuthFile)
            {
                authNode.Call("login_anonymous");
            }
            return;
        }

        var authVar = authNode.Get("auth");
        if (authVar.VariantType != Variant.Type.Dictionary)
        {
            return;
        }

        var authDict = (Godot.Collections.Dictionary)authVar;
        var email = _cachedEmail;
        if (string.IsNullOrEmpty(email))
        {
            email = "anonymous";
        }
        if (authDict.ContainsKey("email"))
        {
            var emailVar = (Variant)authDict["email"];
            if (emailVar.VariantType != Variant.Type.Nil)
            {
                var emailStr = emailVar.AsString();
                if (!string.IsNullOrEmpty(emailStr))
                {
                    email = emailStr;
                    _cachedEmail = emailStr;
                    CurrentUserEmail = emailStr;
                }
            }
        }

        var uid = _cachedUid;
        if (authDict.ContainsKey("localid"))
        {
            uid = ((Variant)authDict["localid"]).AsString();
        }
        else if (authDict.ContainsKey("localId"))
        {
            uid = ((Variant)authDict["localId"]).AsString();
        }

        if (!string.IsNullOrEmpty(uid))
        {
            _cachedUid = uid;
        }

        if (string.IsNullOrEmpty(uid))
        {
            return;
        }

        if (string.IsNullOrEmpty(email) || email == "anonymous")
        {
            _pendingScoreUid = uid;
            _pendingScoreValue = scoreToSubmit;
            RequestUserdataForScore(authNode);
            return;
        }

        SubmitScorePayload(database, uid, email, scoreToSubmit);
    }

    private void RequestUserdataForScore(Node authNode)
    {
        if (_waitingForUserdata)
        {
            return;
        }

        _waitingForUserdata = true;
        var callable = new Callable(this, nameof(OnUserdataReceived));
        if (!authNode.IsConnected("userdata_received", callable))
        {
            authNode.Connect("userdata_received", callable);
        }
        authNode.Call("get_user_data");
    }

    private void OnUserdataReceived(GodotObject userdata)
    {
        _waitingForUserdata = false;

        if (userdata == null)
        {
            return;
        }

        var emailVar = userdata.Get("email");
        var email = "anonymous";
        if (emailVar.VariantType != Variant.Type.Nil)
        {
            var emailStr = emailVar.AsString();
            if (!string.IsNullOrEmpty(emailStr))
            {
                email = emailStr;
            }
        }

        var uid = _pendingScoreUid;
        if (string.IsNullOrEmpty(uid))
        {
            var uidVar = userdata.Get("local_id");
            if (uidVar.VariantType != Variant.Type.Nil)
            {
                uid = uidVar.AsString();
            }
        }

        if (!string.IsNullOrEmpty(uid))
        {
            _cachedUid = uid;
        }

        if (!string.IsNullOrEmpty(email) && email != "anonymous")
        {
            _cachedEmail = email;
            CurrentUserEmail = email;
        }

        if (string.IsNullOrEmpty(_pendingScoreUid) && _pendingScoreValue <= 0)
        {
            _pendingScoreUid = "";
            return;
        }

        var firebase = GetNodeOrNull<Node>("/root/Firebase");
        var database = firebase?.GetNodeOrNull<Node>("Database");
        if (database == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(uid))
        {
            var scoreValue = _pendingScoreValue;
            if (scoreValue <= 0)
            {
                scoreValue = score;
            }
            SubmitScorePayload(database, uid, email, scoreValue);
        }
        _pendingScoreUid = "";
        _pendingScoreValue = 0;
    }

    private void PrimeAuthCache()
    {
        var firebase = GetNodeOrNull<Node>("/root/Firebase");
        var authNode = firebase?.GetNodeOrNull<Node>("Auth");
        if (authNode == null)
        {
            return;
        }

        var isLoggedInVar = (Variant)authNode.Call("is_logged_in");
        var isLoggedIn = isLoggedInVar.VariantType == Variant.Type.Bool && isLoggedInVar.AsBool();
        if (!isLoggedIn)
        {
            authNode.Call("check_auth_file");
            return;
        }

        var authVar = authNode.Get("auth");
        if (authVar.VariantType != Variant.Type.Dictionary)
        {
            return;
        }

        var authDict = (Godot.Collections.Dictionary)authVar;
        if (authDict.ContainsKey("localid"))
        {
            _cachedUid = ((Variant)authDict["localid"]).AsString();
        }
        else if (authDict.ContainsKey("localId"))
        {
            _cachedUid = ((Variant)authDict["localId"]).AsString();
        }

        if (authDict.ContainsKey("email"))
        {
            var emailVar = (Variant)authDict["email"];
            if (emailVar.VariantType != Variant.Type.Nil)
            {
                var emailStr = emailVar.AsString();
                if (!string.IsNullOrEmpty(emailStr))
                {
                    _cachedEmail = emailStr;
                    CurrentUserEmail = emailStr;
                    return;
                }
            }
        }

        RequestUserdataForScore(authNode);
    }

    private void SubmitScorePayload(Node database, string uid, string email, int scoreValue)
    {
        if (_scoreSubmitted)
        {
            return;
        }

        CheckAndSubmitBest(database, uid, email, scoreValue);
    }

    private void CheckAndSubmitBest(Node database, string uid, string email, int scoreValue)
    {
        if (_scoreSubmitted)
        {
            return;
        }

        if (_waitingForBestScore)
        {
            if (scoreValue > _pendingBestScore)
            {
                _pendingBestScore = scoreValue;
                _pendingBestEmail = email;
                _pendingBestUid = uid;
            }
            return;
        }

        _waitingForBestScore = true;
        _pendingBestScore = scoreValue;
        _pendingBestEmail = email;
        _pendingBestUid = uid;
        _pendingBestDatabase = database;

        var reference = (Node)database.Call("get_once_database_reference", "leaderboards/global");
        var successCallable = new Callable(this, nameof(OnBestScoreLoaded));
        var failedCallable = new Callable(this, nameof(OnBestScoreFailed));
        if (!reference.IsConnected("once_successful", successCallable))
        {
            reference.Connect("once_successful", successCallable);
        }
        if (!reference.IsConnected("once_failed", failedCallable))
        {
            reference.Connect("once_failed", failedCallable);
        }
        reference.Call("once", uid);
    }

    private void OnBestScoreLoaded(Godot.Collections.Dictionary snapshot)
    {
        _waitingForBestScore = false;

        var existingScore = 0;
        if (snapshot != null && snapshot.Count > 0 && snapshot.ContainsKey("score"))
        {
            existingScore = ParseScoreVariant((Variant)snapshot["score"]);
        }

        if (_pendingBestScore <= existingScore)
        {
            _scoreSubmitted = true;
            return;
        }

        if (_pendingBestDatabase == null || string.IsNullOrEmpty(_pendingBestUid))
        {
            return;
        }

        var payload = new Godot.Collections.Dictionary
        {
            { "email", _pendingBestEmail },
            { "score", _pendingBestScore },
            { "updatedAt", Time.GetUnixTimeFromSystem() }
        };

        var reference = (Node)_pendingBestDatabase.Call("get_once_database_reference", "leaderboards/global");
        reference.Call("update", _pendingBestUid, payload);

        // Also push this individual run to run history so the OLAP daily chart
        // reflects every session, not just each player's best score.
        string runId = $"{_pendingBestUid}_{(long)Time.GetUnixTimeFromSystem()}";
        var runPayload = new Godot.Collections.Dictionary
        {
            { "email",     _pendingBestEmail              },
            { "score",     _pendingBestScore              },
            { "timestamp", Time.GetUnixTimeFromSystem()   }
        };
        var runRef = (Node)_pendingBestDatabase.Call("get_once_database_reference", "leaderboards/runs");
        runRef.Call("update", runId, runPayload);

        _scoreSubmitted = true;
    }

    private void OnBestScoreFailed()
    {
        _waitingForBestScore = false;
    }

    private static int ParseScoreVariant(Variant value)
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
        return 0;
    }

    private void OnScoreAuthRequest(long resultCode, Variant _resultContent)
    {
        _waitingForScoreAuth = false;
        if (resultCode == 1)
        {
            SubmitScoreToLeaderboard();
        }
    }

    public void _on_ressume_pressed()
    {
        Engine.TimeScale = 1f;
        paused.Visible = false;
        UnfreezeLayers();
    }

    public void _on_exit_pressed()
    {
        Engine.TimeScale = 1f;
        GetTree().ChangeSceneToFile("res://Scenes/titlescreen.tscn");
    }
}