using UnityEngine;
using TMPro;

/// <summary>
/// 재화 UI 표시 컴포넌트
/// MoneyManager의 재화를 TextMeshPro로 화면에 표시합니다.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class MoneyUI : MonoBehaviour
{
    private TMP_Text moneyText;

    void Awake()
    {
        moneyText = GetComponent<TMP_Text>();
    }

    void OnEnable()
    {
        // MoneyManager 이벤트 구독
        if (MoneyManager.Instance != null)
        {
            MoneyManager.Instance.OnMoneyChanged += UpdateMoneyDisplay;
            UpdateMoneyDisplay(MoneyManager.Instance.CurrentMoney);
        }
    }

    void OnDisable()
    {
        // 이벤트 구독 해지
        if (MoneyManager.Instance != null)
        {
            MoneyManager.Instance.OnMoneyChanged -= UpdateMoneyDisplay;
        }
    }

    void Start()
    {
        // 초기 재화 표시
        if (MoneyManager.Instance != null)
        {
            UpdateMoneyDisplay(MoneyManager.Instance.CurrentMoney);
        }
    }

    void UpdateMoneyDisplay(int money)
    {
        if (moneyText != null)
        {
            moneyText.text = $"{money}";
        }
    }
}
