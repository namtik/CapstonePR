using UnityEngine;

namespace Battle.Relic
{
    // 복사경 (10015): 매 전투 시작 시, 처음 사용하는 카드의 효과를 한 번 더 발동한다.
    [CreateAssetMenu(menuName = "Relic/FirstCardRecast", fileName = "FirstCardRecastRelic")]
    public class FirstCardRecastRelic : RelicSO
    {
        public override void OnBattleStart(IRelicBattleContext ctx)
        {
            ctx.EnableFirstCardRecast();
        }

        // 첫 카드 사용 전까지 1(대기), 사용 후 숫자 없음
        public override int GetBattleCounter(IRelicBattleContext ctx) => ctx.CardsPlayedThisBattle == 0 ? 1 : -1;
    }
}
