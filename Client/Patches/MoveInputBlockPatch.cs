using System.Collections.Generic;
using System.Reflection;
using EFT;
using EFT.InputSystem;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace PlayableMusicTapes.Client;

internal class MoveInputBlockPatch : ModulePatch
{
    private static readonly HashSet<ECommand> Blocked = new HashSet<ECommand> { ECommand.ScrollNext, ECommand.ScrollPrevious };

    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(MoveInputTranslator), nameof(MoveInputTranslator.TranslateCommand));
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
