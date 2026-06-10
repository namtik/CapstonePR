using UnityEngine;

// �޽�
[CreateAssetMenu(menuName = "Round/RestRound")]
public class RestRoundData : RoundData
{
    public float healPercent = 0.2f; // 최대 체력 회복 비율 (기획서 0.6v: 명상 20%)

    public override IRoundHandler CreateHandler()
    {
        return new RestRoundHandler(this);
    }
}