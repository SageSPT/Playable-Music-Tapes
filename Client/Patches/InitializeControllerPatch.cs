using System.Reflection;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace PlayableMusicTapes.Client;

internal class InitializeControllerPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(Player.UsableItemController), nameof(Player.UsableItemController.InitializeController));
    }

    [PatchPrefix]
    private static void Prefix(Player.UsableItemController __instance, Player player, WeaponPrefab weaponPrefab)
    {
        TapeListen.PrepareRecorderRig(__instance, player, weaponPrefab);
    }

    [PatchPostfix]
    private static void Postfix(Player.UsableItemController __instance, Player player)
    {
        TapeListen.InitRecorderSounds(__instance, player);
    }
}
