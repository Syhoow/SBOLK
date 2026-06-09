using Godot;
using System;

public partial class Titlescreen : Node2D
{
    private const string AdminEmail = "admin123@gmail.com";

    private Label _userLabel;
    private RichTextLabel _top1Label;
    private Button _analyticsButton;
    private Node _firebase;
    private Node _auth;
    private Node _database;
    private bool _waitingForAuth;

    public override void _Ready()
    {
        _userLabel = GetNodeOrNull<Label>("Label");
        _top1Label = GetNodeOrNull<RichTextLabel>("Top1NameLabel");
        _analyticsButton = GetNodeOrNull<Button>("AnalyticsButton");
        SetUserLabelFromAuth();
        FetchTop1();
    }

    public void _on_button_pressed()
    {
        GameControl.IsTutorialMode = false;
        GetTree().ChangeSceneToFile("res://Scenes/main.tscn");
    }

    public void _on_TutorialButton_pressed()
    {
        GameControl.IsTutorialMode = true;
        GetTree().ChangeSceneToFile("res://Scenes/main.tscn");
    }

    public void _on_sign_out_button_pressed()
    {
        var firebase = GetNodeOrNull<Node>("/root/Firebase");
        var authNode = firebase?.GetNodeOrNull<Node>("Auth");
        authNode?.Call("logout");
        GetTree().ChangeSceneToFile("res://Scenes/Authentication.tscn");
    }

    public void _on_leaderboard_button_pressed()
    {
        GetTree().ChangeSceneToFile("res://Scenes/leaderboard.tscn");
    }

    private void SetUserLabelFromAuth()
    {
        if (_userLabel == null)
        {
            return;
        }

        var firebase = GetNodeOrNull<Node>("/root/Firebase");
        if (firebase == null)
        {
            _userLabel.Text = "Logged In As : [unknown]";
            UpdateAnalyticsAccess("");
            return;
        }

        var authNode = firebase.GetNodeOrNull<Node>("Auth");
        if (authNode == null)
        {
            _userLabel.Text = "Logged In As : [unknown]";
            UpdateAnalyticsAccess("");
            return;
        }

        var authVar = authNode.Get("auth");
        if (authVar.VariantType == Variant.Type.Dictionary)
        {
            var authDict = (Godot.Collections.Dictionary)authVar;
            if (authDict.ContainsKey("email"))
            {
                var emailVar = (Variant)authDict["email"];
                var email = "";
                if (emailVar.VariantType != Variant.Type.Nil)
                {
                    email = emailVar.AsString();
                }
                if (!string.IsNullOrEmpty(email))
                {
                    GameControl.CurrentUserEmail = email;
                    _userLabel.Text = "Logged In As : " + email;
                    UpdateAnalyticsAccess(email);
                    return;
                }
            }
        }

        var callable = new Callable(this, nameof(OnUserdataReceived));
        if (!authNode.IsConnected("userdata_received", callable))
        {
            authNode.Connect("userdata_received", callable);
        }

        authNode.Call("get_user_data");
        _userLabel.Text = "Logged In As : [loading]";
        UpdateAnalyticsAccess("");
    }

    private void OnUserdataReceived(GodotObject userdata)
    {
        if (_userLabel == null || userdata == null)
        {
            return;
        }

        var emailVar = userdata.Get("email");
        var email = "";
        if (emailVar.VariantType != Variant.Type.Nil)
        {
            email = emailVar.AsString();
        }
        _userLabel.Text = string.IsNullOrEmpty(email)
            ? "Logged In As : [unknown]"
            : "Logged In As : " + email;
        GameControl.CurrentUserEmail = email;
        UpdateAnalyticsAccess(email);
    }

    private void FetchTop1()
    {
        if (_top1Label == null)
        {
            return;
        }

        _firebase = GetNodeOrNull<Node>("/root/Firebase");
        _auth = _firebase?.GetNodeOrNull<Node>("Auth");
        _database = _firebase?.GetNodeOrNull<Node>("Database");
        if (_auth == null || _database == null)
        {
            _top1Label.Text = "No.1: [unavailable]";
            return;
        }

        var isLoggedInVar = (Variant)_auth.Call("is_logged_in");
        var isLoggedIn = isLoggedInVar.VariantType == Variant.Type.Bool && isLoggedInVar.AsBool();
        if (!isLoggedIn)
        {
            _top1Label.Text = "No.1: [loading]";
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
                _auth.Call("login_anonymous");
            }
            return;
        }

        var reference = (Node)_database.Call("get_once_database_reference", "leaderboards/global");
        var successCallable = new Callable(this, nameof(OnTop1Loaded));
        var failedCallable = new Callable(this, nameof(OnTop1Failed));
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

    private void OnTop1Loaded(Godot.Collections.Dictionary snapshot)
    {
        if (_top1Label == null)
        {
            return;
        }

        var bestEmail = "[none]";
        var bestScore = -1;

        if (snapshot != null)
        {
            foreach (var key in snapshot.Keys)
            {
                var entryVar = (Variant)snapshot[key];
                if (entryVar.VariantType != Variant.Type.Dictionary)
                {
                    continue;
                }

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
                    score = ParseScoreVariant((Variant)entry["score"]);
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestEmail = email;
                }
            }
        }

        if (bestScore < 0)
        {
            _top1Label.Text = "No.1: [none]";
            return;
        }

        _top1Label.Text = $"No.1: {bestEmail} [wave][rainbow]({bestScore})[/rainbow][/wave]";
    }

    private void OnTop1Failed()
    {
        if (_top1Label != null)
        {
            _top1Label.Text = "No.1: [failed]";
        }
    }

    private void OnAuthRequest(long resultCode, Variant _resultContent)
    {
        _waitingForAuth = false;
        if (resultCode == 1)
        {
            FetchTop1();
        }
        else if (_top1Label != null)
        {
            _top1Label.Text = "No.1: [auth failed]";
        }
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

    private void _on_ShopButton_pressed()
    {
        GetTree().ChangeSceneToFile("res://Scenes/shop.tscn");
    }

    private void _on_InventoryButton_pressed()
    {
        GetTree().ChangeSceneToFile("res://Scenes/inventory.tscn");
    }

    private void _on_MarketplaceButton_pressed()
    {
        GetTree().ChangeSceneToFile("res://Scenes/marketplace.tscn");
    }

    private void _on_AnalyticsButton_pressed()
    {
        if (!IsCurrentUserAdmin())
        {
            GetTree().ChangeSceneToFile("res://Scenes/titlescreen.tscn");
            return;
        }

        GetTree().ChangeSceneToFile("res://Scenes/analytics.tscn");
    }

    private void _on_Exit_pressed()
    {
        GetTree().Quit();
    }

    private bool IsCurrentUserAdmin()
    {
        return string.Equals(GameControl.CurrentUserEmail, AdminEmail, StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateAnalyticsAccess(string email)
    {
        if (_analyticsButton != null)
        {
            _analyticsButton.Visible = string.Equals(email, AdminEmail, StringComparison.OrdinalIgnoreCase);
        }
    }
}
