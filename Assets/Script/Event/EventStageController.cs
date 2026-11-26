using System.Collections.Generic;
using Battle;
using Battle.Card;
using Battle.UI;
using Battle.Relic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class EventStageController : MonoBehaviour
{
    // 도박 결과(성공/실패) 본문 묶음
    [System.Serializable]
    public class GambleResultText
    {
        [TextArea] public string successBody;   // 성공 시 본문
        [TextArea] public string failureBody;   // 실패 시 본문
    }

    // 선택지별 결과 본문 모음
    [System.Serializable]
    public class EventResultTexts
    {
        [Header("이벤트1")]
        public GambleResultText event1Choice1;   // 이벤트1 선택1(도박) 성공/실패 본문
        [TextArea] public string event1Choice2Body;  // 이벤트1 선택2(회복) 본문
        [TextArea] public string event1Choice3Body;  // 이벤트1 선택3(속성+탈진) 본문

        [Header("이벤트2")]
        [TextArea] public string event2Choice1Body;  // 이벤트2 선택1(속성카드) 본문
        [TextArea] public string event2Choice2Body;  // 이벤트2 선택2(복귀) 본문

        [Header("이벤트3")]
        [TextArea] public string event3Branch1IntroBody;   // 1차: 버튼1(오른쪽) 선택 후
        [TextArea] public string event3Branch2IntroBody;   // 1차: 버튼2(왼쪽) 선택 후
        [TextArea] public string event3Branch1Choice1Body;   // 1-1 최종
        [TextArea] public string event3Branch1Choice2Body;   // 1-2 최종
        [TextArea] public string event3Branch2Choice1Body;   // 2-1 최종
        [TextArea] public string event3Branch2Choice2Body;   // 2-2 최종

        [Header("이벤트4")]
        [TextArea] public string event4Choice1Body;   // 선택1(카드 3장 제거) 결과
        [TextArea] public string event4Choice2Body;   // 선택2(효과 없음) 결과

        [Header("이벤트5")]
        [TextArea] public string event5Choice1Body;   // 선택1(기본 카드 변화) 결과
        [TextArea] public string event5Choice2Body;   // 선택2(기본 카드 공방 2배) 결과
        [TextArea] public string event5Choice1NoBasicBody;   // BASIC 카드 없을 때
        [TextArea] public string event5Choice1FailedBody;   // 변화 실패 시
    }

    // 결과 본문 전용 텍스트 스타일 설정
    [System.Serializable]
    public class ResultTextStyle
    {
        [Tooltip("비워 두면 스토리 본문의 폰트를 그대로 사용한다.")]
        public TMP_FontAsset font;   // 폰트 에셋
        public float fontSize = 30f;   // 글자 크기
        public Color color = Color.black;   // 글자 색
        public FontStyles fontStyle = FontStyles.Normal;   // 글꼴 스타일
        public TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft;   // 정렬 방식
        [Tooltip("자간")] public float characterSpacing = 0f;   // 자간
        [Tooltip("줄 간격")] public float lineSpacing = 0f;   // 줄 간격
        [Tooltip("문단 간격")] public float paragraphSpacing = 0f;   // 문단 간격
    }

    // 스토리 본문의 원래 스타일을 보관하는 스냅샷 구조체
    private struct BodyStyleSnapshot
    {
        public bool captured;   // 스냅샷 보관 여부
        public TMP_FontAsset font;   // 폰트 에셋
        public float fontSize;   // 글자 크기
        public Color color;   // 글자 색
        public FontStyles fontStyle;   // 글꼴 스타일
        public TextAlignmentOptions alignment;   // 정렬 방식
        public float characterSpacing;   // 자간
        public float lineSpacing;   // 줄 간격
        public float paragraphSpacing;   // 문단 간격
    }

    // 이벤트 진행 단계
    private enum EventPhase { Idle, Story, ResultTyping, ResultDone }

    // 이벤트3 전용: 1차 선택 → 2차 선택 → 최종 결과
    private enum Event3Phase { Initial, BranchIntro, FinalResult }

    // 이벤트5 전용: 선택1 클릭 후 BASIC 카드 고르기
    private enum Event5Phase { Initial, CardSelect }

    // 선택지 실행 결과: 본문 + (선택) 골드 보상 표시량
    private struct EventResult
    {
        public string body;
        public int goldReward;

        public static EventResult TextOnly(string body) => new EventResult { body = body, goldReward = 0 };

        public static EventResult WithGold(string body, int goldReward) =>
            new EventResult { body = body, goldReward = Mathf.Max(0, goldReward) };
    }

    // 결과 본문 아래 골드 아이콘 + 숫자 UI
    private sealed class GoldRewardRowUI
    {
        public GameObject root;
        public RectTransform rect;
        public Image icon;
        public TextMeshProUGUI amount;
    }

    public const int EventCount = 5;
    private const int Event3Branch1Choice1HpLoss = 10;
    private const int Event3PollutedElementCardId = 504;

    private enum DebugEventPick
    {
        Event1 = 0,
        Event2 = 1,
        Event3 = 2,
        Event4 = 3,
        Event5 = 4,
    }

    [Header("Debug")]
    [Tooltip("켜면 아래에서 고른 이벤트만 등장한다. 끄면 기존처럼 무작위.")]
    [SerializeField] private bool useDebugEventPick;
    // [테스트] 다음 BeginEvent 한 번만 이 이벤트(0~4)를 연다. -1이면 사용 안 함.
    public static int DebugEventOverride = -1;
    [SerializeField] private DebugEventPick debugEventPick = DebugEventPick.Event3;

    [Header("Event Canvases")]
    [SerializeField] private GameObject event1Canvas;   // 이벤트1 캔버스
    [SerializeField] private GameObject event2Canvas;   // 이벤트2 캔버스
    [SerializeField] private GameObject event3Canvas;   // 이벤트3 캔버스
    [SerializeField] private GameObject event4Canvas;   // 이벤트4 캔버스
    [SerializeField] private GameObject event5Canvas;   // 이벤트5 캔버스

    [Header("Story Typing")]
    [SerializeField] private TextMeshProUGUI event1StoryTitleText;   // 이벤트1 제목 텍스트
    [SerializeField] private TextMeshProUGUI event2StoryTitleText;   // 이벤트2 제목 텍스트
    [SerializeField] private TextMeshProUGUI event3StoryTitleText;   // 이벤트3 제목 텍스트
    [SerializeField] private TextMeshProUGUI event4StoryTitleText;   // 이벤트4 제목 텍스트
    [SerializeField] private TextMeshProUGUI event5StoryTitleText;   // 이벤트5 제목 텍스트
    [SerializeField] private TextMeshProUGUI event1StoryBodyText;   // 이벤트1 본문 텍스트
    [SerializeField] private TextMeshProUGUI event2StoryBodyText;   // 이벤트2 본문 텍스트
    [SerializeField] private TextMeshProUGUI event3StoryBodyText;   // 이벤트3 본문 텍스트
    [SerializeField] private TextMeshProUGUI event4StoryBodyText;   // 이벤트4 본문 텍스트
    [SerializeField] private TextMeshProUGUI event5StoryBodyText;   // 이벤트5 본문 텍스트
    [SerializeField] private bool useTypewriter = true;   // 타자기 연출 사용 여부
    [SerializeField] private float titleCharactersPerSecond = 40f;   // 제목 타이핑 속도(글자/초)
    [SerializeField] private float bodyCharactersPerSecond = 30f;   // 본문 타이핑 속도(글자/초)

    [Header("Event 1 Buttons")]
    [SerializeField] private Button event1Choice1Button;   // 이벤트1 선택1 버튼
    [SerializeField] private Button event1Choice2Button;   // 이벤트1 선택2 버튼
    [SerializeField] private Button event1Choice3Button;   // 이벤트1 선택3 버튼

    [Header("Event 2 Buttons")]
    [SerializeField] private Button event2Choice1Button;   // 이벤트2 선택1 버튼
    [SerializeField] private Button event2Choice2Button;   // 이벤트2 선택2 버튼

    [Header("Event 4 Buttons")]
    [SerializeField] private Button event4Choice1Button;   // 이벤트4 선택1 버튼
    [SerializeField] private Button event4Choice2Button;   // 이벤트4 선택2 버튼

    [Header("Event 5 Buttons")]
    [SerializeField] private Button event5Choice1Button;   // 이벤트5 선택1 버튼
    [SerializeField] private Button event5Choice2Button;   // 이벤트5 선택2 버튼

    [Header("Event 3 Buttons - 1차")]
    [SerializeField] private Button event3Choice1Button;   // 오른쪽으로 간다
    [SerializeField] private Button event3Choice2Button;   // 왼쪽으로 간다

    [Header("Event 3 Buttons - 2차 (버튼1 분기)")]
    [SerializeField] private Button event3Branch1Choice1Button;   // 1-1
    [SerializeField] private Button event3Branch1Choice2Button;   // 1-2

    [Header("Event 3 Buttons - 2차 (버튼2 분기)")]
    [SerializeField] private Button event3Branch2Choice1Button;   // 2-1
    [SerializeField] private Button event3Branch2Choice2Button;   // 2-2

    [Header("Event Logic")]
    [SerializeField] private List<int> elementalCardPool = new List<int> { 100, 200, 300, 400 };   // 속성 카드 후보 풀
    [SerializeField] private int exhaustionCardId = 500;   // 탈진 카드 ID

    [Header("Event1 Button1: 50 소모, 40%로 120 획득")]
    [SerializeField] private int event1Button1Cost = 50;   // 이벤트1 선택1 소모 재화
    [SerializeField] private float event1Button1WinChance = 0.4f;   // 이벤트1 선택1 성공 확률
    [SerializeField] private int event1Button1Reward = 120;   // 이벤트1 선택1 성공 보상

    [Header("Event1 Button2: 최대체력 20% 회복")]
    [Range(0f, 1f)]
    [SerializeField] private float event1Button2HealPercent = 0.2f;   // 이벤트1 선택2 회복 비율

    [Header("Event3 2-1: 200 골드 획득")]
    [SerializeField] private int event3Branch2Choice1Reward = 200;
    [Tooltip("1-1 콤보 획득 시 결과 본문 맨 아래에 붙일 문구. {0} = 콤보 이름")]
    [SerializeField] private string event3Branch1Choice1ComboFormat = "\n\n획득: {0}";

    [Header("Event4 Button1: 보유 카드 무작위 3장 제거")]
    [SerializeField] private int event4RemoveCardCount = 3;
    [Tooltip("제거된 카드 이름을 결과 본문에 붙일 때 사용. {0} = 카드 이름 목록 (앞 빈 줄은 코드에서 자동 추가)")]
    [SerializeField] private string event4RemovedCardsFormat = "제거: {0}";

    [Header("결과 화면: 얻은 부적(카드) 표시")]
    [Tooltip("이벤트로 얻은 부적을 화면 가운데에 크게 띄우고, 아무 곳이나 클릭하면 인벤토리로 날아간다.")]
    [SerializeField] private bool showGainedCards = true;
    // 직렬화 이름을 바꿨다: 씬에 첫 버전 값(배율 0.6·간격 220·본문 아래 오프셋)이 저장돼 있어 새 기본값이 무시됐다
    [SerializeField, Range(0.2f, 4f)] private float gainedCardCenterScale = 2.4f; // 카드 배율(1 = 300x400)
    [SerializeField] private float gainedCardCenterSpacing = 800f; // 여러 장일 때 카드 간 가로 간격(카드 폭 300×배율보다 크게)
    [Tooltip("화면 가운데 기준 위치.")]
    [SerializeField] private Vector2 gainedCardCenterOffset = Vector2.zero;
    float gainedCardScale => gainedCardCenterScale;
    float gainedCardSpacing => gainedCardCenterSpacing;
    Vector2 gainedCardOffset => gainedCardCenterOffset;
    [SerializeField, Range(0f, 1f)] private float gainedCardDimAlpha = 0.6f; // 뒤 배경 딤
    [SerializeField] private float gainedCardFlyStagger = 0.08f; // 여러 장일 때 날아가는 시차
    private readonly List<int> _pendingGainedCardIds = new List<int>(); // 이번 결과에서 얻은 카드(결과 표시 시 소비)
    private GameObject _gainedCardsRoot; // 획득 카드 오버레이 루트(딤+카드)
    private readonly List<NewCardView> _gainedCardViews = new List<NewCardView>();
    private Image _gainedDim;
    private bool _gainedFlying; // 인벤토리로 날아가는 중

    [Header("Event5 Button1: BASIC 카드 1장 선택 후 같은 속성 랜덤 카드로 변화")]
    [SerializeField] private NewCardView event5CardViewPrefab;
    [Tooltip("비우면 event5Canvas 자식으로 카드 선택 UI를 생성한다.")]
    [SerializeField] private Transform event5CardPickContainer;
    [Tooltip("카드 선택에 쓸 더미 보기 패널(전투 '뽑을 더미에서 선택'과 같은 UI). 비우면 씬의 PileViewerPanel을 복제해 쓴다.")]
    [SerializeField] private PileViewerPanel event5CardPickPanel;
    [Tooltip("패널 프레임 종류. Draw = 뽑을 더미 선택과 같은 모습.")]
    [SerializeField] private PileViewerKind event5CardPickPanelKind = PileViewerKind.Draw;
    [SerializeField] private string event5CardPickTitle = "변화시킬 기본 카드를 선택하세요";
    [Tooltip("비워 두면 event5StoryBodyText 폰트를 사용한다.")]
    [SerializeField] private TMP_FontAsset event5CardPickTitleFont;
    [Tooltip("0이면 event5StoryBodyText 글자 크기를 사용한다.")]
    [SerializeField] private float event5CardPickTitleFontSize = 32f;
    [SerializeField] private Color event5CardPickTitleColor = Color.white;
    [SerializeField] private float event5CardPickScale = 0.75f;
    [SerializeField] private float event5CardPickSpacing = 280f;
    [SerializeField] private bool event5CardPickEnableHoverScale = true;
    [Tooltip("호버 시 카드 슬롯 배율(1 = 그대로).")]
    [SerializeField, Range(1f, 1.5f)] private float event5CardPickHoverScale = 1.08f;
    [SerializeField] private float event5CardPickHoverLerpSpeed = 14f;
    [Tooltip("변화 결과 본문에 붙일 문구. {0}=이전 이름, {1}=새 이름")]
    [SerializeField] private string event5TransformResultFormat = "변화: {0} → {1}";

    [Header("Gold Reward Display")]
    [SerializeField] private Sprite goldRewardIcon;
    [Tooltip("Gold Reward Icon이 비어 있으면 MoneyManager의 화폐 아이콘을 사용")]
    [SerializeField] private bool fallbackToMoneyManagerIcon = true;
    [SerializeField] private float goldRewardIconSize = 48f;
    [SerializeField] private float goldRewardRowSpacing = 12f;
    [SerializeField] private float goldRewardIconTextSpacing = 8f;
    [SerializeField] private string goldRewardAmountFormat = "+{0}";
    [Tooltip("비워 두면 결과 본문(또는 스토리 본문) 폰트를 사용한다.")]
    [SerializeField] private TMP_FontAsset goldRewardFont;
    [Tooltip("0이면 결과 본문(또는 스토리 본문) 글자 크기를 사용한다.")]
    [SerializeField] private float goldRewardFontSize;
    [SerializeField] private bool useGoldRewardFontColor;
    [SerializeField] private Color goldRewardFontColor = Color.black;

    [Header("선택지 결과 텍스트")]
    [SerializeField] private EventResultTexts resultTexts = new EventResultTexts();   // 선택지 결과 본문 모음

    [Header("선택지 결과 텍스트 스타일 (스토리 본문과 별도로 지정)")]
    [Tooltip("켜면 결과 본문에 아래 스타일을 적용하고, 스토리로 돌아가면 원래 스타일로 복원한다.")]
    [SerializeField] private bool overrideResultTextStyle = true;   // 결과 본문 스타일 덮어쓰기 사용 여부
    [SerializeField] private ResultTextStyle resultTextStyle = new ResultTextStyle();   // 결과 본문에 적용할 스타일

    private RoundManager roundManager;   // 라운드 관리자 참조
    private int currentEventIndex;   // 현재 진행 중인 이벤트 인덱스(0~4)
    public int CurrentEventIndex => currentEventIndex; // 배경음이 이벤트 번호를 읽는다
    private EventPhase phase = EventPhase.Idle;   // 현재 이벤트 단계
    private int phaseEnteredFrame;   // 단계 진입 시점의 프레임 번호
    private Coroutine typingRoutine;   // 타이핑 연출 코루틴 핸들
    private TextMeshProUGUI activeResultBody;   // 현재 타이핑 중인 결과 본문 텍스트
    private string event1TitleCached;   // 이벤트1 제목 원본 캐시
    private string event2TitleCached;   // 이벤트2 제목 원본 캐시
    private string event3TitleCached;   // 이벤트3 제목 원본 캐시
    private string event4TitleCached;   // 이벤트4 제목 원본 캐시
    private string event5TitleCached;   // 이벤트5 제목 원본 캐시
    private string event1StoryCached;   // 이벤트1 스토리 본문 원본 캐시
    private string event2StoryCached;   // 이벤트2 스토리 본문 원본 캐시
    private string event3StoryCached;   // 이벤트3 스토리 본문 원본 캐시
    private string event4StoryCached;   // 이벤트4 스토리 본문 원본 캐시
    private string event5StoryCached;   // 이벤트5 스토리 본문 원본 캐시
    private BodyStyleSnapshot event1BodyStyleBackup;   // 이벤트1 본문 스타일 백업
    private BodyStyleSnapshot event2BodyStyleBackup;   // 이벤트2 본문 스타일 백업
    private BodyStyleSnapshot event3BodyStyleBackup;   // 이벤트3 본문 스타일 백업
    private BodyStyleSnapshot event4BodyStyleBackup;   // 이벤트4 본문 스타일 백업
    private BodyStyleSnapshot event5BodyStyleBackup;   // 이벤트5 본문 스타일 백업

    private Event3Phase event3Phase = Event3Phase.Initial;   // 이벤트3 진행 단계
    private Event5Phase event5Phase = Event5Phase.Initial;   // 이벤트5 진행 단계
    private GameObject event5CardPickRoot;   // 이벤트5 카드 선택 UI 루트(예비 UI)
    private PileViewerPanel _event5ActivePanel;   // 이벤트5 카드 선택에 열어 둔 더미 패널
    private PileViewerPanel _event5PanelClone;    // 인스펙터 미지정 시 복제해 만든 패널
    private readonly List<CardData> _event5PickCandidates = new List<CardData>();   // 패널에 깐 카드 목록
    private int event3SelectedBranch;   // 1차에서 고른 분기 (1 또는 2)
    private int pendingGoldReward;   // 현재 결과에 표시할 골드 획득량
    private readonly Dictionary<int, GoldRewardRowUI> goldRewardRows = new Dictionary<int, GoldRewardRowUI>();

    // 선택지 버튼 콜백을 연결하고 캔버스를 모두 끈다
    void Awake()
    {
        BindButton(event1Choice1Button, OnChoice1Clicked);
        BindButton(event1Choice2Button, OnChoice2Clicked);
        BindButton(event1Choice3Button, OnChoice3Clicked);

        BindButton(event2Choice1Button, OnChoice1Clicked);
        BindButton(event2Choice2Button, OnChoice2Clicked);

        BindButton(event4Choice1Button, OnChoice1Clicked);
        BindButton(event4Choice2Button, OnChoice2Clicked);

        BindButton(event5Choice1Button, OnEvent5Choice1Clicked);
        BindButton(event5Choice2Button, OnChoice2Clicked);

        BindButton(event3Choice1Button, OnEvent3InitialChoice1Clicked);
        BindButton(event3Choice2Button, OnEvent3InitialChoice2Clicked);
        BindButton(event3Branch1Choice1Button, OnEvent3Branch1Choice1Clicked);
        BindButton(event3Branch1Choice2Button, OnEvent3Branch1Choice2Clicked);
        BindButton(event3Branch2Choice1Button, OnEvent3Branch2Choice1Clicked);
        BindButton(event3Branch2Choice2Button, OnEvent3Branch2Choice2Clicked);

        SetCanvasState(-1);
    }

    void OnEnable()
    {
        if (MoneyManager.Instance != null)
            MoneyManager.Instance.OnMoneyChanged += OnMoneyChanged;
        RefreshEvent1Choice1Interactable();
    }

    void OnDisable()
    {
        if (MoneyManager.Instance != null)
            MoneyManager.Instance.OnMoneyChanged -= OnMoneyChanged;
    }

    void OnMoneyChanged(int _) => RefreshEvent1Choice1Interactable();

    // 이벤트1 선택1(50골드) 지불 가능 여부
    bool CanAffordEvent1Choice1()
    {
        return MoneyManager.Instance != null
            && MoneyManager.Instance.CurrentMoney >= event1Button1Cost;
    }

    // 이벤트1 선택1 버튼 — 골드 부족 시 비활성화
    void RefreshEvent1Choice1Interactable()
    {
        if (event1Choice1Button == null) return;
        if (currentEventIndex != 0 || phase != EventPhase.Story)
        {
            event1Choice1Button.interactable = true;
            return;
        }

        event1Choice1Button.interactable = CanAffordEvent1Choice1();
    }

    // ── 본문/선택지 버튼 겹침 방지 ──
    // 본문 TMP는 Overflow 모드(박스 250px 고정)라 문장이 길면 아래로 흘러넘쳐 선택지 버튼 뒤로 깔렸다.
    // 선택지 버튼이 보이는 동안 본문 맨 아래가 가장 위 버튼보다 위에 오도록 글자 크기를 줄여 맞춘다.
    [Header("본문-선택지 겹침 방지")]
    [SerializeField] private bool fitBodyAboveChoices = true;
    [Tooltip("본문 맨 아래와 가장 위 선택지 버튼 사이에 둘 최소 간격(px).")]
    [SerializeField] private float bodyChoiceGap = 16f;
    [Tooltip("겹침을 피하려고 줄일 수 있는 최소 글자 크기.")]
    [SerializeField] private float bodyMinFontSize = 22f;

    private TextMeshProUGUI _fitBody;   // 마지막으로 맞춘 본문
    private string _fitText;            // 마지막으로 맞춘 문장
    private float _fitBaseSize;         // 줄이기 전 글자 크기
    private float _fitAppliedSize = -1f; // 맞춘 결과 글자 크기
    private float _fitButtonTop = float.NaN; // 맞출 때 기준이 된 버튼 상단 높이
    private readonly Vector3[] _fitCorners = new Vector3[4];

    void LateUpdate()
    {
        if (fitBodyAboveChoices && phase != EventPhase.Idle)
            FitStoryBodyAboveChoices();
    }

    // 현재 이벤트에서 화면에 나올 수 있는 모든 선택지 버튼(이벤트3은 갈래 버튼 포함)
    IEnumerable<Button> AllChoiceButtonsOfCurrentEvent()
    {
        foreach (Button b in CurrentChoiceButtons()) yield return b;
        if (currentEventIndex == 2)
        {
            yield return event3Branch1Choice1Button;
            yield return event3Branch1Choice2Button;
            yield return event3Branch2Choice1Button;
            yield return event3Branch2Choice2Button;
        }
    }

    // 결과 단계에서 클릭을 받아 타이핑 스킵 또는 맵 복귀를 처리한다
    void Update()
    {
        if (phase != EventPhase.ResultTyping && phase != EventPhase.ResultDone)
            return;

        // 인벤토리·도감이 열려 있으면 클릭이 이벤트(스킵/맵복귀)를 진행시키지 않도록 막는다
        if (Battle.UI.InventoryPanelController.IsOpen || Battle.UI.CodexPanelController.IsOpen)
            return;

        // 단계 전환을 유발한 클릭이 곧바로 다음 동작을 트리거하지 않도록 가드
        if (Time.frameCount <= phaseEnteredFrame)
            return;

        if (!Input.GetMouseButtonDown(0))
            return;

        if (phase == EventPhase.ResultTyping)
            SkipResultTyping();
        else if (_gainedCardsRoot != null)
        {
            // 획득 카드가 떠 있으면 첫 클릭은 카드를 인벤토리로 날리고, 다 날아간 뒤 클릭해야 맵으로 돌아간다
            if (!_gainedFlying) StartCoroutine(FlyGainedCardsToInventory());
        }
        else
            ReturnToMap();
    }

    // 이벤트를 무작위로 골라 스토리 단계로 시작한다
    public void BeginEvent(EventRoundData data, RoundManager manager)
    {
        roundManager = manager;

        if (DebugEventOverride >= 0)
        {
            // [테스트] StageTestPanel이 고른 이벤트(1회용)
            currentEventIndex = DebugEventOverride;
            DebugEventOverride = -1;
        }
        else
            currentEventIndex = useDebugEventPick
                ? (int)debugEventPick
                : PickEventIndexForRun();
        SetCanvasState(currentEventIndex);
        HideAllGoldRewardRows();
        pendingGoldReward = 0;
        RestoreStoryVisuals();
        SetPhase(EventPhase.Story);

        if (currentEventIndex == 2)
            ResetEvent3Flow();

        if (currentEventIndex == 4)
            ResetEvent5Flow();

        PlayStoryTypewriter();
        RefreshEvent1Choice1Interactable();
    }

    // 런 진행 상태를 반영해 다음 이벤트 인덱스를 고른다
    int PickEventIndexForRun()
    {
        if (GameStateController.Instance != null)
            return GameStateController.Instance.PickNextEventIndex(EventCount);

        return Random.Range(0, EventCount);
    }

    // 버튼의 기존 콜백을 제거 후 새로 등록한다(중복 방지)
    void BindButton(Button button, UnityEngine.Events.UnityAction callback)
    {
        if (button == null) return;
        button.onClick.RemoveListener(callback);
        button.onClick.AddListener(callback);
    }

    // 선택된 이벤트 인덱스에 맞춰 캔버스 활성화를 전환한다
    void SetCanvasState(int eventIndex)
    {
        if (event1Canvas != null)
            event1Canvas.SetActive(eventIndex == 0);

        if (event2Canvas != null)
            event2Canvas.SetActive(eventIndex == 1);

        if (event3Canvas != null)
            event3Canvas.SetActive(eventIndex == 2);

        if (event4Canvas != null)
            event4Canvas.SetActive(eventIndex == 3);

        if (event5Canvas != null)
            event5Canvas.SetActive(eventIndex == 4);
    }

    // 선택지1 클릭 처리: 해당 이벤트의 선택1 효과 실행 후 결과 표시
    public void OnChoice1Clicked()
    {
        if (phase != EventPhase.Story) return;
        if (currentEventIndex == 0 && !CanAffordEvent1Choice1()) return;

        EventResult result = currentEventIndex switch
        {
            0 => ExecuteEvent1Choice1(),
            1 => EventResult.TextOnly(ExecuteEvent2Choice1()),
            3 => ExecuteEvent4Choice1(),
            _ => default
        };

        PlayResultText(result.body, result.goldReward);
    }

    // 선택지2 클릭 처리: 해당 이벤트의 선택2 효과 실행 후 결과 표시
    public void OnChoice2Clicked()
    {
        if (phase != EventPhase.Story) return;
        if (currentEventIndex == 4 && event5Phase == Event5Phase.CardSelect)
            return;

        string resultBody = currentEventIndex switch
        {
            0 => ExecuteEvent1Choice2(),
            1 => ExecuteEvent2Choice2(),
            3 => ExecuteEvent4Choice2(),
            4 => ExecuteEvent5Choice2(),
            _ => string.Empty
        };

        PlayResultText(resultBody);
    }

    // 선택지3 클릭 처리: 이벤트1 전용 선택3
    public void OnChoice3Clicked()
    {
        if (phase != EventPhase.Story) return;
        if (currentEventIndex != 0) return;

        PlayResultText(ExecuteEvent1Choice3());
    }

    System.Collections.IEnumerator FlyOne(RectTransform rt, Vector2 from, Vector2 to, CanvasGroup group, System.Action done)
    {
        yield return InventoryFlyFx.Fly(rt, from, to, a => { if (group != null) group.alpha = a; }, gainedCardScale);
        if (rt != null) rt.gameObject.SetActive(false);
        done?.Invoke();
    }

    // 단계를 변경하고 진입 프레임을 기록한다
    void SetPhase(EventPhase next)
    {
        phase = next;
        phaseEnteredFrame = Time.frameCount;
    }

    NewCardView ResolveEventCardViewPrefab()
    {
        if (event5CardViewPrefab != null) return event5CardViewPrefab;
        var hud = FindFirstObjectByType<CardHandHUD>(FindObjectsInactive.Include);
        return hud != null ? hud.CardPrefab : null;
    }

    // 스토리 본문을 보관해 둔 원래 스타일로 되돌린다
    void RestoreStoryBodyStyle(TextMeshProUGUI bodyTarget)
    {
        if (!overrideResultTextStyle || bodyTarget == null) return;
        ApplyBodyStyle(bodyTarget, GetBodyStyleBackupRef(currentEventIndex));
    }

    // 현재 이벤트의 제목 텍스트 컴포넌트를 반환한다
    TextMeshProUGUI CurrentTitleTarget()
    {
        return GetStoryTitleText(currentEventIndex);
    }

    // 현재 이벤트의 선택지 버튼들을 차례로 반환한다
    IEnumerable<Button> CurrentChoiceButtons()
    {
        switch (currentEventIndex)
        {
            case 0:
                yield return event1Choice1Button;
                yield return event1Choice2Button;
                yield return event1Choice3Button;
                yield break;
            case 1:
                yield return event2Choice1Button;
                yield return event2Choice2Button;
                yield break;
            case 2:
                yield return event3Choice1Button;
                yield return event3Choice2Button;
                yield break;
            case 3:
                yield return event4Choice1Button;
                yield return event4Choice2Button;
                yield break;
            case 4:
                yield return event5Choice1Button;
                yield return event5Choice2Button;
                yield break;
        }
    }

    // 이벤트를 종료하고 맵 화면으로 복귀한다
    void ReturnToMap()
    {
        HideAllGoldRewardRows();
        pendingGoldReward = 0;
        _pendingGainedCardIds.Clear();
        ClearGainedCardViews();
        phase = EventPhase.Idle;
        event3Phase = Event3Phase.Initial;
        ClearEvent5CardPick();
        event5Phase = Event5Phase.Initial;

        if (roundManager != null)
            roundManager.ReturnToMap();
        else
            GameStateController.Instance?.ShowMap();
    }

}
