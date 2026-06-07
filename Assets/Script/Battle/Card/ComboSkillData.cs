using System;
using System.Collections.Generic;

namespace Battle.Card
{
    /// <summary>
    /// ComboSkills.json 한 줄 = 콤보 1개(특정 슬롯 순서).
    /// 같은 속성 조합의 다른 순서는 별도 ComboID를 가지지만 refComboId로 효과를 공유한다.
    /// (canonical: 1000~1019, alias: 1020~1063)
    /// </summary>
    [Serializable]
    public class ComboSkillData
    {
        public int id;            // ComboID (1000~1063)
        public string slot1;      // FIRE/WATER/WIND/EARTH
        public string slot2;
        public string slot3;
        public int cooldown;      // 재사용까지 필요한 각성 입력 횟수
        public string comboName;  // 표시 이름 (현재 미정 가능)
        public string skillImg;   // 카드와 동일한 규약: Resources/ComboSkillIcons/{skillImg}
        public string description;
        public int refComboId;    // 효과 정의 참조 ID (1000~1019)
    }

    [Serializable]
    public class ComboSkillDataList
    {
        public List<ComboSkillData> combos = new List<ComboSkillData>();
    }

    /// <summary>
    /// ComboEffects.json 한 줄 = 한 효과 블록. refComboId 기준으로 EffectIndex 순서대로 실행.
    /// 카드 효과(CardEffectData)와 동일한 데이터 드리븐 스키마.
    /// </summary>
    [Serializable]
    public class ComboEffectData
    {
        public int refComboId;
        public int index;
        public string when;
        public string ifCond;
        public string doAction;
        public string target;
        public int amount;
        public string formula;
        public int hits;
        public string hitFormula;
        public string status;
        public string cardFilter;
        public string fromZone;
        public string toZone;
        public string select;
        public string repeat;
        public string extra;
        public string runtimeKey;
    }

    [Serializable]
    public class ComboEffectDataList
    {
        public List<ComboEffectData> effects = new List<ComboEffectData>();
    }
}
