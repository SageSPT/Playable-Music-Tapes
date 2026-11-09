using System.Reflection;
using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace PlayableMusicTapes.Client;

internal class NotificationReturnToPoolPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.PropertyGetter(typeof(BaseNotificationView), nameof(BaseNotificationView.ReturnToPool));
    }

    [PatchPostfix]
    private static void Postfix(BaseNotificationView __instance, ref bool __result)
    {
        if (__result && __instance != null && __instance.GetComponent<TapeNotificationMarker>() != null)
        {
            __result = false;
        }
    }
}
