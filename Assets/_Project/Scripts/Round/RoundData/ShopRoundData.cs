using UnityEngine;

[CreateAssetMenu(menuName = "Round/ShopRound")]
public class ShopRoundData : RoundData
{

    // 상점 라운드 핸들러를 생성한다.
    public override IRoundHandler CreateHandler()
    {
        return new ShopRoundHandler(this);
    }
}
