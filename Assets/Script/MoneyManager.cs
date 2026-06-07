using UnityEngine;

// ��ȭ �ý��� ���� �̱���
// ���� óġ�� ������ 50���� �����ϰ�, ����-�� ��ȯ �ÿ��� ��ȭ�� �����˴ϴ�.
public class MoneyManager : MonoBehaviour
{
    public static MoneyManager Instance { get; private set; }

    [Header("��ȭ ����")]
    [SerializeField] private int moneyPerKill = 50;
    [SerializeField] private Sprite moneyIconSprite;  // ��ȭ ������ ��������Ʈ

    [Header("신규 전투 시스템 - 전투 종료 골드 보상")]
    [SerializeField] private Vector2Int normalCombatGoldRange = new Vector2Int(30, 50);
    [SerializeField] private Vector2Int eliteCombatGoldRange = new Vector2Int(100, 150);
    [SerializeField] private Vector2Int bossCombatGoldRange = new Vector2Int(200, 250);
    
    private int currentMoney = 0;

    public int CurrentMoney => currentMoney;
    public int MoneyPerKill => moneyPerKill;
    public Sprite MoneyIcon => moneyIconSprite;  // ��ȭ ������ ������

    // ��ȭ ���� �̺�Ʈ (UI ������Ʈ��)
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

    // ��ȭ �߰�
    public void AddMoney(int amount)
    {
        if (amount <= 0) return;

        currentMoney += amount;
        OnMoneyChanged?.Invoke(currentMoney);
    }

    // ��ȭ ��� (���� ��� ���)
    public bool SpendMoney(int amount)
    {
        if (amount <= 0) return false;
        if (currentMoney < amount) return false;

        currentMoney -= amount;
        OnMoneyChanged?.Invoke(currentMoney);
        return true;
    }

    // �� óġ �� ȣ��Ǵ� �޼���
    public void OnEnemyKilled()
    {
        Debug.Log($"[MoneyManager.OnEnemyKilled] ȣ��� - moneyPerKill: {moneyPerKill}");
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

    // ���� ��ȭ�� ��ȯ
    public int GetMoney()
    {
        return currentMoney;
    }

    // ��ȭ ���� ���� (ġƮ, �׽�Ʈ��)
    public void SetMoney(int amount)
    {
        currentMoney = Mathf.Max(0, amount);
        OnMoneyChanged?.Invoke(currentMoney);
    }
}
