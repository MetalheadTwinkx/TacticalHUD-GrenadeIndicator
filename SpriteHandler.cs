using System.Collections.Generic;
using System.IO;
using BepInEx.Logging;
using UnityEngine;
using Logger = BepInEx.Logging.Logger;

namespace TacticalHUD.GrenadeIndicator;

public static class SpriteHandler
{
    private static readonly ManualLogSource _logger = Logger.CreateLogSource("SpriteHandler");
    private static Dictionary<string, Sprite> _sprites;
    private static List<string> _resourcePaths;

    public static void Init()
    {
        var basePath = Plugin.Path;
        var compassPath = Path.Combine(basePath, Settings.CompassImageFileName.Value);
        var compassOverlayPath = Path.Combine(basePath, Settings.CompassOverlayImageFileName.Value);
        var grenadePath = Path.Combine(basePath, Settings.GrenadeImageFileName.Value);

        _resourcePaths = [compassPath, compassOverlayPath, grenadePath];
    }

    public static void AddResourcePath(string filePath)
    {
        if (!string.IsNullOrEmpty(filePath) && !_resourcePaths.Contains(filePath)) _resourcePaths.Add(filePath);
    }

    public static void LoadSprites()
    {
        _sprites = [];
        foreach (var texturePath in _resourcePaths)
        {
            var fileData = File.ReadAllBytes(texturePath);
            Texture2D texture = new(2, 2);
            texture.LoadImage(fileData);
            var textureSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f));
            _sprites.Add(texturePath, textureSprite);
        }

        _logger.LogInfo($"Loaded {_resourcePaths.Count} sprites.");
    }

    public static void ClearSprites()
    {
        _logger.LogInfo($"Clearing {_sprites.Count} sprites");
        _sprites.Clear();
    }

    private static bool LoadSprite(string filePath, out Sprite sprite)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogError("Could not find file at path: " + filePath);
            sprite = null;
            return false;
        }

        var fileData = File.ReadAllBytes(filePath);
        Texture2D texture = new(2, 2);
        texture.LoadImage(fileData);
        var textureSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f));
        _sprites.Add(filePath, textureSprite);

        sprite = textureSprite;
        return true;
    }

    public static Sprite GetSprite(string filePath)
    {
        if (_sprites.TryGetValue(filePath, out var sprite)) return sprite;

        if (LoadSprite(filePath, out sprite)) return sprite;

        _logger.LogError($"Could not find sprite at {filePath} at all!");
        return null;
    }
}