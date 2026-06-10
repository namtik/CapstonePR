using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Round/CombatRound")]
public class CombatRoundData : RoundData
{
    public List<EnemyData> enemies;
    public int columnIndex;// 컴럼별 배율

    public override IRoundHandler CreateHandler()
    {
        return new CombatRoundHandler(this);
    }
}