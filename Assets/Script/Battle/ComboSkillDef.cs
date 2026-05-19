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
    /// 피버 동안만 활성화되며, 발동된 콤보는 그 피버 사이클 동안 비활성.
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

        public bool Matches(IList<CardElement> input)
        {
            if (input == null || input.Count < 3) return false;
            return input[0] == slot1 && input[1] == slot2 && input[2] == slot3;
        }

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
