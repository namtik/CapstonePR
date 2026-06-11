using System.Collections.Generic;
using UnityEngine;
using Battle.Card;

namespace Battle
{
    // 콤보 스킬이 발동하는 효과 종류
    public enum ComboEffectType
    {
        Damage,    // 적에게 피해
        Burn,      // 적에게 화상 부여
        Guard,     // 플레이어 방어도
        Heal,      // 플레이어 회복
        Draw,      // 카드 드로우
    }

    // 보유 콤보 스킬 정의 (3장 속성 시퀀스 매칭 시 효과 발동, 각성 전용)
    [System.Serializable]
    public class ComboSkillDef
    {
        public string displayName = "콤보스킬";              // 콤보 표시 이름
        public CardElement slot1 = CardElement.Fire;         // 매칭 시퀀스 1번째 속성
        public CardElement slot2 = CardElement.Fire;         // 매칭 시퀀스 2번째 속성
        public CardElement slot3 = CardElement.Fire;         // 매칭 시퀀스 3번째 속성
        public ComboEffectType effect = ComboEffectType.Damage; // 레거시 단일 효과 종류
        public int amount = 10;                              // 레거시 단일 효과 수치

        [System.NonSerialized] public bool fromDatabase;     // DB 기반 콤보 여부
        [System.NonSerialized] public int refComboId;        // DB 콤보 참조 ID
        [System.NonSerialized] public string skillImg;       // 스킬 아이콘 이미지 경로
        [System.NonSerialized] public Sprite skillIcon;      // 로드된 스킬 아이콘
        [System.NonSerialized] public string descriptionKR;  // 한글 설명
        [System.NonSerialized] public List<ComboEffectData> dbEffects; // DB 다중 효과 목록
        // 입력 속성 시퀀스가 이 콤보와 매칭되는지 판정 — 슬롯 순서대로 입력해야 발동(순차)
        public bool Matches(IList<CardElement> input)
        {
            if (input == null || input.Count < 3) return false;
            return input[0] == slot1 && input[1] == slot2 && input[2] == slot3;
        }

        // 콤보 시퀀스를 짧은 한글 문자열로 표현
        public string ComboString()
        {
            return $"{Short(slot1)}-{Short(slot2)}-{Short(slot3)}";
        }

        // 속성을 짧은 한글 약자로 변환
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
