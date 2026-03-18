using UnityEngine;

/// <summary>
/// 재화 시스템 관리 싱글톤
/// 몹을 처치할 때마다 50원씩 지급하고, 전투-맵 전환 시에도 재화가 유지됩니다.
/// </summary>
public class MoneyManager : MonoBehaviour
{
    public static MoneyManager Instance { get; private set; }

    [Header("재화 설정")]
    [SerializeField] private int moneyPerKill = 50;
    [SerializeField] private Sprite moneyIconSprite;  // 재화 아이콘 스프라이트
    
    private int currentMoney = 0;

    public int CurrentMoney => currentMoney;
    public int MoneyPerKill => moneyPerKill;
    public Sprite MoneyIcon => moneyIconSprite;  // 재화 아이콘 접근자

    // 재화 변경 이벤트 (UI 업데이트용)
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

    /// <summary>
    /// 재화 추가
    /// </summary>
    public void AddMoney(int amount)
    {
        if (amount <= 0) return;

        currentMoney += amount;
        OnMoneyChanged?.Invoke(currentMoney);
    }

    /// <summary>
    /// 재화 사용 (상점 등에서 사용)
    /// </summary>
    public bool SpendMoney(int amount)
    {
        if (amount <= 0) return false;
        if (currentMoney < amount) return false;

        currentMoney -= amount;
        OnMoneyChanged?.Invoke(currentMoney);
        return true;
    }

    /// <summary>
    /// 적 처치 시 호출되는 메서드
    /// </summary>
    public void OnEnemyKilled()
    {
        Debug.Log($"[MoneyManager.OnEnemyKilled] 호출됨 - moneyPerKill: {moneyPerKill}");
        AddMoney(moneyPerKill);
    }

    /// <summary>
    /// 현재 재화량 반환
    /// </summary>
    public int GetMoney()
    {
        return currentMoney;
    }

    /// <summary>
    /// 재화 직접 설정 (치트, 테스트용)
    /// </summary>
    public void SetMoney(int amount)
    {
        currentMoney = Mathf.Max(0, amount);
        OnMoneyChanged?.Invoke(currentMoney);
    }
}
