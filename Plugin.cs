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
            if (grenade != null)
            {
                var position = grenade.transform.position;
                if (ShouldTrackGrenade(grenade.Grenade, position))
                {
                    if (!grenade.IsTracking) // If it's not already being tracked
                        grenade.StartTracking(); // Start tracking the grenade
                }
                else
                {
                    if (grenade.IsTracking) // If it was being tracked but is now out of range
                        grenade.StopTracking(); // Stop tracking the grenade
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
        var distanceSqr = (position - _camera.transform.position).sqrMagnitude;
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
        }
    }
}

internal class TrackedGrenade : MonoBehaviour
{
    private const float ExpireTimeoutDuration = 10f;
    internal static Sprite GrenadeSprite;
    internal static Sprite CompassSprite;
    internal static Sprite CompassOverlaySprite;

    internal static readonly List<TrackedGrenade> TrackedGrenades = new();
    private static Grenade _closestGrenade;
    private Camera _camera;

    private float _compassAlpha; //transition alpha

    // NEW: Store references to compass and overlay
    private GUIObject _compassIndicator;
    private GUIObject _compassOverlay;

    private float _distance;
    private float _expireTime;
    private GUIObject _indicator;
    private Vector3 _position;
    private float _targetDistanceAlpha; //target distance alpha
    private TrailRenderer _trailRenderer;
    public bool IsTracking { get; private set; }

    public Grenade Grenade { get; private set; }

    private void Awake()
    {
        TrackedGrenades.Add(this);
        _expireTime = Time.time + ExpireTimeoutDuration;
        Grenade = GetComponent<Grenade>();
        _camera = Camera.main;

        CreateWorldIndicator();
        SetupTrailRenderer();

        CreateCompassIndicator();
        CreateCompassOverlay();
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

    public static void OnGUI()
    {
        foreach (var trackedGrenade in TrackedGrenades)
        {
            if (trackedGrenade._compassIndicator != null && trackedGrenade._compassIndicator.Enabled)
                DebugGizmos.DrawScreenImage(trackedGrenade._compassIndicator.Sprite,
                    trackedGrenade._compassIndicator.Scale, trackedGrenade._compassIndicator.Rotation,
                    trackedGrenade._compassIndicator.Color);

            if (trackedGrenade._compassOverlay != null && trackedGrenade._compassOverlay.Enabled)
                DebugGizmos.DrawScreenImage(trackedGrenade._compassOverlay.Sprite, trackedGrenade._compassOverlay.Scale,
                    trackedGrenade._compassOverlay.Rotation, trackedGrenade._compassOverlay.Color);
        }
    }

    public void StartTracking()
    {
        IsTracking = true;
    }

    public void StopTracking()
    {
        IsTracking = false;
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

        var canSee = !Settings.RequireLos.Value ||
                     !Physics.Raycast(_camera.transform.position, direction, _distance,
                         LayerMaskClass.HighPolyWithTerrainMaskAI);

        _indicator.Enabled = true;

        var normalizedDistance = Mathf.Clamp01(_distance / Settings.MaxTrackingDistance.Value);
        var minScale = Settings.IndicatorSize.Value * 0.65f;
        _indicator.Scale = Mathf.Lerp(minScale, Settings.IndicatorSize.Value, 1f - normalizedDistance);

        var targetAlpha = _distance <= Settings.MaxTrackingDistance.Value && canSee
            ? 1f - normalizedDistance
            : 0f;

        var fadeSpeed = 0.1f;
        var currentAlpha = _indicator.Color.a;
        var newAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, fadeSpeed * Time.deltaTime);

        _indicator.Color = new Color(_indicator.Color.r, _indicator.Color.g, _indicator.Color.b, newAlpha);

        if (newAlpha <= 0f) _indicator.Enabled = false;
    }

    private void CreateCompassIndicator()
    {
        if (CompassSprite != null)
        {
            _compassIndicator =
                DebugGizmos.CreateScreenImage(CompassSprite, Settings.CompassSize.Value);

            _compassIndicator.Enabled = false;
        }
    }

    private void CreateCompassOverlay()
    {
        if (CompassOverlaySprite != null)
        {
            _compassOverlay =
                DebugGizmos.CreateScreenImage(CompassOverlaySprite, Settings.CompassSize.Value);

            _compassOverlay.Enabled = false;
        }
    }

