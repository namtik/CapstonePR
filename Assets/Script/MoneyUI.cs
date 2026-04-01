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
    
    private static MoneyUI activeInstance;
    private TMP_Text moneyText;
    private bool isPrimaryInstance;

    void Awake()
    {
        if (activeInstance != null && activeInstance != this)
        {
            gameObject.SetActive(false);
            return;
        }

        activeInstance = this;
        isPrimaryInstance = true;
        moneyText = GetComponent<TMP_Text>();
        
        // MoneyManager���� ��ȭ ������ ��������
        if (moneyIconImage != null && MoneyManager.Instance != null && MoneyManager.Instance.MoneyIcon != null)
        {
            moneyIconImage.sprite = MoneyManager.Instance.MoneyIcon;
        }
    }

    void OnEnable()
    {
        if (!isPrimaryInstance) return;

        // MoneyManager �̺�Ʈ ����
        if (MoneyManager.Instance != null)
        {
            MoneyManager.Instance.OnMoneyChanged += UpdateMoneyDisplay;
            UpdateMoneyDisplay(MoneyManager.Instance.CurrentMoney);
        }
    }

    void OnDisable()
    {
        if (!isPrimaryInstance) return;

        // �̺�Ʈ ���� ����
        if (MoneyManager.Instance != null)
        {
            MoneyManager.Instance.OnMoneyChanged -= UpdateMoneyDisplay;
        }
    }

    void OnDestroy()
    {
        if (activeInstance == this)
            activeInstance = null;
    }

    void Start()
    {
        if (!isPrimaryInstance) return;

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
