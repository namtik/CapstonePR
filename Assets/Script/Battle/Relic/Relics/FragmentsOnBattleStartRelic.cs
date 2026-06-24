using UnityEngine;

namespace Battle.Relic
{
    // 깨진 불상 (10007): 매 전투 시작 시 뽑을 더미에 무작위 파편 카드를 섞어 넣는다.
    [CreateAssetMenu(menuName = "Relic/FragmentsOnBattleStart", fileName = "FragmentsOnBattleStartRelic")]
    public class FragmentsOnBattleStartRelic : RelicSO
    {
        [Header("효과")]
        [Tooltip("전투 진입 시 뽑을 더미에 섞을 파편 카드 수")]
        public int fragmentCount = 5; // 삽입 파편 수

        public override void OnBattleStart(IRelicBattleContext ctx)
        {
            ctx.ShuffleFragmentsIntoDrawPile(fragmentCount);
            ctx.PulseRelic(this, $"유물 {DisplayLabel} 발동", null);
        }
    }
}
