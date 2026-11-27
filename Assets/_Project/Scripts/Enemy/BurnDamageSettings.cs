using UnityEngine;

public sealed class BurnDamageSettings : ScriptableObject
{
    [SerializeField, Range(0.1f, 4f), InspectorName("재생 속도 (배율)")]
    [Tooltip("1 = 기본 속도, 0.5 = 절반 속도, 2 = 두 배 속도. 불꽃 애니메이션에 적용됩니다.")]
    private float playbackSpeed = 1f;

    [SerializeField, InspectorName("화상 효과음")]
    [Tooltip("Burn Damage Trigger로 재생할 때 사용할 클립입니다.")]
    private AudioClip hitSound;

    public float PlaybackSpeed => Mathf.Clamp(playbackSpeed, 0.1f, 4f);
    public AudioClip HitSound => hitSound;

    internal static BurnDamageSettings Load() =>
        Resources.Load<BurnDamageSettings>("CardEffects/BurnDamageSettings");
}
