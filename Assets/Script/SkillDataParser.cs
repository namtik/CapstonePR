using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using Random = UnityEngine.Random;


public class SkillDataParser : MonoBehaviour
{
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
        public int guardAmount;

        public string description;  // 스킬 설명
    }

    public static SkillDataParser Instance;
    public SkillRewardUI SkillRewardUI; // 보상 UI 참조

    // ID를 통해 스킬 데이터 탐색용 딕셔너리
    public Dictionary<int, SkillData> skillDic = new Dictionary<int, SkillData>();
    public List<SkillData> allSkills = new List<SkillData>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        LoadSkillData();
        Debug.Log(Application.persistentDataPath);
    }

    void LoadSkillData()
    {
        // 로드할 파일명 "SkillDB"
        TextAsset csvData = Resources.Load<TextAsset>("SkillDB");

        if (csvData == null)
        {
            Debug.LogError("SkillDB.csv 파일을 찾을 수 없습니다!");
            return;
        }

        // 줄바꿈으로 데이터 쪼개기
        string[] lines = csvData.text.Split('\n');

        // 0번(타입), 1번(헤더) 줄은 건너뛰고 2번부터 데이터 파싱
        for (int i = 2; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue; // 빈 줄 무시

            // 따옴표("") 안의 쉼표는 무시하고 데이터를 분리
            string[] row = Regex.Split(lines[i].Trim(), ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)");

            // 데이터 개수 체크 (총 10개 컬럼)
            if (row.Length < 10) continue;

            try
            {
                SkillData skill = new SkillData();

                skill.id = int.Parse(row[0]);               // ID
                skill.name = row[1];                        // Name
                skill.damage = float.Parse(row[2]) / 100f;  // Damage
                skill.combo = row[3];                       // Combo
                skill.effectName = row[4];                  // EffectName
                skill.iconName = row[5];                    // SkillImg
                skill.statusType = row[6];                  // StatusType
                skill.statusAmount = int.Parse(row[7]);     // StatusAmount

                skill.guardAmount = int.Parse(row[8]);      //GuardAmount

                // 엑셀 줄바꿈 문자 및 양끝 따옴표 제거
                skill.description = row[9].Replace("\r", "").Replace("\"", "");

                skill.skillIcon = Resources.Load<Sprite>($"SkillIcons/{skill.iconName}");
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

    public SkillData GetSkill(int id)
    {
        if (skillDic.ContainsKey(id)) return skillDic[id];
        return null;
    }

    public List<SkillData> GetRandomSkills(int count, HashSet<int> excludeIds) // 스킬 랜덤 추출 (중복 없이)
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
            tempPool.RemoveAt(randIndex); // 중복 뽑기 방지
        }
        return result;
    }
}
