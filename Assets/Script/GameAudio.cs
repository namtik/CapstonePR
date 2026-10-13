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
    [Tooltip("켜져 있는 동안 메인 곡을 재생한다.")]
    [SerializeField] private GameObject mainMenuRoot; // 메인 화면 루트
    [Tooltip("켜져 있는 동안 맵 곡을 재생한다. 메인 화면이 켜져 있으면 메인 곡이 우선한다.")]
    [SerializeField] private GameObject mapRoot; // 맵 화면 루트
    [Range(0f, 1f)]
    [SerializeField] private float bgmVolume = 1f; // 배경음 기본 볼륨(BGM 채널과 곱함)

    [Header("UI")]
    [Tooltip("클릭음용. 배경음 소스와 분리해야 UI 볼륨이 배경음에 섞이지 않는다.")]
    [SerializeField] private AudioSource uiSource; // 클릭음 소스
    [SerializeField] private AudioClip uiClickClip; // 버튼 클릭음
    [Range(0f, 1f)]
    [SerializeField] private float uiClickVolume = 1f; // 클릭음 기본 볼륨(UI 채널과 곱함)

    AudioClip playingBgm; // 지금 소스에 올라가 있는 배경음
    readonly List<RaycastResult> hits = new List<RaycastResult>(); // 클릭 위치의 UI 히트

    // 소스를 준비하고, 비어 있는 화면 참조를 씬에서 채운다
    void Awake()
    {
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
    }

    // 보이는 화면에 맞는 배경음을 유지하고, 버튼 클릭음을 재생한다
    void Update()
    {
        UpdateBgm();

        if (Input.GetMouseButtonUp(0) && IsReleasingOverButton())
            PlayUiClick();
    }

    // 메인 화면, 맵 화면 순으로 곡을 고르고 화면이 바뀌면 교체한다
    void UpdateBgm()
    {
        if (bgmSource == null)
            return;

        AudioClip next = null;
        if (mainMenuRoot != null && mainMenuRoot.activeInHierarchy)
            next = mainMenuClip;
        else if (mapRoot != null && mapRoot.activeInHierarchy)
            next = mapClip;

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

        var button = hits[0].gameObject.GetComponentInParent<Button>();
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
