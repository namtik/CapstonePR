using UnityEngine;

// 몬스터 속성(불/물/바람/땅)
public enum EnemyElement { None, Fire, Water, Wind, Earth }

// 몬스터 타입
public enum EnemyClass { Corrupted, Normal, Elite, Boss }

// 방해행동 종류
// 저주=0, 탈진=1, 흡수=2, 강화=3, 회복=4, 버리기=5
public enum DisruptionAction
{
    Curse = 0,   // 저주
    Exhaust = 1, // 탈진
    Absorb = 2,  // 흡수
    Enhance = 3, // 강화
    Recover = 4, // 회복
    Discard = 5, // 버리기
}

//  특수 능력. 몬스터별 스크립트 동작을 EnemyController가 트리거
public enum EnemySpecialAbility
{
    None,
    OnDeathCurseElement,     // 100000 오염된 불: 처치 시 무작위 속성에 5행동 저주
    OnDeathDiscardCards,     // 100001 오염된 물: 처치 시 카드 2장 버리기
    OnDeathResetAwaken,      // 100002 오염된 바람: 처치 시 각성 게이지 초기화
    OnDeathInsertExhaust,    // 100003 오염된 땅: 처치 시 탈진 카드 2장 삽입
    ReflectDamageOnHit,      // 100006 화웅: 피격 시 플레이어에게 1 피해
    DuplicateAtHalfHp,       // 100009 액괴: 체력 절반 도달 시 자신 복제(1회)
    EscalatingAttack,        // 100012 풍식귀: 공격 시 피해가 1씩 증가
    CharmCardsOnBattleStart, // 100016 구미호: 전투 시작 시 매혹 카드 7장 삽입
    CycleElementCurse,       // 100017 백족승: 속성 저주 후 공격마다 다른 속성으로 전환
    DoubleDamageAtHalfHp,    // 100018 무사: 체력 절반이 되면 피해 2배
}

[CreateAssetMenu(fileName = "EnemyData", menuName = "Enemy/EnemyData")]
public class EnemyData : ScriptableObject
{
    public string enemyName; // 적 이름
    public Sprite enemySprite; // 적 기본 스프라이트
    [Tooltip("피격 시 잠깐 바뀌는 이미지. 비우면 이미지 교체 없이 기존 피격 연출만 적용된다.")]
    public Sprite hitSprite; // 피격 시 표시 이미지
    [Tooltip("공격 모션 프레임들. 적이 공격할 때 순서대로 잠깐 재생한 뒤 평상시 이미지로 복귀.\n" +
             "1장이면 단순 교체, 여러 장이면 프레임 애니메이션. 비우면 EnemyView에 직접 지정한 프레임을 사용.")]
    public Sprite[] attackSprites; // 공격 모션 프레임(여러 장 가능)

    [Header("스탯")]
    [Tooltip("엑셀 'ID' 컬럼(100000~). 데이터 식별/추적용.")]
    public int id; // 몬스터 ID
    [Tooltip("엑셀 '속성' 컬럼. -는 None.")]
    public EnemyElement element = EnemyElement.None; // 속성
    [Tooltip("엑셀 '타입' 컬럼. 오염된 원소/일반/정예/보스.")]
    public EnemyClass enemyClass = EnemyClass.Normal; // 타입
    [Tooltip("엑셀 '특이사항' 컬럼의 특수 능력. 선택 시 EnemyController가 해당 동작을 트리거한다.")]
    public EnemySpecialAbility specialAbility = EnemySpecialAbility.None; // 특이사항 능력
    [Tooltip("엑셀 '특이사항' 컬럼 원문(참고/툴팁용 텍스트).")]
    [TextArea]
    public string Note; // 특이사항 원문

    public float maxHp = 1000f; // 최대 체력
    [Tooltip("행동 게이지 최대치(10~30). 다 차면 공격, 절반에서 방해행동. 0/미설정 시 기본 20.")]
    public int actionGaugeMax = 20; // 행동 게이지 최대치(10~30)

    [Header("공격 시퀀스")]
    [Tooltip("게이지가 가득 찰 때마다 순서대로 순환하는 공격 피해 값(엑셀 1차/2차/3차 공격, '-'는 제외).\n" +
             "비우면 EnemyController에 지정한 폴백 시퀀스를 사용.")]
    public int[] attackSequence; // 1차/2차/3차 공격 피해

    [Header("방해행동")]
    [Tooltip("이 몬스터가 사용할 수 있는 방해행동 목록. 적 AI/랜덤 선택이 이 목록으로 제한된다.\n" +
             "비우면 6종(저주/탈진/흡수/강화/회복/버리기) 전체를 허용.")]
    public DisruptionAction[] disruptionActions; // 방해행동 목록

    [Header("전투 배경")]
    [Tooltip("이 몬스터와 매칭되는 전투 배경 후보. 전투 진입 시 이 중 하나가 랜덤으로 선택된다. 비우면 라운드 타입 기본 배경을 사용.")]
    public Sprite[] backgroundSprites; // 이 몬스터 전용 전투 배경 후보

    // 방해행동 6종
    public bool[] BuildDisruptionMask()
    {
        var mask = new bool[6];
        if (disruptionActions == null || disruptionActions.Length == 0)
        {
            for (int i = 0; i < mask.Length; i++) mask[i] = true;
            return mask;
        }
        foreach (var a in disruptionActions)
        {
            int idx = (int)a;
            if (idx >= 0 && idx < mask.Length) mask[idx] = true;
        }
        return mask;
    }
}
