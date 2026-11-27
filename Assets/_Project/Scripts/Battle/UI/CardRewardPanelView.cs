using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Battle.UI
{
    // 부적(카드) 보상창 프리팹의 참조 묶음 — 배치·이미지는 프리팹에서, 카드 생성·선택 처리는 CardRewardUI가 한다.
    public class CardRewardPanelView : MonoBehaviour
    {
        [Tooltip("카드가 들어갈 슬롯(왼쪽부터). 슬롯 크기에 맞춰 카드가 비율 유지로 맞춰진다. 제시 장수보다 많으면 남는 슬롯은 숨김.")]
        [SerializeField] private List<RectTransform> cardSlots = new List<RectTransform>();
        [Tooltip("선택하지 않고 넘어가기 버튼.")]
        [SerializeField] private Button skipButton;

        public IReadOnlyList<RectTransform> CardSlots => cardSlots;
        public Button SkipButton => skipButton;
    }
}
