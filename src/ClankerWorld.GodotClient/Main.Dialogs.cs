using Godot;

namespace ClankerWorld.GodotClient;

/// <summary>Confirmation dialogs styled like the game's panels.</summary>
public partial class Main
{
    private static Theme? confirmationTheme;

    /// <summary>
    /// Gives a confirmation the panel colors and names the action on its
    /// confirm button, so "Quit to Menu" reads as what happens instead of OK.
    /// </summary>
    private static void StyleConfirmation(ConfirmationDialog dialog, string title, string confirm)
    {
        dialog.Title = title;
        dialog.OkButtonText = confirm;
        dialog.CancelButtonText = "Cancel";
        dialog.Theme = confirmationTheme ??= ConfirmationTheme();
        StyleButton(dialog.GetOkButton(), primary: true);
        StyleButton(dialog.GetCancelButton());
        dialog.GetOkButton().CustomMinimumSize = new Vector2(150, 36);
        dialog.GetCancelButton().CustomMinimumSize = new Vector2(110, 36);
        dialog.GetLabel().AutowrapMode = TextServer.AutowrapMode.WordSmart;
    }

    private static Theme ConfirmationTheme()
    {
        const int titleHeight = 34;
        var theme = new Theme();
        var frame = new StyleBoxFlat
        {
            BgColor = new Color("192631"),
            BorderColor = new Color("345363"),
            BorderWidthLeft = 1,
            BorderWidthTop = 1,
            BorderWidthRight = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
            ExpandMarginTop = titleHeight,
            ExpandMarginLeft = 1,
            ExpandMarginRight = 1,
            ExpandMarginBottom = 1,
            ShadowColor = new Color(0, 0, 0, 0.45f),
            ShadowSize = 12,
        };
        theme.SetStylebox("embedded_border", "Window", frame);
        theme.SetStylebox("embedded_unfocused_border", "Window", frame);
        theme.SetConstant("title_height", "Window", titleHeight);
        theme.SetColor("title_color", "Window", new Color("F4F0E3"));
        theme.SetStylebox("panel", "AcceptDialog", new StyleBoxFlat
        {
            BgColor = new Color("192631"),
            ContentMarginLeft = 16,
            ContentMarginRight = 16,
            ContentMarginTop = 12,
            ContentMarginBottom = 12,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
        });
        theme.SetColor("font_color", "Label", new Color("E5EFEA"));
        return theme;
    }
}
