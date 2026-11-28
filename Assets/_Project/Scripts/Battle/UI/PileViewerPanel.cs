using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Battle.Card;

namespace Battle.UI
{
    // 더미 보기 종류
    public enum PileViewerKind
    {
        Draw,       // 뽑을 더미
        Discard,    // 버린 더미(묘지)
        RunDeck,    // 런 덱 전체(menubar 덱 버튼)
        CardRemove, // 상점 카드 제거(카드 클릭으로 선택)
    }

    // 덱/묘지/런 덱 보기 패널 — 프리팹에서 배치·스킨을 잡고, 호출 측은 데이터만 넘긴다.
    // 카드 선택(픽커) 모드와는 독립(픽커는 CardHandHUD 런타임 생성 그대로).
    // 표시 방식 두 가지:
    //   Show           — 전투 더미 보기(CardInstance + CardHandHUD 바인딩, 호버/클릭은 HUD가 처리)
    //   ShowSelectable — HUD 없는 화면(상점 카드 제거 등). CardData를 셀 래퍼에 깔고 클릭 콜백으로 선택받음
    public class PileViewerPanel : MonoBehaviour
    {
        [Header("구성 요소")]
        [Tooltip("화면 전체 딤(뒤쪽 클릭 차단). 비워도 됨.")]
        [SerializeField] private Image dimBackground;
        [Tooltip("ON이면 딤 클릭으로도 닫힘. 인벤토리와 동일하게 기본 OFF(X 버튼으로만 닫기).")]
        [SerializeField] private bool closeOnDimClick = false;
        [Tooltip("더미 종류별 스프라이트가 들어갈 패널 프레임 이미지.")]
        [SerializeField] private Image frameImage;
        [Tooltip("전체 제목(예: \"내 덱 (20)\"). 프레임 스프라이트에 제목이 그려져 있으면 숨김 처리됨.")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private ScrollRect scrollRect;
        [Tooltip("카드가 생성될 컨테이너(ScrollRect.content).")]
        [SerializeField] private RectTransform content;
        [Tooltip("닫기(X) 버튼 — 인벤토리 CloseButton과 동일 구성.")]
        [SerializeField] private Button closeButton;
        [Tooltip("더미가 비었을 때 보여줄 오브젝트. 비워도 됨.")]
        [SerializeField] private GameObject emptyLabel;

        [Header("더미 종류별 프레임 스프라이트 (제목 배너 포함 이미지)")]
        [SerializeField] private Sprite drawPileSprite;
        [SerializeField] private Sprite discardPileSprite;
        [Tooltip("비우면 defaultFrameSprite + titleText 사용.")]
        [SerializeField] private Sprite runDeckSprite;
        [Tooltip("상점 '부적 제거' 프레임.")]
        [SerializeField] private Sprite cardRemoveSprite;
        [Tooltip("종류별 스프라이트가 없을 때 쓰는 프레임(제목 미포함 가정 -> titleText 표시).")]
        [SerializeField] private Sprite defaultFrameSprite;

        [Header("카드 그리드")]
        [Tooltip("한 행 최대 카드 수.")]
        [SerializeField] private int columns = 4;
        [Tooltip("ON=좌측 정렬(덜 찬 마지막 행도 왼쪽부터), OFF=행마다 가운데 정렬.")]
        [SerializeField] private bool alignLeft = true;
        [Tooltip("카드 가로 간격(중심->중심, px).")]
        [SerializeField] private float columnSpacing = 400f;
        [Tooltip("카드 세로 간격(중심->중심, px).")]
        [SerializeField] private float rowSpacing = 520f;
        [Tooltip("x=좌우(현재 미사용 예비), y=상하 합계 패딩.")]
        [SerializeField] private Vector2 contentPadding = new Vector2(80f, 120f);
        [Tooltip("그리드 전체 크기 배율(content 스케일). 간격도 함께 배율 적용됨.")]
        [SerializeField] private float cardScale = 1f;

        [Header("선택 모드(ShowSelectable) 호버")]
        [Tooltip("마우스 오버 시 카드가 제자리에서 커지는 배율(인벤토리와 동일 방식).")]
        [SerializeField] private float selectableHoverScale = 1.1f;
        [Tooltip("호버 카드가 이웃 위로 그려질 정렬 순서. 확인 팝업보다 낮아야 함.")]
        [SerializeField] private int selectableHoverSortingOrder = 200;

        readonly List<NewCardView> _views = new List<NewCardView>();   // Show 모드 카드
        readonly List<GameObject> _cells = new List<GameObject>();     // ShowSelectable 모드 셀 래퍼
        System.Action _onCloseRequested;
        bool _wired;

        public bool IsOpen => gameObject.activeSelf;

        // Show 모드로 깐 카드 뷰인지(카드 선택 픽커가 클릭 판정에 사용)
        public bool ContainsView(NewCardView view) => view != null && _views.Contains(view);

        // 닫기(X) 버튼 표시 — 카드 선택 중에는 숨긴다(열 때마다 다시 표시됨)
        public void SetCloseButtonVisible(bool visible)
        {
            if (closeButton != null) closeButton.gameObject.SetActive(visible);
        }

        void Awake()
        {
            WireEvents();
        }

        // 닫기 버튼·딤 클릭을 닫기 요청으로 연결
        void WireEvents()
        {
            if (_wired) return;
            _wired = true;

            if (closeButton != null)
                closeButton.onClick.AddListener(RequestClose);

            if (dimBackground != null) dimBackground.raycastTarget = true; // 뒤쪽 UI 클릭 차단

            if (closeOnDimClick && dimBackground != null)
            {
                var trigger = dimBackground.GetComponent<EventTrigger>();
                if (trigger == null) trigger = dimBackground.gameObject.AddComponent<EventTrigger>();
                var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
                entry.callback.AddListener(_ => RequestClose());
                trigger.triggers.Add(entry);
            }
        }

        void RequestClose()
        {
            var cb = _onCloseRequested;
            if (cb != null) cb();
            else Hide();
        }

        // 패널을 열고 카드를 그리드로 깐다(전투 더미 보기).
        // hud: 카드 뷰 호버/클릭 처리를 위해 바인딩(슬롯 -1 = 그리드 카드)
        public void Show(PileViewerKind kind, string title, IList<CardInstance> cards,
            NewCardView cardPrefab, CardHandHUD hud, System.Action onCloseRequested)
        {
            int count = cards != null ? cards.Count : 0;
            Open(kind, title, onCloseRequested);

            if (count > 0 && cardPrefab != null && PrepareContent(count))
            {
                for (int i = 0; i < count; i++)
                {
                    var view = Instantiate(cardPrefab, content);
                    view.Bind(hud, -1); // 슬롯 -1 = 그리드 카드(제자리 호버)
                    var vrect = (RectTransform)view.transform;
                    PlaceInGrid(vrect, i, count);
                    vrect.localRotation = Quaternion.identity;
                    view.SetCard(cards[i]);
                    view.CaptureHome();
                    _views.Add(view);
                }
            }
            else ResetContentSize(); // 빈 더미 — 이전 더미의 콘텐츠 높이가 남지 않게

            FinishOpen(count);
        }

        // HUD 없이 카드 목록을 깔고, 카드 클릭 시 onCardClicked(목록 인덱스)를 호출한다(상점 카드 제거 등).
        public void ShowSelectable(PileViewerKind kind, string title, IList<CardData> cards,
            NewCardView cardPrefab, System.Action<int> onCardClicked, System.Action onCloseRequested)
        {
            int count = cards != null ? cards.Count : 0;
            Open(kind, title, onCloseRequested);

            if (count > 0 && cardPrefab != null && PrepareContent(count))
            {
                RectTransform clip = scrollRect != null ? scrollRect.viewport : null;
                for (int i = 0; i < count; i++)
                    _cells.Add(CreateSelectableCell(cards[i], i, count, cardPrefab, clip, onCardClicked));
            }
            else ResetContentSize();

            FinishOpen(count);
        }

        // 셀 래퍼(투명 raycast 이미지 + Button) 안에 카드를 미리보기로 깐다.
        // 카드 자체는 raycast를 막아(CanvasGroup) 클릭이 래퍼 Button으로 가게 한다
        // — NewCardView는 HUD가 없으면 클릭을 무시하고 이벤트도 부모로 넘기지 않기 때문.
        GameObject CreateSelectableCell(CardData data, int index, int total, NewCardView cardPrefab,
            RectTransform clip, System.Action<int> onCardClicked)
        {
            var cellGo = new GameObject("CardCell", typeof(RectTransform), typeof(Image), typeof(Button));
            var cellRt = (RectTransform)cellGo.transform;
            cellRt.SetParent(content, false);

            var cellImg = cellGo.GetComponent<Image>();
            cellImg.color = new Color(0f, 0f, 0f, 0f); // 완전 투명
            cellImg.raycastTarget = true;

            var btn = cellGo.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            int captured = index;
            btn.onClick.AddListener(() => onCardClicked?.Invoke(captured));

            NewCardView view = Instantiate(cardPrefab, cellRt);
            view.ApplyShopPreview(data);
            var cg = view.GetComponent<CanvasGroup>();
            if (cg == null) cg = view.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false; // interactable은 건드리지 않음 — 카드 안 Button이 비활성 색(어둡게)으로 바뀜

            // 셀 크기 = 카드 표시 크기, 카드는 셀 중앙
            var cardRt = (RectTransform)view.transform;
            Vector2 native = cardRt.sizeDelta;
            if (native.x < 1f || native.y < 1f) native = new Vector2(300f, 400f);
            Vector3 cardScaleVec = cardRt.localScale;
            cellRt.sizeDelta = new Vector2(native.x * Mathf.Abs(cardScaleVec.x), native.y * Mathf.Abs(cardScaleVec.y));
            cardRt.anchorMin = cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.anchoredPosition = Vector2.zero;
            cardRt.localRotation = Quaternion.identity;

            PlaceInGrid(cellRt, index, total);

            // 호버 제자리 확대(인벤토리 방식 — 마우스 위치 직접 판정)
            var slot = cellGo.AddComponent<InventoryCardSlot>();
            slot.Configure(selectableHoverScale > 0f ? selectableHoverScale : 1f, selectableHoverSortingOrder, clip);

            return cellGo;
        }

        // 공통 열기: 활성화·스킨·기존 카드 정리
        void Open(PileViewerKind kind, string title, System.Action onCloseRequested)
        {
            WireEvents();
            _onCloseRequested = onCloseRequested;

            gameObject.SetActive(true);
            transform.SetAsLastSibling();

            ApplySkin(kind, title);
            ClearViews();
            SetCloseButtonVisible(true);
        }

        // 공통 마무리: 빈 안내·스크롤 맨 위
        void FinishOpen(int count)
        {
            if (emptyLabel != null) emptyLabel.SetActive(count == 0);

            if (scrollRect != null)
            {
                Canvas.ForceUpdateCanvases(); // content 크기 반영 후 스크롤바/위치 계산
                scrollRect.StopMovement();
                scrollRect.verticalNormalizedPosition = 1f;
            }
        }

        // 패널을 닫고 카드 뷰를 정리
        public void Hide()
        {
            _onCloseRequested = null;
            ClearViews();
            gameObject.SetActive(false);
        }

        // 더미 종류에 맞춰 프레임 스프라이트·제목 갱신
        void ApplySkin(PileViewerKind kind, string title)
        {
            Sprite kindSprite = kind switch
            {
                PileViewerKind.Draw => drawPileSprite,
                PileViewerKind.Discard => discardPileSprite,
                PileViewerKind.RunDeck => runDeckSprite,
                PileViewerKind.CardRemove => cardRemoveSprite,
                _ => null,
            };

            if (frameImage != null)
            {
                Sprite s = kindSprite != null ? kindSprite : defaultFrameSprite;
                frameImage.sprite = s;
                frameImage.enabled = s != null || frameImage.color.a > 0f;
            }

            // 스프라이트에 제목 배너가 그려져 있으면 텍스트 제목은 숨김
            if (titleText != null)
            {
                bool showTitle = kindSprite == null;
                titleText.gameObject.SetActive(showTitle);
                if (showTitle) titleText.text = title;
            }
        }

        // 카드 수에 맞춰 content 높이·스케일 설정(호버 시 형제 순서가 바뀌므로 LayoutGroup 대신 수동 배치)
        bool PrepareContent(int total)
        {
            if (content == null)
            {
                Debug.LogWarning("[PileViewerPanel] content 미설정 — 카드를 표시할 수 없습니다.");
                return false;
            }

            int maxPerRow = Mathf.Max(1, columns);
            int rows = Mathf.CeilToInt(total / (float)maxPerRow);

            float contentHeight = rows * rowSpacing + contentPadding.y;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0f, contentHeight);
            content.anchoredPosition = Vector2.zero;
            // 카드 개별 스케일은 호버 시 베이스로 리셋되므로 컨테이너 전체를 스케일
            content.localScale = Vector3.one * Mathf.Max(0.01f, cardScale);
            return true;
        }

