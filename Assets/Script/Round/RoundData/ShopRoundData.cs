using UnityEngine;

[CreateAssetMenu(menuName = "Round/ShopRound")]
public class ShopRoundData : RoundData
{
    //public List<CardData> CardPool;
    //public int itemCount = 3;

    public override IRoundHandler CreateHandler()
    {
        return new ShopRoundHandler(this);
    }
}
