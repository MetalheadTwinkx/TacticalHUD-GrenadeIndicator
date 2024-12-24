using System.Linq;
using UnityEngine;

namespace TacticalHUD.GrenadeIndicator;

internal class ApplyToStyle
{
    public static void BackgroundAllStates(Texture2D normal, params GUIStyle[] styles)
    {
        BackgroundOn(normal, styles);
        BackgroundNotOn(normal, styles);
    }

    public static void BackgroundAllStates(Texture2D normal, Texture2D active, params GUIStyle[] styles)
    {
        BackgroundOn(normal, styles);
        BackgroundNotOn(active, styles);
    }

    public static void BackgroundNotOn(Texture2D texture, params GUIStyle[] styles)
    {
        for (var i = 0; i < styles.Length; i++)
            Background(texture, styles[i], StyleState.Normal, StyleState.Hover, StyleState.Focused, StyleState.Active);
    }

    public static void BackgroundOn(Texture2D texture, params GUIStyle[] styles)
    {
        for (var i = 0; i < styles.Length; i++)
            Background(texture, styles[i], StyleState.OnNormal, StyleState.OnHover, StyleState.OnFocused,
                StyleState.OnActive);
    }

    public static void BackgroundNormal(Texture2D normal, Texture2D onNormal, params GUIStyle[] styles)
    {
        for (var i = 0; i < styles.Length; i++)
        {
            Background(normal, styles[i], StyleState.Normal);
            Background(onNormal, styles[i], StyleState.OnNormal);
        }
    }

    public static void BackgroundActive(Texture2D active, Texture2D onActive, params GUIStyle[] styles)
    {
        for (var i = 0; i < styles.Length; i++)
        {
            Background(active, styles[i], StyleState.Active);
            Background(onActive, styles[i], StyleState.OnActive);
        }
    }

    public static void BackgroundFocused(Texture2D focused, Texture2D onFocused, params GUIStyle[] styles)
    {
        for (var i = 0; i < styles.Length; i++)
        {
            Background(focused, styles[i], StyleState.Focused);
            Background(onFocused, styles[i], StyleState.OnFocused);
        }
    }

    public static void BackgroundHover(Texture2D hover, Texture2D onHover, params GUIStyle[] styles)
    {
        for (var i = 0; i < styles.Length; i++)
        {
            Background(hover, styles[i], StyleState.Hover);
            Background(onHover, styles[i], StyleState.OnHover);
        }
    }

    public static void BackgroundNormal(Texture2D normal, params GUIStyle[] styles)
    {
        BackgroundNormal(normal, normal, styles);
    }

    public static void BackgroundActive(Texture2D active, params GUIStyle[] styles)
    {
        BackgroundActive(active, active, styles);
    }

    public static void BackgroundFocused(Texture2D focused, params GUIStyle[] styles)
    {
        BackgroundFocused(focused, focused, styles);
    }

    public static void BackgroundHover(Texture2D hover, params GUIStyle[] styles)
    {
        BackgroundHover(hover, hover, styles);
    }

    public static void Background(Texture2D texture, GUIStyle style, params StyleState[] states)
    {
        if (states.Contains(StyleState.Normal)) style.normal.background = texture;
        if (states.Contains(StyleState.OnNormal)) style.onNormal.background = texture;
        if (states.Contains(StyleState.Active)) style.active.background = texture;
        if (states.Contains(StyleState.OnActive)) style.onActive.background = texture;
        if (states.Contains(StyleState.Hover)) style.hover.background = texture;
        if (states.Contains(StyleState.OnHover)) style.onHover.background = texture;
        if (states.Contains(StyleState.Focused)) style.focused.background = texture;
        if (states.Contains(StyleState.OnFocused)) style.onFocused.background = texture;
    }

    public static void TextColorAllStates(Color normal, params GUIStyle[] styles)
    {
        TextColorNotOn(normal, styles);
        TextColorOn(normal, styles);
    }

    public static void TextColorAllStates(Color normal, Color active, params GUIStyle[] styles)
    {
        TextColorNotOn(normal, styles);
        TextColorOn(active, styles);
    }

    public static void TextColorNotOn(Color texture, params GUIStyle[] styles)
    {
        for (var i = 0; i < styles.Length; i++)
            TextColor(texture, styles[i], StyleState.Normal, StyleState.Hover, StyleState.Focused, StyleState.Active);
    }

    public static void TextColorOn(Color texture, params GUIStyle[] styles)
    {
        for (var i = 0; i < styles.Length; i++)
            TextColor(texture, styles[i], StyleState.OnNormal, StyleState.OnHover, StyleState.OnFocused,
                StyleState.OnActive);
    }

    public static void TextColorNormal(Color normal, Color onNormal, params GUIStyle[] styles)
    {
        for (var i = 0; i < styles.Length; i++)
        {
            TextColor(normal, styles[i], StyleState.Normal);
            TextColor(onNormal, styles[i], StyleState.OnNormal);
        }
    }

    public static void TextColorActive(Color active, Color onActive, params GUIStyle[] styles)
    {
        for (var i = 0; i < styles.Length; i++)
        {
            TextColor(active, styles[i], StyleState.Active);
            TextColor(onActive, styles[i], StyleState.OnActive);
        }
    }

    public static void TextColorFocused(Color focused, Color onFocused, params GUIStyle[] styles)
    {
        for (var i = 0; i < styles.Length; i++)
        {
            TextColor(focused, styles[i], StyleState.Focused);
            TextColor(onFocused, styles[i], StyleState.OnFocused);
        }
    }

    public static void TextColorHover(Color hover, Color onHover, params GUIStyle[] styles)
    {
        for (var i = 0; i < styles.Length; i++)
        {
            TextColor(hover, styles[i], StyleState.Hover);
            TextColor(onHover, styles[i], StyleState.OnHover);
        }
    }

    public static void TextColorNormal(Color normal, params GUIStyle[] styles)
    {
        TextColorNormal(normal, normal, styles);
    }

    public static void TextColorActive(Color active, params GUIStyle[] styles)
    {
        TextColorActive(active, active, styles);
    }

    public static void TextColorFocused(Color focused, params GUIStyle[] styles)
    {
        TextColorFocused(focused, focused, styles);
    }

    public static void TextColorHover(Color hover, params GUIStyle[] styles)
    {
        TextColorHover(hover, hover, styles);
    }

    public static void TextColor(Color color, GUIStyle style, params StyleState[] states)
    {
        if (states.Contains(StyleState.Normal)) style.normal.textColor = color;
        if (states.Contains(StyleState.OnNormal)) style.onNormal.textColor = color;
        if (states.Contains(StyleState.Active)) style.active.textColor = color;
        if (states.Contains(StyleState.OnActive)) style.onActive.textColor = color;
        if (states.Contains(StyleState.Hover)) style.hover.textColor = color;
        if (states.Contains(StyleState.OnHover)) style.onHover.textColor = color;
        if (states.Contains(StyleState.Focused)) style.focused.textColor = color;
        if (states.Contains(StyleState.OnFocused)) style.onFocused.textColor = color;
    }
}