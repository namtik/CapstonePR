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
            // 방어도 칸을 따로 만들지 않고, 호신부 기록에 올려 호버 시 초록 숫자로 보이게 한다.
            Battle.UI.BattleLog.SetGuardLogging(false);
            Battle.UI.BattleLog.BeginCombatCapture();
            try
            {
                ctx.AddPlayerGuard(guardAmount);
                ctx.PulseRelic(this, $"유물 {DisplayLabel} 발동", null);
            }
            finally
            {
                Battle.UI.BattleLog.SetGuardLogging(true);
                Battle.UI.BattleLog.CancelCombatCapture();
            }
        }
    }
}
