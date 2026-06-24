using UnityEngine;

namespace Battle.Relic
{
    // 이무기의 여의주: 각성 종료 후 성공한 콤보 수만큼 각성 게이지를 회복한다.
    [CreateAssetMenu(menuName = "Relic/AwakenGaugeRecover", fileName = "AwakenGaugeRecoverRelic")]
    public class AwakenGaugeRecoverRelic : RelicSO
    {
        // 각성 종료 — 성공 콤보 수만큼 게이지 회복 (즉시 재발동 방지는 컨텍스트가 max-1로 상한)
        public override void OnAwakenEnded(IRelicBattleContext ctx, int successCombos)
        {
            ctx.RecoverAwakenGauge(successCombos);
        }
    }
}
