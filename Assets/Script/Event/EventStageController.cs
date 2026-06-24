using System.Collections.Generic;
using Battle;
using Battle.Card;
using Battle.UI;
using Battle.Relic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EventStageController : MonoBehaviour
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

    private const int EventCount = 5;
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

    [Header("Button Entrance Animation")]
    [SerializeField] private bool useButtonEntranceAnimation = false;   // 버튼 등장 애니메이션 사용 여부

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

    [Header("Event5 Button1: BASIC 카드 1장 선택 후 같은 속성 랜덤 카드로 변화")]
    [SerializeField] private NewCardView event5CardViewPrefab;
    [Tooltip("비우면 event5Canvas 자식으로 카드 선택 UI를 생성한다.")]
    [SerializeField] private Transform event5CardPickContainer;
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
    private GameObject event5CardPickRoot;   // 이벤트5 카드 선택 UI 루트
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

    // 결과 단계에서 클릭을 받아 타이핑 스킵 또는 맵 복귀를 처리한다
    void Update()
    {
        if (phase != EventPhase.ResultTyping && phase != EventPhase.ResultDone)
            return;

        // 단계 전환을 유발한 클릭이 곧바로 다음 동작을 트리거하지 않도록 가드
        if (Time.frameCount <= phaseEnteredFrame)
            return;

        if (!Input.GetMouseButtonDown(0))
            return;

        if (phase == EventPhase.ResultTyping)
            SkipResultTyping();
        else
            ReturnToMap();
    }

    // 이벤트를 무작위로 골라 스토리 단계로 시작한다
    public void BeginEvent(EventRoundData data, RoundManager manager)
    {
        roundManager = manager;

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

    // 스토리 단계 진입 시 제목과 선택지 버튼을 다시 표시한다
    void RestoreStoryVisuals()
    {
        if (currentEventIndex == 2)
        {
            ResetEvent3Flow();
            return;
        }

        if (currentEventIndex == 4)
        {
            ResetEvent5Flow();
            return;
        }

        TextMeshProUGUI title = CurrentTitleTarget();
        if (title != null)
            title.gameObject.SetActive(true);

        foreach (Button button in CurrentChoiceButtons())
        {
            if (button != null)
                button.gameObject.SetActive(true);
        }

        RefreshEvent1Choice1Interactable();
    }

    // 스토리 제목/본문을 캐시 텍스트로 채우고 타자기 연출을 재생한다
    void PlayStoryTypewriter()
    {
        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }

        TextMeshProUGUI titleTarget = GetStoryTitleText(currentEventIndex);
        TextMeshProUGUI bodyTarget = GetStoryBodyText(currentEventIndex);
        HideAllGoldRewardRows();
        pendingGoldReward = 0;
        if (titleTarget == null && bodyTarget == null)
            return;

        // 결과 스타일로 덮어쓴 적이 있으면 스토리 본문 원래 스타일로 복원
        RestoreStoryBodyStyle(bodyTarget);

        CacheAndApplyStoryText(currentEventIndex, titleTarget, bodyTarget);

        if (!useTypewriter)
        {
            if (titleTarget != null)
                titleTarget.maxVisibleCharacters = int.MaxValue;

            if (bodyTarget != null)
                bodyTarget.maxVisibleCharacters = int.MaxValue;

            RefreshEvent1Choice1Interactable();
            return;
        }

        typingRoutine = StartCoroutine(TypewriterRoutine(titleTarget, bodyTarget));
    }

    // 제목과 본문을 글자 단위로 차례대로 노출시키는 타자기 코루틴
    System.Collections.IEnumerator TypewriterRoutine(TextMeshProUGUI titleTarget, TextMeshProUGUI bodyTarget)
    {
        int titleChars = 0;
        int bodyChars = 0;

        if (titleTarget != null)
        {
            titleTarget.ForceMeshUpdate();
            titleChars = titleTarget.textInfo.characterCount;
            titleTarget.maxVisibleCharacters = 0;
        }

        if (bodyTarget != null)
        {
            bodyTarget.ForceMeshUpdate();
            bodyChars = bodyTarget.textInfo.characterCount;
            bodyTarget.maxVisibleCharacters = 0;
        }

        if (titleChars <= 0 && bodyChars <= 0)
        {
            typingRoutine = null;
            RefreshEvent1Choice1Interactable();
            yield break;
        }

        float titleCps = Mathf.Max(1f, titleCharactersPerSecond);
        float bodyCps = Mathf.Max(1f, bodyCharactersPerSecond);

        float titleProgress = 0f;
        float bodyProgress = 0f;

        while (true)
        {
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f)
                dt = 1f / 60f;

            titleProgress += titleCps * dt;
            bodyProgress += bodyCps * dt;

            if (titleTarget != null)
                titleTarget.maxVisibleCharacters = Mathf.Min(Mathf.FloorToInt(titleProgress), titleChars);

            if (bodyTarget != null)
                bodyTarget.maxVisibleCharacters = Mathf.Min(Mathf.FloorToInt(bodyProgress), bodyChars);

            bool titleDone = titleTarget == null || titleTarget.maxVisibleCharacters >= titleChars;
            bool bodyDone = bodyTarget == null || bodyTarget.maxVisibleCharacters >= bodyChars;
            if (titleDone && bodyDone)
                break;

            yield return null;
        }

        if (titleTarget != null)
            titleTarget.maxVisibleCharacters = int.MaxValue;

        if (bodyTarget != null)
            bodyTarget.maxVisibleCharacters = int.MaxValue;

        typingRoutine = null;
        RefreshEvent1Choice1Interactable();
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

    void OnEvent3InitialChoice1Clicked() => OnEvent3InitialChoice(1);
    void OnEvent3InitialChoice2Clicked() => OnEvent3InitialChoice(2);
    void OnEvent3Branch1Choice1Clicked() => OnEvent3BranchChoice(1, 1);
    void OnEvent3Branch1Choice2Clicked() => OnEvent3BranchChoice(1, 2);
    void OnEvent3Branch2Choice1Clicked() => OnEvent3BranchChoice(2, 1);
    void OnEvent3Branch2Choice2Clicked() => OnEvent3BranchChoice(2, 2);

    // 이벤트3 1차 선택: 중간 결과 텍스트 + 2차 버튼 노출
    void OnEvent3InitialChoice(int branch)
    {
        if (currentEventIndex != 2 || phase != EventPhase.Story || event3Phase != Event3Phase.Initial)
            return;

        event3SelectedBranch = branch;
        event3Phase = Event3Phase.BranchIntro;

        SetEvent3InitialButtonsActive(false);

        string introBody = branch == 1
            ? resultTexts.event3Branch1IntroBody
            : resultTexts.event3Branch2IntroBody;

        PlayEvent3BranchIntro(introBody, branch);
    }

    // 이벤트3 2차 선택: 최종 결과 + 기능 적용 후 맵 복귀 대기
    void OnEvent3BranchChoice(int branch, int subChoice)
    {
        if (currentEventIndex != 2 || phase != EventPhase.Story || event3Phase != Event3Phase.BranchIntro)
            return;
        if (branch != event3SelectedBranch)
            return;

        SetEvent3BranchButtonsActive(branch, false);
        event3Phase = Event3Phase.FinalResult;

        EventResult result = ExecuteEvent3FinalChoice(branch, subChoice);
        PlayResultText(result.body, result.goldReward);
    }

    void ResetEvent3Flow()
    {
        event3Phase = Event3Phase.Initial;
        event3SelectedBranch = 0;

        TextMeshProUGUI title = event3StoryTitleText;
        if (title != null)
            title.gameObject.SetActive(true);

        SetEvent3InitialButtonsActive(true);
        SetEvent3BranchButtonsActive(1, false);
        SetEvent3BranchButtonsActive(2, false);
    }

    void SetEvent3InitialButtonsActive(bool active)
    {
        if (event3Choice1Button != null)
            event3Choice1Button.gameObject.SetActive(active);

        if (event3Choice2Button != null)
            event3Choice2Button.gameObject.SetActive(active);
    }

    void SetEvent3BranchButtonsActive(int branch, bool active)
    {
        if (branch == 1)
        {
            if (event3Branch1Choice1Button != null)
                event3Branch1Choice1Button.gameObject.SetActive(active);
            if (event3Branch1Choice2Button != null)
                event3Branch1Choice2Button.gameObject.SetActive(active);
        }
        else if (branch == 2)
        {
            if (event3Branch2Choice1Button != null)
                event3Branch2Choice1Button.gameObject.SetActive(active);
            if (event3Branch2Choice2Button != null)
                event3Branch2Choice2Button.gameObject.SetActive(active);
        }
    }

    // 1차 선택 후 중간 텍스트를 표시하고 해당 분기 2차 버튼을 연다
    void PlayEvent3BranchIntro(string body, int branch)
    {
        if (event3StoryTitleText != null)
            event3StoryTitleText.gameObject.SetActive(false);

        TextMeshProUGUI bodyTarget = event3StoryBodyText;
        if (bodyTarget == null)
        {
            SetEvent3BranchButtonsActive(branch, true);
            return;
        }

        ApplyResultTextStyle(bodyTarget);

        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }

        bodyTarget.text = body ?? string.Empty;
        SetEvent3BranchButtonsActive(branch, true);

        if (!useTypewriter)
        {
            bodyTarget.maxVisibleCharacters = int.MaxValue;
            return;
        }

        typingRoutine = StartCoroutine(Event3BranchIntroTypeRoutine(bodyTarget));
    }

    System.Collections.IEnumerator Event3BranchIntroTypeRoutine(TextMeshProUGUI bodyTarget)
    {
        bodyTarget.ForceMeshUpdate();
        int total = bodyTarget.textInfo.characterCount;
        bodyTarget.maxVisibleCharacters = 0;

        if (total <= 0)
        {
            bodyTarget.maxVisibleCharacters = int.MaxValue;
            typingRoutine = null;
            yield break;
        }

        float cps = Mathf.Max(1f, bodyCharactersPerSecond);
        float progress = 0f;

        while (bodyTarget.maxVisibleCharacters < total)
        {
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f)
                dt = 1f / 60f;

            progress += cps * dt;
            bodyTarget.maxVisibleCharacters = Mathf.Min(Mathf.FloorToInt(progress), total);
            yield return null;
        }

        bodyTarget.maxVisibleCharacters = int.MaxValue;
        typingRoutine = null;
    }

    // 제목/선택지를 숨기고 결과 본문을 새로 타이핑한다
    void PlayResultText(string body, int goldReward = 0)
    {
        pendingGoldReward = Mathf.Max(0, goldReward);
        HideAllGoldRewardRows();
        TextMeshProUGUI title = CurrentTitleTarget();
        if (title != null)
            title.gameObject.SetActive(false);

        foreach (Button button in CurrentChoiceButtons())
        {
            if (button != null)
                button.gameObject.SetActive(false);
        }

        TextMeshProUGUI bodyTarget = GetStoryBodyText(currentEventIndex);
        activeResultBody = bodyTarget;

        // 결과 본문 전용 스타일 적용(원래 스타일은 보관 후 덮어씀)
        ApplyResultTextStyle(bodyTarget);

        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }

        if (bodyTarget == null)
        {
            EnterResultDone();
            return;
        }

        bodyTarget.text = body ?? string.Empty;

        if (!useTypewriter)
        {
            bodyTarget.maxVisibleCharacters = int.MaxValue;
            EnterResultDone();
            return;
        }

        SetPhase(EventPhase.ResultTyping);
        typingRoutine = StartCoroutine(ResultTypeRoutine(bodyTarget));
    }

    // 결과 본문을 글자 단위로 노출시키는 타자기 코루틴
    System.Collections.IEnumerator ResultTypeRoutine(TextMeshProUGUI bodyTarget)
    {
        bodyTarget.ForceMeshUpdate();
        int total = bodyTarget.textInfo.characterCount;
        bodyTarget.maxVisibleCharacters = 0;

        if (total <= 0)
        {
            bodyTarget.maxVisibleCharacters = int.MaxValue;
            typingRoutine = null;
            EnterResultDone();
            yield break;
        }

        float cps = Mathf.Max(1f, bodyCharactersPerSecond);
        float progress = 0f;

        while (bodyTarget.maxVisibleCharacters < total)
        {
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f)
                dt = 1f / 60f;

            progress += cps * dt;
            bodyTarget.maxVisibleCharacters = Mathf.Min(Mathf.FloorToInt(progress), total);
            yield return null;
        }

        bodyTarget.maxVisibleCharacters = int.MaxValue;
        typingRoutine = null;
        EnterResultDone();
    }

    // 진행 중인 결과 타이핑을 즉시 끝까지 표시한다
    void SkipResultTyping()
    {
        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }

        if (activeResultBody != null)
            activeResultBody.maxVisibleCharacters = int.MaxValue;

        EnterResultDone();
    }

    // 결과 완료 단계로 전환한다
    void EnterResultDone()
    {
        if (pendingGoldReward > 0 && activeResultBody != null)
            ShowGoldRewardRow(activeResultBody, pendingGoldReward);
        else
            HideAllGoldRewardRows();

        SetPhase(EventPhase.ResultDone);
    }

    // 단계를 변경하고 진입 프레임을 기록한다
    void SetPhase(EventPhase next)
    {
        phase = next;
        phaseEnteredFrame = Time.frameCount;
    }

    // 이벤트1 선택1(도박): 재화 소모 후 확률로 보상을 지급하고 결과 본문을 반환
    EventResult ExecuteEvent1Choice1()
    {
        if (MoneyManager.Instance == null)
        {
            Debug.LogWarning("[EventStage] MoneyManager가 없어 이벤트1 버튼1 보상을 처리하지 못했습니다.");
            return EventResult.TextOnly(resultTexts.event1Choice1.failureBody);
        }

        bool spent = MoneyManager.Instance.SpendMoney(event1Button1Cost);
        if (!spent)
        {
            Debug.Log($"[EventStage] 재화 부족으로 이벤트1 버튼1 실패 (필요: {event1Button1Cost})");
            return EventResult.TextOnly(resultTexts.event1Choice1.failureBody);
        }

        float chance = Mathf.Clamp01(event1Button1WinChance);
        bool won = Random.value < chance;
        if (won)
        {
            MoneyManager.Instance.AddMoney(event1Button1Reward);
            Debug.Log($"[EventStage] 이벤트1 버튼1 성공: -{event1Button1Cost}, +{event1Button1Reward}");
            return EventResult.WithGold(resultTexts.event1Choice1.successBody, event1Button1Reward);
        }

        Debug.Log($"[EventStage] 이벤트1 버튼1 실패: -{event1Button1Cost}, 획득 없음");
        return EventResult.TextOnly(resultTexts.event1Choice1.failureBody);
    }

    // 이벤트1 선택2(회복): 최대 체력 비율만큼 회복하고 결과 본문을 반환
    string ExecuteEvent1Choice2()
    {
        Player player = Player.Resolve(true);
        if (player == null)
        {
            Debug.LogWarning("[EventStage] Player를 찾지 못해 회복을 수행하지 못했습니다.");
            return resultTexts.event1Choice2Body;
        }

        int healAmount = Mathf.Max(1, Mathf.RoundToInt(player.maxHp * Mathf.Clamp01(event1Button2HealPercent)));
        player.Heal(healAmount);
        Debug.Log($"[EventStage] 이벤트1 버튼2: 최대체력 비율 회복 +{healAmount}");
        return resultTexts.event1Choice2Body;
    }

    // 이벤트1 선택3: 속성 카드와 탈진 카드를 덱에 추가하고 결과 본문을 반환
    string ExecuteEvent1Choice3()
    {
        int elementalId = PickRandomCardId(elementalCardPool);
        AddCardToRunDeck(elementalId, "이벤트1 버튼3 속성카드");
        AddCardToRunDeck(exhaustionCardId, "이벤트1 버튼3 탈진카드");
        return resultTexts.event1Choice3Body;
    }

    // 이벤트2 선택1: 속성 카드를 덱에 추가하고 결과 본문을 반환
    string ExecuteEvent2Choice1()
    {
        int elementalId = PickRandomCardId(elementalCardPool);
        AddCardToRunDeck(elementalId, "이벤트2 버튼1 속성카드");
        return resultTexts.event2Choice1Body;
    }

    // 이벤트2 선택2(복귀): 별도 효과 없이 결과 본문을 반환
    string ExecuteEvent2Choice2()
    {
        Debug.Log("[EventStage] 이벤트2 버튼2: 맵으로 복귀");
        return resultTexts.event2Choice2Body;
    }

    // 이벤트4 선택1: 보유 카드 중 무작위 N장 제거
    EventResult ExecuteEvent4Choice1()
    {
        List<string> removedNames = RemoveRandomCardsFromRunDeck(event4RemoveCardCount);
        string body = resultTexts.event4Choice1Body;

        if (removedNames.Count > 0 && !string.IsNullOrEmpty(event4RemovedCardsFormat))
        {
            string removalLine = string.Format(
                event4RemovedCardsFormat.TrimStart('\n', '\r'),
                string.Join(", ", removedNames));
            body += "\n\n" + removalLine;
        }

        Debug.Log($"[EventStage] 이벤트4 버튼1: 카드 {removedNames.Count}장 제거");
        return EventResult.TextOnly(body);
    }

    // 이벤트4 선택2: 효과 없음 (결과 텍스트만)
    string ExecuteEvent4Choice2()
    {
        return resultTexts.event4Choice2Body;
    }

    void OnEvent5Choice1Clicked()
    {
        if (currentEventIndex != 4 || phase != EventPhase.Story || event5Phase != Event5Phase.Initial)
            return;

        List<int> basicCardIds = CollectBasicCardIdsInDeck();
        if (basicCardIds.Count == 0)
        {
            string noBasicBody = string.IsNullOrEmpty(resultTexts.event5Choice1NoBasicBody)
                ? resultTexts.event5Choice1Body
                : resultTexts.event5Choice1NoBasicBody;
            PlayResultText(noBasicBody);
            return;
        }

        SetEvent5ChoiceButtonsActive(false);
        ShowEvent5CardPick(basicCardIds);
    }

    string ExecuteEvent5Choice2()
    {
        RunDeckState.EnsureExists().DoubleBasicCardStats();
        Debug.Log("[EventStage] 이벤트5 버튼2: BASIC 카드 공격/방어 2배");
        return resultTexts.event5Choice2Body;
    }

    void ResetEvent5Flow()
    {
        event5Phase = Event5Phase.Initial;
        ClearEvent5CardPick();
        SetEvent5ChoiceButtonsActive(true);
    }

    void SetEvent5ChoiceButtonsActive(bool active)
    {
        if (event5Choice1Button != null)
            event5Choice1Button.gameObject.SetActive(active);
        if (event5Choice2Button != null)
            event5Choice2Button.gameObject.SetActive(active);
    }

    List<int> CollectBasicCardIdsInDeck()
    {
        var result = new List<int>();
        RunDeckState deck = RunDeckState.EnsureExists();
        deck.EnsureSeeded();
        CardDatabase.EnsureInit();

        IReadOnlyList<CardDatabase.DeckEntry> entries = deck.RunDeck;
        for (int i = 0; i < entries.Count; i++)
        {
            CardDatabase.DeckEntry entry = entries[i];
            if (entry.count <= 0)
                continue;

            CardData cardData = CardDatabase.GetById(entry.cardId);
            if (cardData == null || !cardData.HasTag("BASIC"))
                continue;

            if (!result.Contains(entry.cardId))
                result.Add(entry.cardId);
        }

        return result;
    }

    void ShowEvent5CardPick(List<int> basicCardIds)
    {
        ClearEvent5CardPick();
        event5Phase = Event5Phase.CardSelect;

        Transform parent = event5CardPickContainer;
        if (parent == null && event5Canvas != null)
            parent = event5Canvas.transform;

        if (parent == null)
        {
            Debug.LogWarning("[EventStage] 이벤트5 카드 선택 UI 부모를 찾지 못했습니다.");
            OnEvent5CardPicked(basicCardIds[0]);
            return;
        }

        event5CardPickRoot = new GameObject("Event5CardPick", typeof(RectTransform));
        var rootRect = (RectTransform)event5CardPickRoot.transform;
        rootRect.SetParent(parent, false);
        StretchRectFull(rootRect);
        rootRect.SetAsLastSibling();

        var titleGo = new GameObject("Title", typeof(RectTransform));
        var titleRect = (RectTransform)titleGo.transform;
        titleRect.SetParent(rootRect, false);
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(900f, 60f);
        titleRect.anchoredPosition = new Vector2(0f, -40f);
        var titleText = titleGo.AddComponent<TextMeshProUGUI>();
        titleText.alignment = TextAlignmentOptions.Center;
        ApplyEvent5CardPickTitleStyle(titleText);
        titleText.raycastTarget = false;

        var rowGo = new GameObject("CardRow", typeof(RectTransform));
        var rowRect = (RectTransform)rowGo.transform;
        rowRect.SetParent(rootRect, false);
        rowRect.anchorMin = new Vector2(0.5f, 0.5f);
        rowRect.anchorMax = new Vector2(0.5f, 0.5f);
        rowRect.pivot = new Vector2(0.5f, 0.5f);
        rowRect.sizeDelta = new Vector2(1200f, 500f);
        rowRect.anchoredPosition = Vector2.zero;

        int count = basicCardIds.Count;
        float spacing = event5CardPickSpacing;
        float startX = -spacing * (count - 1) / 2f;

        for (int i = 0; i < count; i++)
        {
            int cardId = basicCardIds[i];
            CardData cardData = CardDatabase.GetById(cardId);
            if (cardData == null)
                continue;

            var slotGo = new GameObject($"PickSlot_{cardId}", typeof(RectTransform));
            var slotRect = (RectTransform)slotGo.transform;
            slotRect.SetParent(rowRect, false);
            slotRect.anchorMin = new Vector2(0.5f, 0.5f);
            slotRect.anchorMax = new Vector2(0.5f, 0.5f);
            slotRect.pivot = new Vector2(0.5f, 0.5f);
            slotRect.sizeDelta = new Vector2(300f, 400f);
            slotRect.anchoredPosition = new Vector2(startX + spacing * i, 0f);
            slotRect.localScale = Vector3.one * event5CardPickScale;

            if (event5CardViewPrefab != null)
            {
                NewCardView view = Instantiate(event5CardViewPrefab, slotRect);
                var viewRect = view.GetComponent<RectTransform>();
                if (viewRect != null)
                {
                    viewRect.anchorMin = new Vector2(0.5f, 0.5f);
                    viewRect.anchorMax = new Vector2(0.5f, 0.5f);
                    viewRect.pivot = new Vector2(0.5f, 0.5f);
                    viewRect.anchoredPosition = Vector2.zero;
                    viewRect.localScale = Vector3.one;
                    viewRect.SetAsFirstSibling();
                }

                view.SetCard(new CardInstance(cardData));
                view.Refresh();
                view.enabled = false;
                foreach (Graphic graphic in view.GetComponentsInChildren<Graphic>(true))
                    graphic.raycastTarget = false;
            }
            else
            {
                var labelGo = new GameObject("Label", typeof(RectTransform));
                var labelRect = (RectTransform)labelGo.transform;
                labelRect.SetParent(slotRect, false);
                StretchRectFull(labelRect);
                labelRect.SetAsFirstSibling();
                var label = labelGo.AddComponent<TextMeshProUGUI>();
                label.alignment = TextAlignmentOptions.Center;
                label.fontSize = 28f;
                label.text = cardData.displayName;
                label.raycastTarget = false;
            }

            var hitImage = slotGo.AddComponent<Image>();
            hitImage.color = new Color(1f, 1f, 1f, 0.01f);
            hitImage.raycastTarget = true;
            var button = slotGo.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            int capturedId = cardId;
            button.onClick.AddListener(() => OnEvent5CardPicked(capturedId));

            if (event5CardPickEnableHoverScale)
            {
                var hover = slotGo.AddComponent<ButtonHoverScale>();
                hover.Configure(event5CardPickHoverScale, event5CardPickHoverScale, event5CardPickHoverLerpSpeed);
                hover.SetBaseScale(slotRect.localScale);
            }
        }
    }

    void ApplyEvent5CardPickTitleStyle(TextMeshProUGUI titleText)
    {
        if (titleText == null)
            return;

        titleText.text = event5CardPickTitle;

        if (event5CardPickTitleFont != null)
            titleText.font = event5CardPickTitleFont;
        else if (event5StoryBodyText != null && event5StoryBodyText.font != null)
            titleText.font = event5StoryBodyText.font;

        if (event5CardPickTitleFontSize > 0f)
            titleText.fontSize = event5CardPickTitleFontSize;
        else if (event5StoryBodyText != null)
            titleText.fontSize = event5StoryBodyText.fontSize;
        else
            titleText.fontSize = 32f;

        titleText.color = event5CardPickTitleColor;
    }

    void OnEvent5CardPicked(int sourceCardId)
    {
        if (currentEventIndex != 4 || event5Phase != Event5Phase.CardSelect)
            return;

        ClearEvent5CardPick();
        event5Phase = Event5Phase.Initial;

        if (TryTransformBasicCard(sourceCardId, out string oldName, out string newName))
        {
            string body = resultTexts.event5Choice1Body;
            if (!string.IsNullOrEmpty(event5TransformResultFormat))
            {
                body += "\n\n" + string.Format(
                    event5TransformResultFormat.TrimStart('\n', '\r'),
                    oldName,
                    newName);
            }

            Debug.Log($"[EventStage] 이벤트5 버튼1: {oldName} → {newName}");
            PlayResultText(body);
            return;
        }

        string failedBody = string.IsNullOrEmpty(resultTexts.event5Choice1FailedBody)
            ? resultTexts.event5Choice1Body
            : resultTexts.event5Choice1FailedBody;
        PlayResultText(failedBody);
    }

    bool TryTransformBasicCard(int sourceCardId, out string sourceName, out string newName)
    {
        sourceName = string.Empty;
        newName = string.Empty;

        CardDatabase.EnsureInit();
        CardData source = CardDatabase.GetById(sourceCardId);
        if (source == null || !source.HasTag("BASIC"))
            return false;

        var candidates = new List<CardData>();
        IReadOnlyList<CardData> allCards = CardDatabase.All;
        for (int i = 0; i < allCards.Count; i++)
        {
            CardData candidate = allCards[i];
            if (candidate == null || candidate.id == sourceCardId)
                continue;
            if (candidate.element != source.element)
                continue;
            candidates.Add(candidate);
        }

        if (candidates.Count == 0)
            return false;

        CardData picked = candidates[Random.Range(0, candidates.Count)];
        RunDeckState deck = RunDeckState.EnsureExists();
        if (!deck.TryRemoveCard(sourceCardId))
            return false;

        deck.AddCard(picked.id);
        sourceName = source.displayName;
        newName = picked.displayName;
        return true;
    }

    void ClearEvent5CardPick()
    {
        if (event5CardPickRoot != null)
        {
            Destroy(event5CardPickRoot);
            event5CardPickRoot = null;
        }
    }

    static void StretchRectFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }

    // 런 덱에서 무작위로 count장 제거하고 제거된 카드 이름 목록을 반환
    List<string> RemoveRandomCardsFromRunDeck(int count)
    {
        var removedNames = new List<string>();
        if (count <= 0)
            return removedNames;

        RunDeckState deck = RunDeckState.EnsureExists();
        deck.EnsureSeeded();

        var pool = new List<int>();
        IReadOnlyList<CardDatabase.DeckEntry> entries = deck.RunDeck;
        for (int i = 0; i < entries.Count; i++)
        {
            CardDatabase.DeckEntry entry = entries[i];
            if (entry.count <= 0)
                continue;

            for (int c = 0; c < entry.count; c++)
                pool.Add(entry.cardId);
        }

        int removeCount = Mathf.Min(count, pool.Count);
        for (int i = 0; i < removeCount; i++)
        {
            int pickIndex = Random.Range(0, pool.Count);
            int cardId = pool[pickIndex];
            pool.RemoveAt(pickIndex);

            if (!deck.TryRemoveCard(cardId))
                continue;

            CardData cardData = CardDatabase.GetById(cardId);
            removedNames.Add(cardData != null ? cardData.displayName : cardId.ToString());
        }

        return removedNames;
    }

    // 이벤트3 2차 선택별 최종 효과 + 결과 본문
    EventResult ExecuteEvent3FinalChoice(int branch, int subChoice)
    {
        if (branch == 1 && subChoice == 1)
            return ExecuteEvent3_1_1();
        if (branch == 1 && subChoice == 2)
            return ExecuteEvent3_1_2();
        if (branch == 2 && subChoice == 1)
            return ExecuteEvent3_2_1();
        if (branch == 2 && subChoice == 2)
            return ExecuteEvent3_2_2();

        return default;
    }

    // 1-1: HP -10, 무작위 미보유 콤보 스킬 획득
    EventResult ExecuteEvent3_1_1()
    {
        Player player = Player.Resolve(true);
        if (player != null)
        {
            player.TakeDamage(Event3Branch1Choice1HpLoss, "self_loss");
            Debug.Log($"[EventStage] 이벤트3 1-1: HP -{Event3Branch1Choice1HpLoss}");
        }
        else
        {
            Debug.LogWarning("[EventStage] Player를 찾지 못해 HP 차감을 건너뜁니다.");
        }

        string body = resultTexts.event3Branch1Choice1Body;
        string comboName = GrantRandomUnownedCombo("이벤트3 1-1");
        if (!string.IsNullOrEmpty(comboName) && !string.IsNullOrEmpty(event3Branch1Choice1ComboFormat))
            body += string.Format(event3Branch1Choice1ComboFormat, comboName);

        return EventResult.TextOnly(body);
    }

    // 1-2: 효과 없음 (결과 텍스트만)
    EventResult ExecuteEvent3_1_2()
    {
        return EventResult.TextOnly(resultTexts.event3Branch1Choice2Body);
    }

    // 2-1: 골드 획득
    EventResult ExecuteEvent3_2_1()
    {
        int reward = 0;
        if (event3Branch2Choice1Reward > 0 && MoneyManager.Instance != null)
        {
            MoneyManager.Instance.AddMoney(event3Branch2Choice1Reward);
            reward = event3Branch2Choice1Reward;
            Debug.Log($"[EventStage] 이벤트3 2-1: +{event3Branch2Choice1Reward} 골드");
        }

        return EventResult.WithGold(resultTexts.event3Branch2Choice1Body, reward);
    }

    // 2-2: 오염된 원소 카드(id504) 획득
    EventResult ExecuteEvent3_2_2()
    {
        AddCardToRunDeck(Event3PollutedElementCardId, "이벤트3 2-2 오염된 원소 카드");
        return EventResult.TextOnly(resultTexts.event3Branch2Choice2Body);
    }

    string GrantRandomUnownedCombo(string context)
    {
        List<ComboSkillDef> all = ComboSkillDatabase.BuildOwnedCombos();
        if (all == null || all.Count == 0)
        {
            Debug.LogWarning($"[EventStage] {context} - 콤보 DB가 비어 있습니다.");
            return null;
        }

        HashSet<int> ownedRefIds = CollectOwnedComboRefIds();
        var candidates = new List<ComboSkillDef>();
        for (int i = 0; i < all.Count; i++)
        {
            ComboSkillDef skill = all[i];
            if (skill == null)
                continue;

            if (ownedRefIds.Contains(skill.refComboId))
                continue;

            candidates.Add(skill);
        }

        if (candidates.Count == 0)
            candidates = all;

        ComboSkillDef picked = candidates[Random.Range(0, candidates.Count)];
        if (picked == null)
        {
            Debug.LogWarning($"[EventStage] {context} - 지급할 콤보를 고르지 못했습니다.");
            return null;
        }

        NewBattleController battle = NewBattleController.Instance
            ?? FindFirstObjectByType<NewBattleController>(FindObjectsInactive.Include);

        if (battle == null)
        {
            Debug.LogWarning($"[EventStage] {context} - NewBattleController를 찾지 못해 콤보를 지급하지 못했습니다.");
            return null;
        }

        battle.AddOwnedCombo(picked.refComboId);
        Debug.Log($"[EventStage] {context} 콤보 획득: {picked.displayName} (ref={picked.refComboId})");
        return picked.displayName;
    }

    HashSet<int> CollectOwnedComboRefIds()
    {
        var result = new HashSet<int>();
        NewBattleController battle = NewBattleController.Instance
            ?? FindFirstObjectByType<NewBattleController>(FindObjectsInactive.Include);

        if (battle == null || battle.OwnedComboSkills == null)
            return result;

        for (int i = 0; i < battle.OwnedComboSkills.Count; i++)
        {
            ComboSkillDef skill = battle.OwnedComboSkills[i];
            if (skill == null)
                continue;

            result.Add(skill.refComboId);
        }

        return result;
    }

    TextMeshProUGUI GetStoryTitleText(int eventIndex)
    {
        return eventIndex switch
        {
            0 => event1StoryTitleText,
            1 => event2StoryTitleText,
            2 => event3StoryTitleText,
            3 => event4StoryTitleText,
            4 => event5StoryTitleText,
            _ => null
        };
    }

    TextMeshProUGUI GetStoryBodyText(int eventIndex)
    {
        return eventIndex switch
        {
            0 => event1StoryBodyText,
            1 => event2StoryBodyText,
            2 => event3StoryBodyText,
            3 => event4StoryBodyText,
            4 => event5StoryBodyText,
            _ => null
        };
    }

    void CacheAndApplyStoryText(int eventIndex, TextMeshProUGUI titleTarget, TextMeshProUGUI bodyTarget)
    {
        ref string titleCache = ref GetTitleCacheRef(eventIndex);
        ref string storyCache = ref GetStoryCacheRef(eventIndex);

        if (titleTarget != null)
        {
            if (string.IsNullOrEmpty(titleCache))
                titleCache = titleTarget.text;
            titleTarget.text = titleCache;
        }

        if (bodyTarget != null)
        {
            if (string.IsNullOrEmpty(storyCache))
                storyCache = bodyTarget.text;
            bodyTarget.text = storyCache;
        }
    }

    ref string GetTitleCacheRef(int eventIndex)
    {
        switch (eventIndex)
        {
            case 0: return ref event1TitleCached;
            case 1: return ref event2TitleCached;
            case 2: return ref event3TitleCached;
            case 3: return ref event4TitleCached;
            case 4: return ref event5TitleCached;
            default:
                throw new System.ArgumentOutOfRangeException(nameof(eventIndex));
        }
    }

    ref string GetStoryCacheRef(int eventIndex)
    {
        switch (eventIndex)
        {
            case 0: return ref event1StoryCached;
            case 1: return ref event2StoryCached;
            case 2: return ref event3StoryCached;
            case 3: return ref event4StoryCached;
            case 4: return ref event5StoryCached;
            default:
                throw new System.ArgumentOutOfRangeException(nameof(eventIndex));
        }
    }

    ref BodyStyleSnapshot GetBodyStyleBackupRef(int eventIndex)
    {
        switch (eventIndex)
        {
            case 0: return ref event1BodyStyleBackup;
            case 1: return ref event2BodyStyleBackup;
            case 2: return ref event3BodyStyleBackup;
            case 3: return ref event4BodyStyleBackup;
            case 4: return ref event5BodyStyleBackup;
            default:
                throw new System.ArgumentOutOfRangeException(nameof(eventIndex));
        }
    }

    // 주어진 카드 ID를 검증한 뒤 런 덱에 추가한다
    void AddCardToRunDeck(int cardId, string context)
    {
        if (cardId <= 0)
        {
            Debug.LogWarning($"[EventStage] {context} - 유효한 카드 ID가 없습니다.");
            return;
        }

        CardData cardData = CardDatabase.GetById(cardId);
        if (cardData == null)
        {
            Debug.LogWarning($"[EventStage] {context} - 카드 ID {cardId}를 찾을 수 없습니다.");
            return;
        }

        RunDeckState.EnsureExists().AddCard(cardId);
        Debug.Log($"[EventStage] {context} 획득: {cardData.displayName}({cardId})");
    }

    // 풀에서 데이터베이스에 존재하는 카드 ID 하나를 무작위로 고른다
    int PickRandomCardId(List<int> pool)
    {
        if (pool == null || pool.Count == 0)
            return -1;

        var candidates = new List<int>(pool.Count);
        for (int i = 0; i < pool.Count; i++)
        {
            int cardId = pool[i];
            if (CardDatabase.GetById(cardId) != null)
                candidates.Add(cardId);
        }

        if (candidates.Count == 0)
            return -1;

        return candidates[Random.Range(0, candidates.Count)];
    }

    // 결과 본문에 인스펙터 지정 스타일을 적용한다(원래 스타일은 1회 보관)
    void ApplyResultTextStyle(TextMeshProUGUI bodyTarget)
    {
        if (!overrideResultTextStyle || bodyTarget == null) return;

        CaptureBodyStyle(bodyTarget, ref GetBodyStyleBackupRef(currentEventIndex));

        if (resultTextStyle.font != null) bodyTarget.font = resultTextStyle.font;
        bodyTarget.fontSize = resultTextStyle.fontSize;
        bodyTarget.color = resultTextStyle.color;
        bodyTarget.fontStyle = resultTextStyle.fontStyle;
        bodyTarget.alignment = resultTextStyle.alignment;
        bodyTarget.characterSpacing = resultTextStyle.characterSpacing;
        bodyTarget.lineSpacing = resultTextStyle.lineSpacing;
        bodyTarget.paragraphSpacing = resultTextStyle.paragraphSpacing;
    }

    // 스토리 본문을 보관해 둔 원래 스타일로 되돌린다
    void RestoreStoryBodyStyle(TextMeshProUGUI bodyTarget)
    {
        if (!overrideResultTextStyle || bodyTarget == null) return;
        ApplyBodyStyle(bodyTarget, GetBodyStyleBackupRef(currentEventIndex));
    }

    // 텍스트의 현재 스타일을 스냅샷에 1회 저장한다
    void CaptureBodyStyle(TextMeshProUGUI t, ref BodyStyleSnapshot s)
    {
        if (s.captured || t == null) return;
        s.font = t.font;
        s.fontSize = t.fontSize;
        s.color = t.color;
        s.fontStyle = t.fontStyle;
        s.alignment = t.alignment;
        s.characterSpacing = t.characterSpacing;
        s.lineSpacing = t.lineSpacing;
        s.paragraphSpacing = t.paragraphSpacing;
        s.captured = true;
    }

    // 저장된 스냅샷 스타일을 텍스트에 적용한다
    void ApplyBodyStyle(TextMeshProUGUI t, BodyStyleSnapshot s)
    {
        if (t == null || !s.captured) return;
        t.font = s.font;
        t.fontSize = s.fontSize;
        t.color = s.color;
        t.fontStyle = s.fontStyle;
        t.alignment = s.alignment;
        t.characterSpacing = s.characterSpacing;
        t.lineSpacing = s.lineSpacing;
        t.paragraphSpacing = s.paragraphSpacing;
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
        phase = EventPhase.Idle;
        event3Phase = Event3Phase.Initial;
        ClearEvent5CardPick();
        event5Phase = Event5Phase.Initial;

        if (roundManager != null)
            roundManager.ReturnToMap();
        else
            GameStateController.Instance?.ShowMap();
    }

    Sprite ResolveGoldRewardIcon()
    {
        if (goldRewardIcon != null)
            return goldRewardIcon;

        if (fallbackToMoneyManagerIcon && MoneyManager.Instance != null)
            return MoneyManager.Instance.MoneyIcon;

        return null;
    }

    void HideAllGoldRewardRows()
    {
        foreach (KeyValuePair<int, GoldRewardRowUI> pair in goldRewardRows)
        {
            if (pair.Value?.root != null)
                pair.Value.root.SetActive(false);
        }
    }

    void ShowGoldRewardRow(TextMeshProUGUI bodyTarget, int amount)
    {
        if (bodyTarget == null || amount <= 0)
            return;

        Sprite iconSprite = ResolveGoldRewardIcon();
        if (iconSprite == null)
        {
            Debug.LogWarning("[EventStage] 골드 보상 아이콘이 없어 골드 표시를 생략합니다.");
            return;
        }

        GoldRewardRowUI row = GetOrCreateGoldRewardRow(bodyTarget);
        if (row == null)
            return;

        row.icon.sprite = iconSprite;
        row.icon.preserveAspect = true;
        row.icon.raycastTarget = false;

        ConfigureGoldAmountText(row.amount, bodyTarget);
        row.amount.text = string.IsNullOrEmpty(goldRewardAmountFormat)
            ? amount.ToString()
            : string.Format(goldRewardAmountFormat, amount);

        ApplyGoldRewardRowLayout(row);
        PositionGoldRewardRow(bodyTarget, row);
        row.root.transform.SetAsLastSibling();
        row.root.SetActive(true);
    }

    void ApplyGoldRewardRowLayout(GoldRewardRowUI row)
    {
        if (row?.root == null)
            return;

        HorizontalLayoutGroup layout = row.root.GetComponent<HorizontalLayoutGroup>();
        if (layout != null)
            layout.spacing = goldRewardIconTextSpacing;

        LayoutElement iconLayout = row.icon != null ? row.icon.GetComponent<LayoutElement>() : null;
        if (iconLayout != null)
        {
            iconLayout.preferredWidth = goldRewardIconSize;
            iconLayout.preferredHeight = goldRewardIconSize;
        }
    }

    GoldRewardRowUI GetOrCreateGoldRewardRow(TextMeshProUGUI bodyTarget)
    {
        int key = bodyTarget.GetInstanceID();
        if (goldRewardRows.TryGetValue(key, out GoldRewardRowUI existing) && existing?.root != null)
        {
            if (existing.root.transform.parent != bodyTarget.transform)
                existing.root.transform.SetParent(bodyTarget.transform, false);

            return existing;
        }

        Transform parent = bodyTarget.transform;

        var rowGo = new GameObject("GoldRewardRow", typeof(RectTransform));
        rowGo.transform.SetParent(parent, false);

        RectTransform rowRect = rowGo.GetComponent<RectTransform>();

        HorizontalLayoutGroup layout = rowGo.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.spacing = goldRewardIconTextSpacing;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = rowGo.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject iconGo = new GameObject("GoldIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconGo.transform.SetParent(rowGo.transform, false);
        LayoutElement iconLayout = iconGo.AddComponent<LayoutElement>();
        iconLayout.preferredWidth = goldRewardIconSize;
        iconLayout.preferredHeight = goldRewardIconSize;
        Image icon = iconGo.GetComponent<Image>();

        GameObject amountGo = new GameObject("GoldAmount", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        amountGo.transform.SetParent(rowGo.transform, false);
        TextMeshProUGUI amount = amountGo.GetComponent<TextMeshProUGUI>();
        amount.raycastTarget = false;
        amount.textWrappingMode = TextWrappingModes.NoWrap;

        rowGo.SetActive(false);

        var row = new GoldRewardRowUI
        {
            root = rowGo,
            rect = rowRect,
            icon = icon,
            amount = amount
        };

        goldRewardRows[key] = row;
        return row;
    }

    void ConfigureGoldAmountText(TextMeshProUGUI amount, TextMeshProUGUI bodyReference)
    {
        if (amount == null || bodyReference == null)
            return;

        if (goldRewardFont != null)
            amount.font = goldRewardFont;
        else if (overrideResultTextStyle && resultTextStyle.font != null)
            amount.font = resultTextStyle.font;
        else
            amount.font = bodyReference.font;

        if (goldRewardFontSize > 0f)
            amount.fontSize = goldRewardFontSize;
        else if (overrideResultTextStyle)
            amount.fontSize = resultTextStyle.fontSize;
        else
            amount.fontSize = bodyReference.fontSize;

        if (useGoldRewardFontColor)
            amount.color = goldRewardFontColor;
        else if (overrideResultTextStyle)
            amount.color = resultTextStyle.color;
        else
            amount.color = bodyReference.color;

        if (overrideResultTextStyle)
            amount.fontStyle = resultTextStyle.fontStyle;
        else
            amount.fontStyle = bodyReference.fontStyle;

        amount.alignment = TextAlignmentOptions.MidlineLeft;
    }

    void PositionGoldRewardRow(TextMeshProUGUI bodyTarget, GoldRewardRowUI row)
    {
        if (bodyTarget == null || row?.rect == null)
            return;

        RectTransform bodyRect = bodyTarget.rectTransform;
        bodyTarget.ForceMeshUpdate(true, true);
        Canvas.ForceUpdateCanvases();

        Bounds textBounds = bodyTarget.textBounds;
        Vector2 anchor = bodyRect.pivot;
        float textCenterX = (textBounds.min.x + textBounds.max.x) * 0.5f;

        row.rect.anchorMin = anchor;
        row.rect.anchorMax = anchor;
        row.rect.pivot = new Vector2(0.5f, 1f);
        row.rect.anchoredPosition = new Vector2(
            textCenterX,
            textBounds.min.y - goldRewardRowSpacing);

        LayoutRebuilder.ForceRebuildLayoutImmediate(row.rect);
    }
}
