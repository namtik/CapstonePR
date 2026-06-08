using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전투 효과음(SFX) 재생기. 씬에 빈 오브젝트를 만들어 이 컴포넌트를 붙이고
/// 클립을 인스펙터에 연결한다(코드로 오브젝트를 생성하지 않음).
/// 코드 곳곳에서 <c>SfxManager.Instance?.PlayCardUse()</c> 등으로 호출한다(미배치 시 null-safe).
///
/// [씬 배치 방법]
/// 1) 빈 GameObject 생성(예: "SfxManager") → 이 스크립트 추가(AudioSource가 자동으로 함께 추가됨)
/// 2) 인스펙터에서 Card Use Clip / Draw Clip / Effect Default Clip 에 오디오 클립 연결
/// 3) (선택) 특정 이펙트만 다른 소리: Effect Clips 리스트에 effectName(예: Fire_ATK)+클립 추가
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class SfxManager : MonoBehaviour
{
    public static SfxManager Instance { get; private set; }

    [Tooltip("재생에 쓸 AudioSource. 비우면 같은 오브젝트의 것을 사용.")]
    [SerializeField] private AudioSource source;

    [Header("기본 클립")]
    [Tooltip("카드 사용 시 재생.")]
    [SerializeField] private AudioClip cardUseClip;
    [Tooltip("카드 드로우 시 재생.")]
    [SerializeField] private AudioClip drawClip;
    [Tooltip("카드/콤보 이펙트 재생 시 기본 소리(아래 effectName 매핑이 없을 때).")]
    [SerializeField] private AudioClip effectDefaultClip;

    [System.Serializable]
    public struct EffectClip
    {
        [Tooltip("이펙트 이름(예: Fire_ATK, Heal_EFF). CardEffectOverlay가 재생하는 이름과 일치.")]
        public string effectName;
        public AudioClip clip;
    }

    [Header("이펙트별 소리 오버라이드 (effectName → clip)")]
    [Tooltip("특정 이펙트에만 다른 소리를 주고 싶을 때. 매칭 없으면 Effect Default Clip 사용.")]
    [SerializeField] private List<EffectClip> effectClips = new List<EffectClip>();

    [Header("볼륨")]
    [Range(0f, 1f)] [SerializeField] private float cardUseVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float drawVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float effectVolume = 1f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        if (source == null) source = GetComponent<AudioSource>();
        if (source != null) source.playOnAwake = false;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>카드 사용 효과음.</summary>
    public void PlayCardUse() => Play(cardUseClip, cardUseVolume);

    /// <summary>카드 드로우 효과음.</summary>
    public void PlayDraw() => Play(drawClip, drawVolume);

    /// <summary>이펙트 효과음(effectName별 오버라이드 우선, 없으면 기본 클립).</summary>
    public void PlayEffect(string effectName) => Play(ResolveEffectClip(effectName), effectVolume);

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

    void Play(AudioClip clip, float volume)
    {
        if (clip == null || source == null) return;
        source.PlayOneShot(clip, Mathf.Clamp01(volume));
    }
}
