using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using TMPro;
using Battle.Card;
using Battle.Deck;

namespace Battle.UI
{
    // 손패 HUD — 카드 슬롯 구성·콤보/각성 UI 토글
    public class CardHandHUD : MonoBehaviour
    {
        public const int SLOT_COUNT = CardDeckSystem.MAX_HAND_LIMIT; // UI 슬롯 사전 생성 수(패 한도 상한)
        public const int LAYOUT_REFERENCE_COUNT = CardDeckSystem.HAND_LIMIT; // 레이아웃 기준 카드 수

        [Header("프리팹 (필수)")]
        [SerializeField] private NewCardView cardPrefab; // 카드 뷰 프리팹
        // 외부에서 카드 뷰 프리팹을 재사용하기 위한 접근자
        public NewCardView CardPrefab => cardPrefab;

        [Header("배치")]
        [Tooltip("카드 슬롯들이 정렬될 부모. 비우면 본 트랜스폼 자식으로 자동 생성.")]
        [SerializeField] private RectTransform handRoot; // 카드 슬롯 부모
        [Tooltip("드래그 중 카드가 옮겨질 상위 캔버스 레이어. 비우면 같은 캔버스의 최상단을 자동 사용.")]
        [SerializeField] private RectTransform dragLayer; // 드래그 레이어
        // 드래그 레이어 반환(없으면 handRoot)
        public RectTransform DragLayer => dragLayer != null ? dragLayer : handRoot;

        [Header("레이아웃 — 슬롯 위치 자동 정렬")]
        [Tooltip("슬롯 간 간격 (x). NewSkillCard 프리팹이 scale=1, 300×400일 때 650~750 권장.")]
        [SerializeField] private Vector2 slotSpacing = new Vector2(700f, 0f); // 슬롯 간 간격
        [SerializeField] private Vector2 handAnchoredPos = new Vector2(0f, 240f); // 손패 루트 위치
        [Tooltip("드래그 종료 시 스크린 Y가 이 값 이상이면 카드 사용으로 간주.")]
        [SerializeField] private float useThresholdY = 500f; // 카드 사용 판정 Y

        [Header("Fan Layout — 손패 부채꼴 연출")]
        [Tooltip("ON이면 카드들이 호 형태로 회전·배치된다.")]
        [SerializeField] private bool fanLayout = true; // 부채꼴 배치 사용 여부
        [Tooltip("부채꼴 반지름. 클수록 호가 평평해짐.")]
        [SerializeField] private float fanRadius = 1800f; // 부채꼴 반지름
        [Tooltip("부채꼴 전체 각도(도). 클수록 카드 사이 간격이 넓어짐. 5장 기준 36~44 권장.")]
        [SerializeField] private float fanArcAngle = 40f; // 부채꼴 전체 각도
        [Tooltip("호 위 가장자리에서 카드들이 떨어질 깊이. 작을수록 카드들이 살짝 아래로 호를 그림.")]
        [SerializeField] private float fanVerticalDip = 50f; // 호 하강 깊이

        [Header("드로우 등장 연출")]
        [Tooltip("여러 장을 동시에 뽑을 때 카드마다 떠오르기 시작을 늦추는 간격(초). 0이면 동시에 올라옴.")]
        [SerializeField] private float drawIntroStagger = 0.05f; // 드로우 등장 지연 간격

        [Header("콤보 UI — 각성 전용")]
        [Tooltip("각성 발동 시 활성화될 콤보 슬롯 패널.")]
        [SerializeField] private GameObject comboSlotPanel; // 콤보 슬롯 패널
        [Tooltip("각성 발동 시 활성화될 콤보 스킬 목록 패널.")]
        [SerializeField] private GameObject comboSkillPanel; // 콤보 스킬 패널
        [Tooltip("콤보 슬롯 3칸의 Image. 비워두면 comboSlotPanel 자식에서 자동 탐색.")]
        [SerializeField] private Image[] comboSlotImages = new Image[3]; // 콤보 슬롯 이미지 3칸
        [SerializeField] private Color emptyComboSlotColor = new Color(0.25f, 0.25f, 0.25f, 0.4f); // 빈 콤보 슬롯 색

        [Header("콤보 슬롯 입력 연출 (각성 중 속성이 슬롯에 들어올 때)")]
        [Tooltip("각성 중 콤보 슬롯에 속성이 들어올 때 해당 칸을 팝(확대→복귀)시키는 연출 ON/OFF.")]
        [SerializeField] private bool comboSlotPopEnabled = true; // 콤보 슬롯 팝 연출 사용 여부
        [Tooltip("팝 시 최대 확대 배율(원본 스케일 기준). 1.5 = 50% 커졌다 돌아옴.")]
        [SerializeField] private float comboSlotPopScale = 1.5f; // 팝 최대 확대 배율
        [Tooltip("커졌다 원래대로 돌아오는 전체 시간(초).")]
        [SerializeField] private float comboSlotPopDuration = 0.28f; // 팝 전체 시간
        [Tooltip("입력 순간 칸을 흰색으로 잠깐 플래시할지 여부.")]
        [SerializeField] private bool comboSlotFlashEnabled = true; // 입력 칸 플래시 사용 여부

        [Header("콤보 스킬 목록 (SkillListItem 프리팹)")]
        [Tooltip("콤보 스킬 한 항목을 표현할 프리팹. SkillListItem.prefab 사용.")]
        [SerializeField] private SkillListItemUI comboSkillItemPrefab; // 콤보 스킬 항목 프리팹
        [Tooltip("콤보 스킬 항목들이 배치될 컨테이너. 비워두면 comboSkillPanel을 사용.")]
        [SerializeField] private RectTransform comboSkillItemContainer; // 콤보 스킬 항목 컨테이너

        [Header("정보 텍스트")]
        [SerializeField] private TMP_Text drawCountText; // 뽑을 더미 수 텍스트
        [SerializeField] private TMP_Text discardCountText; // 버린 더미 수 텍스트
        [FormerlySerializedAs("feverCountText")]
        [SerializeField] private TMP_Text awakenCountText; // 각성 카운트 텍스트

        [Header("버린 더미 카운트 강조 (숫자 변할 때 팝)")]
        [Tooltip("버린 더미 숫자가 바뀔 때 잠깐 커졌다 줄어드는 팝 연출.")]
        [SerializeField] private bool discardPopEnabled = true; // 버린 더미 팝 연출 사용 여부
        [Tooltip("팝 시 최대 확대 배율(원본 스케일 기준). 1.6 = 60% 더 커졌다 돌아옴.")]
        [SerializeField] private float discardPopScale = 1.6f; // 팝 최대 확대 배율
        [Tooltip("커졌다 원래대로 돌아오는 전체 시간(초).")]
        [SerializeField] private float discardPopDuration = 0.35f; // 팝 전체 시간

        [Header("리셔플 카운트업 (묘지→덱 복귀 시 덱 숫자 1,2,3 떨어짐)")]
        [Tooltip("버린 더미가 덱으로 돌아갈 때 덱 숫자가 1씩 위에서 떨어지며 올라가는 연출.")]
        [SerializeField] private bool reshuffleCountUpEnabled = true; // 리셔플 카운트업 사용 여부
        [Tooltip("숫자 하나가 위에서 제자리로 떨어지는 시간(초).")]
        [SerializeField] private float reshuffleStepDuration = 0.12f; // 한 단계 낙하 시간
        [Tooltip("숫자와 숫자 사이 간격(초). 0이면 끊김 없이 연속.")]
        [SerializeField] private float reshuffleStepGap = 0.04f; // 단계 간 간격
        [Tooltip("숫자가 떨어지기 시작하는 높이(px, 제자리 기준 위쪽).")]
        [SerializeField] private float reshuffleDropHeight = 36f; // 낙하 시작 높이
        [Tooltip("착지 시 살짝 커지는 팝 배율(1=없음).")]
        [SerializeField] private float reshuffleLandScale = 1.35f; // 착지 팝 배율
        [Tooltip("카운트업으로 표시할 최대 단계 수 — 너무 많으면 길어지므로 상한(이상은 마지막에 실제값으로 정착).")]
        [SerializeField] private int reshuffleMaxSteps = 12; // 카운트업 최대 단계

        [Header("각성 게이지 (HP바 아래)")]
        [Tooltip("게이지 슬라이더 — 비우면 SetAwakenGaugeAnchor 호출 또는 첫 갱신 시 자동 생성.")]
        [FormerlySerializedAs("feverGauge")]
        [SerializeField] private Slider awakenGauge; // 각성 게이지 슬라이더
        [Tooltip("게이지를 부착할 기준 RectTransform — 보통 Player.hpBar의 RectTransform. 비우면 NewBattleController가 런타임에 넣어줌.")]
        [FormerlySerializedAs("feverGaugeAnchor")]
        [SerializeField] private RectTransform awakenGaugeAnchor; // 게이지 부착 기준
        [Tooltip("게이지 자동 생성 시 크기.")]
        [FormerlySerializedAs("feverGaugeSize")]
        [SerializeField] private Vector2 awakenGaugeSize = new Vector2(300f, 22f); // 게이지 크기
        [Tooltip("anchor 기준 오프셋. y 음수 = 아래(HP바 아래).")]
        [FormerlySerializedAs("feverGaugeOffset")]
        [SerializeField] private Vector2 awakenGaugeOffset = new Vector2(0f, -32f); // anchor 기준 오프셋
        [Tooltip("anchor(Player.hpBar) 미배선 시 게이지를 표시할 위치 — 화면 상단 중앙 기준(y 음수=아래로). hpBar 배선되면 무시.")]
        [SerializeField] private Vector2 awakenGaugeFallbackPos = new Vector2(0f, -40f); // anchor 없을 때 위치
        [Tooltip("게이지 위에 표시할 라벨 (예: 6/10, 10.0s). 비우면 게이지 자동 생성 시 함께 생성.")]
        [FormerlySerializedAs("feverGaugeLabel")]
        [SerializeField] private TMP_Text awakenGaugeLabel; // 게이지 라벨
        [Tooltip("충전 진행 중 색.")]
        [FormerlySerializedAs("feverChargeColor")]
        [SerializeField] private Color awakenChargeColor = new Color(1f, 0.55f, 0.15f, 1f); // 충전 중 색
        [Tooltip("발동 중 색(카운트다운).")]
        [FormerlySerializedAs("feverActiveColor")]
        [SerializeField] private Color awakenActiveColor = new Color(0.75f, 0.35f, 1f, 1f); // 발동 중 색

