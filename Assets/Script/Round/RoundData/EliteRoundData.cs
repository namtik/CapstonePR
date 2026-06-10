using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Round/EliteRound")]
public class EliteRoundData : RoundData
{
    public List<EnemyData> enemies; // 등장 적 목록
    public int columnIndex; // 진행 열 인덱스(난이도 스케일용)

    // 정예 전투 라운드 핸들러를 생성한다.
    public override IRoundHandler CreateHandler()
    {
        return new EliteRoundHandler(this);
    }
}