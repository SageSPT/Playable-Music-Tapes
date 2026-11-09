using System.Reflection;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace PlayableMusicTapes.Client;

internal class GameEndPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(TarkovApplication), nameof(TarkovApplication.OnGameEnd));
    }

    [PatchPrefix]
    private static void Prefix()
    {
        TapeRecorder.OnRaidEnded();
    }
}