        [Header("각성 입력 히스토리 (왼쪽 표시)")]
        [Tooltip("OFF면 각성 입력 히스토리(왼쪽 세로 격자)를 표시하지 않는다. 콤보 슬롯/스킬 목록에는 영향 없음.")]
        [SerializeField] private bool awakenHistoryEnabled = true; // 입력 히스토리 표시 여부
        [Tooltip("각성 동안 입력된 속성 카드 전체 히스토리가 표시될 부모. 비우면 자동 생성.")]
        [FormerlySerializedAs("feverHistoryContainer")]
        [SerializeField] private RectTransform awakenHistoryContainer; // 입력 히스토리 부모
        [Tooltip("히스토리 한 칸 크기.")]
        [FormerlySerializedAs("feverHistoryItemSize")]
        [SerializeField] private Vector2 awakenHistoryItemSize = new Vector2(60f, 60f); // 히스토리 칸 크기
        [Tooltip("히스토리 칸 간 세로 간격.")]
        [FormerlySerializedAs("feverHistoryItemSpacing")]
        [SerializeField] private float awakenHistoryItemSpacing = 8f; // 히스토리 세로 간격
        [Tooltip("한 열에 표시할 최대 항목 수 — 이 이상이면 오른쪽 새 열로 이동.")]
        [FormerlySerializedAs("feverHistoryItemsPerColumn")]
        [SerializeField] private int awakenHistoryItemsPerColumn = 8; // 한 열 최대 항목 수
        [Tooltip("열과 열 사이 가로 간격.")]
        [FormerlySerializedAs("feverHistoryColumnSpacing")]
        [SerializeField] private float awakenHistoryColumnSpacing = 8f; // 열 간 가로 간격
        [Tooltip("히스토리 컨테이너 위치(앵커 기준). 좌측 가운데 추천.")]
        [FormerlySerializedAs("feverHistoryAnchoredPos")]
        [SerializeField] private Vector2 awakenHistoryAnchoredPos = new Vector2(80f, 0f); // 히스토리 컨테이너 위치
        [Tooltip("표시할 최대 항목 수 — 초과 시 가장 오래된 것부터 숨김.")]
        [FormerlySerializedAs("feverHistoryMaxItems")]
        [SerializeField] private int awakenHistoryMaxItems = 64; // 히스토리 최대 항목 수

        private readonly List<NewCardView> _cardViews = new List<NewCardView>(); // 슬롯별 카드 뷰 풀
        private readonly List<RectTransform> _slotAnchors = new List<RectTransform>(); // 슬롯 앵커 목록
        private readonly HashSet<CardInstance> _prevHandSet = new HashSet<CardInstance>(); // 직전 패 구성(신규 판별용)
        private CardDeckSystem _deck; // 바인딩된 덱 시스템
        public System.Func<CardInstance, bool> UseCardCallback; // 카드 사용 콜백

        private int _prevDiscardCount = int.MinValue; // 직전 표시한 버린 더미 수
        private Coroutine _discardPopCo; // 버린 더미 팝 코루틴
        private Vector3 _discardCountBaseScale = Vector3.one; // 버린 더미 텍스트 기준 스케일
        private bool _discardCountBaseCaptured; // 버린 더미 기준 캡처 여부

        private Coroutine _drawCountCo; // 리셔플 카운트업 코루틴
        private bool _drawCountAnimating; // 카운트업 진행 중 여부
        private Vector2 _drawCountHomePos; // 덱 수 텍스트 홈 위치
        private Vector3 _drawCountBaseScale = Vector3.one; // 덱 수 텍스트 기준 스케일
        private bool _drawCountBaseCaptured; // 덱 수 기준 캡처 여부
        public System.Action SelectionClosedCallback; // 선택/픽커 완료 직후 콜백

        private Image _awakenGaugeFill; // 각성 게이지 채움 이미지

        private System.Action<CardInstance> _selectionCallback; // 선택 모드 콜백
        private System.Func<CardInstance, bool> _selectionFilter; // 선택 가능 카드 필터
        private CardInstance _selectionExcludeCard; // 선택 제외 카드
        public bool IsSelectionMode => _selectionCallback != null; // 선택 모드 여부

        [Header("선택 모드 UI")]
        [Tooltip("선택 모드 안내 텍스트. 비워두면 표시 안 함.")]
        [SerializeField] private TMP_Text selectionPromptText; // 선택 모드 안내 텍스트
        [Tooltip("선택 모드 시 화면을 덮는 어둡게 처리 오버레이. 비우면 자동 생성.")]
        [SerializeField] private RectTransform dimOverlay; // 화면 딤 오버레이
        [Tooltip("dim 오버레이 색(알파로 어둡기 조절) — 선택/픽커 모드.")]
        [SerializeField] private Color dimColor = new Color(0f, 0f, 0f, 0.6f); // 선택/픽커 딤 색
        [Tooltip("각성 모드 dim 색 — 일반 선택 모드와 다른 톤으로 구분 가능. 카드 선택 dim(dimColor)과 독립.")]
        [FormerlySerializedAs("feverDimColor")]
        [SerializeField] private Color awakenDimColor = new Color(0.02f, 0.0f, 0.08f, 0.82f); // 각성 모드 딤 색

        // 손패 루트/드래그 레이어/슬롯 셋업 및 초기 상태 구성
        void Awake()
        {
            EnsureHandRoot();
            EnsureDragLayer();
            BuildSlotAnchors();
            SetAwakenMode(false);
            SetupPileClickHandlers();
        }

        private bool _viewerMode; // 더미 보기 모드 여부
        private int _viewerPile = -1; // 보는 더미(0=뽑을, 1=버린)
        public bool IsViewerMode => _viewerMode; // 더미 보기 모드 여부

        // 뽑을/버린 더미 카운트 텍스트에 클릭 핸들러 연결
        void SetupPileClickHandlers()
        {
            AttachPileClick(drawCountText, () => TogglePileViewer(0));
            AttachPileClick(discardCountText, () => TogglePileViewer(1));
        }

        // 텍스트에 PointerClick 이벤트 트리거를 부착
        void AttachPileClick(TMP_Text txt, System.Action action)
        {
            if (txt == null) return;
            txt.raycastTarget = true;
            var trigger = txt.gameObject.GetComponent<EventTrigger>();
            if (trigger == null) trigger = txt.gameObject.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            entry.callback.AddListener(_ => action());
            trigger.triggers.Add(entry);
        }

        // 지정 더미 보기를 토글(0=뽑을, 1=버린)
        public void TogglePileViewer(int pile)
        {
            if (_deck == null) return;
            if (IsSelectionMode || IsPickerMode) return;
            if (_viewerMode && _viewerPile == pile) { CloseViewer(); return; }

            var src = pile == 0 ? _deck.DrawPile : _deck.DiscardPile;
            var list = new List<CardInstance>(src);
            list.Sort((a, b) => a.Id.CompareTo(b.Id));
            string title = (pile == 0 ? "뽑을 더미" : "버린 더미") + $" ({list.Count})";
            EnterPileViewer(title, list, pile);
        }

        // 더미 보기 화면을 띄우고 카드들을 깐다
        void EnterPileViewer(string title, List<CardInstance> cards, int pile)
        {
            if (cardPrefab == null)
            {
                Debug.LogWarning("[CardHandHUD] cardPrefab 미설정 — 더미 보기 불가.");
                return;
            }
            CloseViewer();
            _viewerMode = true;
            _viewerPile = pile;

            EnsureDimOverlay();
            EnsurePickerRoot();

            if (handRoot != null) handRoot.SetAsFirstSibling();
            if (dimOverlay != null)
            {
                dimOverlay.gameObject.SetActive(true);
                dimOverlay.SetAsLastSibling();
            }
            if (selectionPromptText != null)
            {
                selectionPromptText.text = title;
                selectionPromptText.gameObject.SetActive(true);
                selectionPromptText.transform.SetAsLastSibling();
            }
            _pickerRoot.gameObject.SetActive(true);
            _pickerRoot.SetAsLastSibling();
            if (cards.Count > 0) BuildPickerCards(cards);
            else ClearPickerViews();
            BringAwakenGaugeToFront();
        }

