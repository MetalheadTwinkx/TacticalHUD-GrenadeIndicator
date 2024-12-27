using System;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using SPT.Reflection.Patching;
using SPT.Reflection.Utils;

namespace TacticalHUD.GrenadeIndicator;

internal class GameEndedPatch : ModulePatch
{
    private static Type _targetClassType;
    private new static readonly ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource(nameof(GameEndedPatch));

    protected override MethodBase GetTargetMethod()
    {
        _targetClassType = PatchConstants.EftTypes.SingleOrDefault(targetClass =>
            !targetClass.IsInterface &&
            !targetClass.IsNested &&
            targetClass.GetMethods().Any(method => method.Name == "LocalRaidEnded") &&
            targetClass.GetMethods().Any(method => method.Name == "ReceiveInsurancePrices")
        );

        if (_targetClassType == null)
        {
            Logger.LogError("Could not find target type for GameEndedPatch.");
            return null;
        }

        return AccessTools.Method(_targetClassType.GetTypeInfo(), "LocalRaidEnded");
    }

    [PatchPostfix]
    public static void Postfix()
    {
        Logger.LogInfo("Raid ended. Destroying Grenade Compass UI.");
        CleanupUI();
    }

    private static void CleanupUI()
    {
        Logger.LogDebug("Clearing loaded sprites.");
        SpriteHandler.ClearSprites();

        Logger.LogDebug("Clearing tracked grenades.");
        TrackedGrenade.TrackedGrenades.Clear();
    }
}