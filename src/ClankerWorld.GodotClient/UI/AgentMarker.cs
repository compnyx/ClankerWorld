using Godot;

namespace ClankerWorld.GodotClient.UI;

/// <summary>A fixed-size map hit target; unlike Button, text/theme minima cannot enlarge it.</summary>
public partial class AgentMarker : Control
{
    private string caption = string.Empty;
    private bool selected;
    private bool hovered;

    public event Action? Activated;

    public string Caption
    {
        get => caption;
        set { if (caption != value) { caption = value; QueueRedraw(); } }
    }

    public bool Selected
    {
        get => selected;
        set { if (selected != value) { selected = value; QueueRedraw(); } }
    }

    public AgentMarker()
    {
        MouseFilter = MouseFilterEnum.Pass;
        MouseEntered += () => { hovered = true; QueueRedraw(); };
        MouseExited += () => { hovered = false; QueueRedraw(); };
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
        {
            Activated?.Invoke();
            AcceptEvent();
        }
    }

    public override void _Draw()
    {
        var rect = new Rect2(Vector2.Zero, Size);
        var color = selected || hovered ? new Color("D98B53") : new Color("B86F48");
        DrawRect(rect, color);
        DrawRect(rect, new Color(selected || hovered ? "FFF0B5" : "F4C78A"), filled: false,
            width: Size.X >= 16 && Size.Y >= 16 ? 2 : 1);
        if (Size.X < 28 || Size.Y < 22 || caption.Length == 0) return;
        var fontSize = Math.Clamp((int)(Math.Min(Size.X, Size.Y) * 0.24f), 9, 15);
        var text = caption.Length > 6 ? caption[..5] + "…" : caption;
        DrawString(ThemeDB.FallbackFont, new Vector2(3, Size.Y / 2f + fontSize / 3f),
            text, fontSize: fontSize, modulate: Colors.White);
    }
}