        // 더미 보기 화면을 닫는다
        public void CloseViewer()
        {
            if (!_viewerMode) return;
            _viewerMode = false;
            _viewerPile = -1;
            ClearPickerViews();
            if (_pickerRoot != null) _pickerRoot.gameObject.SetActive(false);
            if (dimOverlay != null) dimOverlay.gameObject.SetActive(false);
            if (selectionPromptText != null) selectionPromptText.gameObject.SetActive(false);
            if (handRoot != null) handRoot.SetAsLastSibling();
            BringAwakenGaugeToFront();
        }

        // 덱 시스템을 바인딩하고 이벤트 구독을 갱신
        public void Bind(CardDeckSystem deck)
        {
            if (_deck != null)
            {
                _deck.OnPileChanged -= Refresh;
                _deck.OnReshuffled -= HandleReshuffled;
            }
            _deck = deck;
            if (_deck != null)
            {
                _deck.OnPileChanged += Refresh;
                _deck.OnReshuffled += HandleReshuffled;
            }
            Refresh();
        }

        // 덱 이벤트 구독을 해제
        void OnDestroy()
        {
            if (_deck != null)
            {
                _deck.OnPileChanged -= Refresh;
                _deck.OnReshuffled -= HandleReshuffled;
            }
        }

        // 각성 카운트 텍스트를 설정
        public void SetAwakenText(string text)
        {
            if (awakenCountText != null) awakenCountText.text = text;
        }

        // 각성 게이지 부착 기준을 외부에서 지정
        public void SetAwakenGaugeAnchor(RectTransform anchor)
        {
            if (anchor == null) return;
            if (awakenGaugeAnchor == anchor && awakenGauge != null) return;
            awakenGaugeAnchor = anchor;
            EnsureAwakenGauge();
            RefitAwakenGaugeToAnchor();
        }

        // 각성 게이지 표시 여부를 제어
        public void SetAwakenGaugeVisible(bool visible)
        {
            if (visible) EnsureAwakenGauge();
            if (awakenGauge != null)
                awakenGauge.gameObject.SetActive(visible);

            if (awakenGaugeLabel != null && awakenGaugeLabel.transform.parent != (awakenGauge != null ? awakenGauge.transform : null))
                awakenGaugeLabel.gameObject.SetActive(visible);
        }

        // 충전/발동 상태에 따라 각성 게이지 fill·색·라벨을 갱신
        public void UpdateAwakenGauge(int chargeCur, int chargeMax, bool active, float timeRemaining, float timeMax)
        {
            EnsureAwakenGauge();
            if (awakenGauge == null) return;

            float fill;
            Color color;
            string label;
            if (active)
            {
                float denom = timeMax > 0f ? timeMax : 1f;
                fill = Mathf.Clamp01(timeRemaining / denom);
                color = awakenActiveColor;
                label = $"{Mathf.Max(0f, timeRemaining):F1}s";
            }
            else
            {
                int max = Mathf.Max(1, chargeMax);
                int cur = Mathf.Clamp(chargeCur, 0, max);
                fill = (float)cur / max;
                color = awakenChargeColor;
                label = $"{cur}/{max}";
            }

            awakenGauge.value = fill;
            if (_awakenGaugeFill != null)
                _awakenGaugeFill.color = new Color(color.r, color.g, color.b, 1f);
            if (awakenGaugeLabel != null) awakenGaugeLabel.text = label;
        }

        // 각성 게이지가 없으면 슬라이더/배경/채움/라벨을 코드로 생성
        void EnsureAwakenGauge()
        {
            if (awakenGauge != null)
            {
                if (_awakenGaugeFill == null) _awakenGaugeFill = ResolveGaugeFill(awakenGauge);
                return;
            }

            // anchor가 있으면 그 부모, 없으면 캔버스를 부모로 결정
            Transform parent = null;
            if (awakenGaugeAnchor != null)
                parent = awakenGaugeAnchor.parent != null ? awakenGaugeAnchor.parent : (Transform)awakenGaugeAnchor;
            if (parent == null)
            {
                Canvas canvas = ResolveTargetCanvas();
                parent = canvas != null ? canvas.transform : transform;
            }
            Debug.Log($"[CardHandHUD] AwakenGauge 자동 생성 — parent='{(parent != null ? parent.name : "(null)")}', anchor='{(awakenGaugeAnchor != null ? awakenGaugeAnchor.name : "(없음, 캔버스 중앙 fallback)")}'");

            // 게이지 루트 생성
            var go = new GameObject("AwakenGauge", typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = awakenGaugeSize;

            // 배경 생성
            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(rect, false);
            var bgRect = (RectTransform)bgGo.transform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImg = bgGo.GetComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.6f);
            bgImg.raycastTarget = false;

            // Fill Area 생성
            var fillAreaGo = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaGo.transform.SetParent(rect, false);
            var fillAreaRect = (RectTransform)fillAreaGo.transform;
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = new Vector2(2f, 2f);
            fillAreaRect.offsetMax = new Vector2(-2f, -2f);

            // Fill 생성
            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(fillAreaRect, false);
            var fillRect = (RectTransform)fillGo.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImg = fillGo.GetComponent<Image>();
            fillImg.color = awakenChargeColor;
            fillImg.raycastTarget = false;

            var slider = go.GetComponent<Slider>();
            slider.transition = Selectable.Transition.None;
            slider.interactable = false;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.fillRect = fillRect;
            slider.targetGraphic = fillImg;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
            slider.direction = Slider.Direction.LeftToRight;

            awakenGauge = slider;
            _awakenGaugeFill = fillImg;

            // 라벨이 없으면 생성
            if (awakenGaugeLabel == null)
            {
                var labelGo = new GameObject("Label", typeof(RectTransform));
                labelGo.transform.SetParent(rect, false);
                var labelRect = (RectTransform)labelGo.transform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;
                var tmp = labelGo.AddComponent<TextMeshProUGUI>();
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontSize = 16;
                tmp.fontStyle = FontStyles.Bold;
                tmp.color = Color.white;
                tmp.text = "";
                tmp.raycastTarget = false;
                awakenGaugeLabel = tmp;
            }

            RefitAwakenGaugeToAnchor();

            // anchor 미배선 시 상단 중앙 가시 위치로 배치
            if (awakenGaugeAnchor == null && awakenGauge != null)
            {
                var grect = (RectTransform)awakenGauge.transform;
                grect.anchorMin = grect.anchorMax = grect.pivot = new Vector2(0.5f, 1f);
                grect.anchoredPosition = awakenGaugeFallbackPos;
                grect.SetAsLastSibling();
            }
        }

        // 각성 게이지를 anchor의 정렬/위치에 맞춰 재배치
        void RefitAwakenGaugeToAnchor()
        {
            if (awakenGauge == null || awakenGaugeAnchor == null) return;
            var rect = (RectTransform)awakenGauge.transform;
            var anchorParent = awakenGaugeAnchor.parent != null
                ? awakenGaugeAnchor.parent
                : (Transform)awakenGaugeAnchor;
            if (rect.parent != anchorParent) rect.SetParent(anchorParent, false);

            // LayoutGroup의 위치 덮어쓰기를 막기 위해 layout에서 분리
            var le = awakenGauge.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = awakenGauge.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;

            // anchor와 같은 정렬 기준에 오프셋 적용
            rect.anchorMin = awakenGaugeAnchor.anchorMin;
            rect.anchorMax = awakenGaugeAnchor.anchorMax;
            rect.pivot = awakenGaugeAnchor.pivot;
            rect.anchoredPosition = awakenGaugeAnchor.anchoredPosition + awakenGaugeOffset;
            rect.sizeDelta = awakenGaugeSize;
            rect.SetAsLastSibling();
        }

        // 각성 게이지를 형제 중 최상단으로 올림
        public void BringAwakenGaugeToFront()
        {
            if (awakenGauge != null)
                ((RectTransform)awakenGauge.transform).SetAsLastSibling();
        }

        // 슬라이더의 fill Image를 찾아 반환
        static Image ResolveGaugeFill(Slider slider)
        {
            if (slider == null) return null;
            if (slider.fillRect != null)
            {
                var img = slider.fillRect.GetComponent<Image>();
                if (img != null) return img;
            }
            return slider.GetComponentInChildren<Image>();
        }

        // 각성 활성/비활성에 따라 콤보 UI와 딤을 토글
        public void SetAwakenMode(bool active)
        {
            if (comboSlotPanel != null) comboSlotPanel.SetActive(active);
            if (comboSkillPanel != null) comboSkillPanel.SetActive(active);
            if (awakenHistoryContainer != null) awakenHistoryContainer.gameObject.SetActive(active && awakenHistoryEnabled);

            if (active) ShowAwakenDim();
            else HideAwakenDim();
        }

        // 각성 전용 딤을 켜고 손패/콤보 UI를 그 위로 올림
        void ShowAwakenDim()
        {
            EnsureDimOverlay();
            if (dimOverlay != null)
            {
                // 딤 색을 각성 전용으로 변경
                var img = dimOverlay.GetComponent<Image>();
                if (img != null) img.color = awakenDimColor;
                dimOverlay.gameObject.SetActive(true);
                dimOverlay.SetAsLastSibling();
            }
            // 손패/콤보 UI를 딤 위로 올려 어두워지지 않게 함
            if (handRoot != null) handRoot.SetAsLastSibling();
            if (comboSlotPanel != null) comboSlotPanel.transform.SetAsLastSibling();
            if (comboSkillPanel != null) comboSkillPanel.transform.SetAsLastSibling();
            if (awakenHistoryContainer != null) awakenHistoryContainer.SetAsLastSibling();
            if (awakenCountText != null) awakenCountText.transform.SetAsLastSibling();
            BringAwakenGaugeToFront();
        }

