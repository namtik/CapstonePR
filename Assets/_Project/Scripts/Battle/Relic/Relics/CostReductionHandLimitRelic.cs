using UnityEngine;

namespace Battle.Relic
{
    // 독주 (10013): 모든 카드의 코스트가 줄고, 전투 중 패에 카드를 1장만 보유할 수 있다.
    [CreateAssetMenu(menuName = "Relic/CostReductionHandLimit", fileName = "CostReductionHandLimitRelic")]
    public class CostReductionHandLimitRelic : RelicSO
    {
        [Header("효과")]
        [Tooltip("모든 카드 게이지 코스트 가감(음수=감소). 적용 후 하한 0")]
        public int gaugeCostDelta = -1; // 코스트 가감
        [Tooltip("전투 중 손패 한도 강제값")]
        public int handLimit = 1;       // 손패 한도

        public override int ModifyGaugeCostPerCard() => gaugeCostDelta;
        public override int OverrideHandLimit() => handLimit;
    }
}
