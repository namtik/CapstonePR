using System.Collections.Generic;
using UnityEngine;

// 전투 효과음(SFX) 재생기 싱글톤 — 인스펙터에 연결한 클립을 재생한다
[RequireComponent(typeof(AudioSource))]
public class SfxManager : MonoBehaviour
{
    public static SfxManager Instance { get; private set; } // 전역 싱글톤 인스턴스

    [Tooltip("재생에 쓸 AudioSource. 비우면 같은 오브젝트의 것을 사용.")]
    [SerializeField] private AudioSource source; // 재생용 오디오 소스

    [Header("기본 클립")]
    [Tooltip("카드 사용 시 재생.")]
    [SerializeField] private AudioClip cardUseClip; // 카드 사용 효과음
    [Tooltip("카드 드로우 시 재생.")]
    [SerializeField] private AudioClip drawClip; // 카드 드로우 효과음
    [Tooltip("각성 중 콤보가 완성(매칭)될 때마다 재생 — 표식이 뜨는 순간 소리.")]
    [SerializeField] private AudioClip comboCompleteClip; // 콤보 완성 효과음
    [Tooltip("카드/콤보 이펙트 재생 시 기본 소리(아래 effectName 매핑이 없을 때).")]
    [SerializeField] private AudioClip effectDefaultClip; // 이펙트 기본 효과음

    // 이펙트 이름과 전용 클립을 묶는 매핑 데이터
    [System.Serializable]
    public struct EffectClip
    {
        [Tooltip("이펙트 이름(예: Fire_ATK, Heal_EFF). CardEffectOverlay가 재생하는 이름과 일치.")]
        public string effectName; // 이펙트 이름
        public AudioClip clip; // 해당 이펙트 클립
    }

    [Header("이펙트별 소리 오버라이드 (effectName → clip)")]
    [Tooltip("특정 이펙트에만 다른 소리를 주고 싶을 때. 매칭 없으면 Effect Default Clip 사용.")]
    [SerializeField] private List<EffectClip> effectClips = new List<EffectClip>(); // 이펙트별 소리 오버라이드 목록

    [Header("볼륨")]
    [Range(0f, 1f)] [SerializeField] private float cardUseVolume = 1f; // 카드 사용 볼륨
    [Range(0f, 1f)] [SerializeField] private float drawVolume = 1f; // 드로우 볼륨
    [Range(0f, 1f)] [SerializeField] private float comboCompleteVolume = 1f; // 콤보 완성 볼륨
    [Range(0f, 1f)] [SerializeField] private float effectVolume = 1f; // 이펙트 볼륨

    // 싱글톤 등록 및 오디오 소스 초기화
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        if (source == null) source = GetComponent<AudioSource>();
        if (source != null) source.playOnAwake = false;
    }

    // 파괴 시 싱글톤 참조를 해제한다
    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // 카드 사용 효과음을 재생한다
    public void PlayCardUse() => Play(cardUseClip, cardUseVolume);

    // 카드 드로우 효과음을 재생한다
    public void PlayDraw() => Play(drawClip, drawVolume);

    // 콤보 완성 효과음을 재생한다(각성 중 콤보 매칭 시)
    public void PlayComboComplete() => Play(comboCompleteClip, comboCompleteVolume);

    // 이펙트 효과음을 재생한다(이름별 오버라이드 우선)
    public void PlayEffect(string effectName) => Play(ResolveEffectClip(effectName), effectVolume);

    // 이펙트 이름에 맞는 클립을 찾고 없으면 기본 클립을 반환한다
    AudioClip ResolveEffectClip(string effectName)
    {
        if (!string.IsNullOrEmpty(effectName))
        {
            for (int i = 0; i < effectClips.Count; i++)
            {
                if (effectClips[i].clip != null &&
                    string.Equals(effectClips[i].effectName, effectName, System.StringComparison.OrdinalIgnoreCase))
                    return effectClips[i].clip;
            }
        }
        return effectDefaultClip;
    }

    // 클립을 단발성으로 재생한다(클립/소스 없으면 무시)
    void Play(AudioClip clip, float volume)
    {
        if (clip == null || source == null) return;
        source.PlayOneShot(clip, Mathf.Clamp01(volume));
    }
}
