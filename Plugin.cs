using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using Comfort.Common;
using EFT;
using UnityEngine;

namespace TacticalHUD.GrenadeIndicator;

[BepInPlugin("TacticalHUD.GrenadeIndicator", "Grenade Indicator", "1.0.9")]
public class Plugin : BaseUnityPlugin
{
    public static string Path { get; private set; }

    private void Awake()
    {
        Path = System.IO.Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        Settings.Init(Config);

        EnablePatches();
        InitializeSprites();
        Logger.LogInfo("Grenade Indicator Plugin initialized.");
    }

    private void OnGUI()
    {
        TrackedGrenade.OnGUI();
        DebugGizmos.OnGUI();
    }

    private void EnablePatches()
    {
        new GameStartPatch().Enable();
        new GameEndedPatch().Enable();
        new AddIndicatorPatch().Enable();
    }

    private void InitializeSprites()
    {
        SpriteHandler.Init();

        AddSpriteResources();
        LoadAndAssignSprites();
    }

    private void AddSpriteResources()
    {
        SpriteHandler.AddResourcePath(System.IO.Path.Combine(Path, Settings.CompassImageFileName.Value));
        SpriteHandler.AddResourcePath(System.IO.Path.Combine(Path, Settings.CompassOverlayImageFileName.Value));
        SpriteHandler.AddResourcePath(System.IO.Path.Combine(Path, Settings.GrenadeImageFileName.Value));
    }

    private void LoadAndAssignSprites()
    {
        SpriteHandler.LoadSprites();
        TrackedGrenade.GrenadeSprite =
            SpriteHandler.GetSprite(System.IO.Path.Combine(Path, Settings.GrenadeImageFileName.Value));
        TrackedGrenade.CompassSprite =
            SpriteHandler.GetSprite(System.IO.Path.Combine(Path, Settings.CompassImageFileName.Value));
        TrackedGrenade.CompassOverlaySprite =
            SpriteHandler.GetSprite(System.IO.Path.Combine(Path, Settings.CompassOverlayImageFileName.Value));
    }

    public static Color GetColor(string hexColorCode, Color defaultColor)
    {
        return TryHexToColor(hexColorCode, out var color) ? color : defaultColor;
    }

    public static bool TryHexToColor(string hex, out Color color)
    {
        color = Color.white;
        if (string.IsNullOrEmpty(hex)) return false;

        hex = hex.Replace("#", "");

        if ((hex.Length == 6 || hex.Length == 8) &&
            int.TryParse(hex.Substring(0, 2), NumberStyles.HexNumber, null, out var r) &&
            int.TryParse(hex.Substring(2, 2), NumberStyles.HexNumber, null, out var g) &&
            int.TryParse(hex.Substring(4, 2), NumberStyles.HexNumber, null, out var b))
        {
            byte a = 255;
            if (hex.Length == 8 &&
                int.TryParse(hex.Substring(6, 2), NumberStyles.HexNumber, null, out var alpha))
                a = (byte)alpha;
            color = new Color32((byte)r, (byte)g, (byte)b, a);
            return true;
        }

        return false;
    }
}

public class GrenadeIndicatorComponent : MonoBehaviour
{
    private readonly Dictionary<int, TrackedGrenade> _grenades = new();
    private Camera _camera;
    private bool _subscribed;
    private float MaxGrenadeTrackDistSqr => Settings.MaxTrackingDistance.Value * Settings.MaxTrackingDistance.Value;

    private void Start()
    {
        GameWorld.OnDispose += Dispose;
    }

    private void Update()
    {
        if (!_subscribed) SubscribeToGrenadeEvents();

        // Check for grenades that are within tracking distance
        foreach (var grenade in _grenades.Values)
        {
            if (grenade != null)
            {
                Vector3 position = grenade.transform.position;
                if (ShouldTrackGrenade(grenade.Grenade, position))
                {
                    if (!grenade.IsTracking) // If it's not already being tracked
                    {
                        grenade.StartTracking(); // Start tracking the grenade
                    }
                }
                else
                {
                    if (grenade.IsTracking) // If it was being tracked but is now out of range
                    {
                        grenade.StopTracking(); // Stop tracking the grenade
                    }
                }
            }
        }
    }

    private void SubscribeToGrenadeEvents()
    {
        var botEvent = Singleton<BotEventHandler>.Instance;
        if (botEvent != null)
        {
            botEvent.OnGrenadeThrow += GrenadeThrown;
            _subscribed = true;
        }
    }
    
