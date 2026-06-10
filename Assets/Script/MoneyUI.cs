using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 화폐량을 TextMeshPro 텍스트로 표시하는 UI 컴포넌트
[RequireComponent(typeof(TMP_Text))]
public class MoneyUI : MonoBehaviour
{
    [Header("��ȭ ������ (���û���)")]
    [SerializeField] private Image moneyIconImage;  // 화폐 아이콘 이미지

    private TMP_Text moneyText; // 화폐량을 출력할 텍스트

    // 텍스트 참조와 화폐 아이콘을 초기화한다
    void Awake()
    {
        moneyText = GetComponent<TMP_Text>();

        if (moneyIconImage != null && MoneyManager.Instance != null && MoneyManager.Instance.MoneyIcon != null)
        {
            moneyIconImage.sprite = MoneyManager.Instance.MoneyIcon;
        }
    }

    // 화폐 변경 이벤트를 구독하고 현재 값을 표시한다
    void OnEnable()
    {
        if (MoneyManager.Instance != null)
        {
            MoneyManager.Instance.OnMoneyChanged += UpdateMoneyDisplay;
            UpdateMoneyDisplay(MoneyManager.Instance.CurrentMoney);
        }
    }

    // 화폐 변경 이벤트 구독을 해제한다
    void OnDisable()
    {
        if (MoneyManager.Instance != null)
        {
            MoneyManager.Instance.OnMoneyChanged -= UpdateMoneyDisplay;
        }
    }

    // 파괴 시 처리(현재 동작 없음)
    void OnDestroy()
    {
    }

    // 시작 시 초기 화폐량을 표시한다
    void Start()
    {
        if (MoneyManager.Instance != null)
        {
            UpdateMoneyDisplay(MoneyManager.Instance.CurrentMoney);
        }
    }

    // 화폐 텍스트를 주어진 값으로 갱신한다
    void UpdateMoneyDisplay(int money)
    {
        if (moneyText != null)
        {
            moneyText.text = $"{money}";
        }
    }
}
