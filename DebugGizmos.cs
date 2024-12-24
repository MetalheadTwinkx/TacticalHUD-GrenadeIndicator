using System.Collections.Generic;
using EFT;
using UnityEngine;

namespace TacticalHUD.GrenadeIndicator;

public static class DebugGizmos
{
    private const float ScreenScaleCheckInterval = 10f;
    private static readonly List<GUIObject> Labels = new();
    private static GUIStyle _defaultStyle;
    private static float _screenScale = 1.0f;
    private static float _nextCheckScreenTime;

    static DebugGizmos()
    {
        GameWorld.OnDispose += Dispose;
    }

    private static void Dispose()
    {
        Labels.Clear();
    }

    public static void OnGUI()
    {
        foreach (var obj in Labels)
        {
            if (!obj.Enabled) continue;

            if (obj.IsScreenImage && obj.Sprite != null)
            {
                DrawScreenImage(obj.Sprite, obj.Scale, obj.Rotation, obj.Color);
                if (obj.Sprite2 != null) DrawScreenImage(obj.Sprite2, obj.Scale, obj.Rotation, obj.Color);
            }
            else if (obj.IsImage && obj.Sprite != null)
            {
                DrawWorldImage(obj.WorldPos, obj.Sprite, obj.Scale, obj.Color);
            }
        }
    }

    public static GUIObject CreateLabel(Vector3 worldPos, string text, GUIStyle guiStyle, float scale)
    {
        var color = Plugin.GetColor(Settings.IndicatorColorMode.Value, Color.red);
        ApplyToStyle.TextColorAllStates(color, guiStyle);
        var obj = new GUIObject
        {
            WorldPos = worldPos,
            Style = guiStyle,
            Scale = scale,
            Color = color
        };
        AddGUIObject(obj);
        return obj;
    }

    public static GUIObject CreateImage(Vector3 worldPos, Sprite sprite, float scale)
    {
        var color = Plugin.GetColor(Settings.IndicatorColorMode.Value, Color.red);
        var obj = new GUIObject
        {
            WorldPos = worldPos,
            Sprite = sprite,
            Scale = scale,
            IsImage = true,
            Color = color
        };
        AddGUIObject(obj);
        return obj;
    }

    public static GUIObject CreateScreenImage(Sprite sprite, float scale)
    {
        var obj = new GUIObject
        {
            Sprite = sprite,
            Scale = scale,
            IsScreenImage = true
        };
        AddGUIObject(obj);
        return obj;
    }

    public static void AddGUIObject(GUIObject obj)
    {
        if (!Labels.Contains(obj)) Labels.Add(obj);
    }

    public static void DestroyLabel(GUIObject obj)
    {
        Labels.Remove(obj);
    }

    public static void OnGUIDrawLabel(Vector3 worldPos, string text, GUIStyle guiStyle = null, float scale = 1f)
    {
        var screenPos = Camera.main.WorldToScreenPoint(worldPos);
        if (screenPos.z <= 0) return;

        guiStyle ??= GetDefaultGUIStyle();
        var originalFontSize = guiStyle.fontSize;
        guiStyle.fontSize = Mathf.RoundToInt(originalFontSize * scale);

        var content = new GUIContent(text);
        var currentScreenScale = GetScreenScale();
        var guiSize = guiStyle.CalcSize(content);

        var x = screenPos.x * currentScreenScale - guiSize.x / 2f;
        var y = Screen.height - (screenPos.y * currentScreenScale + guiSize.y);
        var rect = new Rect(new Vector2(x, y), guiSize);

        GUI.Label(rect, content, guiStyle);
        guiStyle.fontSize = originalFontSize;
    }

    private static GUIStyle GetDefaultGUIStyle()
    {
        if (_defaultStyle == null)
            _defaultStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 20,
                margin = new RectOffset(3, 3, 3, 3)
            };
        return _defaultStyle;
    }

    public static void DrawWorldImage(Vector3 worldPos, Sprite sprite, float scale = 1f, Color? forcedColor = null)
    {
        var screenPos = Camera.main.WorldToScreenPoint(worldPos);
        if (screenPos.z <= 0) return;

        var currentScreenScale = GetScreenScale();
        var scaledWidth = sprite.texture.width * scale;
        var scaledHeight = sprite.texture.height * scale;
        var x = screenPos.x * currentScreenScale - scaledWidth / 2f;
        var y = Screen.height - (screenPos.y * currentScreenScale + scaledHeight);
        var rect = new Rect(x, y, scaledWidth, scaledHeight);

        var colorToUse = forcedColor.GetValueOrDefault(Color.white);

        if (Settings.EnableOcclusion.Value) colorToUse.a = IsOccluded(worldPos) ? Settings.OcclusionAlpha.Value : 1f;

        DrawTexture(rect, sprite.texture, colorToUse);
    }

    private static bool IsOccluded(Vector3 worldPos)
    {
        var cameraPos = Camera.main.transform.position;
        var direction = worldPos - cameraPos;
        var distanceToTarget = direction.magnitude;
        return Physics.Raycast(cameraPos, direction, out _, distanceToTarget, LayerMaskClass.HighPolyWithTerrainMask);
    }

    public static void DrawScreenImage(Sprite sprite, float scale = 1f, float rotation = 0f, Color? forcedColor = null)
    {
        if (sprite == null) return;

        var scaledWidth = sprite.texture.width * scale;
        var scaledHeight = sprite.texture.height * scale;
        var x = Screen.width / 2f - scaledWidth / 2f;
        var y = Screen.height / 2f - scaledHeight / 2f;
        var rect = new Rect(x, y, scaledWidth, scaledHeight);
        var colorToUse = forcedColor.GetValueOrDefault(Color.white);

        DrawRotatedTexture(rect, sprite.texture, rotation, colorToUse);
    }

    private static void DrawTexture(Rect rect, Texture texture, Color color)
    {
        var originalColor = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, texture);
        GUI.color = originalColor;
    }

    private static void DrawRotatedTexture(Rect rect, Texture texture, float rotation, Color color)
    {
        var matrixBackup = GUI.matrix;
        var originalColor = GUI.color;

        GUI.color = color;
        GUIUtility.RotateAroundPivot(rotation, rect.center);
        GUI.DrawTexture(rect, texture);

        GUI.matrix = matrixBackup;
        GUI.color = originalColor;
    }

    private static float GetScreenScale()
    {
        if (_nextCheckScreenTime < Time.time && CameraClass.Instance.SSAA.isActiveAndEnabled)
        {
            _nextCheckScreenTime = Time.time + ScreenScaleCheckInterval;
            _screenScale = (float)CameraClass.Instance.SSAA.GetOutputWidth() /
                           CameraClass.Instance.SSAA.GetInputWidth();
        }

        return _screenScale;
    }
}