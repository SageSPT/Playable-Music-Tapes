using System;
using System.Reflection;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace PlayableMusicTapes.Client;

internal class GameStartedPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(GameWorld), nameof(GameWorld.OnGameStarted));
    }

    [PatchPostfix]
    private static void Postfix(GameWorld __instance)
    {
        try
        {
            TapeRecorder.OnRaidStarted(__instance);
        }
        catch (Exception error)
        {
            Log.Error("Tapes", "raid start: " + error);
        }
    }
}
