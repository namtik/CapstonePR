using System;
using UnityEngine;

namespace Battle.Card
{
    // 카드 정적 정의(JSON에서 로드되는 데이터 모델)
    [Serializable]
    public class CardData
    {
        public int id;                  // 카드 고유 ID
        public string displayName;      // 표시 이름
        public CardElement element;     // 카드 속성
        public CardType type;           // 카드 유형
        public int gauge;               // 게이지 코스트
        public string description;      // 설명 문구

        public CardRarity rarity;       // 카드 등급

        public string[] tags;           // 동작 태그 목록

        public bool comboSlot;          // 콤보 슬롯 입력 여부

        public string effectName;       // 아트 바인딩용 효과명

        public string skillImg;         // 아트 바인딩용 스킬 이미지명

        // 기본 생성자
        public CardData() { }

        // 모든 필드를 받는 생성자
        public CardData(int id, string displayName, CardElement element, CardType type,
                        int gauge, string description,
                        string[] tags = null, bool comboSlot = true,
                        string effectName = "", string skillImg = "",
                        CardRarity rarity = CardRarity.Normal)
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
            this.rarity = rarity;
        }

        // 파편 카드 여부
        public bool IsFragment => element == CardElement.Fragment;
        // 무속성 카드 여부
        public bool IsNeutral => element == CardElement.Neutral;
        // 콤보 슬롯을 건너뛰는 카드 여부
        public bool BypassComboSlot => !comboSlot;

        // 지정 태그 보유 여부 확인
        public bool HasTag(string tag)
        {
            if (tags == null || string.IsNullOrEmpty(tag)) return false;
            for (int i = 0; i < tags.Length; i++)
                if (string.Equals(tags[i], tag, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }

    // 덱/패/더미를 순회하는 런타임 카드 인스턴스
    public class CardInstance
    {
        public CardData data;          // 원본 카드 데이터
        public bool transient;         // 이번 전투 한정 임시 카드 여부
        public bool cursed;            // 저주 여부

        public int gaugeSinceDrawn;    // 패에 들어온 뒤 진행된 게이지 수

        public bool justDrawn;         // 드로우 직후 미사용 상태 여부

        public int selfUseCount;       // 이 인스턴스 누적 사용 횟수

        // 카드 데이터로 인스턴스 생성
        public CardInstance(CardData data, bool transient = false)
        {
            this.data = data;
            this.transient = transient;
            this.gaugeSinceDrawn = 0;
            this.justDrawn = false;
        }

        // 카드 ID
        public int Id => data.id;
        // 카드 속성
        public CardElement Element => data.element;
        // 카드 유형
        public CardType Type => data.type;
        // 게이지 코스트
        public int Gauge => data.gauge;
    }
}
