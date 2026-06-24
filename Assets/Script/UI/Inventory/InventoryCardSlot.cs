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
        Canvas _tempCanvas;              // 호버 시 위로 올리는 임시 캔버스
        GraphicRaycaster _tempRaycaster; // 임시 캔버스용 레이캐스터

        // 호버 배율/정렬 순서/클립 영역 설정(인벤토리 탭에서 주입)
        public void Configure(float hoverScale, int hoverSortingOrder, RectTransform clipRect)
        {
            _hoverScale = hoverScale > 0f ? hoverScale : 1f;
            _hoverSortingOrder = hoverSortingOrder;
            _clipRect = clipRect;
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

            // 카드 위 + (있으면) 스크롤 뷰포트 안일 때만 호버 — 스크롤로 잘려 안 보이는 카드는 제외
            Vector2 mouse = Input.mousePosition;
            bool over = RectTransformUtility.RectangleContainsScreenPoint(_rect, mouse, _eventCam)
                        && (_clipRect == null || RectTransformUtility.RectangleContainsScreenPoint(_clipRect, mouse, _eventCam));
            if (over == _hovering) return;
            _hovering = over;

            _rect.localScale = over ? _baseScale * _hoverScale : _baseScale;
            BringToFront(over);
        }

        // 호버 카드가 이웃 카드 위에 그려지도록 임시 캔버스 on/off
        // (UnityEngine.Object에 ?? / ?. 를 쓰면 가짜-null을 우회해 MissingComponentException이 나므로
        //  반드시 명시적 == null / != null 로 검사한다)
        void BringToFront(bool on)
        {
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
            _hovering = false;
            if (_baseScaleCaptured && _rect != null) _rect.localScale = _baseScale;
            if (_tempCanvas != null) _tempCanvas.overrideSorting = false;
        }
    }
}
