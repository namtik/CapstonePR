using System.Collections.Generic;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardUpgradeUIController : MonoBehaviour
{
    [Header("Element Card Sprites")]
    [SerializeField] private Sprite fireElementSprite;
    [SerializeField] private Sprite waterElementSprite;
    [SerializeField] private Sprite windElementSprite;
    [SerializeField] private Sprite landElementSprite;

    public event Action OnCardUpgradeSelected;

    private readonly Dictionary<string, Button> cardButtons = new Dictionary<string, Button>();
    private readonly Dictionary<string, ElementSlotSystem.RunDeckCard> cardBindings = new Dictionary<string, ElementSlotSystem.RunDeckCard>();
    private bool upgradeClaimedThisReward;

    void Awake()
    {
        CacheButtons();
    }

    public void ShowUpgradeOptions()
    {
        CacheButtons();
        BindCards();
        RefreshView();
    }

    public void SetUpgradeClaimed(bool claimed)
    {
        upgradeClaimedThisReward = claimed;
        RefreshView();
    }

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

    static bool IsCardButtonName(string buttonName)
    {
        if (string.IsNullOrEmpty(buttonName) || buttonName.Length != 3)
            return false;

        return (buttonName[0] == 'Q' || buttonName[0] == 'W' || buttonName[0] == 'E' || buttonName[0] == 'R')
            && buttonName[1] == '_'
            && buttonName[2] >= '1'
            && buttonName[2] <= '9';
    }

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

    static int CardIndexFromButtonName(string buttonName)
    {
        if (string.IsNullOrEmpty(buttonName) || buttonName.Length < 3)
            return -1;

        return (buttonName[2] - '1');
    }

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
