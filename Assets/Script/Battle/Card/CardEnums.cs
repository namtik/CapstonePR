namespace Battle.Card
{
    public enum CardElement
    {
        Fire,
        Water,
        Wind,
        Earth,
        Neutral,
        Fragment
    }

    public enum CardType
    {
        Attack,
        Skill,
        Power
    }

    /// <summary>카드 등급(Rarity). DB의 Normal/Rare/Epic.</summary>
    public enum CardRarity
    {
        Normal,
        Rare,
        Epic
    }

    /// <summary>카드 사용 시 드롭 타깃 구분</summary>
    public enum CardTarget
    {
        Enemy,
        Player,
        Self
    }
}
