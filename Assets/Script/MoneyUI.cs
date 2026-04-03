using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ��ȭ UI ǥ�� ������Ʈ
/// MoneyManager�� ��ȭ�� TextMeshPro�� ȭ�鿡 ǥ���մϴ�.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class MoneyUI : MonoBehaviour
{
    [Header("��ȭ ������ (���û���)")]
    [SerializeField] private Image moneyIconImage;  // ��ȭ ������ �̹��� (�ؽ�Ʈ ���� ��ġ)

    private TMP_Text moneyText;

    void Awake()
    {
        moneyText = GetComponent<TMP_Text>();
        
        // MoneyManager���� ��ȭ ������ ��������
        if (moneyIconImage != null && MoneyManager.Instance != null && MoneyManager.Instance.MoneyIcon != null)
        {
            moneyIconImage.sprite = MoneyManager.Instance.MoneyIcon;
        }
    }

    void OnEnable()
    {
        // MoneyManager �̺�Ʈ ����
        if (MoneyManager.Instance != null)
        {
            MoneyManager.Instance.OnMoneyChanged += UpdateMoneyDisplay;
            UpdateMoneyDisplay(MoneyManager.Instance.CurrentMoney);
        }
    }

    void OnDisable()
    {
        // �̺�Ʈ ���� ����
        if (MoneyManager.Instance != null)
        {
            MoneyManager.Instance.OnMoneyChanged -= UpdateMoneyDisplay;
        }
    }

    void OnDestroy()
    {
    }

    void Start()
    {
        // �ʱ� ��ȭ ǥ��
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
