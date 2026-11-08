using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 화면 배경음과 UI 클릭음을 한 오브젝트에서 재생한다
public class GameAudio : MonoBehaviour
{
    [Header("배경음")]
    [Tooltip("루프 배경음용. 비우면 이 오브젝트의 AudioSource를 사용한다.")]
    [SerializeField] private AudioSource bgmSource; // 배경음 소스
    [SerializeField] private AudioClip mainMenuClip; // 메인 화면 곡
    [SerializeField] private AudioClip mapClip; // 맵 화면 곡
    [SerializeField] private AudioClip normalCombatClip; // 일반 전투 곡
    [SerializeField] private AudioClip eliteCombatClip; // 정예 전투 곡
    [SerializeField] private AudioClip bossCombatClip; // 보스 전투 곡
    [SerializeField] private AudioClip shopClip; // 상점 곡
    [SerializeField] private AudioClip restClip; // 휴식 곡
    [SerializeField] private AudioClip event1Clip; // 이벤트 1 곡
    [SerializeField] private AudioClip event2Clip; // 이벤트 2~5 곡
    [Tooltip("켜져 있는 동안 메인 곡을 재생한다.")]
    [SerializeField] private GameObject mainMenuRoot; // 메인 화면 루트
    [Tooltip("켜져 있는 동안 맵 곡을 재생한다. 메인 화면이 켜져 있으면 메인 곡이 우선한다.")]
    [SerializeField] private GameObject mapRoot; // 맵 화면 루트
    [Tooltip("켜져 있는 동안 전투 곡을 재생한다. 메인·맵보다 나중에 본다.")]
    [SerializeField] private GameObject combatRoot; // 전투 화면 루트
    [Tooltip("켜져 있는 동안 상점 곡을 재생한다.")]
    [SerializeField] private GameObject shopRoot; // 상점 화면 루트
    [Tooltip("켜져 있는 동안 휴식 곡을 재생한다.")]
    [SerializeField] private GameObject restRoot; // 휴식 화면 루트
    [Tooltip("켜져 있는 동안 이벤트 곡을 재생한다. 1번은 event1Clip, 2~5번은 event2Clip.")]
    [SerializeField] private GameObject eventRoot; // 이벤트 화면 루트
    [Range(0f, 1f)]
    [SerializeField] private float bgmVolume = 1f; // 배경음 기본 볼륨(BGM 채널과 곱함)

    [Header("UI")]
    [Tooltip("클릭음용. 배경음 소스와 분리해야 UI 볼륨이 배경음에 섞이지 않는다.")]
    [SerializeField] private AudioSource uiSource; // 클릭음 소스
    [SerializeField] private AudioClip uiClickClip; // 버튼 클릭음
    [Range(0f, 1f)]
    [SerializeField] private float uiClickVolume = 1f; // 클릭음 기본 볼륨(UI 채널과 곱함)

    public static GameAudio Instance { get; private set; }

    AudioClip playingBgm; // 지금 소스에 올라가 있는 배경음
    NodeType combatTrack = NodeType.Combat; // 전투 화면에서 재생할 곡 종류
    readonly List<RaycastResult> hits = new List<RaycastResult>(); // 클릭 위치의 UI 히트

