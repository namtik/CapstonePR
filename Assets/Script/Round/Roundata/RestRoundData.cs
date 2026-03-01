using UnityEngine;

// 휴식
[CreateAssetMenu(menuName = "Round/RestRound")]
public class RestRoundData : RoundData
{
    public float healPercent = 0.3f; // 최대 회복량

    public override IRoundHandler CreateHandler()
    {
        return new RestRoundHandler(this);
    }
}