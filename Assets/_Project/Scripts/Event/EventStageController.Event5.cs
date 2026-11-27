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
    void OnEvent5Choice1Clicked()
    {
        if (currentEventIndex != 4 || phase != EventPhase.Story || event5Phase != Event5Phase.Initial)
            return;

        List<int> basicCardIds = CollectBasicCardIdsInDeck();
        if (basicCardIds.Count == 0)
        {
            string noBasicBody = string.IsNullOrEmpty(resultTexts.event5Choice1NoBasicBody)
                ? resultTexts.event5Choice1Body
                : resultTexts.event5Choice1NoBasicBody;
            PlayResultText(noBasicBody);
            return;
        }

        SetEvent5ChoiceButtonsActive(false);
        ShowEvent5CardPick(basicCardIds);
    }

    string ExecuteEvent5Choice2()
    {
        RunDeckState.EnsureExists().DoubleBasicCardStats();
        Debug.Log("[EventStage] 이벤트5 버튼2: BASIC 카드 공격/방어 2배");
        return resultTexts.event5Choice2Body;
    }

    void ResetEvent5Flow()
    {
        event5Phase = Event5Phase.Initial;
        ClearEvent5CardPick();
        SetEvent5ChoiceButtonsActive(true);
    }

    void SetEvent5ChoiceButtonsActive(bool active)
    {
        if (event5Choice1Button != null)
            event5Choice1Button.gameObject.SetActive(active);
        if (event5Choice2Button != null)
            event5Choice2Button.gameObject.SetActive(active);
    }

    List<int> CollectBasicCardIdsInDeck()
    {
        var result = new List<int>();
        RunDeckState deck = RunDeckState.EnsureExists();
        deck.EnsureSeeded();
        CardDatabase.EnsureInit();

        IReadOnlyList<CardDatabase.DeckEntry> entries = deck.RunDeck;
        for (int i = 0; i < entries.Count; i++)
        {
            CardDatabase.DeckEntry entry = entries[i];
            if (entry.count <= 0)
                continue;

            CardData cardData = CardDatabase.GetById(entry.cardId);
            if (cardData == null || !cardData.HasTag("BASIC"))
                continue;

            if (!result.Contains(entry.cardId))
                result.Add(entry.cardId);
        }

        return result;
    }

    // 이벤트5 카드 선택 — 전투의 '뽑을 더미에서 카드 선택'과 같은 PileViewerPanel UI로 덱의 기본 카드를 보여 준다.
    void ShowEvent5CardPick(List<int> basicCardIds)
    {
        ClearEvent5CardPick();
        event5Phase = Event5Phase.CardSelect;

        PileViewerPanel panel = ResolveEventCardPickPanel();
        NewCardView prefab = ResolveEventCardViewPrefab();
        if (panel != null && prefab != null)
        {
            // 덱 보기처럼 보유 장수만큼 펼쳐 보여 준다
            _event5PickCandidates.Clear();
            RunDeckState deck = RunDeckState.EnsureExists();
            foreach (var entry in deck.RunDeck)
            {
                if (entry.count <= 0 || !basicCardIds.Contains(entry.cardId)) continue;
                CardData data = CardDatabase.GetById(entry.cardId);
                if (data == null) continue;
                for (int i = 0; i < entry.count; i++) _event5PickCandidates.Add(data);
            }

            if (_event5PickCandidates.Count > 0)
            {
                _event5ActivePanel = panel;
                panel.ShowSelectable(event5CardPickPanelKind, event5CardPickTitle, _event5PickCandidates, prefab,
                    OnEvent5PanelCardClicked, OnEvent5PanelCancelled);
                return;
            }
        }

        ShowEvent5CardPickLegacy(basicCardIds);
    }

    void OnEvent5PanelCardClicked(int index)
    {
        if (index < 0 || index >= _event5PickCandidates.Count) return;
        OnEvent5CardPicked(_event5PickCandidates[index].id);
    }

    // 닫기(X) — 카드를 고르지 않고 선택지로 돌아간다
    void OnEvent5PanelCancelled()
    {
        if (currentEventIndex != 4 || event5Phase != Event5Phase.CardSelect) return;
        ResetEvent5Flow();
    }

    // 인스펙터 지정 패널 → 없으면 씬의 PileViewerPanel(전투 HUD/상점)을 이벤트 캔버스 아래로 복제해 쓴다
    PileViewerPanel ResolveEventCardPickPanel()
    {
        if (event5CardPickPanel != null) return event5CardPickPanel;
        if (_event5PanelClone != null) return _event5PanelClone;

        Transform parent = event5CardPickContainer != null ? event5CardPickContainer
            : event5Canvas != null ? event5Canvas.transform : null;
        if (parent == null) return null;

        PileViewerPanel source = null;
        var all = FindObjectsByType<PileViewerPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null) continue;
            source = all[i];
            if (all[i].GetComponentInParent<CardHandHUD>(true) != null) break; // 전투 더미 보기 패널 우선
        }
        if (source == null)
        {
            Debug.LogWarning("[EventStage] PileViewerPanel을 찾지 못해 기존 카드 선택 UI를 사용합니다.");
            return null;
        }

        bool wasActive = source.gameObject.activeSelf;
        source.gameObject.SetActive(false); // 복제본이 활성 상태로 Awake되지 않게
        _event5PanelClone = Instantiate(source, parent, false);
        source.gameObject.SetActive(wasActive);
        _event5PanelClone.name = "EventCardPickPanel";
        return _event5PanelClone;
    }

    // (예비) 패널을 쓸 수 없을 때의 가로 나열 선택 UI
    void ShowEvent5CardPickLegacy(List<int> basicCardIds)
    {
        Transform parent = event5CardPickContainer;
        if (parent == null && event5Canvas != null)
            parent = event5Canvas.transform;

        if (parent == null)
        {
            Debug.LogWarning("[EventStage] 이벤트5 카드 선택 UI 부모를 찾지 못했습니다.");
            OnEvent5CardPicked(basicCardIds[0]);
            return;
        }

        event5CardPickRoot = new GameObject("Event5CardPick", typeof(RectTransform));
        var rootRect = (RectTransform)event5CardPickRoot.transform;
        rootRect.SetParent(parent, false);
        StretchRectFull(rootRect);
        rootRect.SetAsLastSibling();

        var titleGo = new GameObject("Title", typeof(RectTransform));
        var titleRect = (RectTransform)titleGo.transform;
        titleRect.SetParent(rootRect, false);
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(900f, 60f);
        titleRect.anchoredPosition = new Vector2(0f, -40f);
        var titleText = titleGo.AddComponent<TextMeshProUGUI>();
        titleText.alignment = TextAlignmentOptions.Center;
        ApplyEvent5CardPickTitleStyle(titleText);
        titleText.raycastTarget = false;

        var rowGo = new GameObject("CardRow", typeof(RectTransform));
        var rowRect = (RectTransform)rowGo.transform;
        rowRect.SetParent(rootRect, false);
        rowRect.anchorMin = new Vector2(0.5f, 0.5f);
        rowRect.anchorMax = new Vector2(0.5f, 0.5f);
        rowRect.pivot = new Vector2(0.5f, 0.5f);
        rowRect.sizeDelta = new Vector2(1200f, 500f);
        rowRect.anchoredPosition = Vector2.zero;

        int count = basicCardIds.Count;
        float spacing = event5CardPickSpacing;
        float startX = -spacing * (count - 1) / 2f;

        for (int i = 0; i < count; i++)
        {
            int cardId = basicCardIds[i];
            CardData cardData = CardDatabase.GetById(cardId);
            if (cardData == null)
                continue;

            var slotGo = new GameObject($"PickSlot_{cardId}", typeof(RectTransform));
            var slotRect = (RectTransform)slotGo.transform;
            slotRect.SetParent(rowRect, false);
            slotRect.anchorMin = new Vector2(0.5f, 0.5f);
            slotRect.anchorMax = new Vector2(0.5f, 0.5f);
            slotRect.pivot = new Vector2(0.5f, 0.5f);
            slotRect.sizeDelta = new Vector2(300f, 400f);
            slotRect.anchoredPosition = new Vector2(startX + spacing * i, 0f);
            slotRect.localScale = Vector3.one * event5CardPickScale;

            if (event5CardViewPrefab != null)
            {
                NewCardView view = Instantiate(event5CardViewPrefab, slotRect);
                var viewRect = view.GetComponent<RectTransform>();
                if (viewRect != null)
                {
                    viewRect.anchorMin = new Vector2(0.5f, 0.5f);
                    viewRect.anchorMax = new Vector2(0.5f, 0.5f);
                    viewRect.pivot = new Vector2(0.5f, 0.5f);
                    viewRect.anchoredPosition = Vector2.zero;
                    viewRect.localScale = Vector3.one;
                    viewRect.SetAsFirstSibling();
                }

                view.SetCard(new CardInstance(cardData));
                view.Refresh();
                view.enabled = false;
                foreach (Graphic graphic in view.GetComponentsInChildren<Graphic>(true))
                    graphic.raycastTarget = false;
            }
            else
            {
                var labelGo = new GameObject("Label", typeof(RectTransform));
                var labelRect = (RectTransform)labelGo.transform;
                labelRect.SetParent(slotRect, false);
                StretchRectFull(labelRect);
                labelRect.SetAsFirstSibling();
                var label = labelGo.AddComponent<TextMeshProUGUI>();
                label.alignment = TextAlignmentOptions.Center;
                label.fontSize = 28f;
                label.text = cardData.displayName;
                label.raycastTarget = false;
            }

            var hitImage = slotGo.AddComponent<Image>();
            hitImage.color = new Color(1f, 1f, 1f, 0.01f);
            hitImage.raycastTarget = true;
            var button = slotGo.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            int capturedId = cardId;
            button.onClick.AddListener(() => OnEvent5CardPicked(capturedId));

            if (event5CardPickEnableHoverScale)
            {
                var hover = slotGo.AddComponent<ButtonHoverScale>();
                hover.Configure(event5CardPickHoverScale, event5CardPickHoverScale, event5CardPickHoverLerpSpeed);
                hover.SetBaseScale(slotRect.localScale);
            }
        }
    }

    void ApplyEvent5CardPickTitleStyle(TextMeshProUGUI titleText)
    {
        if (titleText == null)
            return;

        titleText.text = event5CardPickTitle;

        if (event5CardPickTitleFont != null)
            titleText.font = event5CardPickTitleFont;
        else if (event5StoryBodyText != null && event5StoryBodyText.font != null)
            titleText.font = event5StoryBodyText.font;

        if (event5CardPickTitleFontSize > 0f)
            titleText.fontSize = event5CardPickTitleFontSize;
        else if (event5StoryBodyText != null)
            titleText.fontSize = event5StoryBodyText.fontSize;
        else
            titleText.fontSize = 32f;

        titleText.color = event5CardPickTitleColor;
    }

    void OnEvent5CardPicked(int sourceCardId)
    {
        if (currentEventIndex != 4 || event5Phase != Event5Phase.CardSelect)
            return;

        ClearEvent5CardPick();
        event5Phase = Event5Phase.Initial;

        if (TryTransformBasicCard(sourceCardId, out string oldName, out string newName))
        {
            string body = resultTexts.event5Choice1Body;
            if (!string.IsNullOrEmpty(event5TransformResultFormat))
            {
                body += "\n\n" + string.Format(
                    event5TransformResultFormat.TrimStart('\n', '\r'),
                    oldName,
                    newName);
            }

            Debug.Log($"[EventStage] 이벤트5 버튼1: {oldName} → {newName}");
            PlayResultText(body);
            return;
        }

        string failedBody = string.IsNullOrEmpty(resultTexts.event5Choice1FailedBody)
            ? resultTexts.event5Choice1Body
            : resultTexts.event5Choice1FailedBody;
        PlayResultText(failedBody);
    }

    bool TryTransformBasicCard(int sourceCardId, out string sourceName, out string newName)
    {
        sourceName = string.Empty;
        newName = string.Empty;

        CardDatabase.EnsureInit();
        CardData source = CardDatabase.GetById(sourceCardId);
        if (source == null || !source.HasTag("BASIC"))
            return false;

        var candidates = new List<CardData>();
        IReadOnlyList<CardData> allCards = CardDatabase.All;
        for (int i = 0; i < allCards.Count; i++)
        {
            CardData candidate = allCards[i];
            if (candidate == null || candidate.id == sourceCardId)
                continue;
            if (candidate.element != source.element)
                continue;
            candidates.Add(candidate);
        }

        if (candidates.Count == 0)
            return false;

        CardData picked = candidates[Random.Range(0, candidates.Count)];
        RunDeckState deck = RunDeckState.EnsureExists();
        if (!deck.TryRemoveCard(sourceCardId))
            return false;

        deck.AddCard(picked.id);
        _pendingGainedCardIds.Add(picked.id); // 결과 화면에 바뀐 카드를 띄운다
        sourceName = source.displayName;
        newName = picked.displayName;
        return true;
    }

    void ClearEvent5CardPick()
    {
        if (_event5ActivePanel != null)
        {
            PileViewerPanel panel = _event5ActivePanel;
            _event5ActivePanel = null;
            if (panel.IsOpen) panel.Hide();
        }

        if (event5CardPickRoot != null)
        {
            Destroy(event5CardPickRoot);
            event5CardPickRoot = null;
        }
    }

    static void StretchRectFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }
}
