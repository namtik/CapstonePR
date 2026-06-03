using UnityEngine;

[CreateAssetMenu(fileName = "EnemyData", menuName = "Enemy/EnemyData")]
public class EnemyData : ScriptableObject
{
    public string enemyName; // ���� �̸�
    public Sprite enemySprite; // ���� ��������Ʈ

    public float maxHp = 1000f; // �ִ� ü��
    public float attackDamage= 10f; // ���ݷ�
    public float gaugeSpeed=10f; // �ൿ ������ ���� �ӵ�
    public int baseAttackCount = 3;
    [Tooltip("기획서 0.6v: 행동 게이지 최대치(10~30). 다 차면 공격, 절반에서 방해행동. 0/미설정 시 기본 20.")]
    public int actionGaugeMax = 20; // 10~30
}
