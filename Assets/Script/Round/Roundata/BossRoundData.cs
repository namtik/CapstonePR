using UnityEngine;

[CreateAssetMenu(menuName = "Round/BossRound")]
public class BossRoundData : RoundData
{
    public EnemyData bossEnemy;
    public int columnIndex;// 컴럼별 배율

    public override IRoundHandler CreateHandler()
    {
        return new BossRoundHandler(this);
    }
}