        // 각성 딤을 끄고 색을 기본으로 복구
        void HideAwakenDim()
        {
            if (dimOverlay != null)
            {
                dimOverlay.gameObject.SetActive(false);
                // 다음 선택/픽커 모드를 위해 기본 딤 색으로 복구
                var img = dimOverlay.GetComponent<Image>();
                if (img != null) img.color = dimColor;
            }
            if (handRoot != null) handRoot.SetAsLastSibling();
        }

        private readonly List<Image> _awakenHistoryViews = new List<Image>(); // 입력 히스토리 뷰 풀

        // 각성 입력 히스토리 표시 여부를 런타임에 토글
        public void SetAwakenHistoryEnabled(bool enabled)
        {
            awakenHistoryEnabled = enabled;
            if (!enabled && awakenHistoryContainer != null)
                awakenHistoryContainer.gameObject.SetActive(false);
        }

        // 각성 입력 히스토리를 좌측 세로 격자로 갱신
        public void UpdateAwakenInputHistory(IList<CardElement> history)
        {
            // 비활성화 상태면 컨테이너를 생성하지 않고 숨김
            if (!awakenHistoryEnabled)
            {
                if (awakenHistoryContainer != null) awakenHistoryContainer.gameObject.SetActive(false);
                return;
            }

            EnsureAwakenHistoryContainer();
            if (awakenHistoryContainer == null) return;

            int total = history != null ? history.Count : 0;
            int max = Mathf.Max(1, awakenHistoryMaxItems);
            // 최근 max개만 표시(오래된 것은 잘림)
            int start = Mathf.Max(0, total - max);
            int visible = total - start;

            // 필요 만큼 뷰 풀 확장
            while (_awakenHistoryViews.Count < visible)
            {
                var go = new GameObject($"AwakenHist_{_awakenHistoryViews.Count}", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(awakenHistoryContainer, false);
                var rect = (RectTransform)go.transform;
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = awakenHistoryItemSize;
                var img = go.GetComponent<Image>();
                img.raycastTarget = false;
                _awakenHistoryViews.Add(img);
            }

            // 한 열에 itemsPerColumn개씩 배치하고 초과 시 새 열로 wrap
            int itemsPerCol = Mathf.Max(1, awakenHistoryItemsPerColumn);
            float colW = awakenHistoryItemSize.x + awakenHistoryColumnSpacing;
            float rowH = awakenHistoryItemSize.y + awakenHistoryItemSpacing;
            for (int i = 0; i < _awakenHistoryViews.Count; i++)
            {
                var img = _awakenHistoryViews[i];
                if (i < visible)
                {
                    img.gameObject.SetActive(true);
                    var rect = (RectTransform)img.transform;
                    int col = i / itemsPerCol;
                    int row = i % itemsPerCol;
                    rect.anchoredPosition = new Vector2(col * colW, -row * rowH);

                    var element = history[start + i];
                    var sp = cardPrefab != null ? cardPrefab.GetElementSprite(element) : null;
                    if (sp != null)
                    {
                        img.sprite = sp;
                        img.color = Color.white;
                    }
                    else
                    {
                        img.sprite = null;
                        img.color = ColorForElement(element);
                    }
                }
                else
                {
                    img.gameObject.SetActive(false);
                }
            }
        }

        // 각성 입력 히스토리 컨테이너가 없으면 좌측 중앙에 생성
        void EnsureAwakenHistoryContainer()
        {
            if (awakenHistoryContainer != null) return;
            Canvas canvas = ResolveTargetCanvas();
            Transform parent = canvas != null ? canvas.transform : transform;

            var go = new GameObject("AwakenHistoryContainer", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            awakenHistoryContainer = (RectTransform)go.transform;
            // 좌측 중앙 정렬
            awakenHistoryContainer.anchorMin = new Vector2(0f, 0.5f);
            awakenHistoryContainer.anchorMax = new Vector2(0f, 0.5f);
            awakenHistoryContainer.pivot = new Vector2(0f, 0.5f);
            awakenHistoryContainer.anchoredPosition = awakenHistoryAnchoredPos;
            awakenHistoryContainer.sizeDelta = new Vector2(awakenHistoryItemSize.x, 600f);
        }

        // 콤보 슬롯 3칸을 현재 입력 시퀀스로 갱신(빈 칸은 회색)
        public void UpdateComboSlot(IList<CardElement> input)
        {
            EnsureComboSlotImagesBound();

            if (comboSlotImages == null || comboSlotImages.Length == 0)
            {
                Debug.LogWarning("[CardHandHUD] UpdateComboSlot: comboSlotImages가 비어있고 자동 탐색도 실패. ComboSlotPanel의 자식 Image 3개를 인스펙터에 연결하세요.");
                return;
            }

            for (int i = 0; i < comboSlotImages.Length; i++)
            {
                var img = comboSlotImages[i];
                if (img == null) continue;

                if (input != null && i < input.Count)
                {
                    Sprite s = cardPrefab != null ? cardPrefab.GetElementSprite(input[i]) : null;
                    if (s != null)
                    {
                        img.sprite = s;
                        img.color = Color.white;
                    }
                    else
                    {
                        img.sprite = null;
                        img.color = ColorForElement(input[i]);
                    }
                    img.enabled = true;
                }
                else
                {
                    img.sprite = null;
                    img.color = emptyComboSlotColor;
                    img.enabled = true;
                }
            }
        }

        // 콤보 슬롯 Image가 미연결이면 패널 자식에서 3개를 자동 탐색
        void EnsureComboSlotImagesBound()
        {
            bool needBind = comboSlotImages == null || comboSlotImages.Length < 3
                || comboSlotImages[0] == null || comboSlotImages[1] == null || comboSlotImages[2] == null;
            if (!needBind) return;
            if (comboSlotPanel == null) return;

            var collected = new List<Image>();
            // 직계 자식 우선 수집
            for (int i = 0; i < comboSlotPanel.transform.childCount && collected.Count < 3; i++)
            {
                var img = comboSlotPanel.transform.GetChild(i).GetComponent<Image>();
                if (img != null) collected.Add(img);
            }
            // 부족하면 모든 후손에서 수집
            if (collected.Count < 3)
            {
                var all = comboSlotPanel.GetComponentsInChildren<Image>(true);
                foreach (var img in all)
                {
                    if (img == null) continue;
                    if (img.gameObject == comboSlotPanel) continue; // 패널 자체 제외
                    if (collected.Contains(img)) continue;
                    collected.Add(img);
                    if (collected.Count >= 3) break;
                }
            }

            if (collected.Count >= 3)
            {
                comboSlotImages = new Image[3] { collected[0], collected[1], collected[2] };
                Debug.Log("[CardHandHUD] 콤보 슬롯 Image 3개 자동 바인딩 완료.");
            }
        }

        private Coroutine[] _comboSlotPopCos;   // 콤보 슬롯 칸별 팝 코루틴
        private Vector3[] _comboSlotBaseScales; // 콤보 슬롯 칸별 기준 스케일

        // 콤보 슬롯 slotIndex 칸을 팝(+플래시)으로 강조 — 각성 중 속성이 들어올 때 호출
        public void PlayComboSlotInputEffect(int slotIndex)
        {
            if (!comboSlotPopEnabled) return;
            EnsureComboSlotImagesBound();
            if (comboSlotImages == null || slotIndex < 0 || slotIndex >= comboSlotImages.Length) return;
            if (comboSlotImages[slotIndex] == null) return;

            // 칸 수에 맞춰 풀/기준 스케일 초기화
            if (_comboSlotPopCos == null || _comboSlotPopCos.Length != comboSlotImages.Length)
            {
                _comboSlotPopCos = new Coroutine[comboSlotImages.Length];
                _comboSlotBaseScales = new Vector3[comboSlotImages.Length];
                for (int i = 0; i < comboSlotImages.Length; i++)
                    _comboSlotBaseScales[i] = comboSlotImages[i] != null
                        ? comboSlotImages[i].transform.localScale : Vector3.one;
            }

            if (_comboSlotPopCos[slotIndex] != null) StopCoroutine(_comboSlotPopCos[slotIndex]);
            _comboSlotPopCos[slotIndex] = StartCoroutine(ComboSlotPopRoutine(slotIndex));
        }

        // 콤보 슬롯 한 칸을 커졌다 줄이며(+흰색 플래시) 강조하는 코루틴
        System.Collections.IEnumerator ComboSlotPopRoutine(int slotIndex)
        {
            var img = comboSlotImages[slotIndex];
            if (img == null) { _comboSlotPopCos[slotIndex] = null; yield break; }

            Transform tr = img.transform;
            Vector3 baseScale = _comboSlotBaseScales[slotIndex];
            Vector3 peak = baseScale * Mathf.Max(1f, comboSlotPopScale);
            Color baseColor = img.color; // UpdateComboSlot가 직전에 칠한 속성 색
            float dur = Mathf.Max(0.01f, comboSlotPopDuration);
            const float upPortion = 0.3f; // 앞 30%는 확대, 나머지는 복귀 구간

            float t = 0f;
            while (t < dur)
            {
                if (img == null) { _comboSlotPopCos[slotIndex] = null; yield break; }
                t += Time.deltaTime;
                float n = Mathf.Clamp01(t / dur);

                // 0→peak(ease-out) 후 peak→base(선형) 엔벨로프
                float env = n < upPortion
                    ? 1f - (1f - n / upPortion) * (1f - n / upPortion)
                    : 1f - (n - upPortion) / (1f - upPortion);
                env = Mathf.Clamp01(env);

                tr.localScale = Vector3.Lerp(baseScale, peak, env);
                if (comboSlotFlashEnabled) img.color = Color.Lerp(baseColor, Color.white, env * 0.7f);
                yield return null;
            }

            tr.localScale = baseScale;
            img.color = baseColor;
            _comboSlotPopCos[slotIndex] = null;
        }

        private readonly List<SkillListItemUI> _comboSkillItems = new List<SkillListItemUI>(); // 콤보 스킬 항목 풀

        // 콤보 스킬 목록을 프리팹으로 표시(쿨다운은 흐리게 + 남은 횟수)
        public void UpdateComboSkillList(IList<ComboSkillDef> skills, int[] cooldownRemaining)
        {
            if (comboSkillItemPrefab == null)
            {
                Debug.LogWarning("[CardHandHUD] comboSkillItemPrefab이 미연결 — 콤보 스킬 목록 표시 불가.");
                return;
            }

            RectTransform container = comboSkillItemContainer != null
                ? comboSkillItemContainer
                : (comboSkillPanel != null ? comboSkillPanel.transform as RectTransform : null);

            if (container == null)
            {
                Debug.LogWarning("[CardHandHUD] 콤보 스킬 컨테이너가 없음 — comboSkillItemContainer 또는 comboSkillPanel 연결 필요.");
                return;
            }

            // 필요 만큼 항목 인스턴스화
            int needed = skills != null ? skills.Count : 0;
            while (_comboSkillItems.Count < needed)
            {
                var item = Instantiate(comboSkillItemPrefab, container);
                _comboSkillItems.Add(item);
            }

            // 속성 스프라이트는 카드 프리팹 매핑을 공유
            Sprite fireSp  = cardPrefab != null ? cardPrefab.GetElementSprite(CardElement.Fire)  : null;
            Sprite waterSp = cardPrefab != null ? cardPrefab.GetElementSprite(CardElement.Water) : null;
            Sprite windSp  = cardPrefab != null ? cardPrefab.GetElementSprite(CardElement.Wind)  : null;
            Sprite earthSp = cardPrefab != null ? cardPrefab.GetElementSprite(CardElement.Earth) : null;

            // 각 항목 표시 갱신
            for (int i = 0; i < _comboSkillItems.Count; i++)
            {
                var item = _comboSkillItems[i];
                if (item == null) continue;

                if (i < needed && skills[i] != null)
                {
                    item.gameObject.SetActive(true);
                    int cooldown = (cooldownRemaining != null && i < cooldownRemaining.Length)
                        ? cooldownRemaining[i] : 0;
                    item.SetupForCombo(skills[i], cooldown, fireSp, waterSp, windSp, earthSp);
                }
                else
                {
                    item.gameObject.SetActive(false);
                }
            }
        }

        // 속성에 대응하는 표시 색을 반환
        static Color ColorForElement(CardElement element) => element switch
        {
            CardElement.Fire    => new Color(0.95f, 0.55f, 0.40f, 1f),
            CardElement.Water   => new Color(0.50f, 0.75f, 0.95f, 1f),
            CardElement.Wind    => new Color(0.60f, 0.90f, 0.60f, 1f),
            CardElement.Earth   => new Color(0.85f, 0.70f, 0.45f, 1f),
            CardElement.Neutral => new Color(0.70f, 0.70f, 0.70f, 1f),
            _ => new Color(0.5f, 0.5f, 0.5f, 1f)
        };

        // 현재 손패에 맞춰 카드 뷰/슬롯/카운트 텍스트를 갱신
        public void Refresh()
        {
            if (_deck == null) return;

            var hand = _deck.Hand;
            // 손패 수에 맞춰 슬롯 위치 재계산
            PositionSlotAnchorsForCount(hand.Count);

            int newCardOrder = 0; // 이번에 새로 들어온 카드 순번(stagger 계산용)

            for (int i = 0; i < SLOT_COUNT; i++)
            {
                NewCardView view = i < _cardViews.Count ? _cardViews[i] : null;
                if (view == null && cardPrefab != null && i < _slotAnchors.Count)
                {
                    view = Instantiate(cardPrefab, _slotAnchors[i]);
                    view.Bind(this, i);
                    _cardViews.Add(view);
                }
                if (view == null) continue;

                // 등장 연출 중이 아니면 카드 뷰를 슬롯으로 복원
                if (i < _slotAnchors.Count && !view.IsPlayingDrawIntro)
                {
                    RectTransform vrect = (RectTransform)view.transform;
                    vrect.SetParent(_slotAnchors[i], false);
                    vrect.anchorMin = new Vector2(0.5f, 0.5f);
                    vrect.anchorMax = new Vector2(0.5f, 0.5f);
                    vrect.pivot = new Vector2(0.5f, 0.5f);
                    vrect.anchoredPosition = Vector2.zero;
                    vrect.localRotation = Quaternion.identity; // 슬롯의 fan 회전을 따름
                    view.ResetToHomeScale();
                }

                CardInstance card = i < hand.Count ? hand[i] : null;
                bool isNewToHand = card != null && !_prevHandSet.Contains(card);
                view.SetCard(card);

                // 연출 중이 아닐 때만 홈 좌표 재캡처
                if (!view.IsPlayingDrawIntro)
                    view.CaptureHome();

                // 새로 들어온 카드만 등장 연출 재생
                if (isNewToHand)
                    view.PlayDrawIntro(newCardOrder++ * Mathf.Max(0f, drawIntroStagger));
            }

            // 다음 Refresh의 신규 판별용으로 현재 패를 스냅샷
            _prevHandSet.Clear();
            for (int i = 0; i < hand.Count; i++)
                if (hand[i] != null) _prevHandSet.Add(hand[i]);

            // 카운트업 중이 아닐 때만 덱 수 텍스트 갱신
            if (drawCountText != null && !_drawCountAnimating) drawCountText.text = $"{_deck.DrawCount}";
            if (discardCountText != null)
            {
                int discardCount = _deck.DiscardCount;
                discardCountText.text = $"{discardCount}";
                // 값이 실제로 바뀐 경우에만 팝 강조
                if (_prevDiscardCount != int.MinValue && discardCount != _prevDiscardCount)
                    PlayDiscardPop();
                _prevDiscardCount = discardCount;
            }
        }

        // 버린 더미 숫자 변화 시 팝 강조를 시작
        void PlayDiscardPop()
        {
            if (!discardPopEnabled || discardCountText == null) return;

            // 기준 스케일은 최초 1회만 캡처
            if (!_discardCountBaseCaptured)
            {
                _discardCountBaseScale = discardCountText.transform.localScale;
                _discardCountBaseCaptured = true;
            }

            if (_discardPopCo != null) StopCoroutine(_discardPopCo);
            _discardPopCo = StartCoroutine(DiscardPopRoutine());
        }

        // 버린 더미 텍스트를 커졌다 줄이는 팝 코루틴
        System.Collections.IEnumerator DiscardPopRoutine()
        {
            if (discardCountText == null) { _discardPopCo = null; yield break; }

            Transform tr = discardCountText.transform;
            Vector3 peak = _discardCountBaseScale * Mathf.Max(1f, discardPopScale);
            float dur = Mathf.Max(0.01f, discardPopDuration);
            const float upPortion = 0.35f; // 앞 35%는 확대, 나머지는 복귀 구간

            float t = 0f;
            while (t < dur)
            {
                if (discardCountText == null) { _discardPopCo = null; yield break; }
                t += Time.deltaTime;
                float n = Mathf.Clamp01(t / dur);

                // 0→peak(ease-out) 후 peak→base(선형) 엔벨로프
                float env = n < upPortion
                    ? 1f - (1f - n / upPortion) * (1f - n / upPortion)
                    : 1f - (n - upPortion) / (1f - upPortion);
                env = Mathf.Clamp01(env);

                tr.localScale = Vector3.Lerp(_discardCountBaseScale, peak, env);
                yield return null;
            }

            tr.localScale = _discardCountBaseScale;
            _discardPopCo = null;
        }

        // 리셔플 시 카운트업 연출을 시작
        void HandleReshuffled(int count)
        {
            if (!reshuffleCountUpEnabled || drawCountText == null || count <= 0) return;
            if (_drawCountCo != null) StopCoroutine(_drawCountCo);
            _drawCountCo = StartCoroutine(DrawCountUpRoutine(count));
        }

        // 덱 수를 1씩 위에서 떨어뜨리며 올리는 카운트업 코루틴
        System.Collections.IEnumerator DrawCountUpRoutine(int count)
        {
            var rt = drawCountText.transform as RectTransform;
            if (rt == null) { _drawCountCo = null; yield break; }

            // 홈 위치/스케일은 최초 1회만 캡처
            if (!_drawCountBaseCaptured)
            {
                _drawCountHomePos = rt.anchoredPosition;
                _drawCountBaseScale = rt.localScale;
                _drawCountBaseCaptured = true;
            }

            _drawCountAnimating = true; // 진행 중 Refresh의 덮어쓰기 차단

            int steps = Mathf.Min(count, Mathf.Max(1, reshuffleMaxSteps));
            Vector3 landPeak = _drawCountBaseScale * Mathf.Max(1f, reshuffleLandScale);
            float stepDur = Mathf.Max(0.01f, reshuffleStepDuration);
            Vector2 dropStart = _drawCountHomePos + Vector2.up * reshuffleDropHeight;

            for (int v = 1; v <= steps; v++)
            {
                if (drawCountText == null) break;
                drawCountText.text = v.ToString();

                float t = 0f;
                while (t < stepDur)
                {
                    if (drawCountText == null) break;
                    t += Time.deltaTime;
                    float n = Mathf.Clamp01(t / stepDur);
                    float ease = 1f - (1f - n) * (1f - n); // ease-out 낙하
                    rt.anchoredPosition = Vector2.Lerp(dropStart, _drawCountHomePos, ease);

                    // 착지 직전 마지막 25%에 삼각 엔벨로프로 팝
                    float popEnv = 0f;
                    if (n >= 0.75f) { float m = (n - 0.75f) / 0.25f; popEnv = 1f - Mathf.Abs(2f * m - 1f); }
                    rt.localScale = Vector3.Lerp(_drawCountBaseScale, landPeak, Mathf.Clamp01(popEnv));
                    yield return null;
                }

                rt.anchoredPosition = _drawCountHomePos;
                rt.localScale = _drawCountBaseScale;

                if (reshuffleStepGap > 0f) yield return new WaitForSeconds(reshuffleStepGap);
            }

            // 연출 종료 후 실제 현재 덱 수로 정착
            if (drawCountText != null) drawCountText.text = $"{(_deck != null ? _deck.DrawCount : steps)}";
            rt.anchoredPosition = _drawCountHomePos;
            rt.localScale = _drawCountBaseScale;
            _drawCountAnimating = false;
            _drawCountCo = null;
        }

        // 드래그 위치가 사용 임계선을 넘으면 카드 사용을 시도
        public bool TryUseFromDrag(NewCardView view, PointerEventData ev)
        {
            if (view.Card == null) return false;
            // 선택/픽커/더미보기 중에는 사용 금지
            if (IsSelectionMode || IsPickerMode || IsViewerMode) return false;
            if (UseCardCallback == null) return false;
            if (ev.position.y < useThresholdY) return false;
            return UseCardCallback(view.Card);
        }

        // 클릭(더블클릭)으로 카드 사용을 시도
        public bool TryUseFromClick(NewCardView view)
        {
            if (view == null || view.Card == null) return false;
            if (IsSelectionMode || IsPickerMode || IsViewerMode) return false;
            if (UseCardCallback == null) return false;
            return UseCardCallback(view.Card);
        }

        [Header("카드 사용 연출 — 중앙 표시")]
        [Tooltip("카드 사용 시 화면 중앙에 카드를 잠깐 띄웠다 사라지게 하는 연출 ON/OFF.")]
        [SerializeField] private bool cardUsePresentEnabled = true; // 중앙 표시 연출 사용 여부
        [Tooltip("각성 중 카드 사용 시 중앙 표시 연출을 켤지 여부. OFF면 각성 중에는 중앙 카드 연출을 생략(효과음은 유지). 일반 사용에는 영향 없음.")]
        [SerializeField] private bool awakenCardUsePresentEnabled = true; // 각성 중 중앙 표시 연출 사용 여부
        [Tooltip("중앙 표시 위치(캔버스 중앙 기준 오프셋). y 양수 = 중앙보다 위.")]
        [SerializeField] private Vector2 cardUsePresentPos = new Vector2(0f, 60f); // 중앙 표시 위치
        [Tooltip("중앙 표시 시 카드 크기 배율(프리팹 기본 스케일 기준).")]
        [SerializeField] private float cardUsePresentScale = 1.5f; // 중앙 표시 크기 배율
        [Tooltip("등장(커지며 나타남) 시간(초).")]
        [SerializeField] private float cardUsePresentEnterTime = 0.15f; // 등장 시간
        [Tooltip("중앙에서 머무는 시간(초).")]
        [SerializeField] private float cardUsePresentHold = 0.35f; // 유지 시간
        [Tooltip("퇴장(사라짐) 시간(초). 이 직전에 효과가 발동된다.")]
        [SerializeField] private float cardUsePresentExitTime = 0.2f; // 퇴장 시간

        private Coroutine _interruptablePresentCo; // 교체 가능(각성용) 연출 코루틴
        private NewCardView _interruptablePresentView; // 교체 가능 연출 카드 뷰
        private Coroutine _normalPresentCo; // 일반(차단) 연출 코루틴
        private NewCardView _normalPresentView; // 일반 연출 카드 뷰

        // 사용한 카드를 중앙에 띄웠다 사라뜨리고 사라질 때 onDisappear 호출
        public void PlayCardUsePresentation(CardInstance card, System.Action onDisappear, bool interruptable = false)
        {
            if (card != null) SfxManager.Instance?.PlayCardUse();
            // interruptable == true 는 각성 중 카드 사용 경로 — 전용 토글로 중앙 연출만 생략(효과음은 위에서 이미 재생)
            bool presentEnabled = cardUsePresentEnabled && (!interruptable || awakenCardUsePresentEnabled);
            if (!presentEnabled || card == null || cardPrefab == null || !isActiveAndEnabled)
            {
                onDisappear?.Invoke();
                return;
            }

            if (interruptable)
            {
                // 직전 각성용 연출을 정리하고 새 카드로 교체
                if (_interruptablePresentCo != null) StopCoroutine(_interruptablePresentCo);
                if (_interruptablePresentView != null) Destroy(_interruptablePresentView.gameObject);
                _interruptablePresentView = null;
                _interruptablePresentCo = StartCoroutine(CardUsePresentationRoutine(card, onDisappear, true));
            }
            else
            {
                _normalPresentCo = StartCoroutine(CardUsePresentationRoutine(card, onDisappear, false));
            }
        }

        // 진행 중인 카드 사용 연출을 onDisappear 호출 없이 즉시 정리
        public void CancelCardUsePresentations()
        {
            if (_normalPresentCo != null) { StopCoroutine(_normalPresentCo); _normalPresentCo = null; }
            if (_normalPresentView != null) { Destroy(_normalPresentView.gameObject); _normalPresentView = null; }
            if (_interruptablePresentCo != null) { StopCoroutine(_interruptablePresentCo); _interruptablePresentCo = null; }
            if (_interruptablePresentView != null) { Destroy(_interruptablePresentView.gameObject); _interruptablePresentView = null; }
        }

        // 카드 사용 중앙 표시 연출(등장→유지→효과발동→퇴장) 코루틴
        System.Collections.IEnumerator CardUsePresentationRoutine(CardInstance card, System.Action onDisappear, bool interruptable)
        {
            Canvas canvas = ResolveTargetCanvas();
            Transform parent = canvas != null ? canvas.transform : transform;

            // 손패 풀과 독립된 임시 카드 뷰 생성
            var view = Instantiate(cardPrefab, parent);
            if (interruptable) _interruptablePresentView = view;
            else _normalPresentView = view;
            view.Bind(this, -1);
            var vrect = (RectTransform)view.transform;
            vrect.anchorMin = vrect.anchorMax = new Vector2(0.5f, 0.5f);
            vrect.pivot = new Vector2(0.5f, 0.5f);
            vrect.anchoredPosition = cardUsePresentPos;
            vrect.localRotation = Quaternion.identity;
            view.SetCard(card);
            view.CaptureHome();
            vrect.SetAsLastSibling();

            var cg = view.GetComponent<CanvasGroup>();
            if (cg == null) cg = view.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false; // 연출 카드는 클릭 차단

            Vector3 targetScale = vrect.localScale * Mathf.Max(0.01f, cardUsePresentScale);

            // 시작 상태는 작고 투명
            vrect.localScale = targetScale * 0.6f;
            cg.alpha = 0f;

            // 1) 등장 — 목표 크기·불투명으로 확대
            float t = 0f;
            float enter = Mathf.Max(0.0001f, cardUsePresentEnterTime);
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / enter;
                float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
                vrect.localScale = Vector3.LerpUnclamped(targetScale * 0.6f, targetScale, e);
                cg.alpha = Mathf.Clamp01(e);
                yield return null;
            }
            vrect.localScale = targetScale;
            cg.alpha = 1f;

            // 2) 유지 대기
            float hold = Mathf.Max(0f, cardUsePresentHold);
            while (hold > 0f) { hold -= Time.unscaledDeltaTime; yield return null; }

            // 3) 사라지는 순간 효과 발동
            onDisappear?.Invoke();

            // 4) 퇴장 — 페이드아웃 + 확대
            t = 0f;
            float exit = Mathf.Max(0.0001f, cardUsePresentExitTime);
            Vector3 exitScale = targetScale * 1.12f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / exit;
                float e = Mathf.Clamp01(t);
                vrect.localScale = Vector3.LerpUnclamped(targetScale, exitScale, e);
                cg.alpha = 1f - e;
                yield return null;
            }

            Destroy(view.gameObject);
            if (interruptable)
            {
                _interruptablePresentView = null;
                _interruptablePresentCo = null;
            }
            else
            {
                _normalPresentView = null;
                _normalPresentCo = null;
            }
        }

