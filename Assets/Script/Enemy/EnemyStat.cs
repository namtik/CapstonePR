using System;
using System.Collections.Generic;
using UnityEngine;

public class EnemyStat : MonoBehaviour
{
    public float maxHp; // 최대 체력
    public float currentHp; // 현재 체력
    public float attackDamage; // 공격력
    public float gaugeSpeed; // 행동 게이지 증가 속도
    public bool IsAlive => currentHp > 0; // 생존 여부

    public float guard = 0f; // 방어도
    public Dictionary<string, int> statusEffects = new Dictionary<string, int>(); // 상태이상 스택 맵

    private int plannedAttackCount; // 계획된 공격 횟수
    private int currentAttackCount; // 현재 남은 공격 횟수
    private bool hasDied = false; // 사망 처리 완료 여부

    public const int GAUGE_MAX_STEPS = 20; // 행동 게이지 기본/폴백 최대치
    private int gaugeMaxSteps = GAUGE_MAX_STEPS; // 적별 행동 게이지 최대치(10~30)
    private int gaugeStep = 0; // 현재 게이지 단계
    private bool midPatternTriggered = false; // 이번 사이클 방해행동 발동 여부
    public int GaugeStep => gaugeStep; // 현재 게이지 단계 조회

    private EnemyData enemyData; // 적 데이터 원본
    public EnemyData Data => enemyData; // 적 데이터 원본 조회(공격 시퀀스/방해행동 등 조회용)
    private EnemyController controller; // 적 컨트롤러 참조
    private int columnIndex; // 배치 열 인덱스
    private NodeType nodeType; // 노드 타입(일반/정예/보스)
    private DifficultyConfig config; // 난이도 설정

    public NodeType NodeType => nodeType; // 노드 타입 조회
    // 물8(208) 즉사 예외 판정용 — 보스/정예 여부
    public bool IsBossOrElite => nodeType == NodeType.Boss || nodeType == NodeType.Elite;

    public event Action<float,float> OnHpChanged; // HP 변경 이벤트(현재 HP, 최대 HP)
    public event Action OnDied; // 사망 이벤트
    public event Action<int> OnAttackCountChanged; // 공격 횟수 변경 이벤트
    public event Action OnMidPattern; // 게이지 절반 도달 이벤트
    public event Action OnGaugeFull; // 게이지 가득 참(적 공격) 이벤트
    public event Action<float> OnGaugeStepChanged; // 게이지 비율 변경 이벤트(0~1)
    public event Action<float> OnDamaged; // 피해 발생 이벤트(피격 연출용)
    public bool isNewlyFrozen = false; // 이번에 막 빙결되었는지 여부

    public int CurrentAttackCount => currentAttackCount; // 현재 공격 횟수 조회
    public int PlannedAttackCount => plannedAttackCount; // 계획 공격 횟수 조회

    // HP를 정수로 반올림해 표시(0)와 실제 잔량(0.1) 불일치 방지
    void NormalizeHp()
    {
        maxHp = Mathf.Max(0f, Mathf.Round(maxHp));
        currentHp = Mathf.Clamp(Mathf.Round(currentHp), 0f, maxHp);
    }

    // 상태이상 키 초기화
    private void Awake()
    {
        statusEffects["burn"] = 0;
        statusEffects["wet"] = 0;
        statusEffects["freeze"] = 0;
        statusEffects["frost"] = 0; // 새 전투 시스템 빙결(데이터 키 FROST). 게이지 상승을 막음.
    }
    // 적 데이터로 스탯과 게이지를 초기화한다
    public void Initialize(EnemyData data, int columnIndex, NodeType nodeType, DifficultyConfig config)
    {
        this.enemyData = data;
        this.columnIndex = columnIndex;
        this.nodeType = nodeType;
        this.config = config;

        maxHp = data.maxHp * config.GetHpMultiplier(columnIndex, nodeType);
        attackDamage = data.attackDamage;
        gaugeSpeed = data.gaugeSpeed;
        currentHp = maxHp;
        hasDied = false;
        gaugeStep = 0;
        midPatternTriggered = false;
        NormalizeHp();
        // 기획서 0.6v: 적마다 행동 게이지 최대치 10~30 (미설정/0이면 기본 20)
        gaugeMaxSteps = Mathf.Clamp(data.actionGaugeMax > 0 ? data.actionGaugeMax : GAUGE_MAX_STEPS, 10, 30);

        OnHpChanged?.Invoke(currentHp, maxHp);

        RollNewAttackPlan();

        Debug.Log($"[{data.enemyName}] �÷�{columnIndex} / HP:{maxHp} / DMG:{attackDamage} / Speed:{gaugeSpeed} / ����Ƚ��:{currentAttackCount}");
    }

