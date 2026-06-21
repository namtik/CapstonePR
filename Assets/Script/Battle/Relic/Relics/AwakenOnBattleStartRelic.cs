using UnityEngine;

namespace Battle.Relic
{
    // 황룡옥적 (10010): 매 전투 시작 시 각성 상태가 된다.
    [CreateAssetMenu(menuName = "Relic/AwakenOnBattleStart", fileName = "AwakenOnBattleStartRelic")]
    public class AwakenOnBattleStartRelic : RelicSO
    {
        public override void OnBattleStart(IRelicBattleContext ctx)
        {
            ctx.ForceActivateAwaken();   // 플래그 동기 설정 → StartBattle이 시작 드로우 후 발동
            ctx.PulseRelic(this, $"유물 {DisplayLabel} 발동", null);
        }
    }
}
