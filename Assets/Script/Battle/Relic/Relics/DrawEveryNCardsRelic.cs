using UnityEngine;

namespace Battle.Relic
{
    // 웃는 탈 (10006): 카드를 N장 사용할 때마다 카드를 1장 뽑는다.
    [CreateAssetMenu(menuName = "Relic/DrawEveryNCards", fileName = "DrawEveryNCardsRelic")]
    public class DrawEveryNCardsRelic : RelicSO
    {
        [Header("효과")]
        [Tooltip("이 장수만큼 카드를 사용할 때마다 발동")]
        public int everyN = 3;     // 발동 주기(사용 장수)
        [Tooltip("발동 시 뽑을 카드 수")]
        public int drawCount = 1;  // 드로우 수

        public override void OnCardPlayed(IRelicBattleContext ctx, Battle.Card.CardInstance card, int cardsPlayedThisBattle)
        {
            if (everyN > 0 && cardsPlayedThisBattle % everyN == 0)
                ctx.DrawCards(drawCount);
        }
    }
}
