using System.Reflection;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;

namespace PlayableMusicTapes.Client;

internal class IkProcessPatch : ModulePatch
{
    private static readonly AccessTools.FieldRef<Player, Transform[]> IkTargets = AccessTools.FieldRefAccess<Player, Transform[]>("_ikTargets");
    private static readonly AccessTools.FieldRef<Player, Transform[]> GripReferences = AccessTools.FieldRefAccess<Player, Transform[]>("_gripReferences");

    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(Player), nameof(Player.IkProcess));
    }

    [PatchPrefix]
    [HarmonyPriority(Priority.First)]
    private static void Prefix(Player __instance)
    {
        if (__instance == null || !TapeDeck.IsRecorder(__instance.HandsController?.Item))
        {
            return;
        }

        Transform[] ikTargets = IkTargets(__instance);
        Transform[] handMarkers = GripReferences(__instance);
        if (ikTargets == null || handMarkers == null)
        {
            return;
        }

        for (int i = 0; i < ikTargets.Length && i < handMarkers.Length; i++)
        {
            bool targetHasNoHandMarker = ikTargets[i] != null && handMarkers[i] == null;
            if (targetHasNoHandMarker)
            {
                ikTargets[i] = null;
            }
        }
    }
}