        // 패에서 카드 1장 선택을 받는 모드로 진입(필터/제외 카드 적용)
        public void EnterSelectionMode(string promptMessage, System.Action<CardInstance> callback,
            System.Func<CardInstance, bool> filter = null, CardInstance excludeCard = null)
        {
            _selectionCallback = callback;
            _selectionFilter = filter;
            _selectionExcludeCard = excludeCard;

            // 화면을 어둡게 하고 손패를 위로 올려 강조
            EnsureDimOverlay();
            if (dimOverlay != null)
            {
                dimOverlay.gameObject.SetActive(true);
                dimOverlay.SetAsLastSibling();
            }
            if (handRoot != null) handRoot.SetAsLastSibling();

            if (selectionPromptText != null)
            {
                selectionPromptText.text = promptMessage;
                selectionPromptText.gameObject.SetActive(true);
                selectionPromptText.transform.SetAsLastSibling();
            }
        }

        // 카드 선택 모드를 종료하고 딤/안내를 끔
        public void ExitSelectionMode()
        {
            _selectionCallback = null;
            _selectionFilter = null;
            _selectionExcludeCard = null;

            if (dimOverlay != null) dimOverlay.gameObject.SetActive(false);

            if (selectionPromptText != null)
                selectionPromptText.gameObject.SetActive(false);
        }

