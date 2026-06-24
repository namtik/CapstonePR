using UnityEngine;

namespace Battle.Relic
{
    // 신선도복 (10016): 매 전투 시작 시 연쇄를 얻는다.
    [CreateAssetMenu(menuName = "Relic/ChainOnBattleStart", fileName = "ChainOnBattleStartRelic")]
    public class ChainOnBattleStartRelic : RelicSO
    {
        [Header("효과")]
        [Tooltip("전투 진입 시 얻을 연쇄 스택")]
        public int chainAmount = 20; // 획득 연쇄

        public override void OnBattleStart(IRelicBattleContext ctx)
        {
            ctx.AddChain(chainAmount);
            ctx.PulseRelic(this, $"유물 {DisplayLabel} 발동", null);
        }
    }
}