        // i번째 카드를 그리드 위치에 배치
        void PlaceInGrid(RectTransform rt, int i, int total)
        {
            int maxPerRow = Mathf.Max(1, columns);
            int rows = Mathf.CeilToInt(total / (float)maxPerRow);
            int row = i / maxPerRow;
            int col = i % maxPerRow;
            // 좌측 정렬: 모든 행을 꽉 찬 행 기준으로 배치(그리드 블록은 가운데, 마지막 행은 왼쪽부터 채움)
            int cardsInRow = alignLeft ? maxPerRow
                : (row == rows - 1) ? (total - row * maxPerRow) : maxPerRow;
            float rowWidth = columnSpacing * (cardsInRow - 1);
            float x = -rowWidth * 0.5f + col * columnSpacing;
            float y = -(contentPadding.y * 0.5f) - (row * rowSpacing) - (rowSpacing * 0.5f);

            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
        }

        void ResetContentSize()
        {
            if (content == null) return;
            content.sizeDelta = new Vector2(content.sizeDelta.x, 0f);
            content.anchoredPosition = Vector2.zero;
        }

        void ClearViews()
        {
            for (int i = 0; i < _views.Count; i++)
                if (_views[i] != null) Destroy(_views[i].gameObject);
            _views.Clear();
            for (int i = 0; i < _cells.Count; i++)
                if (_cells[i] != null) Destroy(_cells[i]);
            _cells.Clear();
        }

        void OnDestroy()
        {
            _onCloseRequested = null;
        }
    }
}