        private RectTransform _pickerRoot;       // 픽커 중앙 컨테이너(ScrollRect)
        private RectTransform _pickerViewport;   // 픽커 마스크 영역
        private RectTransform _pickerContent;    // 픽커 카드들이 들어가는 가변 높이 컨테이너
        private readonly List<NewCardView> _pickerViews = new List<NewCardView>(); // 픽커 카드 뷰 목록
        private System.Action<CardInstance> _pickerCallback; // 픽커 선택 콜백
        public bool IsPickerMode => _pickerCallback != null; // 픽커 모드 여부

        [Header("카드 픽커 — 그리드/스크롤")]
        [Tooltip("픽커 한 행에 표시할 최대 카드 수.")]
        [SerializeField] private int pickerColumns = 4; // 한 행 최대 카드 수
        [Tooltip("픽커 카드 가로 간격(중심→중심, 픽셀). 카드 폭 300 기준 360~520 권장.")]
        [SerializeField] private float pickerColumnSpacing = 480f; // 픽커 가로 간격
        [Tooltip("픽커 카드 세로 간격(중심→중심, 픽셀). 카드 높이 400 기준 480~640 권장.")]
        [SerializeField] private float pickerRowSpacing = 620f; // 픽커 세로 간격
        [Tooltip("픽커 viewport(보이는 영역) 크기. 가로는 columns × columnSpacing 이상 + 좌우 패딩 권장.")]
        [SerializeField] private Vector2 pickerViewportSize = new Vector2(1800f, 900f); // 픽커 뷰포트 크기
        [Tooltip("컨텐츠 위쪽/아래쪽 패딩 — 첫/마지막 행이 viewport 가장자리에 붙지 않도록.")]
        [SerializeField] private Vector2 pickerContentPadding = new Vector2(0f, 240f); // 픽커 컨텐츠 패딩
        [Tooltip("마우스 휠 스크롤 감도.")]
        [SerializeField] private float pickerScrollSensitivity = 60f; // 스크롤 감도

