using HarmonyLib;

namespace SkadiNet
{
    internal static partial class ReflectionCache
    {
        private static void InitializeGameplayReflection()
        {
            ZNetViewType = AccessTools.TypeByName("ZNetView");
            MonsterAIType = AccessTools.TypeByName("MonsterAI");
            PlayerType = AccessTools.TypeByName("Player");

            CharacterNViewField = FieldByQualifiedName("Character:m_nview");
            MonsterAINViewField = FieldByQualifiedName("MonsterAI:m_nview");
            ZNetViewZdoField = SilentField(ZNetViewType, "m_zdo");
            PlayerGetPlayerIDMethod = AccessTools.Method(PlayerType, "GetPlayerID");
            ZNetViewGetZDOMethod = AccessTools.Method(ZNetViewType, "GetZDO");
            ZNetViewClaimOwnershipMethod = AccessTools.Method(ZNetViewType, "ClaimOwnership");
        }
    }
}
