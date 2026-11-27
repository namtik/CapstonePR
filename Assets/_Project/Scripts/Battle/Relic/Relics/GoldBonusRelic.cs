using UnityEngine;

namespace Battle.Relic
{
    // 복주머니 (10005): 보상으로 받는 골드량이 증가한다.
    [CreateAssetMenu(menuName = "Relic/GoldBonus", fileName = "GoldBonusRelic")]
    public class GoldBonusRelic : RelicSO
    {
        [Header("효과")]
        [Tooltip("전투 보상 골드 가산량")]
        public int goldBonus = 30; // 골드 가산

        public override int GoldRewardBonus() => goldBonus;
    }
}
