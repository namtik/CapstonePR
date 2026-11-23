using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Battle.UI
{
    // 화면 우하단 드로우 버튼 — 클릭 시 D 키와 동일하게 D 드로우 실행
    // 씬 배치: Image(드로우 버튼 스프라이트) 오브젝트에 이 컴포넌트를 붙이면 Button은 자동으로 추가된다.
    [RequireComponent(typeof(Image))]
    [DisallowMultipleComponent]
    public class DrawButtonUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("참조 (비우면 자동)")]
        [Tooltip("비우면 NewBattleController.Instance 사용")]
        public NewBattleController controller; // 전투 컨트롤러
        [Tooltip("버튼 좌상단 코스트 숫자(적 게이지 증가량). 비우면 표시 안 함")]
        public TMP_Text costText; // 코스트 표시 텍스트
        [Tooltip("옆에 붙는 'D' 키 아이콘 등 — 사용 불가 상태에서 같이 흐려질 그래픽")]
        public Graphic[] extraGraphics; // 함께 흐려질 그래픽들

        [Header("상태 색")]
        public Color normalColor = Color.white; // 사용 가능
        public Color hoverColor = new Color(1f, 0.95f, 0.8f, 1f); // 호버
        public Color disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.8f); // 사용 불가

        [Header("호버 확대")]
        public bool hoverScaleEnabled = true; // 호버 확대 사용 여부
        [Range(1f, 1.3f)] public float hoverScale = 1.06f; // 호버 확대 배율

        static readonly System.Collections.Generic.List<DrawButtonUI> _active =
            new System.Collections.Generic.List<DrawButtonUI>(); // 활성 드로우 버튼들

        // 적 행동 중 덱·묘지와 함께 숨길 대상(버튼 자신 + 자식이 아닌 D 키 아이콘 등)
        public static void CollectHideTargets(System.Collections.Generic.List<GameObject> into)
        {
            foreach (var b in _active)
            {
                if (b == null) continue;
                if (!into.Contains(b.gameObject)) into.Add(b.gameObject);
                if (b.extraGraphics == null) continue;
                foreach (var g in b.extraGraphics)
                {
                    if (g == null || g.transform.IsChildOf(b.transform)) continue;
                    if (!into.Contains(g.gameObject)) into.Add(g.gameObject);
                }
            }
        }

        void OnEnable()
        {
            if (!_active.Contains(this)) _active.Add(this);
        }

        Image _image;
        Button _button;
        Vector3 _baseScale;
        bool _hover;
        bool _lastUsable = true;
        int _lastCost = -1;

        void Awake()
        {
            _image = GetComponent<Image>();
            _button = GetComponent<Button>();
            if (_button == null) _button = gameObject.AddComponent<Button>();
            _button.transition = Selectable.Transition.None; // 색은 이 컴포넌트가 직접 관리
            _button.targetGraphic = _image;
            _button.onClick.AddListener(OnClick);
            _baseScale = transform.localScale;
            ApplyVisual(true);
        }

        void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(OnClick);
        }

        void OnDisable()
        {
            _active.Remove(this);
            _hover = false;
            transform.localScale = _baseScale;
        }

        NewBattleController Ctrl => controller != null ? controller : NewBattleController.Instance;

        void Update()
        {
            var ctrl = Ctrl;
            bool usable = ctrl != null && ctrl.CanDDraw;
            if (usable != _lastUsable) ApplyVisual(usable);

            if (costText != null && ctrl != null)
            {
                int cost = ctrl.DDrawGaugeCost;
                if (cost != _lastCost)
                {
                    _lastCost = cost;
                    costText.text = cost.ToString();
                }
            }
        }

        void OnClick()
        {
            var ctrl = Ctrl;
            if (ctrl == null) return;
            ctrl.RequestDDraw();
            // 클릭 후 UI 포커스가 남아 스페이스/엔터로 재발동되지 않도록 해제
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hover = true;
            ApplyVisual(_lastUsable);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hover = false;
            ApplyVisual(_lastUsable);
        }

        void ApplyVisual(bool usable)
        {
            _lastUsable = usable;
            _button.interactable = usable;

            Color c = !usable ? disabledColor : (_hover ? hoverColor : normalColor);
            _image.color = c;
            if (extraGraphics != null)
                foreach (var g in extraGraphics)
                    if (g != null) g.color = usable ? normalColor : disabledColor;

            float mul = (usable && _hover && hoverScaleEnabled) ? Mathf.Max(1f, hoverScale) : 1f;
            transform.localScale = _baseScale * mul;
        }
    }
}
