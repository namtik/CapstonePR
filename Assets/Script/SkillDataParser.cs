using System;
using System.Collections.Generic;
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
        public int draw;           // 드로우 카드 수
        public String combo;      // 콤보 커맨더
        public string effectName;   // 이펙트 프리팹 이름

        public string iconName;// CSV에서 읽어온 파일명
        public Sprite skillIcon;// 게임에서 사용될 스프라이트

        public string createCard1;   // 생성 카드 1
        public string createCard2;   // 생성 카드 2

        public string description;  // 스킬 설명
    }

    public static SkillDataParser Instance;

    // ID를 통해 스킬 데이터 탐색용 딕셔너리
    public Dictionary<int, SkillData> skillDic = new Dictionary<int, SkillData>();
    public List<SkillData> allSkills = new List<SkillData>();
    public SkillRewardUI SkillRewardUI; // 보상 UI 참조

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        LoadSkillData();
        SkillRewardUI.ShowRewardOptions();
    }

    void LoadSkillData()
    {
        // 경로 Assets/Resources/SkillDB.csv
        TextAsset csvData = Resources.Load<TextAsset>("SkillDB");

        if (csvData == null)
        {
            Debug.LogError("스킬DB.csv 파일을 찾을 수 없습니다!");
            return;
        }

        // 줄바꿈으로 데이터 쪼개기
        string[] lines = csvData.text.Split('\n');

        // 0번(타입), 1번(헤더) 줄은 건너뛰고 2번부터 데이터 파싱
        for (int i = 2; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i])) continue; // 빈 줄 무시

            string[] row = lines[i].Split(',');

            // 데이터 개수 체크
            if (row.Length < 10) continue;

            try
            {
                SkillData skill = new SkillData();

                skill.id = int.Parse(row[0]);           // ID
                skill.name = row[1];                    // Name
                skill.damage = float.Parse(row[2])/100;     // Damage
                skill.combo = row[3];                   // Combo
                skill.draw = int.Parse(row[4]);    // Draw
                skill.effectName = row[5];              // EffectName
                skill.iconName = row[6];                // SkillImg


                skill.createCard1 = row[7];             // 생성 1장
                skill.createCard2 = row[8];             // 생성 2장

                // 설명 (엑셀 줄바꿈 문자 제거)
                skill.description = row[9].Replace("\r", "");

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

    public List<SkillData> GetRandomSkills(int count)// 스킬 랜덤 추출 (중복 없이)
    {
        List<SkillData> result = new List<SkillData>();
        List<SkillData> tempPool = new List<SkillData>(allSkills);

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