        // 임의 카드 목록에서 1장을 선택받는 픽커 모드로 진입
        public void EnterCardPickerMode(string promptMessage, IList<CardInstance> cards,
            System.Action<CardInstance> callback)
        {
            if (cards == null || cards.Count == 0)
            {
                callback?.Invoke(null);
                return;
            }
            if (cardPrefab == null)
            {
                Debug.LogWarning("[CardHandHUD] EnterCardPickerMode — cardPrefab 미설정. 무작위 자동 선택 폴백.");
                callback?.Invoke(cards[Random.Range(0, cards.Count)]);
                return;
            }

            _pickerCallback = callback;

            EnsureDimOverlay();
            EnsurePickerRoot();

            // handRoot(맨 뒤) → dim → 안내문 → pickerRoot(최상단) 순으로 정렬
            if (handRoot != null) handRoot.SetAsFirstSibling();
            if (dimOverlay != null)
            {
                dimOverlay.gameObject.SetActive(true);
                dimOverlay.SetAsLastSibling();
            }
            if (selectionPromptText != null)
            {
                selectionPromptText.text = promptMessage;
                selectionPromptText.gameObject.SetActive(true);
                selectionPromptText.transform.SetAsLastSibling();
            }
            _pickerRoot.gameObject.SetActive(true);
            _pickerRoot.SetAsLastSibling();
            BuildPickerCards(cards);
            BringAwakenGaugeToFront();
        }

        // 픽커 모드를 종료하고 카드/딤/안내를 정리
        public void ExitPickerMode()
        {
            _pickerCallback = null;
            ClearPickerViews();
            if (_pickerRoot != null) _pickerRoot.gameObject.SetActive(false);
            if (dimOverlay != null) dimOverlay.gameObject.SetActive(false);
            if (selectionPromptText != null) selectionPromptText.gameObject.SetActive(false);
            // 뒤로 보냈던 손패를 다시 최상단으로 복원
            if (handRoot != null) handRoot.SetAsLastSibling();
            BringAwakenGaugeToFront();
        }

        // 픽커 루트(ScrollRect/뷰포트/콘텐츠)가 없으면 생성
        void EnsurePickerRoot()
        {
            if (_pickerRoot != null) return;
            Canvas canvas = ResolveTargetCanvas();
            Transform parent = canvas != null ? canvas.transform : transform;

            // 루트(ScrollRect) 생성
            var rootGo = new GameObject("CardPickerRoot", typeof(RectTransform), typeof(ScrollRect));
            rootGo.transform.SetParent(parent, false);
            _pickerRoot = (RectTransform)rootGo.transform;
            _pickerRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _pickerRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _pickerRoot.pivot = new Vector2(0.5f, 0.5f);
            _pickerRoot.anchoredPosition = Vector2.zero;
            _pickerRoot.sizeDelta = pickerViewportSize;

            // Viewport 생성(RectMask2D로 영역 클립, Image는 raycast 수신용)
            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewportGo.transform.SetParent(_pickerRoot, false);
            _pickerViewport = (RectTransform)viewportGo.transform;
            _pickerViewport.anchorMin = Vector2.zero;
            _pickerViewport.anchorMax = Vector2.one;
            _pickerViewport.offsetMin = Vector2.zero;
            _pickerViewport.offsetMax = Vector2.zero;
            var viewportImg = viewportGo.GetComponent<Image>();
            viewportImg.color = new Color(0f, 0f, 0f, 0f); // 완전 투명
            viewportImg.raycastTarget = true;

            // Content 생성(위에서 아래로 가변 높이)
            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(_pickerViewport, false);
            _pickerContent = (RectTransform)contentGo.transform;
            _pickerContent.anchorMin = new Vector2(0f, 1f);
            _pickerContent.anchorMax = new Vector2(1f, 1f);
            _pickerContent.pivot = new Vector2(0.5f, 1f);
            _pickerContent.anchoredPosition = Vector2.zero;
            _pickerContent.sizeDelta = new Vector2(0f, 0f); // 행 수에 따라 이후 갱신

            // ScrollRect 결선
            var scroll = rootGo.GetComponent<ScrollRect>();
            scroll.content = _pickerContent;
            scroll.viewport = _pickerViewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.1f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = pickerScrollSensitivity;
        }

        // 생성된 픽커 카드 뷰들을 모두 파괴
        void ClearPickerViews()
        {
            for (int i = 0; i < _pickerViews.Count; i++)
            {
                if (_pickerViews[i] != null) Destroy(_pickerViews[i].gameObject);
            }
            _pickerViews.Clear();
        }

        // 카드 목록을 그리드로 배치해 픽커 카드 뷰를 생성
        void BuildPickerCards(IList<CardInstance> cards)
        {
            ClearPickerViews();
            int maxPerRow = Mathf.Max(1, pickerColumns);
            float colSpacing = pickerColumnSpacing;
            float rowSpacing = pickerRowSpacing;

            int total = cards.Count;
            int rows = Mathf.CeilToInt(total / (float)maxPerRow);

            // 컨텐츠 높이 = 행 수 × rowSpacing + 패딩
            float contentHeight = rows * rowSpacing + pickerContentPadding.y;
            _pickerContent.sizeDelta = new Vector2(0f, contentHeight);
            // 스크롤은 맨 위에서 시작
            _pickerContent.anchoredPosition = Vector2.zero;

            for (int i = 0; i < total; i++)
            {
                int row = i / maxPerRow;
                int col = i % maxPerRow;
                int cardsInRow = (row == rows - 1) ? (total - row * maxPerRow) : maxPerRow;
                float rowWidth = colSpacing * (cardsInRow - 1);
                float x = -rowWidth * 0.5f + col * colSpacing;
                // top-center 기준 각 카드의 중심 Y 계산
                float y = -(pickerContentPadding.y * 0.5f) - (row * rowSpacing) - (rowSpacing * 0.5f);

                var view = Instantiate(cardPrefab, _pickerContent);
                view.Bind(this, -1); // 슬롯 인덱스 -1은 픽커 뷰
                var vrect = (RectTransform)view.transform;
                vrect.anchorMin = new Vector2(0.5f, 1f);
                vrect.anchorMax = new Vector2(0.5f, 1f);
                vrect.pivot = new Vector2(0.5f, 0.5f);
                vrect.anchoredPosition = new Vector2(x, y);
                vrect.localRotation = Quaternion.identity;
                view.SetCard(cards[i]);
                view.CaptureHome();
                _pickerViews.Add(view);
            }
        }

