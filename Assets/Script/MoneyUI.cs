using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 재화 UI 표시 컴포넌트
/// MoneyManager의 재화를 TextMeshPro로 화면에 표시합니다.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class MoneyUI : MonoBehaviour
{
    [Header("재화 아이콘 (선택사항)")]
    [SerializeField] private Image moneyIconImage;  // 재화 아이콘 이미지 (텍스트 옆에 배치)
    
    private TMP_Text moneyText;

    void Awake()
    {
        moneyText = GetComponent<TMP_Text>();
        
        // MoneyManager에서 재화 아이콘 가져오기
        if (moneyIconImage != null && MoneyManager.Instance != null && MoneyManager.Instance.MoneyIcon != null)
        {
            moneyIconImage.sprite = MoneyManager.Instance.MoneyIcon;
        }
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
