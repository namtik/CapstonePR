using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// 콤보스킬 상점의 각 스킬 아이템 UI를 관리
// - 스킬 이미지, 이름, 가격 표시
// - 구매 버튼 처리
// - 재화 부족 시 비활성화
// - 마우스 오버 시 스킬 설명 툴팁 표시
public class ComboSkillShopItemUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI 참조")]
    [SerializeField] private Image skillIconImage;       // 스킬 아이콘
    [SerializeField] private TextMeshProUGUI skillNameText;   // 스킬 이름
    [SerializeField] private Image moneyIconImage;       // 재화 아이콘 (가격 앞에 표시)
    [SerializeField] private TextMeshProUGUI priceText;       // 가격 텍스트
    [SerializeField] private Button buyButton;           // 구매 버튼

    [Header("툴팁 설정")]
    [SerializeField] private GameObject tooltipPanel;    // 툴팁 패널
    [SerializeField] private TextMeshProUGUI tooltipNameText;
    [SerializeField] private TextMeshProUGUI tooltipComboText;
    [SerializeField] private TextMeshProUGUI tooltipDescText;

    private ComboSkillShopItem item;
    private int itemIndex;
    private ShopStageController shopController;

    // 아이템 UI 초기화
    public void Initialize(ComboSkillShopItem shopItem, int index, ShopStageController controller)
    {
        item = shopItem;
        itemIndex = index;
        shopController = controller;

        // UI 설정
        UpdateUI();

        // 구매 버튼 클릭 리스너 등록
        if (buyButton != null)
            buyButton.onClick.AddListener(OnBuyButtonClicked);

        // 재화 변경 이벤트 구독
        if (MoneyManager.Instance != null)
            MoneyManager.Instance.OnMoneyChanged += OnMoneyChanged;

        // 툴팁 초기화
        if (tooltipPanel != null)
            tooltipPanel.SetActive(false);
    }

    // UI 업데이트 (스킬 정보 표시)
    void UpdateUI()
    {
        if (item == null || item.skill == null) return;

        // 스킬 아이콘
        if (skillIconImage != null)
        {
            if (item.skill.skillIcon != null)
            {
                skillIconImage.sprite = item.skill.skillIcon;
                skillIconImage.enabled = true;
            }
            else
            {
                Debug.LogWarning($"[ComboSkillShopItemUI] 스킬 '{item.skill.name}' (ID: {item.skill.id})의 skillIcon이 null입니다! iconName: {item.skill.iconName}");
                skillIconImage.enabled = false;
            }
        }

        // 스킬 이름
        if (skillNameText != null)
            skillNameText.text = item.skill.name;

        // 재화 아이콘 설정 (MoneyManager에서 가져오기)
        if (moneyIconImage != null && MoneyManager.Instance != null && MoneyManager.Instance.MoneyIcon != null)
            moneyIconImage.sprite = MoneyManager.Instance.MoneyIcon;

        // 가격 (숫자만 표시)
        if (priceText != null)
            priceText.text = item.price.ToString();

        // 재화 확인 후 버튼 활성화 여부 결정
        UpdateBuyButtonState();
    }

    // 구매 버튼 활성화 상태 업데이트
    void UpdateBuyButtonState()
    {
        if (buyButton == null) return;

        int currentMoney = MoneyManager.Instance.CurrentMoney;
        bool canBuy = currentMoney >= item.price;

        buyButton.interactable = canBuy;

        // 비활성화 시 색상 변경
        ColorBlock colors = buyButton.colors;
        if (canBuy)
        {
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.yellow;
        }
        else
        {
            colors.normalColor = new Color(0.5f, 0.5f, 0.5f, 1f); // 회색
            colors.highlightedColor = new Color(0.5f, 0.5f, 0.5f, 1f);
        }
        buyButton.colors = colors;
    }

    // [구매] 버튼 클릭 핸들러
    void OnBuyButtonClicked()
    {
        if (shopController.TryBuySkill(itemIndex))
        {
            buyButton.GetComponent<Image>().color = Color.green;
        }
    }

    // 재화 변경 시 호출되는 콜백
    void OnMoneyChanged(int newMoney)
    {
        UpdateBuyButtonState();
    }

    void OnDestroy()
    {
        // 이벤트 구독 해지
        if (MoneyManager.Instance != null)
            MoneyManager.Instance.OnMoneyChanged -= OnMoneyChanged;
    }

    // 마우스 포인터가 스킬 아이콘에 진입했을 때
    public void OnPointerEnter(PointerEventData eventData)
    {
        ShowTooltip();
    }

    // 마우스 포인터가 스킬 아이콘에서 벗어났을 때
    public void OnPointerExit(PointerEventData eventData)
    {
        HideTooltip();
    }

    // 툴팁 표시
    void ShowTooltip()
    {
        if (tooltipPanel == null || item == null || item.skill == null) return;

        // 툴팁 내용 설정
        if (tooltipNameText != null)
            tooltipNameText.text = item.skill.name;

        if (tooltipComboText != null)
            tooltipComboText.text = $"콤보: {item.skill.combo}";

        if (tooltipDescText != null)
            tooltipDescText.text = item.skill.description;

        tooltipPanel.SetActive(true);
    }

    // 툴팁 숨기기
    void HideTooltip()
    {
        if (tooltipPanel != null)
            tooltipPanel.SetActive(false);
    }
}
