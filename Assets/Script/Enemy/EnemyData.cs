using UnityEngine;

[CreateAssetMenu(fileName = "EnemyData", menuName = "Enemy/EnemyData")]
public class EnemyData : ScriptableObject
{
    public string enemyName; // 적 이름
    public Sprite enemySprite; // 적 스프라이트
    [Tooltip("피격 시 잠깐 바뀌는 이미지. 비우면 이미지 교체 없이 기존 피격 연출만 적용된다.")]
    public Sprite hitSprite; // 피격 당한 이미지
    [Tooltip("공격 모션 프레임들. 적이 공격할 때 순서대로 잠깐 재생한 뒤 평상시 이미지로 복귀.\n" +
             "1장이면 단순 교체, 여러 장이면 프레임 애니메이션. 비우면 EnemyView에 직접 지정한 프레임을 사용.")]
    public Sprite[] attackSprites; // 공격 모션 프레임(여러 장 가능)

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