        // 픽커 모드일 때 카드 클릭을 처리하고 콜백 발화
        bool TryHandlePickerClick(NewCardView view)
        {
            if (_pickerCallback == null) return false;
            if (view == null || view.Card == null) return true;
            if (!_pickerViews.Contains(view)) return true; // 픽커 뷰만 유효

            var cb = _pickerCallback;
            CardInstance picked = view.Card;
            ExitPickerMode();
            cb?.Invoke(picked);
            SelectionClosedCallback?.Invoke(); // 선택 후 보류 각성 발동 트리거
            return true;
        }

        // 전체 화면 딤 오버레이가 없으면 캔버스 안에 생성
        void EnsureDimOverlay()
        {
            if (dimOverlay != null) return;

            Canvas canvas = ResolveTargetCanvas();
            Transform parent = canvas != null ? canvas.transform : transform;

            var go = new GameObject("SelectionDimOverlay", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var img = go.GetComponent<Image>();
            img.color = dimColor;
            img.raycastTarget = true; // 뒤쪽 UI 클릭 차단

            // 더미 보기 중 딤 클릭 시 닫기
            var trigger = go.AddComponent<EventTrigger>();
            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            entry.callback.AddListener(_ => { if (_viewerMode) CloseViewer(); });
            trigger.triggers.Add(entry);

            dimOverlay = rect;
            dimOverlay.gameObject.SetActive(false);
        }

        // 카드 클릭 시 더미보기/픽커/선택 모드를 순서대로 처리
        public void OnCardClicked(NewCardView view)
        {
            // 더미 보기 중에는 클릭으로 보기 닫기
            if (_viewerMode) { CloseViewer(); return; }

            // 픽커 모드 우선 처리
            if (TryHandlePickerClick(view)) return;

            if (!IsSelectionMode) return;
            if (view == null || view.Card == null) return;
            if (_selectionExcludeCard != null && view.Card == _selectionExcludeCard) return;
            if (_selectionFilter != null && !_selectionFilter(view.Card)) return;

            var cb = _selectionCallback;
            CardInstance selected = view.Card;
            ExitSelectionMode();
            cb?.Invoke(selected);
            SelectionClosedCallback?.Invoke(); // 선택 후 보류 각성 발동 트리거
        }

        // UI 좌표계를 보장할 대상 캔버스를 우선순위대로 탐색
        Canvas ResolveTargetCanvas()
        {
            // 1) 자신의 부모 캔버스
            Canvas canvas = GetComponentInParent<Canvas>();
            // 2) handRoot의 부모 캔버스
            if (canvas == null && handRoot != null) canvas = handRoot.GetComponentInParent<Canvas>();
            // 3) dragLayer의 부모 캔버스
            if (canvas == null && dragLayer != null) canvas = dragLayer.GetComponentInParent<Canvas>();
            // 4) 씬 전체 검색
            if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
            return canvas;
        }

        // handRoot가 없으면 캔버스 안에 자동 생성
        void EnsureHandRoot()
        {
            if (handRoot != null) return;

            // 캔버스 밖이어도 슬롯이 보이도록 캔버스 안에 생성
            Canvas canvas = ResolveTargetCanvas();
            Transform parent = canvas != null ? canvas.transform : transform;
            if (canvas == null)
                Debug.LogWarning("[CardHandHUD] 씬에 Canvas가 없어 handRoot가 캔버스 밖에 만들어집니다 — UI가 렌더되지 않을 수 있습니다.");

            var go = new GameObject("HandRoot", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            handRoot = (RectTransform)go.transform;
            handRoot.anchorMin = new Vector2(0.5f, 0f);
            handRoot.anchorMax = new Vector2(0.5f, 0f);
            handRoot.pivot = new Vector2(0.5f, 0f);
            handRoot.anchoredPosition = handAnchoredPos;
            handRoot.sizeDelta = new Vector2(slotSpacing.x * SLOT_COUNT, 400f);
        }

        // dragLayer가 없으면 캔버스 안에 자동 생성
        void EnsureDragLayer()
        {
            if (dragLayer != null) return;

            // 좌표 변환 정확성을 위해 dragLayer는 캔버스 안에 생성
            Canvas canvas = ResolveTargetCanvas();
            Transform parent = canvas != null ? canvas.transform : transform;
            if (canvas == null)
                Debug.LogWarning("[CardHandHUD] 씬에 Canvas가 없어 dragLayer가 캔버스 밖에 만들어집니다 — 드래그 좌표가 어긋날 수 있습니다.");

            var go = new GameObject("DragLayer", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            dragLayer = (RectTransform)go.transform;
            dragLayer.anchorMin = Vector2.zero;
            dragLayer.anchorMax = Vector2.one;
            dragLayer.offsetMin = Vector2.zero;
            dragLayer.offsetMax = Vector2.zero;
            dragLayer.SetAsLastSibling();
        }

        // 레이아웃 설정에 따라 슬롯 앵커를 생성
        void BuildSlotAnchors()
        {
            _slotAnchors.Clear();
            if (fanLayout) BuildFanSlotAnchors();
            else BuildLinearSlotAnchors();
        }

        // 활성 카드 수에 맞춰 슬롯 위치를 재계산(중앙 정렬)
        void PositionSlotAnchorsForCount(int activeCount)
        {
            if (_slotAnchors == null || _slotAnchors.Count == 0) return;
            if (fanLayout) PositionFanForCount(activeCount);
            else PositionLinearForCount(activeCount);
        }

        // 부채꼴 슬롯 위치를 활성 카드 수에 맞춰 재계산
        void PositionFanForCount(int activeCount)
        {
            int total = _slotAnchors.Count;
            // 카드 간격은 기준 카드 수로 유지하고 전체 호만 카드 수에 비례해 축소
            float perStep = fanArcAngle / Mathf.Max(1, LAYOUT_REFERENCE_COUNT - 1);
            float effectiveArc = activeCount > 1 ? perStep * (activeCount - 1) : 0f;
            float startAngle = -effectiveArc * 0.5f;
            float angleStep = activeCount > 1 ? effectiveArc / (activeCount - 1) : 0f;

            for (int i = 0; i < total; i++)
            {
                var rect = _slotAnchors[i];
                if (rect == null) continue;

                if (i < activeCount)
                {
                    float angleDeg = activeCount > 1 ? startAngle + i * angleStep : 0f;
                    float angleRad = angleDeg * Mathf.Deg2Rad;
                    float x = Mathf.Sin(angleRad) * fanRadius;
                    float y = (Mathf.Cos(angleRad) - 1f) * fanRadius - fanVerticalDip;
                    rect.anchoredPosition = new Vector2(x, y);
                    rect.localRotation = Quaternion.Euler(0f, 0f, -angleDeg);
                }
                else
                {
                    // 비활성 슬롯은 화면 밖으로 보냄
                    rect.anchoredPosition = new Vector2(0f, -10000f);
                    rect.localRotation = Quaternion.identity;
                }
            }
        }

        // 일렬 슬롯 위치를 활성 카드 수에 맞춰 재계산
        void PositionLinearForCount(int activeCount)
        {
            int total = _slotAnchors.Count;
            float totalWidth = activeCount > 1 ? slotSpacing.x * (activeCount - 1) : 0f;
            float startX = -totalWidth * 0.5f;

            for (int i = 0; i < total; i++)
            {
                var rect = _slotAnchors[i];
                if (rect == null) continue;
                if (i < activeCount)
                    rect.anchoredPosition = new Vector2(startX + i * slotSpacing.x, slotSpacing.y);
                else
                    rect.anchoredPosition = new Vector2(0f, -10000f);
            }
        }

        // 일렬 배치로 슬롯 앵커들을 생성
        void BuildLinearSlotAnchors()
        {
            float totalWidth = slotSpacing.x * (SLOT_COUNT - 1);
            float startX = -totalWidth * 0.5f;

            for (int i = 0; i < SLOT_COUNT; i++)
            {
                var slotGo = new GameObject($"Slot_{i}", typeof(RectTransform));
                slotGo.transform.SetParent(handRoot, false);
                var rect = (RectTransform)slotGo.transform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(startX + i * slotSpacing.x, slotSpacing.y);
                rect.sizeDelta = Vector2.zero;
                _slotAnchors.Add(rect);
            }
        }

        // 호 위에 회전·배치되는 부채꼴 슬롯 앵커들을 생성
        void BuildFanSlotAnchors()
        {
            int n = SLOT_COUNT;
            float startAngle = -fanArcAngle * 0.5f;
            float angleStep = n > 1 ? fanArcAngle / (n - 1) : 0f;

            // 호의 최상단이 (0,0)이 되도록 원 중심을 아래에 둠
            for (int i = 0; i < n; i++)
            {
                float angleDeg = startAngle + i * angleStep;
                float angleRad = angleDeg * Mathf.Deg2Rad;

                // 원 위의 점 좌표 계산
                float x = Mathf.Sin(angleRad) * fanRadius;
                float y = (Mathf.Cos(angleRad) - 1f) * fanRadius - fanVerticalDip;

                var slotGo = new GameObject($"Slot_{i}", typeof(RectTransform));
                slotGo.transform.SetParent(handRoot, false);
                var rect = (RectTransform)slotGo.transform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(x, y);
                rect.localRotation = Quaternion.Euler(0f, 0f, -angleDeg); // 호 방향 회전
                rect.sizeDelta = Vector2.zero;
                _slotAnchors.Add(rect);
            }
        }
    }
}
