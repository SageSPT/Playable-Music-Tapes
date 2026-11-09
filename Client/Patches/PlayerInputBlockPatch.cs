using System.Collections.Generic;
using System.Reflection;
using EFT;
using EFT.InputSystem;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace PlayableMusicTapes.Client;

internal class PlayerInputBlockPatch : ModulePatch
{
    private static readonly HashSet<ECommand> Blocked = new HashSet<ECommand>
    {
        ECommand.SelectKnife,
        ECommand.SelectFirstPrimaryWeapon,
        ECommand.SelectSecondPrimaryWeapon,
        ECommand.SelectSecondaryWeapon,
        ECommand.QuickSelectSecondaryWeapon,
        ECommand.SelectFastSlot4,
        ECommand.SelectFastSlot5,
        ECommand.SelectFastSlot6,
        ECommand.SelectFastSlot7,
        ECommand.SelectFastSlot8,
        ECommand.SelectFastSlot9,
        ECommand.SelectFastSlot0,
        ECommand.QuickKnifeKick,
        ECommand.ThrowGrenade,
        ECommand.PressThrowGrenade
    };

    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(PlayerInputTranslator), nameof(PlayerInputTranslator.TranslateCommand));
    }

    [PatchPrefix]
    private static bool Prefix(ECommand command, ref InputNode.ETranslateResult __result)
    {
        if (!TapeListen.Holding || !Blocked.Contains(command))
        {
            return true;
        }

        __result = InputNode.ETranslateResult.Block;
        return false;
    }
}
