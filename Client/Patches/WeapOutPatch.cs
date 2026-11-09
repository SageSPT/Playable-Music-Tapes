using System.Reflection;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace PlayableMusicTapes.Client;

internal class WeapOutPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(Player.UsableItemController), "IEventsConsumerOnWeapOut");
    }

    [PatchPostfix]
    private static void Postfix(object __instance)
    {
        TapeListen.OnWeapOut(__instance as Player.UsableItemController);
    }
}
