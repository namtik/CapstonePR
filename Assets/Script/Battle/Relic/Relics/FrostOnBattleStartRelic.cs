using UnityEngine;

namespace Battle.Relic
{
    // 서리화 (10008): 전투 진입 시 적에게 빙결을 부여한다.
    [CreateAssetMenu(menuName = "Relic/FrostOnBattleStart", fileName = "FrostOnBattleStartRelic")]
    public class FrostOnBattleStartRelic : RelicSO
    {
        [Header("효과")]
        [Tooltip("전투 진입 시 적에게 부여할 빙결량")]
        public int frostAmount = 7; // 부여 빙결량

        // 전투 진입 — 빙결 부여 + 아이콘 펄스(피드백). 효과는 즉시 적용해 다중 유물에도 누락 없음.
        public override void OnBattleStart(IRelicBattleContext ctx)
        {
            ctx.ApplyFrostToEnemy(frostAmount);
            ctx.PulseRelic(this, $"유물 {DisplayLabel} 발동", null);
        }
    }
}
