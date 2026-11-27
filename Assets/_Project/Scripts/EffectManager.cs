using System.Collections.Generic;
using UnityEngine;


public class EffectManager : MonoBehaviour
{
    public static EffectManager Instance; // 싱글턴 인스턴스
    private Dictionary<string, StatusEffectBase> effectDictionary; // 효과 타입→효과 객체 매핑

    // 싱글턴 설정 및 효과 사전 초기화
    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        effectDictionary = new Dictionary<string, StatusEffectBase>()
        {
            { "burn", new BurnEffect() },
            { "wet", new WetEffect() },
            { "freeze", new FreezeEffect() },
            { "detonate", new DetonateEffect() },
            { "launcher", new LauncherEffect() },
            { "fortify", new FortifyEffect() },
            { "charge", new ChargeEffect() },
            { "spend", new SpendEffect() }
        };
    }

    // 상태 타입에 맞는 효과를 시전
    public void ApplySkillEffect(string statusType, int amount, IBattleUnit caster, IBattleUnit target)
    {
        if (string.IsNullOrEmpty(statusType) || statusType == "0" || statusType.ToLower() == "none") return;

        if (effectDictionary.ContainsKey(statusType))
        {
            effectDictionary[statusType].Execute(caster, target, amount);
        }
        else
        {
            Debug.LogWarning($"[EffectManager] 정의되지 않은 특수 효과 타입입니다: {statusType}");
        }
    }
}
