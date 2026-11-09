using System.Reflection;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace PlayableMusicTapes.Client;

internal class SpecialIconPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(ItemViewFactory), nameof(ItemViewFactory.GetSpecialIcon));
    }

    [PatchPostfix]
    private static void Postfix(Item item, ref string __result)
    {
        if (string.IsNullOrEmpty(__result) && TapeDeck.IsRecorder(item))
        {
            __result = "special_item";
        }
    }
}
