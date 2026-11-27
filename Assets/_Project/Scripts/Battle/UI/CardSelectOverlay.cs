using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Battle.UI
{
    // 카드 선택 효과 공용 오버레이(프리팹) — 배치·이미지는 프리팹, 선택 로직은 CardHandHUD가 담당.
    //   TitleOnly : "부적을 선택하세요" 이미지만(덱/묘지 선택 — 카드 목록은 더미 패널이 표시)
    //   Row       : 딤 + 띠 패널 + 카드 가로 배치(파편 선택)
    //   Slots     : 딤 + 띠 패널 + 빈 칸 + 확인 버튼(패에서 골라 버리기/소멸 등 — 손패는 오버레이 위에 그려짐)
    public class CardSelectOverlay : MonoBehaviour
    {
        public enum Mode { TitleOnly, Row, Slots }

        [Header("구성 요소")]
        [Tooltip("화면 딤(뒤쪽 클릭 차단). TitleOnly에서는 꺼진다(더미 패널이 자체 딤 사용).")]
        [SerializeField] private GameObject dim;
        [Tooltip("가운데 띠 패널(UI_Battle_Select_Panal).")]
        [SerializeField] private GameObject band;
        [Tooltip("상단 \"부적을 선택하세요\" 이미지.")]
        [SerializeField] private GameObject title;
        [Tooltip("효과별 안내 문구(예: 소멸할 카드). 비우면 표시 안 함.")]
        [SerializeField] private TMP_Text promptText;

        [Header("Row 모드 (파편 선택)")]
        [Tooltip("카드가 가로로 깔릴 컨테이너(가운데 기준).")]
        [SerializeField] private RectTransform cardRow;
        [Tooltip("카드 중심 간 간격(px).")]
        [SerializeField] private float rowSpacing = 450f;

        [Header("Slots 모드 (패에서 선택)")]
        [Tooltip("빈 칸(UI_Battle_Select_Cardback) 버튼. 첫 번째 칸이 템플릿 — 고를 장수만큼 복제해 가로로 가운데 정렬한다. 채워진 칸을 누르면 해제.")]
        [SerializeField] private List<Button> slots = new List<Button>();
        [Tooltip("칸 중심 간 간격(px).")]
        [SerializeField] private float slotSpacing = 420f;
        [Tooltip("칸에 들어간 카드의 표시 크기(px). 칸 틀을 덮도록 칸보다 약간 크게.")]
        [SerializeField] private Vector2 slotCardSize = new Vector2(248f, 317f);
        [Tooltip("확인 버튼(UI_Battle_Select_BT).")]
        [SerializeField] private Button confirmButton;

        public RectTransform CardRow => cardRow;
        public float RowSpacing => rowSpacing;
        public IReadOnlyList<Button> Slots => slots;
        public Vector2 SlotCardSize => slotCardSize;

        readonly List<Button> _activeSlots = new List<Button>(); // 이번 선택에 쓰는 칸
        Vector2 _slotCenter;   // 템플릿 칸의 원래 위치(가로 정렬 기준)
        bool _slotCenterCaptured;

        // 고를 장수만큼 칸을 준비(부족하면 첫 칸을 복제)해 가로 가운데 정렬로 배치하고 반환
        public IReadOnlyList<Button> PrepareSlots(int count)
        {
            _activeSlots.Clear();
            if (slots.Count == 0 || slots[0] == null) return _activeSlots;

            var template = slots[0];
            var trt = (RectTransform)template.transform;
            if (!_slotCenterCaptured) { _slotCenter = trt.anchoredPosition; _slotCenterCaptured = true; }

            count = Mathf.Max(1, count);
            while (slots.Count < count)
            {
                var clone = Instantiate(template, trt.parent);
                clone.name = "Slot" + slots.Count;
                // 복제본에 남은 카드 미리보기 등 자식 제거
                for (int c = clone.transform.childCount - 1; c >= 0; c--)
                    Destroy(clone.transform.GetChild(c).gameObject);
                slots.Add(clone);
            }

            float startX = _slotCenter.x - slotSpacing * (count - 1) * 0.5f;
            for (int i = 0; i < slots.Count; i++)
            {
                var b = slots[i];
                if (b == null) continue;
                bool on = i < count;
                b.gameObject.SetActive(on);
                if (!on) continue;
                ((RectTransform)b.transform).anchoredPosition = new Vector2(startX + slotSpacing * i, _slotCenter.y);
                _activeSlots.Add(b);
            }
            return _activeSlots;
        }
        public Button ConfirmButton => confirmButton;

        void Awake()
        {
            // 비활성(칸 미완성) 색이 반투명이면 뒤 띠 선이 비쳐 보이므로 알파는 항상 1
            if (confirmButton != null && confirmButton.transition == Selectable.Transition.ColorTint)
            {
                var colors = confirmButton.colors;
                Color d = colors.disabledColor;
                d.a = 1f;
                colors.disabledColor = d;
                confirmButton.colors = colors;
            }
        }

        // 모드에 맞춰 구성 요소 표시 전환
        public void Show(Mode mode, string prompt)
        {
            gameObject.SetActive(true);

            bool full = mode != Mode.TitleOnly;
            if (dim != null) dim.SetActive(full);
            if (band != null) band.SetActive(full);
            if (title != null) title.SetActive(true);
            if (cardRow != null) cardRow.gameObject.SetActive(mode == Mode.Row);

            bool slotsOn = mode == Mode.Slots;
            // 칸 표시는 PrepareSlots가 장수에 맞춰 켠다
            if (!slotsOn)
                for (int i = 0; i < slots.Count; i++)
                    if (slots[i] != null) slots[i].gameObject.SetActive(false);
            if (confirmButton != null) confirmButton.gameObject.SetActive(slotsOn);

            if (promptText != null)
            {
                bool hasPrompt = !string.IsNullOrEmpty(prompt);
                promptText.gameObject.SetActive(hasPrompt);
                if (hasPrompt) promptText.text = prompt;
            }
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        // i번째 카드의 가로 위치(가운데 정렬)
        public Vector2 RowPosition(int i, int count)
        {
            float startX = -rowSpacing * (count - 1) * 0.5f;
            return new Vector2(startX + rowSpacing * i, 0f);
        }
    }
}
