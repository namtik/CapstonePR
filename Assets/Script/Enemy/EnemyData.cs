using UnityEngine;

[CreateAssetMenu(fileName = "EnemyData", menuName = "Scriptable Objects/EnemyData")]
public class EnemyData : ScriptableObject
{
    public string enemyName; // 몬스터 이름
    public float maxHp; // 최대 체력
    public float attackDamage; // 공격력
    public float gaugeSpeed; // 행동 게이지 증가 속도
    public Sprite enemySprite; // 몬스터 스프라이트
}
