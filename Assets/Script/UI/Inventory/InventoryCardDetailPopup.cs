using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Battle.Card;

namespace Battle.UI
{
    // 카드 1장의 확대 미리보기 + 이름/설명을 보여주는 상세 팝업
    public class InventoryCardDetailPopup : MonoBehaviour
    {
        [Header("바인딩 (비우면 자식에서 자동 탐색)")]
        [Tooltip("팝업 표시/숨김 대상 루트. 비우면 이 컴포넌트의 GameObject.")]
        [SerializeField] private GameObject popupRoot;        // 팝업 루트
        [Tooltip("확대 표시할 카드 뷰. 비우면 자식에서 NewCardView 자동 탐색.")]
        [SerializeField] private NewCardView cardView;        // 확대 카드 뷰
        [SerializeField] private TMP_Text nameText;           // 카드 이름(선택)
        [SerializeField] private TMP_Text descText;           // 카드 설명(선택)
        [Tooltip("닫기(X) 버튼.")]
        [SerializeField] private Button closeButton;          // 닫기 버튼(선택)
        [Tooltip("배경(딤) 클릭 시 닫기용 버튼.")]
        [SerializeField] private Button backgroundButton;     // 배경 클릭 닫기(선택)

        // 참조 자동 바인딩 및 버튼 연결, 시작 시 숨김
        void Awake()
        {
            if (popupRoot == null) popupRoot = gameObject;
            if (cardView == null) cardView = GetComponentInChildren<NewCardView>(true);
            if (closeButton != null) closeButton.onClick.AddListener(Hide);
            if (backgroundButton != null) backgroundButton.onClick.AddListener(Hide);
            Hide();
        }

        // 카드 데이터로 상세 팝업을 채우고 표시
        public void Show(CardData data)
        {
            if (data == null) return;
            if (cardView != null) cardView.ApplyShopPreview(data);
            if (nameText != null) nameText.text = data.displayName;
            if (descText != null) descText.text = data.description;
            if (popupRoot != null)
            {
                popupRoot.SetActive(true);
                popupRoot.transform.SetAsLastSibling();
            }
        }

        // 팝업을 숨김
        public void Hide()
        {
            if (popupRoot != null) popupRoot.SetActive(false);
        }
    }
}
