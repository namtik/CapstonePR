using UnityEngine;

[CreateAssetMenu(menuName = "Round/RestRound")]
public class RestRoundData : RoundData
{
    public float healPercent = 0.2f; // 최대 체력 회복 비율(명상 20%)

    // 휴식 라운드 핸들러를 생성한다.
    public override IRoundHandler CreateHandler()
    {
        return new RestRoundHandler(this);
    }
}