using System;
using System.Reflection;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace PlayableMusicTapes.Client;

internal class SpawnOperationPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        Type spawn = AccessTools.Inner(typeof(Player.UsableItemController), "SpawnOperation");
        return AccessTools.Method(spawn, "Start", new[] { typeof(Action) });
    }

    [PatchPrefix]
    private static void Prefix(object __instance)
    {
        TapeListen.OnSpawn(__instance);
    }
}
