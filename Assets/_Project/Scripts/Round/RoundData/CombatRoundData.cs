using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Round/CombatRound")]
public class CombatRoundData : RoundData
{
    public List<EnemyData> enemies; // 등장 적 목록
    public int columnIndex; // 진행 열 인덱스(난이도 스케일용)

    // 일반 전투 라운드 핸들러를 생성한다.
    public override IRoundHandler CreateHandler()
    {
        return new CombatRoundHandler(this);
    }
}