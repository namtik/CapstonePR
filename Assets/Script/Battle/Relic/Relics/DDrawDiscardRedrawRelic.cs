using UnityEngine;

namespace Battle.Relic
{
    // 비급서 (10011): 'D 드로우'가 현재 패를 모두 버리고 뽑을 더미에서 5장을 뽑는 효과로 바뀐다.
    // (드로우 동작 자체는 NewBattleController.TryDDraw가 OverridesDDraw 플래그를 보고 처리)
    [CreateAssetMenu(menuName = "Relic/DDrawDiscardRedraw", fileName = "DDrawDiscardRedrawRelic")]
    public class DDrawDiscardRedrawRelic : RelicSO
    {
        [Tooltip("D 드로우 코스트(적 게이지 증가) 추가량. 기본 1에 더해진다.")]
        public int extraGaugeCost = 3;

        public override bool OverridesDDraw => true;
        public override int DDrawExtraGaugeCost => extraGaugeCost;
    }
}
