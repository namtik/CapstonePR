using UnityEngine;
using UnityEngine.UI;

namespace Battle.UI
{
    // 인벤토리 카드(셀 래퍼)에 붙어, 마우스 오버 시 "그 자리에서" 확대(팝업)되는 컴포넌트.
    // EventSystem raycast(Dim/마스크/캔버스 정렬 등)에 영향받지 않도록 매 프레임 마우스 위치로 직접 판정한다.
    // GridLayout 레이아웃을 흩트리지 않도록 localScale만 키우고, 임시 Canvas(overrideSorting)로 이웃 위에 그린다.
    [RequireComponent(typeof(RectTransform))]
    public class InventoryCardSlot : MonoBehaviour
    {
        float _hoverScale = 1.35f;       // 호버 시 배율
        int _hoverSortingOrder = 200;    // 호버 시 정렬 순서(인벤토리 캔버스보다 높게)

        RectTransform _rect;             // 자신의 RectTransform
        RectTransform _clipRect;         // 스크롤 뷰포트(마스크) — 이 영역 안에서만 호버 허용
        Camera _eventCam;                // 좌표 변환용 카메라(Overlay면 null)
        Vector3 _baseScale = Vector3.one; // 평상시 스케일
        bool _baseScaleCaptured;         // 평상시 스케일 캡처 여부
        bool _hovering;                  // 현재 호버 중 여부
        bool _inPlace;                   // 덱/묘지 보기처럼: 살짝 확대만, 위로 끌어올리거나 밀지 않음(스크롤 마스크 안에 머묾)
        Vector3 _baseLocalPos;           // 호버 직전 위치(클립 영역 안으로 밀어낸 걸 되돌릴 때 사용)
        Canvas _tempCanvas;              // 호버 시 위로 올리는 임시 캔버스
        GraphicRaycaster _tempRaycaster; // 임시 캔버스용 레이캐스터

        public event System.Action<bool> HoverChanged; // 호버 진입(true)/종료(false) 알림

        // 호버 배율/정렬 순서/클립 영역 설정(인벤토리 탭에서 주입)
        public void Configure(float hoverScale, int hoverSortingOrder, RectTransform clipRect)
        {
            _hoverScale = hoverScale > 0f ? hoverScale : 1f;
            _hoverSortingOrder = hoverSortingOrder;
            _clipRect = clipRect;
            _inPlace = false;
        }

        // 덱/묘지 보기와 같은 제자리 호버 — 임시 캔버스를 쓰지 않아 스크롤 영역 밖으로 나가지 않는다
        public void ConfigureInPlace(float hoverScale, RectTransform clipRect)
        {
            _hoverScale = hoverScale > 0f ? hoverScale : 1f;
            _clipRect = clipRect;
            _inPlace = true;
        }

        void Awake()
        {
            _rect = GetComponent<RectTransform>();
        }

        // 활성화 시 좌표 변환용 카메라 결정(Overlay 캔버스는 null)
        void OnEnable()
        {
            Canvas c = GetComponentInParent<Canvas>();
            _eventCam = (c != null && c.renderMode != RenderMode.ScreenSpaceOverlay) ? c.worldCamera : null;
        }

