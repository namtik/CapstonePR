using UnityEngine;

namespace Battle.Relic
{
    // 망각의 붓 (10017): 획득 시, 현재 보유한 카드를 랜덤한 카드로 모두 변환한다.
    [CreateAssetMenu(menuName = "Relic/RandomizeDeck", fileName = "RandomizeDeckRelic")]
    public class RandomizeDeckRelic : RelicSO
    {
        public override void OnAcquired(IRelicRunContext run) => run.RandomizeAllOwnedCards();
    }
}
