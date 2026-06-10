using UnityEngine;

[CreateAssetMenu(menuName = "Round/BossRound")]
public class BossRoundData : RoundData
{
    public EnemyData bossEnemy; // 보스 적 데이터
    public int columnIndex; // 진행 열 인덱스(난이도 스케일용)

    // 보스 라운드 핸들러를 생성한다.
    public override IRoundHandler CreateHandler()
    {
        return new BossRoundHandler(this);
    }
}