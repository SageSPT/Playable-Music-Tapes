using System.Reflection;
using EFT.InventoryLogic;
using EFT.UI.DragAndDrop;
using HarmonyLib;
using SPT.Reflection.Patching;
using UnityEngine;

namespace PlayableMusicTapes.Client;

internal class TapeSlotIconPatch : ModulePatch
{
    private const string IconName = "slot_mod_tape";

    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(ModSlotView), nameof(ModSlotView.Show),
            [typeof(Slot), typeof(ItemContext), typeof(ItemController), typeof(EFT.UI.ItemUiContext)]);
    }

    [PatchPostfix]
    private static void Postfix(ModSlotView __instance, Slot slot)
    {
        if (slot == null || slot.Name != TapeCatalog.SlotId)
        {
            return;
        }

        Sprite icon = TapeNotificationViews.Load(IconName);
        if (icon != null)
        {
            __instance.SetSlotBackImage(icon);
        }
    }
}
