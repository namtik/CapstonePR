using System.Collections.Generic;

// 콤보 스킬 보유 데이터 접근 통합 레이어(레거시/신규 모드 분기)
public static class ComboSkillRepository
{
    // 모드에 맞는 습득 스킬 ID 집합 반환
    public static HashSet<int> GetLearnedSkillIds()
    {
        if (ComboSystem.Instance != null)
            return ComboSystem.Instance.GetLearnedSkillIds();

        return ComboSkillProgression.EnsureExists().GetLearnedSkillIds();
    }

    // 모드에 맞게 스킬 습득(중복 시 false)
    public static bool LearnSkill(SkillDataParser.SkillData skill)
    {
        if (skill == null) return false;

        if (ComboSystem.Instance != null)
        {
            HashSet<int> ids = ComboSystem.Instance.GetLearnedSkillIds();
            if (ids.Contains(skill.id)) return false;

            ComboSystem.Instance.LearnSkill(skill);
            return true;
        }

        return ComboSkillProgression.EnsureExists().LearnSkill(skill);
    }
}
