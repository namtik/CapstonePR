using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using Random = UnityEngine.Random;


// CSV(SkillDB)에서 스킬 데이터를 로드·보관하는 싱글톤
public class SkillDataParser : MonoBehaviour
{
    // 스킬 한 개의 데이터
    [System.Serializable]
    public class SkillData
    {
        public int id;              // 스킬 고유 ID
        public string name;         // 스킬 이름
        public float damage;        // 데미지
        public string combo;        // 콤보 커맨드
        public string effectName;   // 이펙트 프리팹 이름

        public string iconName;     // CSV에서 읽어온 파일명
        public Sprite skillIcon;    // 게임에서 사용될 스프라이트

        public string statusType;   // 상태이상 타입
        public int statusAmount;    // 상태이상 수치
        public int guardAmount;     // 방어도 수치

        public string description;  // 스킬 설명
    }

    public static SkillDataParser Instance; // 전역 싱글톤 인스턴스
    public SkillRewardUI SkillRewardUI; // 보상 UI 참조

    public Dictionary<int, SkillData> skillDic = new Dictionary<int, SkillData>(); // ID→스킬 데이터 맵
    public List<SkillData> allSkills = new List<SkillData>(); // 전체 스킬 목록

    // 싱글톤 등록 후 스킬 데이터를 로드한다
    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        LoadSkillData();
        Debug.Log(Application.persistentDataPath);
    }

    // SkillDB CSV를 파싱해 스킬 데이터를 채운다
    void LoadSkillData()
    {
        Dictionary<string, Sprite> skillIconLookup = BuildSkillIconLookup();

        TextAsset csvData = Resources.Load<TextAsset>("SkillDB");

        if (csvData == null)
        {
            Debug.LogError("SkillDB.csv 파일을 찾을 수 없습니다!");
            return;
        }

        string[] lines = csvData.text.Split('\n');

        for (int i = 2; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue;

            string[] row = Regex.Split(lines[i].Trim(), ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");

            if (row.Length < 10) continue;

            try
            {
                SkillData skill = new SkillData();

                skill.id = int.Parse(row[0]);
                skill.name = row[1];
                skill.damage = float.Parse(row[2]) / 100f;
                skill.combo = row[3];
                skill.effectName = row[4];
                skill.iconName = row[5];
                skill.statusType = row[6];
                skill.statusAmount = int.Parse(row[7]);

                skill.guardAmount = int.Parse(row[8]);

                skill.description = row[9].Replace("\r", "").Replace("\"", "");

                skill.skillIcon = ResolveSkillIcon(skill.iconName, skillIconLookup);
                allSkills.Add(skill);

                if (!skillDic.ContainsKey(skill.id))
                {
                    skillDic.Add(skill.id, skill);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"{i}번째 줄 파싱 에러: {e.Message}");
            }
        }

        Debug.Log($"총 {skillDic.Count}개의 스킬 로드 완료!");
    }

    // 스킬 아이콘 스프라이트를 다양한 별칭으로 매핑하는 사전을 만든다
    Dictionary<string, Sprite> BuildSkillIconLookup()
    {
        var lookup = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        Sprite[] sprites = Resources.LoadAll<Sprite>("SkillIcons");
        foreach (Sprite sprite in sprites)
        {
            if (sprite == null) continue;
            AddIconAlias(lookup, sprite.name, sprite);
            AddIconAlias(lookup, sprite.name.Replace(" ", string.Empty), sprite);
            AddIconAlias(lookup, sprite.name.Replace("_", string.Empty), sprite);
            AddIconAlias(lookup, sprite.name.Replace("_", string.Empty).Replace(" ", string.Empty), sprite);
        }
        return lookup;
    }

    // 중복이 없을 때만 별칭→스프라이트 항목을 사전에 추가한다
    static void AddIconAlias(Dictionary<string, Sprite> lookup, string alias, Sprite sprite)
    {
        if (string.IsNullOrWhiteSpace(alias) || sprite == null) return;
        if (!lookup.ContainsKey(alias))
            lookup.Add(alias, sprite);
    }

    // 아이콘 이름으로 스프라이트를 단계적으로 탐색해 반환한다
    Sprite ResolveSkillIcon(string iconName, Dictionary<string, Sprite> lookup)
    {
        if (string.IsNullOrWhiteSpace(iconName))
            return null;

        string key = iconName.Trim();

        Sprite direct = Resources.Load<Sprite>($"SkillIcons/{key}");
        if (direct != null) return direct;

        Sprite withSuffix = Resources.Load<Sprite>($"SkillIcons/{key} 1");
        if (withSuffix != null) return withSuffix;

        if (lookup.TryGetValue(key, out Sprite byKey))
            return byKey;

        string compact = key.Replace(" ", string.Empty);
        if (lookup.TryGetValue(compact, out Sprite byCompact))
            return byCompact;

        string noUnderscore = key.Replace("_", string.Empty);
        if (lookup.TryGetValue(noUnderscore, out Sprite byNoUnderscore))
            return byNoUnderscore;

        string compactNoUnderscore = compact.Replace("_", string.Empty);
        if (lookup.TryGetValue(compactNoUnderscore, out Sprite byCompactNoUnderscore))
            return byCompactNoUnderscore;

        return null;
    }

    // ID로 스킬 데이터를 조회한다
    public SkillData GetSkill(int id)
    {
        if (skillDic.ContainsKey(id)) return skillDic[id];
        return null;
    }

    // 제외 ID를 뺀 풀에서 중복 없이 스킬을 무작위로 뽑는다
    public List<SkillData> GetRandomSkills(int count, HashSet<int> excludeIds)
    {
        List<SkillData> result = new List<SkillData>();
        List<SkillData> tempPool = new List<SkillData>();
        foreach (SkillData skill in allSkills)
        {
            if (!excludeIds.Contains(skill.id))
            {
                tempPool.Add(skill);
            }
        }

        for (int i = 0; i < count; i++)
        {
            if (tempPool.Count == 0) break;
            int randIndex = Random.Range(0, tempPool.Count);
            result.Add(tempPool[randIndex]);
            tempPool.RemoveAt(randIndex);
        }
        return result;
    }
}
