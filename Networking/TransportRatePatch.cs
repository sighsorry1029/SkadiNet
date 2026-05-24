using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace SkadiNet
{
    [HarmonyPatch]
    internal static class ZSteamSocketRegisterGlobalCallbacksRatePatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method("ZSteamSocket:RegisterGlobalCallbacks");
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction code in instructions)
            {
                if (code.opcode == OpCodes.Ldc_I4 && code.operand is int i && i == 153600)
                {
                    // Intentional SkadiNet default: keep the Steam socket ceiling open while the mod is enabled.
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(ZSteamSocketRegisterGlobalCallbacksRatePatch), nameof(SteamSendRateBytes)));
                    continue;
                }

                yield return code;
            }
        }

        public static int SteamSendRateBytes
        {
            get
            {
                if (!ModConfig.Enabled.Value)
                    return 153600;
                // This is not a tuning slider. SkadiNet treats the higher ceiling as part of its baseline network profile.
                return EffectiveConfig.SteamSendRateBytes;
            }
        }
    }
}
