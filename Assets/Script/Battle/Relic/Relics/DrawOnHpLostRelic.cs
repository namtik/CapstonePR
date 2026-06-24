using UnityEngine;

namespace Battle.Relic
{
    // 혈묵주 (10003): 체력을 잃을 시, 카드를 뽑는다.
    [CreateAssetMenu(menuName = "Relic/DrawOnHpLost", fileName = "DrawOnHpLostRelic")]
    public class DrawOnHpLostRelic : RelicSO
    {
        [Header("효과")]
        [Tooltip("체력 손실 시 뽑을 카드 수")]
        public int drawCount = 1; // 드로우 수

        public override void OnPlayerHpLost(IRelicBattleContext ctx, int amount, bool firstThisBattle)
        {
            ctx.DrawCards(drawCount);
        }
    }
}