    // 소스를 준비하고, 비어 있는 화면 참조를 씬에서 채운다
    void Awake()
    {
        Instance = this;

        if (bgmSource == null)
            bgmSource = GetComponent<AudioSource>();
        PrepareBgmSource(bgmSource);
        PrepareUiSource(uiSource);

        if (mainMenuRoot == null)
        {
            var menu = FindFirstObjectByType<MainMenuController>(FindObjectsInactive.Include);
            if (menu != null)
                mainMenuRoot = menu.gameObject;
        }

        if (GameStateController.Instance == null)
            return;

        if (combatRoot == null)
            combatRoot = GameStateController.Instance.combatStage;
        if (shopRoot == null)
            shopRoot = GameStateController.Instance.shopStage;
        if (restRoot == null)
            restRoot = GameStateController.Instance.restStage;
        if (eventRoot == null)
            eventRoot = GameStateController.Instance.eventStage;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // 전투 화면이 켜지면 이 종류에 해당하는 곡을 재생한다.
    public void SetCombatTrack(NodeType type)
    {
        if (type == NodeType.Boss)
            combatTrack = NodeType.Boss;
        else if (type == NodeType.Elite)
            combatTrack = NodeType.Elite;
        else
            combatTrack = NodeType.Combat;
    }

    // 보이는 화면에 맞는 배경음을 유지하고, 버튼 클릭음을 재생한다
    void Update()
    {
        UpdateBgm();

        if (Input.GetMouseButtonUp(0) && IsReleasingOverButton())
            PlayUiClick();
    }

    // 보이는 화면에 맞는 곡을 고르고, 화면이 바뀌면 교체한다
    void UpdateBgm()
    {
        if (bgmSource == null)
            return;

        AudioClip next = null;
        if (mainMenuRoot != null && mainMenuRoot.activeInHierarchy)
            next = mainMenuClip;
        else if (mapRoot != null && mapRoot.activeInHierarchy)
            next = mapClip;
        else if (combatRoot != null && combatRoot.activeInHierarchy)
            next = CombatClip();
        else if (shopRoot != null && shopRoot.activeInHierarchy)
            next = shopClip;
        else if (restRoot != null && restRoot.activeInHierarchy)
            next = restClip;
        else if (eventRoot != null && eventRoot.activeInHierarchy)
            next = EventClip();

        if (next != playingBgm)
        {
            playingBgm = next;
            if (next == null)
            {
                bgmSource.Stop();
                return;
            }

            PlayBgm(next);
        }

        if (next != null)
            bgmSource.volume = Mathf.Clamp01(bgmVolume) * SettingPanel.GetChannelVolume("BGM");
    }

    AudioClip CombatClip()
    {
        if (combatTrack == NodeType.Boss)
            return bossCombatClip;
        if (combatTrack == NodeType.Elite)
            return eliteCombatClip;
        return normalCombatClip;
    }

    // 이벤트 1은 전용 곡, 2~5는 같은 곡을 쓴다.
    AudioClip EventClip()
    {
        var stage = eventRoot != null ? eventRoot.GetComponent<EventStageController>() : null;
        if (stage == null)
            stage = FindFirstObjectByType<EventStageController>(FindObjectsInactive.Include);
        if (stage != null && stage.CurrentEventIndex <= 0)
            return event1Clip;
        return event2Clip;
    }

    // 배경음을 처음부터 반복 재생한다
    void PlayBgm(AudioClip clip)
    {
        if (clip.loadState == AudioDataLoadState.Unloaded)
            clip.LoadAudioData();

        bgmSource.clip = clip;
        bgmSource.volume = Mathf.Clamp01(bgmVolume) * SettingPanel.GetChannelVolume("BGM");
        bgmSource.time = 0f;
        bgmSource.Play();
    }

    // 포인터 아래 맨 위 UI가 사용 가능한 Button인지 확인한다
    bool IsReleasingOverButton()
    {
        var eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;

        var pointer = new PointerEventData(eventSystem);
        pointer.position = Input.mousePosition;
        hits.Clear();
        eventSystem.RaycastAll(pointer, hits);
        if (hits.Count == 0)
            return false;

        var hitObject = hits[0].gameObject;
        var cardView = hitObject.GetComponentInParent<Battle.UI.NewCardView>();
        if (cardView != null && cardView.GetComponentInParent<Battle.UI.CardHandHUD>() != null)
            return false;

        var button = hitObject.GetComponentInParent<Button>();
        return button != null && button.isActiveAndEnabled && button.interactable;
    }

    // UI 채널 볼륨을 적용해 클릭음을 재생한다
    void PlayUiClick()
    {
        if (uiSource == null || uiClickClip == null)
            return;

        float uiScale = SettingPanel.GetChannelVolume("UI");
        uiSource.PlayOneShot(uiClickClip, Mathf.Clamp01(uiClickVolume) * uiScale);
    }

    // 배경음 소스를 2D 루프로 맞춘다
    static void PrepareBgmSource(AudioSource source)
    {
        if (source == null)
            return;

        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.priority = 64;
    }

    // 클릭음 소스를 2D 단발 재생으로 맞춘다
    static void PrepareUiSource(AudioSource source)
    {
        if (source == null)
            return;

        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.clip = null;
    }
}