    public void Dispose()
    {
        GameWorld.OnDispose -= Dispose;
        UnsubscribeFromGrenadeEvents();
        ClearTrackedGrenades();

        // Disable all compass indicators and overlays
        foreach (var compass in TrackedGrenade.CompassIndicators)
        {
            compass.Enabled = false;
        }
        foreach (var overlay in TrackedGrenade.CompassOverlays)
        {
            overlay.Enabled = false;
        }

        // Clear the lists to free resources (if necessary)
        TrackedGrenade.CompassIndicators.Clear();
        TrackedGrenade.CompassOverlays.Clear();
    }

    private void UnsubscribeFromGrenadeEvents()
    {
        if (_subscribed)
        {
            var botEvent = Singleton<BotEventHandler>.Instance;
            if (botEvent != null) botEvent.OnGrenadeThrow -= GrenadeThrown;
        }
    }

    private void ClearTrackedGrenades()
    {
        foreach (var tracker in _grenades.Values) tracker?.Dispose();
        _grenades.Clear();
    }

    private void GrenadeThrown(Grenade grenade, Vector3 position, Vector3 force, float mass)
    {
        if (!ShouldTrackGrenade(grenade, position)) return;

        grenade.DestroyEvent += RemoveGrenade;
        var trackedGrenade = grenade.gameObject.AddComponent<TrackedGrenade>();
        trackedGrenade.StartTracking(); // Start tracking the grenade
        _grenades.Add(grenade.Id, trackedGrenade);
    }

    private bool ShouldTrackGrenade(Grenade grenade, Vector3 position)
    {
        if (grenade == null || !Settings.ModEnabled.Value) return false;

        _camera ??= Camera.main;
        if (_camera == null) return false;

        // Check if the grenade is within the maximum tracking distance
        float distanceSqr = (position - _camera.transform.position).sqrMagnitude;
        return distanceSqr <= MaxGrenadeTrackDistSqr;
    }

    private void RemoveGrenade(Throwable grenade)
    {
        if (grenade == null) return;

        grenade.DestroyEvent -= RemoveGrenade;
        if (_grenades.TryGetValue(grenade.Id, out var indicator))
        {
            indicator.Dispose();
            _grenades.Remove(grenade.Id);

            // Disable associated compass and overlay
            int index = TrackedGrenade.TrackedGrenades.IndexOf(indicator);
            if (index >= 0 && index < TrackedGrenade.CompassIndicators.Count)
            {
                TrackedGrenade.CompassIndicators[index].Enabled = false;
                TrackedGrenade.CompassOverlays[index].Enabled = false;
            }
        }
    }
}

internal class TrackedGrenade : MonoBehaviour
{
    private const float ExpireTimeoutDuration = 10f;
    internal static Sprite GrenadeSprite;
    internal static Sprite CompassSprite;
    internal static Sprite CompassOverlaySprite;

    internal static GUIObject CompassIndicator;
    internal static GUIObject CompassOverlayIndicator;
    internal static List<GUIObject> CompassIndicators = new List<GUIObject>(); // For compass
    internal static List<GUIObject> CompassOverlays = new List<GUIObject>(); // For overlay

    internal static readonly List<TrackedGrenade> TrackedGrenades = new();
    private static Grenade _closestGrenade;
    private Camera _camera;
    public bool IsTracking { get; private set; } = false; // New property to track if the grenade is being tracked

    private float _distance;
    private float _expireTime;
    private GUIObject _indicator;
    private Vector3 _position;
    private TrailRenderer _trailRenderer;

    public Grenade Grenade { get; private set; }

    private void Awake()
    {
        TrackedGrenades.Add(this);
        _expireTime = Time.time + ExpireTimeoutDuration;
        Grenade = GetComponent<Grenade>();
        _camera = Camera.main;

        CreateWorldIndicator();
        SetupTrailRenderer();

        CreateCompassIndicator(); // Create compass indicator for this grenade
        CreateCompassOverlay(); // Create overlay for this grenade
    }


    private void Update()
    {
        if (IsExpired())
        {
            Dispose();
            return;
        }

        if (!IsGrenadeValid()) return;

        UpdateIndicator();
        UpdateCompass();
    }

    public void StartTracking()
    {
        IsTracking = true; // Set tracking state to true
    }

    public void StopTracking()
    {
        IsTracking = false; // Set tracking state to false
    }

