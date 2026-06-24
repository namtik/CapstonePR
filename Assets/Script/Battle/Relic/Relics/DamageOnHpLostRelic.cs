using UnityEngine;

namespace Battle.Relic
{
    // 피 묻은 가시 (10019): 체력을 잃을 시, 무작위 적에게 피해를 준다.
    [CreateAssetMenu(menuName = "Relic/DamageOnHpLost", fileName = "DamageOnHpLostRelic")]
    public class DamageOnHpLostRelic : RelicSO
    {
        [Header("효과")]
        [Tooltip("체력 손실 시 무작위 적에게 줄 피해")]
        public int damage = 5; // 반사 피해

        public override void OnPlayerHpLost(IRelicBattleContext ctx, int amount, bool firstThisBattle)
        {
            ctx.DealDamageToRandomEnemy(damage);
        }
    }
}
