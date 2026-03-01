using UnityEngine;

[CreateAssetMenu(fileName = "EnemyData", menuName = "Enemy/EnemyData")]
public class EnemyData : ScriptableObject
{
    public string enemyName; // 몬스터 이름
    public Sprite enemySprite; // 몬스터 스프라이트

    public float maxHp = 1000f; // 최대 체력
    public float attackDamage= 10f; // 공격력
    public float gaugeSpeed=10f; // 행동 게이지 증가 속도
    
}
