using System.Reflection;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;

namespace PlayableMusicTapes.Client;

internal class RecorderSoundPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(BaseSoundPlayer), nameof(BaseSoundPlayer.PlayClip), new[] { typeof(AudioClip), typeof(int), typeof(float) });
    }

    [PatchPrefix]
    private static bool Prefix(BaseSoundPlayer __instance)
    {
        return !TapeListen.IsMuted(__instance);
    }
}