        // 매 프레임 마우스가 이 셀 위에 있는지 직접 판정 → 호버 진입/종료 처리
        void Update()
        {
            // 레이아웃/스케일이 안정된 뒤 평상시 스케일을 캡처
            if (!_baseScaleCaptured)
            {
                _baseScale = _rect.localScale;
                _baseScaleCaptured = true;
            }

            // 호버 중엔 판정 전에 원래 자리로 되돌린다(밀어낸 위치로 판정하지 않게)
            if (_hovering) _rect.localPosition = _baseLocalPos;

            // 카드 위 + (있으면) 스크롤 뷰포트 안일 때만 호버 — 스크롤로 잘려 안 보이는 카드는 제외
            Vector2 mouse = Input.mousePosition;
            bool over = RectTransformUtility.RectangleContainsScreenPoint(_rect, mouse, _eventCam)
                        && (_clipRect == null || RectTransformUtility.RectangleContainsScreenPoint(_clipRect, mouse, _eventCam));
            if (over == _hovering)
            {
                if (_hovering) KeepInsideClip();
                return;
            }
            _hovering = over;
            if (over) _baseLocalPos = _rect.localPosition;

            _rect.localScale = over ? _baseScale * _hoverScale : _baseScale;
            if (over) KeepInsideClip();
            else _rect.localPosition = _baseLocalPos;
            BringToFront(over);
            HoverChanged?.Invoke(over);
        }

        static readonly Vector3[] s_cardCorners = new Vector3[4];
        static readonly Vector3[] s_clipCorners = new Vector3[4];

        // 확대된 카드가 스크롤 뷰포트 밖(탭·상단 영역)으로 튀어나가지 않도록 안쪽으로 민다.
        // 임시 캔버스(overrideSorting)는 마스크에 잘리지 않으므로 위치로 보정한다.
        void KeepInsideClip()
        {
            if (_clipRect == null || _inPlace) return;
            _rect.GetWorldCorners(s_cardCorners);   // 0=좌하, 2=우상
            _clipRect.GetWorldCorners(s_clipCorners);

            float dx = 0f, dy = 0f;
            if (s_cardCorners[2].y > s_clipCorners[2].y) dy = s_clipCorners[2].y - s_cardCorners[2].y;      // 위로 넘침 → 아래로
            else if (s_cardCorners[0].y < s_clipCorners[0].y) dy = s_clipCorners[0].y - s_cardCorners[0].y; // 아래로 넘침 → 위로
            if (s_cardCorners[0].x < s_clipCorners[0].x) dx = s_clipCorners[0].x - s_cardCorners[0].x;      // 왼쪽 넘침 → 오른쪽
            else if (s_cardCorners[2].x > s_clipCorners[2].x) dx = s_clipCorners[2].x - s_cardCorners[2].x; // 오른쪽 넘침 → 왼쪽

            if (dx != 0f || dy != 0f) _rect.position += new Vector3(dx, dy, 0f);
        }

        // 호버 카드가 이웃 카드 위에 그려지도록 임시 캔버스 on/off
        // (UnityEngine.Object에 ?? / ?. 를 쓰면 가짜-null을 우회해 MissingComponentException이 나므로
        //  반드시 명시적 == null / != null 로 검사한다)
        void BringToFront(bool on)
        {
            if (_inPlace) return;
            if (on)
            {
                if (_tempCanvas == null)
                {
                    _tempCanvas = GetComponent<Canvas>();
                    if (_tempCanvas == null) _tempCanvas = gameObject.AddComponent<Canvas>();
                }
                if (_tempRaycaster == null)
                {
                    _tempRaycaster = GetComponent<GraphicRaycaster>();
                    if (_tempRaycaster == null) _tempRaycaster = gameObject.AddComponent<GraphicRaycaster>();
                }

                if (_tempCanvas != null)
                {
                    _tempCanvas.overrideSorting = true;
                    _tempCanvas.sortingOrder = _hoverSortingOrder;
                }
            }
            else
            {
                if (_tempCanvas != null) _tempCanvas.overrideSorting = false;
            }
        }

        // 비활성/파괴 시 확대 상태가 남지 않도록 정리
        void OnDisable()
        {
            bool wasHovering = _hovering;
            if (_hovering) HoverChanged?.Invoke(false);
            _hovering = false;
            if (_baseScaleCaptured && _rect != null) _rect.localScale = _baseScale;
            if (wasHovering && _rect != null) _rect.localPosition = _baseLocalPos;
            if (_tempCanvas != null) _tempCanvas.overrideSorting = false;
        }
    }
}