    private void UpdateCompass()
    {
        if (!Settings.EnableCompass.Value || Grenade == null)
        {
            SetCompassEnabled(false);
            return;
        }

        var grenadeInRange = Vector3.Distance(_camera.transform.position, Grenade.transform.position) <=
                             Settings.MaxTrackingDistance.Value;
        var fadeSpeed = 2f * Time.deltaTime; // Adjust for faster/slower fading

        if (grenadeInRange)
        {
            _compassAlpha = Mathf.MoveTowards(_compassAlpha, 1f, fadeSpeed); // Fade in
            UpdateCompassRotation(_compassIndicator);
            UpdateCompassRotation(_compassOverlay);

            SetTargetDistanceAlpha(); // set the target distance alpha to new value
            if (_compassAlpha > 0.01f)
                _compassIndicator.Enabled = true;
            else
                _compassIndicator.Enabled = false;

            _compassOverlay.Enabled = _compassAlpha > 0.01f;

            if (_compassIndicator != null)
                _compassIndicator.Color = new Color(_compassIndicator.Color.r, _compassIndicator.Color.g,
                    _compassIndicator.Color.b, _compassAlpha);

            if (_compassOverlay != null)
            {
                var distanceFadeSpeed = 5f * Time.deltaTime; // adjust this to fine tune speed of distance fading
                var currentDistanceAlpha =
                    _compassOverlay.Color.a; // get current distance alpha from color value of overlay
                var newDistanceAlpha =
                    Mathf.MoveTowards(currentDistanceAlpha, _targetDistanceAlpha,
                        distanceFadeSpeed); // interpolate the alpha value to the new target
                var combinedAlpha = _compassAlpha * newDistanceAlpha; // Combine transition and distance alphas
                _compassOverlay.Color = new Color(_compassOverlay.Color.r, _compassOverlay.Color.g,
                    _compassOverlay.Color.b, combinedAlpha);
            }
        }
        else
        {
            _compassAlpha = Mathf.MoveTowards(_compassAlpha, 0f, fadeSpeed); // Fade out
            if (_compassIndicator != null)
                _compassIndicator.Color = new Color(_compassIndicator.Color.r, _compassIndicator.Color.g,
                    _compassIndicator.Color.b, _compassAlpha);

            if (_compassOverlay != null)
            {
                SetTargetDistanceAlpha(); // set the target distance alpha to new value
                var distanceFadeSpeed = 5f * Time.deltaTime; // adjust this to fine tune speed of distance fading
                var currentDistanceAlpha = _compassOverlay.Color.a;
                var newDistanceAlpha =
                    Mathf.MoveTowards(currentDistanceAlpha, _targetDistanceAlpha,
                        distanceFadeSpeed); // interpolate the alpha value to the new target
                var combinedAlpha = _compassAlpha * newDistanceAlpha; // Combine transition and distance alphas
                _compassOverlay.Color = new Color(_compassOverlay.Color.r, _compassOverlay.Color.g,
                    _compassOverlay.Color.b, combinedAlpha);
            }

            if (_compassAlpha < 0.01f)
            {
                _compassIndicator.Enabled = false;
                _compassOverlay.Enabled = false;
            }
        }
    }

    private void UpdateCompassRotation(GUIObject compass)
    {
        var grenadePos = transform.position;
        var playerPos = _camera.transform.position;

        var directionToGrenade = (grenadePos - playerPos).normalized;
        directionToGrenade.y = 0f;

        var targetRotation = Vector3.SignedAngle(_camera.transform.forward, directionToGrenade, Vector3.up);
        compass.Rotation = targetRotation;
    }

    private void SetTargetDistanceAlpha()
    {
        var distanceToGrenade = Vector3.Distance(_camera.transform.position, Grenade.transform.position);
        var maxDangerDistance = 7f;

        var normalizedDistance = Mathf.Clamp01(distanceToGrenade / maxDangerDistance);

        var targetAlpha = 1f - normalizedDistance;

        if (distanceToGrenade > maxDangerDistance) targetAlpha = 0f; //Make fully transparent if out of range.
        _targetDistanceAlpha = targetAlpha;
    }

    private void SetCompassEnabled(bool enabled)
    {
        if (_compassIndicator != null)
            _compassIndicator.Enabled = enabled;

        if (_compassOverlay != null)
            _compassOverlay.Enabled = enabled;
    }

    public void Dispose()
    {
        SetCompassEnabled(false);

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