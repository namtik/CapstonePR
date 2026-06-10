using System.Collections.Generic;
using UnityEngine;

// 콤보 스킬 보유 정보만 관리하는 경량 저장소
public class ComboSkillProgression : MonoBehaviour
{
    public static ComboSkillProgression Instance { get; private set; } // 싱글턴 인스턴스

    private readonly List<SkillDataParser.SkillData> learnedSkills = new List<SkillDataParser.SkillData>(); // 습득한 스킬 목록
    private readonly HashSet<int> learnedSkillIds = new HashSet<int>(); // 습득한 스킬 ID 집합

    // 인스턴스 보장(없으면 탐색 또는 생성)
    public static ComboSkillProgression EnsureExists()
    {
        if (Instance != null) return Instance;

        ComboSkillProgression existing = FindFirstObjectByType<ComboSkillProgression>(FindObjectsInactive.Include);
        if (existing != null)
        {
            Instance = existing;
            if (!existing.gameObject.activeSelf)
                existing.gameObject.SetActive(true);
            return existing;
        }

        GameObject go = new GameObject("ComboSkillProgression");
        return go.AddComponent<ComboSkillProgression>();
    }

    // 싱글턴 초기화
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            return;
        }

        if (Instance != this)
            Destroy(gameObject);
    }

    // 습득한 스킬 ID 집합 복사본 반환
    public HashSet<int> GetLearnedSkillIds()
    {
        return new HashSet<int>(learnedSkillIds);
    }

    // 스킬 습득(중복 시 false)
    public bool LearnSkill(SkillDataParser.SkillData skill)
    {
        if (skill == null) return false;
        if (!learnedSkillIds.Add(skill.id)) return false;

        learnedSkills.Add(skill);
        Debug.Log($"[ComboSkillProgression] 스킬 습득: {skill.name} ({skill.id})");
        return true;
    }

    // 습득한 스킬 개수 반환
    public int LearnedSkillCount()
    {
        return learnedSkills.Count;
    }
}
