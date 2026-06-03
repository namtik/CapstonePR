using System.Collections.Generic;

/// <summary>
/// 콤보 스킬 보유 데이터 접근 통합 레이어.
/// - 레거시 모드: ComboSystem 사용
/// - 신규 전투 모드: ComboSkillProgression 사용
/// </summary>
public static class ComboSkillRepository
{
    public static HashSet<int> GetLearnedSkillIds()
    {
        if (ComboSystem.Instance != null)
            return ComboSystem.Instance.GetLearnedSkillIds();

        return ComboSkillProgression.EnsureExists().GetLearnedSkillIds();
    }

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
