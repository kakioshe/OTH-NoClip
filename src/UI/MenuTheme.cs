using UnityEngine;

internal static class MenuTheme
{
    internal static readonly Color Panel = new(0.055f, 0.075f, 0.10f, 0.98f);
    internal static readonly Color Card = new(0.09f, 0.12f, 0.16f, 1f);
    internal static readonly Color Accent = new(0.38f, 0.83f, 0.74f, 1f);
    internal static readonly Color Detail = new(0.70f, 0.76f, 0.82f);

    internal static GUIStyle Section(GUIStyle label)
    {
        var style = new GUIStyle(label) { fontStyle = FontStyle.Bold };
        style.normal.textColor = Accent;
        return style;
    }
}
