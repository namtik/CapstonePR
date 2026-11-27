using UnityEngine;

namespace Battle.Relic
{
    // 불로단 (10000): 획득 시, 최대 체력이 증가한다.
    [CreateAssetMenu(menuName = "Relic/MaxHp", fileName = "MaxHpRelic")]
    public class MaxHpRelic : RelicSO
    {
        [Header("효과")]
        [Tooltip("획득 시 증가할 최대 체력")]
        public int amount = 10; // 최대 체력 증가량

        public override void OnAcquired(IRelicRunContext run) => run.AddMaxHp(amount);
    }
}
