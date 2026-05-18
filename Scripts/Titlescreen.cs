using Godot;
using System;

public partial class Titlescreen : Node2D
{
    private Label _userLabel;

    public override void _Ready()
    {
        _userLabel = GetNodeOrNull<Label>("Label");
        SetUserLabelFromAuth();
    }

    public void _on_button_pressed()
    {
        GetTree().ChangeSceneToFile("res://Scenes/main.tscn");
    }

    public void _on_sign_out_button_pressed()
    {
        var authPath = "user://user.auth";
        if (FileAccess.FileExists(authPath))
        {
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(authPath));
        }
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
            return;
        }

        var authNode = firebase.GetNodeOrNull<Node>("Auth");
        if (authNode == null)
        {
            _userLabel.Text = "Logged In As : [unknown]";
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
                    _userLabel.Text = "Logged In As : " + email;
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
    }
}
