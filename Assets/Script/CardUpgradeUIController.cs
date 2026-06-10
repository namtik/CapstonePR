using System.Collections.Generic;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardUpgradeUIController : MonoBehaviour
{
    [Header("Element Card Sprites")]
    [SerializeField] private Sprite fireElementSprite; // 불 속성 카드 스프라이트
    [SerializeField] private Sprite waterElementSprite; // 물 속성 카드 스프라이트
    [SerializeField] private Sprite windElementSprite; // 바람 속성 카드 스프라이트
    [SerializeField] private Sprite landElementSprite; // 땅 속성 카드 스프라이트

    public event Action OnCardUpgradeSelected; // 카드 강화 선택 시 발생 이벤트

    private readonly Dictionary<string, Button> cardButtons = new Dictionary<string, Button>(); // 버튼 이름→버튼 매핑
    private readonly Dictionary<string, ElementSlotSystem.RunDeckCard> cardBindings = new Dictionary<string, ElementSlotSystem.RunDeckCard>(); // 버튼 이름→카드 바인딩
    private bool upgradeClaimedThisReward; // 이번 보상에서 강화 사용 여부

    // 버튼 캐싱
    void Awake()
    {
        CacheButtons();
    }

    // 강화 선택 UI 표시 및 바인딩
    public void ShowUpgradeOptions()
    {
        CacheButtons();
        BindCards();
        RefreshView();
    }

    // 강화 사용 여부 설정 후 뷰 갱신
    public void SetUpgradeClaimed(bool claimed)
    {
        upgradeClaimedThisReward = claimed;
        RefreshView();
    }

    // 자식 버튼 중 카드 버튼을 캐싱
    void CacheButtons()
    {
        cardButtons.Clear();
        Button[] buttons = GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (button == null)
                continue;

            string buttonName = button.name;
            if (!IsCardButtonName(buttonName))
                continue;

            cardButtons[buttonName] = button;
        }
    }

    // 런 덱 카드를 버튼에 바인딩
    void BindCards()
    {
        cardBindings.Clear();

        ElementSlotSystem slotSystem = ElementSlotSystem.Instance ?? FindFirstObjectByType<ElementSlotSystem>();
        if (slotSystem == null)
        {
            Debug.LogError("[CardUpgradeUIController] ElementSlotSystem not found.");
            return;
        }

        Dictionary<string, List<ElementSlotSystem.RunDeckCard>> grouped = BuildGroupedCards(slotSystem.RunDeck);

        foreach (KeyValuePair<string, Button> pair in cardButtons)
        {
            string buttonName = pair.Key;
            string elementKey = ElementKeyFromButtonName(buttonName);
            int cardIndex = CardIndexFromButtonName(buttonName);

            if (string.IsNullOrEmpty(elementKey) || cardIndex < 0)
                continue;

            if (!grouped.TryGetValue(elementKey, out List<ElementSlotSystem.RunDeckCard> cards))
                continue;

            if (cardIndex >= cards.Count)
                continue;

            cardBindings[buttonName] = cards[cardIndex];
        }
    }

    // 버튼별 스프라이트/레벨/클릭 상태 갱신
    void RefreshView()
    {
        foreach (KeyValuePair<string, Button> pair in cardButtons)
        {
            string buttonName = pair.Key;
            Button button = pair.Value;
            if (button == null)
                continue;

            if (cardBindings.TryGetValue(buttonName, out ElementSlotSystem.RunDeckCard card) && card != null)
            {
                ApplyButtonSprite(button, buttonName);
                SetLevelText(button, $"Lv.{card.upgradeLevel}");
                button.interactable = !upgradeClaimedThisReward;

                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => UpgradeCard(buttonName));
            }
            else
            {
                SetLevelText(button, "N/A");
                button.interactable = false;
                button.onClick.RemoveAllListeners();
            }
        }
    }

    // 선택한 카드를 강화하고 이벤트 발생
    void UpgradeCard(string buttonName)
    {
        if (upgradeClaimedThisReward)
            return;

        if (!cardBindings.TryGetValue(buttonName, out ElementSlotSystem.RunDeckCard card) || card == null)
            return;

        card.upgradeLevel += 1;
        upgradeClaimedThisReward = true;
        OnCardUpgradeSelected?.Invoke();
        RefreshView();
    }

    // 런 덱 카드를 속성별로 그룹화
    static Dictionary<string, List<ElementSlotSystem.RunDeckCard>> BuildGroupedCards(System.Collections.ObjectModel.ReadOnlyCollection<ElementSlotSystem.RunDeckCard> runDeck)
    {
        Dictionary<string, List<ElementSlotSystem.RunDeckCard>> grouped = new Dictionary<string, List<ElementSlotSystem.RunDeckCard>>
        {
            { "fire", new List<ElementSlotSystem.RunDeckCard>() },
            { "water", new List<ElementSlotSystem.RunDeckCard>() },
            { "wind", new List<ElementSlotSystem.RunDeckCard>() },
            { "earth", new List<ElementSlotSystem.RunDeckCard>() }
        };

        if (runDeck == null)
            return grouped;

        foreach (ElementSlotSystem.RunDeckCard card in runDeck)
        {
            if (card == null || string.IsNullOrEmpty(card.elementKey))
                continue;

            if (grouped.TryGetValue(card.elementKey, out List<ElementSlotSystem.RunDeckCard> list))
                list.Add(card);
        }

        return grouped;
    }

    // 카드 버튼 이름 형식인지 판별
    static bool IsCardButtonName(string buttonName)
    {
        if (string.IsNullOrEmpty(buttonName) || buttonName.Length != 3)
            return false;

        return (buttonName[0] == 'Q' || buttonName[0] == 'W' || buttonName[0] == 'E' || buttonName[0] == 'R')
            && buttonName[1] == '_'
            && buttonName[2] >= '1'
            && buttonName[2] <= '9';
    }

    // 버튼 이름에서 속성 키 추출
    static string ElementKeyFromButtonName(string buttonName)
    {
        if (string.IsNullOrEmpty(buttonName))
            return null;

        switch (buttonName[0])
        {
            case 'Q': return "fire";
            case 'W': return "water";
            case 'E': return "wind";
            case 'R': return "earth";
            default: return null;
        }
    }

    // 버튼 이름에서 카드 인덱스 추출
    static int CardIndexFromButtonName(string buttonName)
    {
        if (string.IsNullOrEmpty(buttonName) || buttonName.Length < 3)
            return -1;

        return (buttonName[2] - '1');
    }

    // 버튼에 속성별 스프라이트 적용
    void ApplyButtonSprite(Button button, string buttonName)
    {
        if (button == null)
            return;

        Sprite targetSprite = null;
        switch (buttonName[0])
        {
            case 'Q': targetSprite = fireElementSprite; break;
            case 'W': targetSprite = waterElementSprite; break;
            case 'E': targetSprite = windElementSprite; break;
            case 'R': targetSprite = landElementSprite; break;
        }

        Image image = button.GetComponent<Image>();
        if (image == null)
            return;

        if (targetSprite != null)
            image.sprite = targetSprite;

        image.preserveAspect = true;
    }

    // 버튼 하위 텍스트에 레벨 표시
    static void SetLevelText(Button button, string text)
    {
        if (button == null)
            return;

        TMP_Text tmp = button.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
        {
            tmp.text = text;
            return;
        }

        Text legacy = button.GetComponentInChildren<Text>(true);
        if (legacy != null)
            legacy.text = text;
    }
}
