using System;
using System.Collections.Generic;
using System.Linq;
using Diz.LanguageExtensions;
using EFT;
using EFT.Communications;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;

namespace PlayableMusicTapes.Client;

internal static class TapeDeck
{
    public static bool IsRecorder(Item item)
    {
        return item != null && item.StringTemplateId == TapeCatalog.RecorderTpl;
    }

    public static bool HasTape(Item recorder)
    {
        return TapeIn(recorder) != null;
    }

    public static Item TapeIn(Item recorder)
    {
        return TapeSlot(recorder)?.ContainedItem;
    }

    public static Item RecorderHolding(Item tape)
    {
        if (tape?.CurrentAddress?.Container is Slot slot && slot.ID == TapeCatalog.SlotId && IsRecorder(slot.ParentItem))
        {
            return slot.ParentItem;
        }

        return null;
    }

    public static IEnumerable<Item> Recorders(IEnumerable<Item> items)
    {
        return items.Where(IsRecorder);
    }

    public static bool IsCarried(Player player, Item item)
    {
        ItemAddress address = item?.CurrentAddress;
        Inventory inventory = player?.InventoryController?.Inventory;
        return address != null && inventory != null && address.IsChildOf(inventory.Equipment);
    }

    public static ItemController MenuController()
    {
        ItemUiContext context = ItemUiContext.Instance;
        if (context == null)
        {
            return null;
        }

        return Traverse.Create(context).Field("_itemController").GetValue<ItemController>();
    }

    public static IEnumerable<Item> MenuItems(ItemController controller)
    {
        if (controller is InventoryController inventoryController && inventoryController.Inventory != null)
        {
            return inventoryController.Inventory.GetPlayerItems(EPlayerItems.AllExceptHideoutStashes).ToList();
        }

        return Enumerable.Empty<Item>();
    }

    public static void Load(ItemController controller, Item recorder, Item tape, Action<RecorderAnimation> loaded)
    {
        Slot slot = TapeSlot(recorder);
        if (slot == null || controller == null)
        {
            Log.Warning("Tapes", "recorder has no tape slot or no inventory controller");
            return;
        }

        Item loadedTape = slot.ContainedItem;
        if (loadedTape == tape)
        {
            loaded(RecorderAnimation.PlayOnly);
            return;
        }

        try
        {
            if (loadedTape == null)
            {
                var move = ItemManipulator.Move(tape, slot.CreateItemAddress(), controller, simulate: true);
                RunMove(controller, move, () => loaded(RecorderAnimation.Insert));
            }
            else
            {
                var swap = ItemManipulator.Swap(tape, slot.CreateItemAddress(), loadedTape, tape.CurrentAddress, controller, simulate: true);
                RecorderTapeLabel.KeepUntilSwapped(loadedTape);
                RunMove(controller, swap, () => loaded(RecorderAnimation.EjectAndInsert));
            }
        }
        catch (Exception error)
        {
            Log.Error("Tapes", "loading the tape into the recorder failed: " + error);
        }
    }

    public static bool Eject(InventoryController controller, Item recorder, Action ejected)
    {
        Item tape = TapeIn(recorder);
        if (tape == null || controller?.Inventory?.Equipment == null)
        {
            return false;
        }

        var searchIn = new CompoundItem[] { controller.Inventory.Equipment };
        var place = ItemManipulator.QuickFindAppropriatePlace(tape, controller, searchIn, ItemManipulator.EMoveItemOrder.PickUp, simulate: true);
        if (place.Failed)
        {
            TapeNotificationViews.Warn("Tapes/NoSpace", "No space for the tape");
            return false;
        }

        RunMove(controller, place, ejected);
        return true;
    }

    private static Slot TapeSlot(Item recorder)
    {
        if (recorder is CompoundItem compound)
        {
            return compound.Slots.FirstOrDefault(slot => slot.ID == TapeCatalog.SlotId);
        }

        return null;
    }

    private static void RunMove<T>(ItemController controller, OperationResult<T> operation, Action done) where T : IOperationResult
    {
        if (operation.Failed)
        {
            Log.Warning("Tapes", "tape could not be moved into the recorder: " + operation.Error);
            NotificationManager.DisplayWarningNotification("The tape doesn't fit in this player");
            return;
        }

        controller.TryRunNetworkTransaction(operation, result =>
        {
            if (result.Succeed)
            {
                done();
            }
            else
            {
                Log.Warning("Tapes", "tape move rejected: " + result.Error);
            }
        });
    }
}
