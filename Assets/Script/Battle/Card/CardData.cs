using UnityEngine;

namespace Battle.Card
{
    /// <summary>
    /// 카드 정적 정의. 효과 자체는 CardEffectResolver가 id로 분기하여 실행한다.
    /// 카드 인스턴스(런타임)는 CardInstance를 사용한다.
    /// </summary>
    [System.Serializable]
    public class CardData
    {
        public int id;
        public string displayName;
        public CardElement element;
        public CardType type;
        public int gauge;
        public string description;

        public CardData(int id, string displayName, CardElement element, CardType type,
                        int gauge, string description)
        {
            this.id = id;
            this.displayName = displayName;
            this.element = element;
            this.type = type;
            this.gauge = gauge;
            this.description = description;
        }

        public bool IsFragment => element == CardElement.Neutral;
    }

    /// <summary>덱/패/더미를 순회하는 런타임 카드 인스턴스.</summary>
    public class CardInstance
    {
        public CardData data;
        /// <summary>이번 전투 한정 생성된 임시 카드(파편). true면 전투 종료 시 소실.</summary>
        public bool transient;
        /// <summary>저주된 카드(미사용 상태). 사용 시 플레이어가 피해를 본다.</summary>
        public bool cursed;

        public CardInstance(CardData data, bool transient = false)
        {
            this.data = data;
            this.transient = transient;
        }

        public int Id => data.id;
        public CardElement Element => data.element;
        public CardType Type => data.type;
        public int Gauge => data.gauge;
    }
}
