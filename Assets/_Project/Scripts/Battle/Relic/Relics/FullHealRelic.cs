using UnityEngine;

namespace Battle.Relic
{
    // 주작의 깃털 (10012): 획득 시, 모든 체력을 회복한다.
    [CreateAssetMenu(menuName = "Relic/FullHeal", fileName = "FullHealRelic")]
    public class FullHealRelic : RelicSO
    {
        public override void OnAcquired(IRelicRunContext run) => run.HealPlayerFull();
    }
}
