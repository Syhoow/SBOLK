using Godot;

public partial class AnimatedButton : Button
{
    [Signal] public delegate void HoveredEventHandler();
    [Signal] public delegate void UnhoveredEventHandler();

    [Export] public bool HoverAnimate = true;
    [Export] public bool ScaleWithWidth = true;
    [Export] public float WidthFullRot = 200f;
    [Export] public bool Disabled = false;

    private Tween tween;

    public override void _Ready()
    {
        PivotOffset = Size / 2f;
        MouseEntered += OnHover;
        MouseExited += OnUnhover;
    }

    private void OnHover()
    {
        if (Disabled) return;
        EmitSignal(SignalName.Hovered);

        if (!HoverAnimate) return;

        PivotOffset = Size / 2f;

        float scaleRatio = Mathf.Clamp(WidthFullRot / Size.X, 0.5f, 1.0f);
        float scaleTarget = 1.0f + 0.2f * scaleRatio;

        if (!ScaleWithWidth)
        {
            scaleTarget = 1.2f;
            scaleRatio = 1.0f;
        }

        GD.Print("Scale target: ", scaleTarget);

        if (tween != null && tween.IsRunning())
            tween.Kill();

        tween = CreateTween()
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Back);

        tween.TweenProperty(this, "scale:x", scaleTarget, 0.2f);
        tween.Parallel().TweenProperty(this, "scale:y", scaleTarget, 0.35f);
        tween.Parallel().TweenProperty(this, "rotation_degrees", 5.0f * scaleRatio * (GD.Randf() > 0.5f ? 1f : -1f), 0.1f);
        tween.Parallel().TweenProperty(this, "rotation_degrees", 0.0f, 0.1f).SetDelay(0.1f);
    }

    private void OnUnhover()
    {
        if (Disabled) return;
        EmitSignal(SignalName.Unhovered);

        PivotOffset = Size / 2f;

        if (!HoverAnimate) return;

        if (tween != null && tween.IsRunning())
            tween.Kill();

        tween = CreateTween()
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Back);

        tween.TweenProperty(this, "scale:x", 1.0f, 0.2f);
        tween.Parallel().TweenProperty(this, "scale:y", 1.0f, 0.2f);
        tween.Parallel().TweenProperty(this, "rotation_degrees", 0.0f, 0.15f);
    }
}