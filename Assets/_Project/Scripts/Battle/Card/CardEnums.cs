namespace Battle.Card
{
    // 카드 속성 종류
    public enum CardElement
    {
        Fire,     // 불 속성
        Water,    // 물 속성
        Wind,     // 바람 속성
        Earth,    // 땅 속성
        Neutral,  // 무속성
        Fragment  // 파편
    }

    // 카드 유형 종류
    public enum CardType
    {
        Attack,  // 공격
        Skill,   // 스킬
        Power    // 파워
    }

    // 카드 등급 종류
    public enum CardRarity
    {
        Normal,  // 일반
        Rare,    // 희귀
        Epic     // 영웅
    }

    // 카드 사용 시 드롭 대상 구분
    public enum CardTarget
    {
        Enemy,   // 적
        Player,  // 플레이어
        Self     // 자기 자신
    }
}
