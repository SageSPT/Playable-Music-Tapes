using System.Reflection;
using EFT;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace PlayableMusicTapes.Client;

internal class InteractionStatePatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(InteractionContextHelper), nameof(InteractionContextHelper.GetAvailableInteractionState));
    }

    [PatchPostfix]
    private static void Postfix(GamePlayerOwner owner, Item rootItem, ref AvailableInteractionState __result)
    {
        TapeListen.AddListenAction(owner, rootItem, __result);
    }
}
