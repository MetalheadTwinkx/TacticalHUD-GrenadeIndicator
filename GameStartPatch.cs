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
    }
}