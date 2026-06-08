using UnityEngine;

[CreateAssetMenu(fileName = "EnemyData", menuName = "Enemy/EnemyData")]
public class EnemyData : ScriptableObject
{
    public string enemyName; // 적 이름
    public Sprite enemySprite; // 적 스프라이트

    public float maxHp = 1000f; // 최대 체력
    public float attackDamage= 10f; // 공격력
    public float gaugeSpeed=10f; // 행동 게이지 증가 속도
    public int baseAttackCount = 3;
    [Tooltip("기획서 0.6v: 행동 게이지 최대치(10~30). 다 차면 공격, 절반에서 방해행동. 0/미설정 시 기본 20.")]
    public int actionGaugeMax = 20; // 10~30

    [Header("전투 배경 (이 몬스터 전용)")]
    [Tooltip("이 몬스터와 매칭되는 전투 배경 후보. 전투 진입 시 이 중 하나가 랜덤으로 선택된다. 비우면 라운드 타입 기본 배경을 사용.")]
    public Sprite[] backgroundSprites;
}
