using UnityEngine;

namespace Battle.Relic
{
    // 유물 호리병 (10018): 유물 라운드 진입 시, 제시된 모든 유물을 획득한다.
    // (실제 일괄 지급은 RelicStageController가 GrantsAllRelicsInStage 플래그를 보고 처리)
    [CreateAssetMenu(menuName = "Relic/GrantAllRelics", fileName = "GrantAllRelicsRelic")]
    public class GrantAllRelicsRelic : RelicSO
    {
        public override bool GrantsAllRelicsInStage => true;
    }
}