    public static void OnGUI()
    {
        foreach (var compassIndicator in CompassIndicators)
        {
            if (compassIndicator.Enabled)
            {
                DebugGizmos.DrawScreenImage(compassIndicator.Sprite, compassIndicator.Scale, compassIndicator.Rotation,
                    compassIndicator.Color);
            }
        }
    }

    private void CreateWorldIndicator()
    {
        if (GrenadeSprite != null)
        {
            _indicator = DebugGizmos.CreateImage(Grenade.transform.position, GrenadeSprite, 1f);
        }
        else
        {
            var style = new GUIStyle(GUI.skin.box);
            _indicator = DebugGizmos.CreateLabel(Grenade.transform.position, "[!]", style, 1f);
        }

        _indicator.Enabled = false;
        _indicator.Color = Plugin.GetColor(Settings.IndicatorColorMode.Value, Color.red);
    }

    private void SetupTrailRenderer()
    {
        if (!Settings.TrailEnabled.Value) return;

        _trailRenderer = gameObject.AddComponent<TrailRenderer>();
        _trailRenderer.enabled = true;
        _trailRenderer.emitting = true;
        _trailRenderer.startWidth = Settings.TrailStartSize.Value;
        _trailRenderer.endWidth = Settings.TrailEndSize.Value;
        _trailRenderer.material.color = Plugin.GetColor(Settings.TrailColorMode.Value, Color.red);
        _trailRenderer.colorGradient.mode = GradientMode.Fixed;
        _trailRenderer.time = Settings.TrailExpireTime.Value;
        _trailRenderer.numCapVertices = 5;
        _trailRenderer.numCornerVertices = 5;
        _trailRenderer.receiveShadows = Settings.TrailShadows.Value;
    }

    private bool IsExpired()
    {
        return Time.time > _expireTime;
    }

    private bool IsGrenadeValid()
    {
        return Grenade != null && Grenade.transform != null;
    }

    private void UpdateIndicator()
    {
        _position = Grenade.transform.position;
        _indicator.WorldPos = _position;

        var direction = _position - _camera.transform.position;
        _distance = direction.magnitude;

        // Check visibility based on line of sight
        var canSee = !Settings.RequireLos.Value ||
                     !Physics.Raycast(_camera.transform.position, direction, _distance,
                         LayerMaskClass.HighPolyWithTerrainMaskAI);

        // Always keep the indicator enabled for smooth fading
        _indicator.Enabled = true;

        // Scaling logic: clamps scale to at least 25% of the base size
        var normalizedDistance = Mathf.Clamp01(_distance / Settings.MaxTrackingDistance.Value);
        var minScale = Settings.IndicatorSize.Value * 0.65f; // Minimum scale is 65% of the base size
        _indicator.Scale = Mathf.Lerp(minScale, Settings.IndicatorSize.Value, 1f - normalizedDistance);

        // Opacity logic: smoothly fades out beyond max distance
        var targetAlpha = _distance <= Settings.MaxTrackingDistance.Value && canSee
            ? 1f - normalizedDistance
            : 0f;

        var fadeSpeed = 0.1f; // Adjust this for faster/slower fading
        var currentAlpha = _indicator.Color.a;
        var newAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, fadeSpeed * Time.deltaTime);

        // Apply the updated alpha
        _indicator.Color = new Color(_indicator.Color.r, _indicator.Color.g, _indicator.Color.b, newAlpha);

