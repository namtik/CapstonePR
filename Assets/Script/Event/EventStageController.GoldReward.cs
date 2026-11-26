using System.Collections.Generic;
using Battle;
using Battle.Card;
using Battle.UI;
using Battle.Relic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class EventStageController
{
    Sprite ResolveGoldRewardIcon()
    {
        if (goldRewardIcon != null)
            return goldRewardIcon;

        if (fallbackToMoneyManagerIcon && MoneyManager.Instance != null)
            return MoneyManager.Instance.MoneyIcon;

        return null;
    }

    void HideAllGoldRewardRows()
    {
        foreach (KeyValuePair<int, GoldRewardRowUI> pair in goldRewardRows)
        {
            if (pair.Value?.root != null)
                pair.Value.root.SetActive(false);
        }
    }

    void ShowGoldRewardRow(TextMeshProUGUI bodyTarget, int amount)
    {
        if (bodyTarget == null || amount <= 0)
            return;

        Sprite iconSprite = ResolveGoldRewardIcon();
        if (iconSprite == null)
        {
            Debug.LogWarning("[EventStage] 골드 보상 아이콘이 없어 골드 표시를 생략합니다.");
            return;
        }

        GoldRewardRowUI row = GetOrCreateGoldRewardRow(bodyTarget);
        if (row == null)
            return;

        row.icon.sprite = iconSprite;
        row.icon.preserveAspect = true;
        row.icon.raycastTarget = false;

        ConfigureGoldAmountText(row.amount, bodyTarget);
        row.amount.text = string.IsNullOrEmpty(goldRewardAmountFormat)
            ? amount.ToString()
            : string.Format(goldRewardAmountFormat, amount);

        ApplyGoldRewardRowLayout(row);
        PositionGoldRewardRow(bodyTarget, row);
        row.root.transform.SetAsLastSibling();
        row.root.SetActive(true);
    }

    void ApplyGoldRewardRowLayout(GoldRewardRowUI row)
    {
        if (row?.root == null)
            return;

        HorizontalLayoutGroup layout = row.root.GetComponent<HorizontalLayoutGroup>();
        if (layout != null)
            layout.spacing = goldRewardIconTextSpacing;

        LayoutElement iconLayout = row.icon != null ? row.icon.GetComponent<LayoutElement>() : null;
        if (iconLayout != null)
        {
            iconLayout.preferredWidth = goldRewardIconSize;
            iconLayout.preferredHeight = goldRewardIconSize;
        }
    }

    GoldRewardRowUI GetOrCreateGoldRewardRow(TextMeshProUGUI bodyTarget)
    {
        int key = bodyTarget.GetInstanceID();
        if (goldRewardRows.TryGetValue(key, out GoldRewardRowUI existing) && existing?.root != null)
        {
            if (existing.root.transform.parent != bodyTarget.transform)
                existing.root.transform.SetParent(bodyTarget.transform, false);

            return existing;
        }

        Transform parent = bodyTarget.transform;

        var rowGo = new GameObject("GoldRewardRow", typeof(RectTransform));
        rowGo.transform.SetParent(parent, false);

        RectTransform rowRect = rowGo.GetComponent<RectTransform>();

        HorizontalLayoutGroup layout = rowGo.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.spacing = goldRewardIconTextSpacing;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = rowGo.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        GameObject iconGo = new GameObject("GoldIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconGo.transform.SetParent(rowGo.transform, false);
        LayoutElement iconLayout = iconGo.AddComponent<LayoutElement>();
        iconLayout.preferredWidth = goldRewardIconSize;
        iconLayout.preferredHeight = goldRewardIconSize;
        Image icon = iconGo.GetComponent<Image>();

        GameObject amountGo = new GameObject("GoldAmount", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        amountGo.transform.SetParent(rowGo.transform, false);
        TextMeshProUGUI amount = amountGo.GetComponent<TextMeshProUGUI>();
        amount.raycastTarget = false;
        amount.textWrappingMode = TextWrappingModes.NoWrap;

        rowGo.SetActive(false);

        var row = new GoldRewardRowUI
        {
            root = rowGo,
            rect = rowRect,
            icon = icon,
            amount = amount
        };

        goldRewardRows[key] = row;
        return row;
    }

    void ConfigureGoldAmountText(TextMeshProUGUI amount, TextMeshProUGUI bodyReference)
    {
        if (amount == null || bodyReference == null)
            return;

        if (goldRewardFont != null)
            amount.font = goldRewardFont;
        else if (overrideResultTextStyle && resultTextStyle.font != null)
            amount.font = resultTextStyle.font;
        else
            amount.font = bodyReference.font;

        if (goldRewardFontSize > 0f)
            amount.fontSize = goldRewardFontSize;
        else if (overrideResultTextStyle)
            amount.fontSize = resultTextStyle.fontSize;
        else
            amount.fontSize = bodyReference.fontSize;

        if (useGoldRewardFontColor)
            amount.color = goldRewardFontColor;
        else if (overrideResultTextStyle)
            amount.color = resultTextStyle.color;
        else
            amount.color = bodyReference.color;

        if (overrideResultTextStyle)
            amount.fontStyle = resultTextStyle.fontStyle;
        else
            amount.fontStyle = bodyReference.fontStyle;

        amount.alignment = TextAlignmentOptions.MidlineLeft;
    }

    void PositionGoldRewardRow(TextMeshProUGUI bodyTarget, GoldRewardRowUI row)
    {
        if (bodyTarget == null || row?.rect == null)
            return;

        RectTransform bodyRect = bodyTarget.rectTransform;
        bodyTarget.ForceMeshUpdate(true, true);
        Canvas.ForceUpdateCanvases();

        Bounds textBounds = bodyTarget.textBounds;
        Vector2 anchor = bodyRect.pivot;
        float textCenterX = (textBounds.min.x + textBounds.max.x) * 0.5f;

        row.rect.anchorMin = anchor;
        row.rect.anchorMax = anchor;
        row.rect.pivot = new Vector2(0.5f, 1f);
        row.rect.anchoredPosition = new Vector2(
            textCenterX,
            textBounds.min.y - goldRewardRowSpacing);

        LayoutRebuilder.ForceRebuildLayoutImmediate(row.rect);
    }
}
