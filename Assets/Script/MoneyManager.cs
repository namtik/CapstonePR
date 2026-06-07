using UnityEngine;

// 화폐 시스템 관리 싱글톤
// 적을 처치할 때마다 50씩 증가하고, 전투-맵 전환 시에도 화폐가 유지됩니다.
public class MoneyManager : MonoBehaviour
{
    public static MoneyManager Instance { get; private set; }

    [Header("화폐 설정")]
    [SerializeField] private int moneyPerKill = 50;
    [SerializeField] private Sprite moneyIconSprite;  // 화폐 아이콘 스프라이트

    [Header("신규 전투 시스템 - 전투 종료 골드 보상")]
    [SerializeField] private Vector2Int normalCombatGoldRange = new Vector2Int(30, 50);
    [SerializeField] private Vector2Int eliteCombatGoldRange = new Vector2Int(100, 150);
    [SerializeField] private Vector2Int bossCombatGoldRange = new Vector2Int(200, 250);

    private int currentMoney = 0;

    public int CurrentMoney => currentMoney;
    public int MoneyPerKill => moneyPerKill;
    public Sprite MoneyIcon => moneyIconSprite;  // 화폐 아이콘 게터

    // 화폐 변경 이벤트 (UI 업데이트용)
    public event System.Action<int> OnMoneyChanged;

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

    // 화폐 추가
    public void AddMoney(int amount)
    {
        if (amount <= 0) return;

        currentMoney += amount;
        OnMoneyChanged?.Invoke(currentMoney);
    }

    // 화폐 사용 (상점 결제 등)
    public bool SpendMoney(int amount)
    {
        if (amount <= 0) return false;
        if (currentMoney < amount) return false;

        currentMoney -= amount;
        OnMoneyChanged?.Invoke(currentMoney);
        return true;
    }

    // 적 처치 시 호출되는 메서드
    public void OnEnemyKilled()
    {
        Debug.Log($"[MoneyManager.OnEnemyKilled] 호출됨 - moneyPerKill: {moneyPerKill}");
        AddMoney(moneyPerKill);
    }

    public int RollCombatRewardGold(bool isBoss, bool isElite, bool isNormalCombat)
    {
        if (isBoss) return RollGoldInRange(bossCombatGoldRange);
        if (isElite) return RollGoldInRange(eliteCombatGoldRange);
        if (isNormalCombat) return RollGoldInRange(normalCombatGoldRange);
        return 0;
    }

    static int RollGoldInRange(Vector2Int range)
    {
        int min = Mathf.Min(range.x, range.y);
        int max = Mathf.Max(range.x, range.y);
        return Random.Range(min, max + 1);
    }

    // 현재 화폐량 반환
    public int GetMoney()
    {
        return currentMoney;
    }

    // 화폐 직접 설정 (치트, 테스트용)
    public void SetMoney(int amount)
    {
        currentMoney = Mathf.Max(0, amount);
        OnMoneyChanged?.Invoke(currentMoney);
    }
}
