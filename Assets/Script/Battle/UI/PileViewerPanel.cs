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
        Draw,     // 뽑을 더미
        Discard,  // 버린 더미(묘지)
        RunDeck,  // 런 덱 전체(menubar 덱 버튼)
    }

    // 덱/묘지/런 덱 보기 패널 — 프리팹에서 배치·스킨을 잡고, CardHandHUD는 데이터만 넘긴다.
    // 카드 선택(픽커) 모드와는 독립(픽커는 CardHandHUD 런타임 생성 그대로).
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
        [Tooltip("종류별 스프라이트가 없을 때 쓰는 프레임(제목 미포함 가정 → titleText 표시).")]
        [SerializeField] private Sprite defaultFrameSprite;

        [Header("카드 그리드")]
        [Tooltip("한 행 최대 카드 수.")]
        [SerializeField] private int columns = 4;
        [Tooltip("ON=좌측 정렬(덜 찬 마지막 행도 왼쪽부터), OFF=행마다 가운데 정렬.")]
        [SerializeField] private bool alignLeft = true;
        [Tooltip("카드 가로 간격(중심→중심, px).")]
        [SerializeField] private float columnSpacing = 400f;
        [Tooltip("카드 세로 간격(중심→중심, px).")]
        [SerializeField] private float rowSpacing = 520f;
        [Tooltip("x=좌우(현재 미사용 예비), y=상하 합계 패딩.")]
        [SerializeField] private Vector2 contentPadding = new Vector2(80f, 120f);
        [Tooltip("그리드 전체 크기 배율(content 스케일). 간격도 함께 배율 적용됨.")]
        [SerializeField] private float cardScale = 1f;

        readonly List<NewCardView> _views = new List<NewCardView>();
        System.Action _onCloseRequested;
        bool _wired;

        public bool IsOpen => gameObject.activeSelf;

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

        // 패널을 열고 카드를 그리드로 깐다.
        // hud: 카드 뷰 호버/클릭 처리를 위해 바인딩(슬롯 -1 = 그리드 카드)
        public void Show(PileViewerKind kind, string title, IList<CardInstance> cards,
            NewCardView cardPrefab, CardHandHUD hud, System.Action onCloseRequested)
        {
            WireEvents();
            _onCloseRequested = onCloseRequested;

            gameObject.SetActive(true);
            transform.SetAsLastSibling();

            int count = cards != null ? cards.Count : 0;
            ApplySkin(kind, title);

            ClearViews();
            if (count > 0 && cardPrefab != null) BuildCards(cards, cardPrefab, hud);
            else ResetContentSize(); // 빈 더미 — 이전 더미의 콘텐츠 높이가 남지 않게
            if (emptyLabel != null) emptyLabel.SetActive(count == 0);

            // 스크롤은 맨 위에서 시작
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

        // 카드 목록을 가운데 정렬 그리드로 배치(호버 시 형제 순서가 바뀌므로 LayoutGroup 대신 수동 배치)
        void BuildCards(IList<CardInstance> cards, NewCardView cardPrefab, CardHandHUD hud)
        {
            if (content == null)
            {
                Debug.LogWarning("[PileViewerPanel] content 미설정 — 카드를 표시할 수 없습니다.");
                return;
            }

            int maxPerRow = Mathf.Max(1, columns);
            int total = cards.Count;
            int rows = Mathf.CeilToInt(total / (float)maxPerRow);

            float contentHeight = rows * rowSpacing + contentPadding.y;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = new Vector2(0f, contentHeight);
            content.anchoredPosition = Vector2.zero;
            // 카드 개별 스케일은 호버 시 베이스로 리셋되므로 컨테이너 전체를 스케일
            content.localScale = Vector3.one * Mathf.Max(0.01f, cardScale);

            for (int i = 0; i < total; i++)
            {
                int row = i / maxPerRow;
                int col = i % maxPerRow;
                // 좌측 정렬: 모든 행을 꽉 찬 행 기준으로 배치(그리드 블록은 가운데, 마지막 행은 왼쪽부터 채움)
                int cardsInRow = alignLeft ? maxPerRow
                    : (row == rows - 1) ? (total - row * maxPerRow) : maxPerRow;
                float rowWidth = columnSpacing * (cardsInRow - 1);
                float x = -rowWidth * 0.5f + col * columnSpacing;
                float y = -(contentPadding.y * 0.5f) - (row * rowSpacing) - (rowSpacing * 0.5f);

                var view = Instantiate(cardPrefab, content);
                view.Bind(hud, -1); // 슬롯 -1 = 그리드 카드(제자리 호버, 클릭 시 HUD가 보기 닫기)
                var vrect = (RectTransform)view.transform;
                vrect.anchorMin = new Vector2(0.5f, 1f);
                vrect.anchorMax = new Vector2(0.5f, 1f);
                vrect.pivot = new Vector2(0.5f, 0.5f);
                vrect.anchoredPosition = new Vector2(x, y);
                vrect.localRotation = Quaternion.identity;
                view.SetCard(cards[i]);
                view.CaptureHome();
                _views.Add(view);
            }
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
        }

        void OnDestroy()
        {
            _onCloseRequested = null;
        }
    }
}
