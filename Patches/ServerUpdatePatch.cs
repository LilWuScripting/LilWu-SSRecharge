using HarmonyLib;
using ProjectM;

namespace LilWu.SSRecharge.Patches;

[HarmonyPatch(typeof(ServerBootstrapSystem), nameof(ServerBootstrapSystem.OnUpdate))]
internal static class ServerUpdatePatch
{
    [HarmonyPostfix]
    private static void Postfix() => RechargeService.Tick();
}