    // 피해를 적용하고 사망을 처리한다
    public void TakeDamage(float damage)
    {
        if (!IsAlive) return;

        currentHp -= damage;
        NormalizeHp();
        OnHpChanged?.Invoke(currentHp, maxHp);
        if (damage > 0f) OnDamaged?.Invoke(damage); // 피격 연출(플래시/흔들기) 트리거

        // 사망 시 OnDied 이벤트가 한 번만 발생하도록 처리
        if (!IsAlive && !hasDied)
        {
            hasDied = true;
            Debug.Log($"[EnemyStat.OnDied] {enemyData.enemyName} ��� - OnDied �̺�Ʈ �߻�");
            OnDied?.Invoke();
        }
    }

    // 기획서 0.6v [방해행동-회복]: 적 HP 회복(최대치 클램프)
    public void Heal(float amount)
    {
        if (!IsAlive || amount <= 0f) return;
        currentHp = Mathf.Min(maxHp, currentHp + amount);
        NormalizeHp();
        OnHpChanged?.Invoke(currentHp, maxHp);
    }

    // 새 공격 계획(공격 횟수)을 굴려 설정한다
    public void RollNewAttackPlan()
    {
        plannedAttackCount = config.GetAttackCount(enemyData.baseAttackCount, columnIndex, nodeType);
        currentAttackCount = plannedAttackCount;
        OnAttackCountChanged?.Invoke(currentAttackCount);
    }

    // 남은 공격 횟수를 줄인다
    public void ReduceAttackCount(int amount = 1)
    {
        currentAttackCount = Mathf.Max(0, currentAttackCount - amount);
        OnAttackCountChanged?.Invoke(currentAttackCount);
    }

    // 특이사항(매혹): 행동 게이지를 즉시 가득 채워 적이 바로 공격하게 한다
    public void FillGaugeToFull()
    {
        if (!IsAlive) return;
        gaugeStep = 0;
        midPatternTriggered = false;
        OnGaugeStepChanged?.Invoke(0f);
        OnGaugeFull?.Invoke();
    }

    // 플레이어 행동 시 호출 — 게이지를 한 단계 올리고 방해행동/공격을 처리한다
    public void ConsumeGaugeStep()
    {
        if (!IsAlive) return;

        // 새 전투 시스템 빙결(FROST): 스택이 있으면 게이지 상승을 막고 스택 1 소모.
        if (statusEffects.TryGetValue("frost", out int frost) && frost > 0)
        {
            var ctrl = GetComponent<EnemyController>();
            if (ctrl != null) ctrl.SetStatus("frost", frost - 1);
            else statusEffects["frost"] = frost - 1;
            return;
        }

        if (statusEffects.ContainsKey("freeze") && statusEffects["freeze"] > 0)
        {
            if (isNewlyFrozen)
            {
                isNewlyFrozen = false; 
            }
            else
            {
  
                EnemyController myController = GetComponent<EnemyController>();
                if (myController != null)
                {
                    myController.AddStatus("freeze", -1);
                }
                return; 
            }
        }

        gaugeStep++;
        OnGaugeStepChanged?.Invoke((float)gaugeStep / gaugeMaxSteps);

        // 절반 지점: 사이클당 한 번 방해행동 발동
        if (gaugeStep == gaugeMaxSteps / 2 && !midPatternTriggered)
        {
            midPatternTriggered = true;
            OnMidPattern?.Invoke();
        }

        // 게이지 가득: 적 공격, 게이지 초기화(초과분은 다음 호출에서 자연 이월)
        if (gaugeStep >= gaugeMaxSteps)
        {
            gaugeStep = 0;
            midPatternTriggered = false;
            OnGaugeStepChanged?.Invoke(0f);
            OnGaugeFull?.Invoke();
        }
    }
}


