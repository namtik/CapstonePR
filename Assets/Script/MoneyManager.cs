using UnityEngine;

// 화폐 시스템 관리 싱글톤
public class MoneyManager : MonoBehaviour
{
    public static MoneyManager Instance { get; private set; } // 전역 싱글톤 인스턴스

    [Header("화폐 설정")]
    [SerializeField] private int moneyPerKill = 50; // 적 처치당 획득 골드
    [SerializeField] private Sprite moneyIconSprite;  // 화폐 아이콘 스프라이트

    [Header("신규 전투 시스템 - 전투 종료 골드 보상")]
    [SerializeField] private Vector2Int normalCombatGoldRange = new Vector2Int(30, 50); // 일반 전투 보상 범위
    [SerializeField] private Vector2Int eliteCombatGoldRange = new Vector2Int(100, 150); // 엘리트 전투 보상 범위
    [SerializeField] private Vector2Int bossCombatGoldRange = new Vector2Int(200, 250); // 보스 전투 보상 범위

    private int currentMoney = 0; // 현재 보유 골드

    public int CurrentMoney => currentMoney; // 현재 보유 골드 게터
    public int MoneyPerKill => moneyPerKill; // 처치당 골드 게터
    public Sprite MoneyIcon => moneyIconSprite;  // 화폐 아이콘 게터

    public event System.Action<int> OnMoneyChanged; // 화폐 변경 이벤트(UI 갱신용)

    // 싱글톤 초기화 및 씬 전환 유지 설정
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 지정한 양만큼 화폐를 추가한다
    public void AddMoney(int amount)
    {
        if (amount <= 0) return;

        currentMoney += amount;
        OnMoneyChanged?.Invoke(currentMoney);
    }

    // 화폐를 차감하며 부족하면 실패를 반환한다
    public bool SpendMoney(int amount)
    {
        if (amount <= 0) return false;
        if (currentMoney < amount) return false;

        currentMoney -= amount;
        OnMoneyChanged?.Invoke(currentMoney);
        return true;
    }

    // 적 처치 시 처치 보상 골드를 지급한다
    public void OnEnemyKilled()
    {
        Debug.Log($"[MoneyManager.OnEnemyKilled] 호출됨 - moneyPerKill: {moneyPerKill}");
        AddMoney(moneyPerKill);
    }

    // 전투 유형(보스/엘리트/일반)에 따른 보상 골드를 굴린다
    public int RollCombatRewardGold(bool isBoss, bool isElite, bool isNormalCombat)
    {
        if (isBoss) return RollGoldInRange(bossCombatGoldRange);
        if (isElite) return RollGoldInRange(eliteCombatGoldRange);
        if (isNormalCombat) return RollGoldInRange(normalCombatGoldRange);
        return 0;
    }

    // 주어진 범위 내에서 무작위 골드 값을 반환한다
    static int RollGoldInRange(Vector2Int range)
    {
        int min = Mathf.Min(range.x, range.y);
        int max = Mathf.Max(range.x, range.y);
        return Random.Range(min, max + 1);
    }

    // 현재 보유 화폐량을 반환한다
    public int GetMoney()
    {
        return currentMoney;
    }

    // 화폐를 지정값으로 직접 설정한다
    public void SetMoney(int amount)
    {
        currentMoney = Mathf.Max(0, amount);
        OnMoneyChanged?.Invoke(currentMoney);
    }

    // 보유 골드를 런 시작값(0)으로 초기화한다
    public void ResetMoney()
    {
        SetMoney(0);
    }
}
