using System;
using System.Reflection;
using HarmonyLib;

namespace SkadiNet
{
    [HarmonyPatch]
    internal static class MonsterAISetTargetOwnershipPatch
    {
        private static MethodBase TargetMethod()
        {
            var monsterAI = AccessTools.TypeByName("MonsterAI");
            var character = AccessTools.TypeByName("Character");
            return AccessTools.Method(monsterAI, "SetTarget", character != null ? new[] { character } : null)
                   ?? AccessTools.Method("MonsterAI:SetTarget");
        }

        private static void Postfix(object __instance, object[] __args)
        {
            if (__args == null || __args.Length == 0) return;
            ClientStutterGuard.MarkCombatWindow();
            OwnershipManager.TryTransferCombatOwnership(__instance, __args[0]);
        }
    }
}
