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
    }
}