        // Fully disable the indicator only after it fades out completely
        if (newAlpha <= 0f) _indicator.Enabled = false;
    }

    private void CreateCompassIndicator()
    {
        if (GrenadeSprite != null)
        {
            var compassIndicator =
                DebugGizmos.CreateScreenImage(TrackedGrenade.CompassSprite, Settings.CompassSize.Value);

            compassIndicator.Enabled = false; // Initially disabled
            CompassIndicators.Add(compassIndicator); // Add to the list
        }
    }

    private void CreateCompassOverlay()
    {
        if (CompassOverlaySprite != null)
        {
            var compassOverlay =
                DebugGizmos.CreateScreenImage(TrackedGrenade.CompassOverlaySprite, Settings.CompassSize.Value);

            compassOverlay.Enabled = false; // Initially disabled
            CompassOverlays.Add(compassOverlay); // Add to the list
        }
    }

    private void UpdateCompass()
    {
        if (!Settings.EnableCompass.Value || TrackedGrenade.TrackedGrenades.Count == 0)
        {
            SetCompassEnabled(false);
            return;
        }

        var grenadesInRange = TrackedGrenade.TrackedGrenades.Where(grenade =>
            grenade.Grenade != null &&
            Vector3.Distance(_camera.transform.position, grenade.Grenade.transform.position) <= Settings.MaxTrackingDistance.Value).ToList();

        for (int i = 0; i < TrackedGrenade.CompassIndicators.Count; i++)
        {
            if (i < grenadesInRange.Count)
            {
                // Grenade is in range; update compass indicator
                var trackedGrenade = grenadesInRange[i];
                var compassIndicator = TrackedGrenade.CompassIndicators[i];
                var compassOverlay = TrackedGrenade.CompassOverlays[i];

                UpdateCompassRotation(compassIndicator, trackedGrenade);
                UpdateCompassRotation(compassOverlay, trackedGrenade);

                UpdateCompassAlpha(compassOverlay, trackedGrenade);

                compassIndicator.Enabled = true;
                compassOverlay.Enabled = true;
            }
            else
            {
                // Disable any unused compass indicators
                TrackedGrenade.CompassIndicators[i].Enabled = false;
                TrackedGrenade.CompassOverlays[i].Enabled = false;
            }
        }
    }

    private void UpdateCompassRotation(GUIObject compass, TrackedGrenade trackedGrenade)
    {
        var grenadePos = trackedGrenade.transform.position;
        var playerPos = _camera.transform.position;

        var directionToGrenade = (grenadePos - playerPos).normalized;
        directionToGrenade.y = 0f;

        var targetRotation = Vector3.SignedAngle(_camera.transform.forward, directionToGrenade, Vector3.up);
        compass.Rotation = targetRotation; // Set rotation for both compass and overlay
    }

    private void UpdateCompassAlpha(GUIObject compassOverlay, TrackedGrenade trackedGrenade)
    {
        var distanceToGrenade = Vector3.Distance(_camera.transform.position, trackedGrenade.Grenade.transform.position);
        var withinMaxDistance = distanceToGrenade <= Settings.MaxTrackingDistance.Value;

        // Opacity logic: smoothly fades in as you get closer
        var normalizedDistance = Mathf.Clamp01(distanceToGrenade / Settings.MaxTrackingDistance.Value);
        var targetAlpha = withinMaxDistance ? 1f - normalizedDistance : 0f; // Fully transparent beyond max range
        var fadeSpeed = 2f; // Adjust for faster/slower fading
        var currentAlpha = compassOverlay.Color.a;
        var newAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, fadeSpeed * Time.deltaTime);

        // Apply alpha to compass overlay
        compassOverlay.Color =
            new Color(compassOverlay.Color.r, compassOverlay.Color.g, compassOverlay.Color.b, newAlpha);

        // Disable overlay when fully faded
        compassOverlay.Enabled = newAlpha > 0f;
    }

    private void UpdateCompassVisibility(GUIObject compass, TrackedGrenade trackedGrenade)
    {
        // Logic to determine if the compass indicator should be visible
        compass.Enabled = trackedGrenade.Grenade != null; // Example logic
    }

    private void SetCompassEnabled(bool enabled)
    {
        if (CompassIndicator != null)
            CompassIndicator.Enabled = enabled;

        if (CompassOverlayIndicator != null)
            CompassOverlayIndicator.Enabled = enabled;
    }

    public void Dispose()
    {
        if (CompassIndicator != null)
        {
            CompassIndicator.Enabled = false;
        }

        if (CompassOverlayIndicator != null)
        {
            CompassOverlayIndicator.Enabled = false;
        }

        DebugGizmos.DestroyLabel(_indicator);
        TrackedGrenades.Remove(this);

        if (_closestGrenade == Grenade)
            _closestGrenade = null;

        Destroy(gameObject);
    }
}

internal static class Settings
{
    public static ConfigEntry<bool> ModEnabled { get; set; }
    public static ConfigEntry<bool> RequireLos { get; set; }
    public static ConfigEntry<float> IndicatorSize { get; set; }
    public static ConfigEntry<bool> TrailEnabled { get; set; }
    public static ConfigEntry<bool> TrailShadows { get; set; }
    public static ConfigEntry<float> TrailStartSize { get; set; }
    public static ConfigEntry<float> TrailEndSize { get; set; }
    public static ConfigEntry<float> TrailExpireTime { get; set; }
    public static ConfigEntry<string> IndicatorColorMode { get; set; }
    public static ConfigEntry<string> TrailColorMode { get; set; }
    public static ConfigEntry<string> GrenadeImageFileName { get; set; }
    public static ConfigEntry<string> CompassImageFileName { get; set; }
    public static ConfigEntry<string> CompassOverlayImageFileName { get; set; }
    public static ConfigEntry<bool> EnableOcclusion { get; set; }
    public static ConfigEntry<float> OcclusionAlpha { get; set; }
    public static ConfigEntry<bool> EnableCompass { get; set; }
    public static ConfigEntry<float> CompassSize { get; set; }

