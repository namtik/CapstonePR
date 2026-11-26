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
    // 제목/선택지를 숨기고 결과 본문을 새로 타이핑한다
    void PlayResultText(string body, int goldReward = 0)
    {
        pendingGoldReward = Mathf.Max(0, goldReward);
        HideAllGoldRewardRows();
        ClearGainedCardViews();
        TextMeshProUGUI title = CurrentTitleTarget();
        if (title != null)
            title.gameObject.SetActive(false);

        foreach (Button button in CurrentChoiceButtons())
        {
            if (button != null)
                button.gameObject.SetActive(false);
        }

        TextMeshProUGUI bodyTarget = GetStoryBodyText(currentEventIndex);
        activeResultBody = bodyTarget;

        // 결과 본문 전용 스타일 적용(원래 스타일은 보관 후 덮어씀)
        ApplyResultTextStyle(bodyTarget);

        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }

        if (bodyTarget == null)
        {
            EnterResultDone();
            return;
        }

        bodyTarget.text = body ?? string.Empty;

        if (!useTypewriter)
        {
            bodyTarget.maxVisibleCharacters = int.MaxValue;
            EnterResultDone();
            return;
        }

        SetPhase(EventPhase.ResultTyping);
        typingRoutine = StartCoroutine(ResultTypeRoutine(bodyTarget));
    }

    // 결과 본문을 글자 단위로 노출시키는 타자기 코루틴
    System.Collections.IEnumerator ResultTypeRoutine(TextMeshProUGUI bodyTarget)
    {
        bodyTarget.ForceMeshUpdate();
        int total = bodyTarget.textInfo.characterCount;
        bodyTarget.maxVisibleCharacters = 0;

        if (total <= 0)
        {
            bodyTarget.maxVisibleCharacters = int.MaxValue;
            typingRoutine = null;
            EnterResultDone();
            yield break;
        }

        float cps = Mathf.Max(1f, bodyCharactersPerSecond);
        float progress = 0f;

        while (bodyTarget.maxVisibleCharacters < total)
        {
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f)
                dt = 1f / 60f;

            progress += cps * dt;
            bodyTarget.maxVisibleCharacters = Mathf.Min(Mathf.FloorToInt(progress), total);
            yield return null;
        }

        bodyTarget.maxVisibleCharacters = int.MaxValue;
        typingRoutine = null;
        EnterResultDone();
    }

    // 진행 중인 결과 타이핑을 즉시 끝까지 표시한다
    void SkipResultTyping()
    {
        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }

        if (activeResultBody != null)
            activeResultBody.maxVisibleCharacters = int.MaxValue;

        EnterResultDone();
    }

    // 결과 완료 단계로 전환한다
    void EnterResultDone()
    {
        if (pendingGoldReward > 0 && activeResultBody != null)
            ShowGoldRewardRow(activeResultBody, pendingGoldReward);
        else
            HideAllGoldRewardRows();

        ShowGainedCards(activeResultBody);
        SetPhase(EventPhase.ResultDone);
    }

    // 이번 결과에서 얻은 부적을 화면 가운데에 크게 띄운다(딤 위). 아무 곳이나 클릭하면 인벤토리로 날아간다.
    void ShowGainedCards(TextMeshProUGUI bodyTarget)
    {
        var ids = new List<int>(_pendingGainedCardIds);
        _pendingGainedCardIds.Clear();
        if (ids.Count == 0) return; // 결과 완료가 두 번 불려도 이미 띄운 카드는 유지
        ClearGainedCardViews();
        if (!showGainedCards || bodyTarget == null)
            return;

        NewCardView prefab = ResolveEventCardViewPrefab();
        Canvas canvas = bodyTarget.canvas != null ? bodyTarget.canvas.rootCanvas : null;
        if (prefab == null || canvas == null)
            return;

        // 화면 전체를 덮는 루트 + 딤(클릭은 Update에서 처리, 딤은 뒤쪽 UI 클릭을 막는다)
        _gainedCardsRoot = new GameObject("GainedCardsOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rootRect = (RectTransform)_gainedCardsRoot.transform;
        rootRect.SetParent(canvas.transform, false);
        StretchRectFull(rootRect);
        rootRect.SetAsLastSibling();
        _gainedDim = _gainedCardsRoot.GetComponent<Image>();
        _gainedDim.color = new Color(0f, 0f, 0f, gainedCardDimAlpha);
        _gainedDim.raycastTarget = true;

        float startX = -gainedCardSpacing * (ids.Count - 1) * 0.5f;
        for (int i = 0; i < ids.Count; i++)
        {
            CardData data = CardDatabase.GetById(ids[i]);
            if (data == null) continue;

            NewCardView view = Instantiate(prefab, rootRect);
            var viewRect = view.GetComponent<RectTransform>();
            if (viewRect != null)
            {
                viewRect.anchorMin = viewRect.anchorMax = viewRect.pivot = new Vector2(0.5f, 0.5f);
                viewRect.anchoredPosition = gainedCardOffset + new Vector2(startX + gainedCardSpacing * i, 0f);
                viewRect.localRotation = Quaternion.identity;
                viewRect.localScale = Vector3.one * gainedCardScale;
            }

            view.SetCard(new CardInstance(data));
            view.Refresh();
            view.enabled = false; // 손패 상호작용(호버·드래그) 없이 보여 주기만
            foreach (Graphic graphic in view.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
            _gainedCardViews.Add(view);
        }

        StartCoroutine(PopInGainedCards());
    }

    // 등장: 살짝 작은 크기에서 커지며 나타난다
    System.Collections.IEnumerator PopInGainedCards()
    {
        const float duration = 0.22f;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            if (_gainedFlying || _gainedCardsRoot == null) yield break;
            float e = Mathf.SmoothStep(0f, 1f, t / duration);
            foreach (NewCardView view in _gainedCardViews)
                if (view != null) view.transform.localScale = Vector3.one * (gainedCardScale * Mathf.Lerp(0.7f, 1f, e));
            yield return null;
        }
        if (_gainedFlying) yield break;
        foreach (NewCardView view in _gainedCardViews)
            if (view != null) view.transform.localScale = Vector3.one * gainedCardScale;
    }

    // 클릭 시: 카드들을 유물 획득과 같은 연출로 인벤토리 버튼까지 날린다(카드는 이미 덱에 들어가 있음)
    System.Collections.IEnumerator FlyGainedCardsToInventory()
    {
        _gainedFlying = true;
        InventoryOpenButton.TryGetTarget(transform, out Vector2 target, out InventoryOpenButton button);

        int running = 0;
        for (int i = 0; i < _gainedCardViews.Count; i++)
        {
            NewCardView view = _gainedCardViews[i];
            if (view == null) continue;
            var rt = (RectTransform)view.transform;
            CanvasGroup group = view.GetComponent<CanvasGroup>();
            if (group == null) group = view.gameObject.AddComponent<CanvasGroup>();
            Vector2 from = InventoryFlyFx.ScreenCenter(rt);
            running++;
            StartCoroutine(FlyOne(rt, from, target, group, () => running--));
            if (gainedCardFlyStagger > 0f && i < _gainedCardViews.Count - 1)
                yield return new WaitForSecondsRealtime(gainedCardFlyStagger);
        }

        // 딤은 카드가 나는 동안 걷힌다
        float dimStart = _gainedDim != null ? _gainedDim.color.a : 0f;
        float t = 0f;
        while (running > 0)
        {
            t += Time.unscaledDeltaTime;
            if (_gainedDim != null)
                _gainedDim.color = new Color(0f, 0f, 0f, Mathf.Lerp(dimStart, 0f, Mathf.Clamp01(t / InventoryFlyFx.Duration)));
            yield return null;
        }

        if (button != null)
            button.PlayReceivePulse();
        yield return new WaitForSecondsRealtime(0.12f);
        ClearGainedCardViews();
    }

    void ClearGainedCardViews()
    {
        if (_gainedCardsRoot != null)
            Destroy(_gainedCardsRoot);
        _gainedCardsRoot = null;
        _gainedCardViews.Clear();
        _gainedDim = null;
        _gainedFlying = false;
    }
}
