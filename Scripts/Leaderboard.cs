using Godot;
using System.Collections.Generic;

public partial class Leaderboard : Node2D
{
    private VBoxContainer _list;
    private Label _status;
    private Node _firebase;
    private Node _database;
    private Node _auth;
    private bool _waitingForAuth;

    public override void _Ready()
    {
        _list = GetNode<VBoxContainer>("MarginContainer/RootVBox/List");
        _status = GetNode<Label>("MarginContainer/RootVBox/Status");
        var backButton = GetNode<Button>("MarginContainer/RootVBox/BackButton");
        backButton.Pressed += OnBackPressed;

        FetchTop(5);
    }

    private void OnBackPressed()
    {
        GetTree().ChangeSceneToFile("res://Scenes/titlescreen.tscn");
    }

    private void FetchTop(int count)
    {
        _firebase = GetNodeOrNull<Node>("/root/Firebase");
        _auth = _firebase?.GetNodeOrNull<Node>("Auth");
        _database = _firebase?.GetNodeOrNull<Node>("Database");

        if (_database == null || _auth == null)
        {
            _status.Text = "Leaderboard unavailable.";
            return;
        }

        var isLoggedInVar = (Variant)_auth.Call("is_logged_in");
        var isLoggedIn = isLoggedInVar.VariantType == Variant.Type.Bool && isLoggedInVar.AsBool();
        if (!isLoggedIn)
        {
            _status.Text = "Loading auth...";

            if (!_waitingForAuth)
            {
                _waitingForAuth = true;
                var callable = new Callable(this, nameof(OnAuthRequest));
                if (!_auth.IsConnected("auth_request", callable))
                {
                    _auth.Connect("auth_request", callable);
                }
            }

            var hasAuthFileVar = (Variant)_auth.Call("check_auth_file");
            var hasAuthFile = hasAuthFileVar.VariantType == Variant.Type.Bool && hasAuthFileVar.AsBool();
            if (!hasAuthFile)
            {
                _status.Text = "Logging in anonymously...";
                _auth.Call("login_anonymous");
            }
            return;
        }

        _status.Text = "Loading...";
        var reference = (Node)_database.Call("get_once_database_reference", "leaderboards/global");
        var successCallable = new Callable(this, nameof(OnLeaderboardOnce));
        var failedCallable = new Callable(this, nameof(OnLeaderboardFailed));

        if (!reference.IsConnected("once_successful", successCallable))
        {
            reference.Connect("once_successful", successCallable);
        }
        if (!reference.IsConnected("once_failed", failedCallable))
        {
            reference.Connect("once_failed", failedCallable);
        }

        reference.Call("once", "");
    }

    private void OnLeaderboardOnce(Godot.Collections.Dictionary snapshot)
    {
        _status.Text = "";
        foreach (Node child in _list.GetChildren())
        {
            child.QueueFree();
        }

        if (snapshot == null || snapshot.Count == 0)
        {
            _status.Text = "No scores yet.";
            return;
        }

        var entries = new List<(string Email, int Score)>();
        foreach (var key in snapshot.Keys)
        {
            var entryVar = (Variant)snapshot[key];
            if (entryVar.VariantType == Variant.Type.Dictionary)
            {
                var entry = entryVar.AsGodotDictionary();
                var email = "[unknown]";
                var score = 0;

                if (entry.ContainsKey("email"))
                {
                    var emailVar = (Variant)entry["email"];
                    var emailStr = emailVar.AsString();
                    if (!string.IsNullOrEmpty(emailStr))
                    {
                        email = emailStr;
                    }
                }

                if (entry.ContainsKey("score"))
                {
                    var scoreVar = (Variant)entry["score"];
                    switch (scoreVar.VariantType)
                    {
                        case Variant.Type.Int:
                            score = (int)scoreVar.AsInt64();
                            break;
                        case Variant.Type.Float:
                            score = (int)scoreVar.AsDouble();
                            break;
                        case Variant.Type.String:
                            if (int.TryParse(scoreVar.AsString(), out var parsed))
                            {
                                score = parsed;
                            }
                            break;
                    }
                }

                entries.Add((email, score));
            }
        }

        entries.Sort((a, b) => b.Score.CompareTo(a.Score));
        if (entries.Count > 5)
        {
            entries.RemoveRange(5, entries.Count - 5);
        }

        var rank = 1;
        foreach (var entry in entries)
        {
            var row = new HBoxContainer();

            var _lbFont = GD.Load<FontFile>("res://Assets/Font/monogram-extended.ttf");

            var rankLabel = new Label
            {
                Text = $"{rank}."
            };
            rankLabel.CustomMinimumSize = new Vector2(40, 0);
            rankLabel.AddThemeFontOverride("font", _lbFont);

            var emailLabel = new Label
            {
                Text = entry.Email
            };
            emailLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            emailLabel.AddThemeFontOverride("font", _lbFont);

            var scoreLabel = new Label
            {
                Text = entry.Score.ToString()
            };
            scoreLabel.CustomMinimumSize = new Vector2(80, 0);
            scoreLabel.HorizontalAlignment = HorizontalAlignment.Right;
            scoreLabel.AddThemeFontOverride("font", _lbFont);

            row.AddChild(rankLabel);
            row.AddChild(emailLabel);
            row.AddChild(scoreLabel);
            _list.AddChild(row);
            rank++;
        }
    }

    private void OnLeaderboardFailed()
    {
        _status.Text = "Failed to load leaderboard.";
    }

    private void OnAuthRequest(long resultCode, Variant _resultContent)
    {
        _waitingForAuth = false;
        if (resultCode == 1)
        {
            FetchTop(5);
        }
        else
        {
            _status.Text = "Failed to authenticate.";
        }
    }
}
