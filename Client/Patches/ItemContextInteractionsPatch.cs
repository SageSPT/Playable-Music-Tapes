using System;
using System.Reflection;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace PlayableMusicTapes.Client;

internal class ItemContextInteractionsPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(ItemUiContext), nameof(ItemUiContext.GetItemContextInteractions));
    }

    [PatchPostfix]
    private static void Postfix(ItemContext itemContext, ContextInteractions<EItemInfoButton> __result)
    {
        Item item = itemContext?.Item;
        if (__result?._dynamicInteractions == null || item == null)
        {
            return;
        }

        bool isRecorder = TapeDeck.IsRecorder(item);
        if (!isRecorder && !TapeCatalog.IsTape(item))
        {
            return;
        }

        try
        {
            bool canPlay = !isRecorder || TapeDeck.HasTape(item);
            if (canPlay)
            {
                AddButton(__result, "PLAYTAPE", "Icon_playtape", () => TapeRecorder.PlayFromMenu(item));
            }

            if (TapeRecorder.IsPlaying)
            {
                AddButton(__result, "NEXTSONG", "Icon_playtape", TapeRecorder.NextFromMenu);
                AddButton(__result, "STOPTAPE", "Icon_stoptape", TapeRecorder.StopFromMenu);
            }
        }
        catch (Exception error)
        {
            Log.Warning("Tapes", "tape menu entries failed: " + error.Message);
        }
    }

    private static void AddButton(ContextInteractions<EItemInfoButton> interactions, string name, string icon, Action onClick)
    {
        if (!interactions._dynamicInteractions.ContainsKey(name))
        {
            interactions.method_2(name, name, onClick, TapeNotificationViews.Load(icon));
        }
    }
}
