using UnityEngine;
using System.Collections.Generic;

// 정예
[CreateAssetMenu(menuName = "Round/EliteRound")]
public class EliteRoundData : RoundData
{
    public List<EnemyData> enemies;
    public int columnIndex;// 컴럼별 배율

    public override IRoundHandler CreateHandler()
    {
        return new EliteRoundHandler(this);
    }
}