using System.Collections.Generic;
using Battle;
using Battle.Card;
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

    [Header("Event Canvases")]
    [SerializeField] private GameObject event1Canvas;   // 이벤트1 캔버스
    [SerializeField] private GameObject event2Canvas;   // 이벤트2 캔버스

    [Header("Story Typing")]
    [SerializeField] private TextMeshProUGUI event1StoryTitleText;   // 이벤트1 제목 텍스트
    [SerializeField] private TextMeshProUGUI event2StoryTitleText;   // 이벤트2 제목 텍스트
    [SerializeField] private TextMeshProUGUI event1StoryBodyText;   // 이벤트1 본문 텍스트
    [SerializeField] private TextMeshProUGUI event2StoryBodyText;   // 이벤트2 본문 텍스트
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

    [Header("선택지 결과 텍스트")]
    [SerializeField] private EventResultTexts resultTexts = new EventResultTexts();   // 선택지 결과 본문 모음

    [Header("선택지 결과 텍스트 스타일 (스토리 본문과 별도로 지정)")]
    [Tooltip("켜면 결과 본문에 아래 스타일을 적용하고, 스토리로 돌아가면 원래 스타일로 복원한다.")]
    [SerializeField] private bool overrideResultTextStyle = true;   // 결과 본문 스타일 덮어쓰기 사용 여부
    [SerializeField] private ResultTextStyle resultTextStyle = new ResultTextStyle();   // 결과 본문에 적용할 스타일

    private RoundManager roundManager;   // 라운드 관리자 참조
    private int currentEventIndex;   // 현재 진행 중인 이벤트 인덱스(0/1)
    private EventPhase phase = EventPhase.Idle;   // 현재 이벤트 단계
    private int phaseEnteredFrame;   // 단계 진입 시점의 프레임 번호
    private Coroutine typingRoutine;   // 타이핑 연출 코루틴 핸들
    private TextMeshProUGUI activeResultBody;   // 현재 타이핑 중인 결과 본문 텍스트
    private string event1TitleCached;   // 이벤트1 제목 원본 캐시
    private string event2TitleCached;   // 이벤트2 제목 원본 캐시
    private string event1StoryCached;   // 이벤트1 스토리 본문 원본 캐시
    private string event2StoryCached;   // 이벤트2 스토리 본문 원본 캐시
    private BodyStyleSnapshot event1BodyStyleBackup;   // 이벤트1 본문 스타일 백업
    private BodyStyleSnapshot event2BodyStyleBackup;   // 이벤트2 본문 스타일 백업

    // 선택지 버튼 콜백을 연결하고 캔버스를 모두 끈다
    void Awake()
    {
        BindButton(event1Choice1Button, OnChoice1Clicked);
        BindButton(event1Choice2Button, OnChoice2Clicked);
        BindButton(event1Choice3Button, OnChoice3Clicked);

        BindButton(event2Choice1Button, OnChoice1Clicked);
        BindButton(event2Choice2Button, OnChoice2Clicked);

        SetCanvasState(-1);
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

        currentEventIndex = Random.Range(0, 2);
        SetCanvasState(currentEventIndex);
        RestoreStoryVisuals();
        SetPhase(EventPhase.Story);
        PlayStoryTypewriter();
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
    }

    // 스토리 단계 진입 시 제목과 선택지 버튼을 다시 표시한다
    void RestoreStoryVisuals()
    {
        TextMeshProUGUI title = CurrentTitleTarget();
        if (title != null)
            title.gameObject.SetActive(true);

        foreach (Button button in CurrentChoiceButtons())
        {
            if (button != null)
                button.gameObject.SetActive(true);
        }
    }

    // 스토리 제목/본문을 캐시 텍스트로 채우고 타자기 연출을 재생한다
    void PlayStoryTypewriter()
    {
        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }

        TextMeshProUGUI titleTarget = currentEventIndex == 0 ? event1StoryTitleText : event2StoryTitleText;
        TextMeshProUGUI bodyTarget = currentEventIndex == 0 ? event1StoryBodyText : event2StoryBodyText;
        if (titleTarget == null && bodyTarget == null)
            return;

        // 결과 스타일로 덮어쓴 적이 있으면 스토리 본문 원래 스타일로 복원
        RestoreStoryBodyStyle(bodyTarget);

        if (currentEventIndex == 0)
        {
            if (titleTarget != null)
            {
                if (string.IsNullOrEmpty(event1TitleCached))
                    event1TitleCached = titleTarget.text;
                titleTarget.text = event1TitleCached;
            }

            if (bodyTarget != null)
            {
                if (string.IsNullOrEmpty(event1StoryCached))
                    event1StoryCached = bodyTarget.text;
                bodyTarget.text = event1StoryCached;
            }
        }
        else
        {
            if (titleTarget != null)
            {
                if (string.IsNullOrEmpty(event2TitleCached))
                    event2TitleCached = titleTarget.text;
                titleTarget.text = event2TitleCached;
            }

            if (bodyTarget != null)
            {
                if (string.IsNullOrEmpty(event2StoryCached))
                    event2StoryCached = bodyTarget.text;
                bodyTarget.text = event2StoryCached;
            }
        }

        if (!useTypewriter)
        {
            if (titleTarget != null)
                titleTarget.maxVisibleCharacters = int.MaxValue;

            if (bodyTarget != null)
                bodyTarget.maxVisibleCharacters = int.MaxValue;

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
    }

    // 선택지1 클릭 처리: 해당 이벤트의 선택1 효과 실행 후 결과 표시
    public void OnChoice1Clicked()
    {
        if (phase != EventPhase.Story) return;

        string resultBody = currentEventIndex == 0
            ? ExecuteEvent1Choice1()
            : ExecuteEvent2Choice1();

        PlayResultText(resultBody);
    }

    // 선택지2 클릭 처리: 해당 이벤트의 선택2 효과 실행 후 결과 표시
    public void OnChoice2Clicked()
    {
        if (phase != EventPhase.Story) return;

        string resultBody = currentEventIndex == 0
            ? ExecuteEvent1Choice2()
            : ExecuteEvent2Choice2();

        PlayResultText(resultBody);
    }

    // 선택지3 클릭 처리: 이벤트1 전용 선택3 효과 실행 후 결과 표시
    public void OnChoice3Clicked()
    {
        if (phase != EventPhase.Story) return;
        // 이벤트2에는 버튼3이 없으므로 이벤트1에서만 동작
        if (currentEventIndex != 0) return;

        string resultBody = ExecuteEvent1Choice3();
        PlayResultText(resultBody);
    }

    // 제목/선택지를 숨기고 결과 본문을 새로 타이핑한다
    void PlayResultText(string body)
    {
        TextMeshProUGUI title = CurrentTitleTarget();
        if (title != null)
            title.gameObject.SetActive(false);

        foreach (Button button in CurrentChoiceButtons())
        {
            if (button != null)
                button.gameObject.SetActive(false);
        }

        TextMeshProUGUI bodyTarget = currentEventIndex == 0 ? event1StoryBodyText : event2StoryBodyText;
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
        SetPhase(EventPhase.ResultDone);
    }

    // 단계를 변경하고 진입 프레임을 기록한다
    void SetPhase(EventPhase next)
    {
        phase = next;
        phaseEnteredFrame = Time.frameCount;
    }

    // 이벤트1 선택1(도박): 재화 소모 후 확률로 보상을 지급하고 결과 본문을 반환
    string ExecuteEvent1Choice1()
    {
        if (MoneyManager.Instance == null)
        {
            Debug.LogWarning("[EventStage] MoneyManager가 없어 이벤트1 버튼1 보상을 처리하지 못했습니다.");
            return resultTexts.event1Choice1.failureBody;
        }

        bool spent = MoneyManager.Instance.SpendMoney(event1Button1Cost);
        if (!spent)
        {
            Debug.Log($"[EventStage] 재화 부족으로 이벤트1 버튼1 실패 (필요: {event1Button1Cost})");
            return resultTexts.event1Choice1.failureBody;
        }

        float chance = Mathf.Clamp01(event1Button1WinChance);
        bool won = Random.value < chance;
        if (won)
        {
            MoneyManager.Instance.AddMoney(event1Button1Reward);
            Debug.Log($"[EventStage] 이벤트1 버튼1 성공: -{event1Button1Cost}, +{event1Button1Reward}");
            return resultTexts.event1Choice1.successBody;
        }

        Debug.Log($"[EventStage] 이벤트1 버튼1 실패: -{event1Button1Cost}, 획득 없음");
        return resultTexts.event1Choice1.failureBody;
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

        if (currentEventIndex == 0) CaptureBodyStyle(bodyTarget, ref event1BodyStyleBackup);
        else CaptureBodyStyle(bodyTarget, ref event2BodyStyleBackup);

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
        if (currentEventIndex == 0) ApplyBodyStyle(bodyTarget, event1BodyStyleBackup);
        else ApplyBodyStyle(bodyTarget, event2BodyStyleBackup);
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
        return currentEventIndex == 0 ? event1StoryTitleText : event2StoryTitleText;
    }

    // 현재 이벤트의 선택지 버튼들을 차례로 반환한다
    IEnumerable<Button> CurrentChoiceButtons()
    {
        if (currentEventIndex == 0)
        {
            yield return event1Choice1Button;
            yield return event1Choice2Button;
            yield return event1Choice3Button;
        }
        else
        {
            yield return event2Choice1Button;
            yield return event2Choice2Button;
        }
    }

    // 이벤트를 종료하고 맵 화면으로 복귀한다
    void ReturnToMap()
    {
        phase = EventPhase.Idle;

        if (roundManager != null)
            roundManager.ReturnToMap();
        else
            GameStateController.Instance?.ShowMap();
    }
}
