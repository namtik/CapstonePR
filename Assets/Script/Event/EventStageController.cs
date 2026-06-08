using System.Collections.Generic;
using Battle;
using Battle.Card;
using Battle.Relic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EventStageController : MonoBehaviour
{
    // 결과 텍스트(확률 분기)
    [System.Serializable]
    public class GambleResultText
    {
        [TextArea] public string successBody;   // 성공 시 본문
        [TextArea] public string failureBody;   // 실패 시 본문
    }

    // 선택지별 결과 본문 모음 (추후 Excel→JSON 로더가 이 구조를 채워줄 예정)
    [System.Serializable]
    public class EventResultTexts
    {
        [Header("이벤트1")]
        public GambleResultText event1Choice1;   // 도박: 성공/실패
        [TextArea] public string event1Choice2Body;  // 회복
        [TextArea] public string event1Choice3Body;  // 속성+탈진 카드

        [Header("이벤트2")]
        [TextArea] public string event2Choice1Body;  // 속성카드
        [TextArea] public string event2Choice2Body;  // 복귀
    }

    private enum EventPhase { Idle, Story, ResultTyping, ResultDone }

    [Header("Event Canvases")]
    [SerializeField] private GameObject event1Canvas;
    [SerializeField] private GameObject event2Canvas;

    [Header("Story Typing")]
    [SerializeField] private TextMeshProUGUI event1StoryTitleText;
    [SerializeField] private TextMeshProUGUI event2StoryTitleText;
    [SerializeField] private TextMeshProUGUI event1StoryBodyText;
    [SerializeField] private TextMeshProUGUI event2StoryBodyText;
    [SerializeField] private bool useTypewriter = true;
    [SerializeField] private float titleCharactersPerSecond = 40f;
    [SerializeField] private float bodyCharactersPerSecond = 30f;

    [Header("Event 1 Buttons")]
    [SerializeField] private Button event1Choice1Button;
    [SerializeField] private Button event1Choice2Button;
    [SerializeField] private Button event1Choice3Button;

    [Header("Event 2 Buttons")]
    [SerializeField] private Button event2Choice1Button;
    [SerializeField] private Button event2Choice2Button;

    [Header("Button Entrance Animation")]
    [SerializeField] private bool useButtonEntranceAnimation = false;

    [Header("Event Logic")]
    [SerializeField] private List<int> elementalCardPool = new List<int> { 100, 200, 300, 400 };
    [SerializeField] private int exhaustionCardId = 500;

    [Header("Event1 Button1: 50 소모, 40%로 120 획득")]
    [SerializeField] private int event1Button1Cost = 50;
    [SerializeField] private float event1Button1WinChance = 0.4f;
    [SerializeField] private int event1Button1Reward = 120;

    [Header("Event1 Button2: 최대체력 20% 회복")]
    [Range(0f, 1f)]
    [SerializeField] private float event1Button2HealPercent = 0.2f;

    [Header("선택지 결과 텍스트")]
    [SerializeField] private EventResultTexts resultTexts = new EventResultTexts();

    private Roundmanager roundManager;
    private int currentEventIndex;
    private EventPhase phase = EventPhase.Idle;
    private int phaseEnteredFrame;
    private Coroutine typingRoutine;
    private TextMeshProUGUI activeResultBody;
    private string event1TitleCached;
    private string event2TitleCached;
    private string event1StoryCached;
    private string event2StoryCached;

    void Awake()
    {
        BindButton(event1Choice1Button, OnChoice1Clicked);
        BindButton(event1Choice2Button, OnChoice2Clicked);
        BindButton(event1Choice3Button, OnChoice3Clicked);

        BindButton(event2Choice1Button, OnChoice1Clicked);
        BindButton(event2Choice2Button, OnChoice2Clicked);

        SetCanvasState(-1);
    }

    void Update()
    {
        if (phase != EventPhase.ResultTyping && phase != EventPhase.ResultDone)
            return;

        // 상태 전환을 유발한 그 클릭이 즉시 다음 액션까지 트리거하지 않도록 가드
        if (Time.frameCount <= phaseEnteredFrame)
            return;

        if (!Input.GetMouseButtonDown(0))
            return;

        if (phase == EventPhase.ResultTyping)
            SkipResultTyping();
        else
            ReturnToMap();
    }

    public void BeginEvent(EventRoundData data, Roundmanager manager)
    {
        roundManager = manager;

        currentEventIndex = Random.Range(0, 2);
        SetCanvasState(currentEventIndex);
        RestoreStoryVisuals();
        SetPhase(EventPhase.Story);
        PlayStoryTypewriter();
    }

    void BindButton(Button button, UnityEngine.Events.UnityAction callback)
    {
        if (button == null) return;
        button.onClick.RemoveListener(callback);
        button.onClick.AddListener(callback);
    }

    void SetCanvasState(int eventIndex)
    {
        if (event1Canvas != null)
            event1Canvas.SetActive(eventIndex == 0);

        if (event2Canvas != null)
            event2Canvas.SetActive(eventIndex == 1);
    }

    // 스토리 단계 진입 시 제목/선택지 버튼을 다시 보이게 복원
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

    public void OnChoice1Clicked()
    {
        if (phase != EventPhase.Story) return;

        string resultBody = currentEventIndex == 0
            ? ExecuteEvent1Choice1()
            : ExecuteEvent2Choice1();

        PlayResultText(resultBody);
    }

    public void OnChoice2Clicked()
    {
        if (phase != EventPhase.Story) return;

        string resultBody = currentEventIndex == 0
            ? ExecuteEvent1Choice2()
            : ExecuteEvent2Choice2();

        PlayResultText(resultBody);
    }

    public void OnChoice3Clicked()
    {
        if (phase != EventPhase.Story) return;
        // 이벤트2에는 버튼3이 없음. 이벤트1 전용.
        if (currentEventIndex != 0) return;

        string resultBody = ExecuteEvent1Choice3();
        PlayResultText(resultBody);
    }

    // 선택 후: 제목/선택지 숨기고 결과 본문을 새로 타이핑
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

    void EnterResultDone()
    {
        SetPhase(EventPhase.ResultDone);
    }

    void SetPhase(EventPhase next)
    {
        phase = next;
        phaseEnteredFrame = Time.frameCount;
    }

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

    string ExecuteEvent1Choice3()
    {
        int elementalId = PickRandomCardId(elementalCardPool);
        AddCardToRunDeck(elementalId, "이벤트1 버튼3 속성카드");
        AddCardToRunDeck(exhaustionCardId, "이벤트1 버튼3 탈진카드");
        return resultTexts.event1Choice3Body;
    }

    string ExecuteEvent2Choice1()
    {
        int elementalId = PickRandomCardId(elementalCardPool);
        AddCardToRunDeck(elementalId, "이벤트2 버튼1 속성카드");
        return resultTexts.event2Choice1Body;
    }

    string ExecuteEvent2Choice2()
    {
        Debug.Log("[EventStage] 이벤트2 버튼2: 맵으로 복귀");
        return resultTexts.event2Choice2Body;
    }

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

    TextMeshProUGUI CurrentTitleTarget()
    {
        return currentEventIndex == 0 ? event1StoryTitleText : event2StoryTitleText;
    }

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

    void ReturnToMap()
    {
        phase = EventPhase.Idle;

        if (roundManager != null)
            roundManager.ReturnToMap();
        else
            GameStateController.Instance?.ShowMap();
    }
}
