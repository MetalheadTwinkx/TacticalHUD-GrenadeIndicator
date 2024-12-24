using System.Reflection;
using BepInEx.Logging;
using EFT;
using SPT.Reflection.Patching;

namespace TacticalHUD.GrenadeIndicator;

internal class GameStartPatch : ModulePatch
{
    private new static readonly ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource(nameof(GameStartPatch));

    protected override MethodBase GetTargetMethod()
    {
        return typeof(GameWorld).GetMethod(nameof(GameWorld.OnGameStarted));
    }

    [PatchPostfix]
    public static void PatchPostfix()
    {
        Logger.LogInfo("Raid started. Re-initializing Grenade Compass UI...");
        InitializeUI();
    }

    private static void InitializeUI()
    {
        Logger.LogDebug("Reloading sprites.");
        SpriteHandler.LoadSprites();

        Logger.LogDebug("Creating compass indicators.");
        TrackedGrenade.CompassIndicator =
            DebugGizmos.CreateScreenImage(TrackedGrenade.CompassSprite, Settings.CompassSize.Value);
        TrackedGrenade.CompassOverlayIndicator =
            DebugGizmos.CreateScreenImage(TrackedGrenade.CompassOverlaySprite, Settings.CompassSize.Value);

        SetInitialCompassVisibility(false);
    }

    private static void SetInitialCompassVisibility(bool isVisible)
    {
        Logger.LogDebug($"Setting initial compass visibility: {isVisible}");
        TrackedGrenade.CompassIndicator.Enabled = isVisible;
        TrackedGrenade.CompassOverlayIndicator.Enabled = isVisible;
    }
}