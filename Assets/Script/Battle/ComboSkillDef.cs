using System.Collections.Generic;
using UnityEngine;
using Battle.Card;

namespace Battle
{
    public enum ComboEffectType
    {
        Damage,    // 적에게 피해
        Burn,      // 적에게 화상 부여
        Guard,     // 플레이어 방어도
        Heal,      // 플레이어 회복
        Draw,      // 카드 드로우
    }

    /// <summary>
    /// 보유 콤보 스킬 정의. 3장 속성 시퀀스 매칭 → 효과 발동.
    /// 각성 동안만 활성화되며, 발동된 콤보는 쿨다운 동안 비활성.
    ///
    /// 두 가지 소스로 채워진다:
    ///  - 레거시(Inspector 수동): slot1/2/3 + effect + amount, 단일 효과, 정순서 매칭.
    ///  - DB(ComboSkillDatabase): fromDatabase=true, dbEffects로 데이터 드리븐 다중 효과 실행.
    ///    acceptedOrders에 같은 refComboId의 모든 속성 순서가 들어가 순서무관 매칭.
    /// </summary>
    [System.Serializable]
    public class ComboSkillDef
    {
        public string displayName = "콤보스킬";
        public CardElement slot1 = CardElement.Fire;
        public CardElement slot2 = CardElement.Fire;
        public CardElement slot3 = CardElement.Fire;
        public ComboEffectType effect = ComboEffectType.Damage;
        public int amount = 10;

        // ── DB 기반 런타임 필드 (Inspector 직렬화 대상 아님) ──
        [System.NonSerialized] public bool fromDatabase;
        [System.NonSerialized] public int refComboId;
        [System.NonSerialized] public string skillImg;
        [System.NonSerialized] public Sprite skillIcon;
        [System.NonSerialized] public string descriptionKR;
        [System.NonSerialized] public List<ComboEffectData> dbEffects;
        // 같은 refComboId를 공유하는 모든 슬롯 순서 키("FIRE,WIND,FIRE" 등). 순서무관 매칭용.
        [System.NonSerialized] public HashSet<string> acceptedOrders;

        public bool Matches(IList<CardElement> input)
        {
            if (input == null || input.Count < 3) return false;

            if (fromDatabase && acceptedOrders != null)
                return acceptedOrders.Contains(OrderKey(input[0], input[1], input[2]));

            return input[0] == slot1 && input[1] == slot2 && input[2] == slot3;
        }

        public static string OrderKey(CardElement a, CardElement b, CardElement c)
            => $"{a},{b},{c}";

        public string ComboString()
        {
            return $"{Short(slot1)}-{Short(slot2)}-{Short(slot3)}";
        }

        static string Short(CardElement e) => e switch
        {
            CardElement.Fire    => "불",
            CardElement.Water   => "물",
            CardElement.Wind    => "바람",
            CardElement.Earth   => "땅",
            CardElement.Neutral => "무",
            _ => "?"
        };
    }
}
