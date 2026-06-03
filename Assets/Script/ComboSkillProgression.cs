using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 신규 전투 시스템에서 콤보 스킬 "보유 정보"만 관리하는 경량 저장소.
/// 전투 발동/입력 처리 로직은 포함하지 않는다.
/// </summary>
public class ComboSkillProgression : MonoBehaviour
{
    public static ComboSkillProgression Instance { get; private set; }

    private readonly List<SkillDataParser.SkillData> learnedSkills = new List<SkillDataParser.SkillData>();
    private readonly HashSet<int> learnedSkillIds = new HashSet<int>();

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

    public HashSet<int> GetLearnedSkillIds()
    {
        return new HashSet<int>(learnedSkillIds);
    }

    public bool LearnSkill(SkillDataParser.SkillData skill)
    {
        if (skill == null) return false;
        if (!learnedSkillIds.Add(skill.id)) return false;

        learnedSkills.Add(skill);
        Debug.Log($"[ComboSkillProgression] 스킬 습득: {skill.name} ({skill.id})");
        return true;
    }

    public int LearnedSkillCount()
    {
        return learnedSkills.Count;
    }
}
