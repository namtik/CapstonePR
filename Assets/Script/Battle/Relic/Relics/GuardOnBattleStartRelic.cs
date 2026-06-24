using UnityEngine;

namespace Battle.Relic
{
    // 호신부 (10002): 매 전투 시작 시 방어도를 얻는다.
    [CreateAssetMenu(menuName = "Relic/GuardOnBattleStart", fileName = "GuardOnBattleStartRelic")]
    public class GuardOnBattleStartRelic : RelicSO
    {
        [Header("효과")]
        [Tooltip("전투 진입 시 얻을 방어도")]
        public int guardAmount = 5; // 획득 방어도

        public override void OnBattleStart(IRelicBattleContext ctx)
        {
            ctx.AddPlayerGuard(guardAmount);
            ctx.PulseRelic(this, $"유물 {DisplayLabel} 발동", null);
        }
    }
}