    public static ConfigEntry<float> MaxTrackingDistance { get; set; }

    public static void Init(ConfigFile config)
    {
        var order = 100;

        var customFiles = Directory.EnumerateFiles(Plugin.Path, "*.png", SearchOption.AllDirectories)
            .Select(Path.GetFileName).ToArray();

        MaxTrackingDistance = config.Bind("General", "Max Tracking Distance", 15f,
            new ConfigDescription("Maximum distance to track grenades (in meters).",
                new AcceptableValueRange<float>(1f, 500f),
                new ConfigurationManagerAttributes { Order = --order }));

        ModEnabled = config.Bind("General", "Enable Indicator", true,
            new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = --order }));

        RequireLos = config.Bind("General", "Require Line of Sight", false,
            new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = --order }));

        IndicatorSize = config.Bind("General", "Indicator Size", 1f,
            new ConfigDescription("", new AcceptableValueRange<float>(0.25f, 5f),
                new ConfigurationManagerAttributes { Order = --order }));

        TrailEnabled = config.Bind("General", "Draw Trail", false,
            new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = --order }));

        TrailShadows = config.Bind("General", "Draw Shadows on Trail", false,
            new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = --order }));

        TrailStartSize = config.Bind("General", "Trail Start Size", 0.075f,
            new ConfigDescription("", new AcceptableValueRange<float>(0.01f, 0.2f),
                new ConfigurationManagerAttributes { Order = --order }));

        TrailEndSize = config.Bind("General", "Trail End Size", 0.001f,
            new ConfigDescription("", new AcceptableValueRange<float>(0.001f, 0.2f),
                new ConfigurationManagerAttributes { Order = --order }));

        TrailExpireTime = config.Bind("General", "Trail Expire Time", 0.8f,
            new ConfigDescription("", new AcceptableValueRange<float>(0.2f, 5f),
                new ConfigurationManagerAttributes { Order = --order }));

        IndicatorColorMode = config.Bind("Color", "Indicator Color", "#FFFFFF",
            new ConfigDescription("Hexadecimal color code for the indicator.", null,
                new ConfigurationManagerAttributes { Order = --order }));

        TrailColorMode = config.Bind("Color", "Trail Color", "#FFFFFF",
            new ConfigDescription("Hexadecimal color code for the trail.", null,
                new ConfigurationManagerAttributes { Order = --order }));

        GrenadeImageFileName = config.Bind("General", "Grenade Indicator Image", "indicator.png",
            new ConfigDescription("The file name of a custom grenade indicator.",
                new AcceptableValueList<string>(customFiles),
                new ConfigurationManagerAttributes { Order = --order }));

        CompassImageFileName = config.Bind("Compass", "Compass Indicator Image", "compass.png",
            new ConfigDescription("The file name of a custom compass indicator.",
                new AcceptableValueList<string>(customFiles),
                new ConfigurationManagerAttributes { Order = --order }));

        CompassOverlayImageFileName = config.Bind("Compass", "Compass Overlay Indicator Image",
            "compass_overlay.png",
            new ConfigDescription("The file name of a custom compass overlay indicator.",
                new AcceptableValueList<string>(customFiles),
                new ConfigurationManagerAttributes { Order = --order }));

        EnableOcclusion = config.Bind("Occlusion", "Enable Occlusion Effect", true,
            new ConfigDescription("Makes the indicator semi-transparent when behind objects.", null,
                new ConfigurationManagerAttributes { Order = --order }));

        OcclusionAlpha = config.Bind("Occlusion", "Occlusion Transparency", 0.3f,
            new ConfigDescription("The transparency level when the indicator is occluded.",
                new AcceptableValueRange<float>(0f, 1f), new ConfigurationManagerAttributes { Order = --order }));

        EnableCompass = config.Bind("Compass", "Enable Compass Indicator", true,
            new ConfigDescription("", null, new ConfigurationManagerAttributes { Order = --order }));

        CompassSize = config.Bind("Compass", "Compass Size", 0.5f,
            new ConfigDescription("", new AcceptableValueRange<float>(0.25f, 5f),
                new ConfigurationManagerAttributes { Order = --order }));
    }
}