using System;
using UnityEngine;

namespace Battle.Card
{
    /// <summary>
    /// 카드 정적 정의 (Resources/CardDB/Cards.json에서 로드).
    /// 효과는 CardEffectResolver가 id로 분기하여 실행.
    /// 카드 인스턴스(런타임)는 CardInstance를 사용.
    /// </summary>
    [Serializable]
    public class CardData
    {
        public int id;
        public string displayName;
        public CardElement element;
        public CardType type;
        public int gauge;
        public string description;

        /// <summary>BASIC / EXHAUST / ONE_TIME / POWER / FRAGMENT / NO_COMBO_SLOT / TEMPORARY_ON_CREATE 등.</summary>
        public string[] tags;

        /// <summary>카드 사용 시 콤보 슬롯에 입력되는지(파편/무속성은 false).</summary>
        public bool comboSlot;

        /// <summary>아트 바인딩용(EffectName 컬럼).</summary>
        public string effectName;

        /// <summary>아트 바인딩용(SkillImg 컬럼).</summary>
        public string skillImg;

        public CardData() { }

        public CardData(int id, string displayName, CardElement element, CardType type,
                        int gauge, string description,
                        string[] tags = null, bool comboSlot = true,
                        string effectName = "", string skillImg = "")
        {
            this.id = id;
            this.displayName = displayName;
            this.element = element;
            this.type = type;
            this.gauge = gauge;
            this.description = description;
            this.tags = tags ?? Array.Empty<string>();
            this.comboSlot = comboSlot;
            this.effectName = effectName;
            this.skillImg = skillImg;
        }

        public bool IsFragment => element == CardElement.Fragment;
        public bool IsNeutral => element == CardElement.Neutral;
        /// <summary>콤보 슬롯에 들어가지 않는 카드(파편 + 무속성 + ComboSlot=false).</summary>
        public bool BypassComboSlot => !comboSlot;

        public bool HasTag(string tag)
        {
            if (tags == null || string.IsNullOrEmpty(tag)) return false;
            for (int i = 0; i < tags.Length; i++)
                if (string.Equals(tags[i], tag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }

    /// <summary>덱/패/더미를 순회하는 런타임 카드 인스턴스.</summary>
    public class CardInstance
    {
        public CardData data;
        /// <summary>이번 전투 한정 생성된 임시 카드(파편). true면 전투 종료 시 소실.</summary>
        public bool transient;
        /// <summary>저주된 카드(미사용 상태). 사용 시 플레이어가 피해를 본다.</summary>
        public bool cursed;

        /// <summary>이 카드가 패로 들어온 이후 진행된 게이지 수(땅6/땅16 등의 공식 GAUGE_SINCE_DRAWN).</summary>
        public int gaugeSinceDrawn;

        /// <summary>드로우 직후 아직 어떤 카드도 사용되지 않은 상태(바람11/310 USED_IMMEDIATELY_AFTER_DRAW용).</summary>
        public bool justDrawn;

        public CardInstance(CardData data, bool transient = false)
        {
            this.data = data;
            this.transient = transient;
            this.gaugeSinceDrawn = 0;
            this.justDrawn = false;
        }

        public int Id => data.id;
        public CardElement Element => data.element;
        public CardType Type => data.type;
        public int Gauge => data.gauge;
    }
}
