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
    void FitStoryBodyAboveChoices()
    {
        TextMeshProUGUI body = GetStoryBodyText(currentEventIndex);
        if (body == null || !body.isActiveAndEnabled) return;
        RectTransform bodyRt = body.rectTransform;
        RectTransform parent = bodyRt.parent as RectTransform;
        if (parent == null) return;

        // 지금 보이는 선택지 버튼 중 가장 높은 상단(본문 부모 좌표)
        bool anyButton = false;
        float buttonTop = float.NegativeInfinity;
        foreach (Button b in AllChoiceButtonsOfCurrentEvent())
        {
            if (b == null || !b.gameObject.activeInHierarchy) continue;
            ((RectTransform)b.transform).GetWorldCorners(_fitCorners);
            for (int i = 0; i < 4; i++)
                buttonTop = Mathf.Max(buttonTop, parent.InverseTransformPoint(_fitCorners[i]).y);
            anyButton = true;
        }
        if (!anyButton) return; // 결과 단계(버튼 숨김) — 결과 스타일을 건드리지 않는다

        string text = body.text ?? string.Empty;
        bool sameText = body == _fitBody && text == _fitText;
        if (sameText && Mathf.Abs(body.fontSize - _fitAppliedSize) < 0.01f
            && !float.IsNaN(_fitButtonTop) && Mathf.Abs(buttonTop - _fitButtonTop) < 1f)
            return; // 이미 맞춤

        // 다른 문장이거나 외부(스타일 복원 등)에서 글자 크기를 바꿨으면 그 크기를 새 기준으로
        if (!sameText || Mathf.Abs(body.fontSize - _fitAppliedSize) >= 0.01f)
            _fitBaseSize = body.fontSize;

        bodyRt.GetWorldCorners(_fitCorners);
        float bodyTop = parent.InverseTransformPoint(_fitCorners[1]).y;
        float available = bodyTop - buttonTop - bodyChoiceGap;
        float width = bodyRt.rect.width;

        float size = _fitBaseSize;
        if (available > 1f && width > 1f && text.Length > 0)
        {
            body.fontSize = size;
            float h = body.GetPreferredValues(text, width, 0f).y;
            float min = Mathf.Min(bodyMinFontSize, _fitBaseSize);
            while (h > available && size - 1f >= min)
            {
                size -= 1f;
                body.fontSize = size;
                h = body.GetPreferredValues(text, width, 0f).y;
            }
        }
        body.fontSize = size;

        _fitBody = body;
        _fitText = text;
        _fitAppliedSize = size;
        _fitButtonTop = buttonTop;
    }

    // 스토리 단계 진입 시 제목과 선택지 버튼을 다시 표시한다
    void RestoreStoryVisuals()
    {
        if (currentEventIndex == 2)
        {
            ResetEvent3Flow();
            return;
        }

        if (currentEventIndex == 4)
        {
            ResetEvent5Flow();
            return;
        }

        TextMeshProUGUI title = CurrentTitleTarget();
        if (title != null)
            title.gameObject.SetActive(true);

        foreach (Button button in CurrentChoiceButtons())
        {
            if (button != null)
                button.gameObject.SetActive(true);
        }

        RefreshEvent1Choice1Interactable();
    }

    // 스토리 제목/본문을 캐시 텍스트로 채우고 타자기 연출을 재생한다
    void PlayStoryTypewriter()
    {
        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }

        TextMeshProUGUI titleTarget = GetStoryTitleText(currentEventIndex);
        TextMeshProUGUI bodyTarget = GetStoryBodyText(currentEventIndex);
        HideAllGoldRewardRows();
        pendingGoldReward = 0;
        if (titleTarget == null && bodyTarget == null)
            return;

        // 결과 스타일로 덮어쓴 적이 있으면 스토리 본문 원래 스타일로 복원
        RestoreStoryBodyStyle(bodyTarget);

        CacheAndApplyStoryText(currentEventIndex, titleTarget, bodyTarget);

        if (!useTypewriter)
        {
            if (titleTarget != null)
                titleTarget.maxVisibleCharacters = int.MaxValue;

            if (bodyTarget != null)
                bodyTarget.maxVisibleCharacters = int.MaxValue;

            RefreshEvent1Choice1Interactable();
            return;
        }

        typingRoutine = StartCoroutine(TypewriterRoutine(titleTarget, bodyTarget));
    }

    // 제목과 본문을 글자 단위로 차례대로 노출시키는 타자기 코루틴
    System.Collections.IEnumerator TypewriterRoutine(TextMeshProUGUI titleTarget, TextMeshProUGUI bodyTarget)
    {
        int titleChars = 0;
        int bodyChars = 0;

        if (titleTarget != null)
        {
            titleTarget.ForceMeshUpdate();
            titleChars = titleTarget.textInfo.characterCount;
            titleTarget.maxVisibleCharacters = 0;
        }

        if (bodyTarget != null)
        {
            bodyTarget.ForceMeshUpdate();
            bodyChars = bodyTarget.textInfo.characterCount;
            bodyTarget.maxVisibleCharacters = 0;
        }

        if (titleChars <= 0 && bodyChars <= 0)
        {
            typingRoutine = null;
            RefreshEvent1Choice1Interactable();
            yield break;
        }

        float titleCps = Mathf.Max(1f, titleCharactersPerSecond);
        float bodyCps = Mathf.Max(1f, bodyCharactersPerSecond);

        float titleProgress = 0f;
        float bodyProgress = 0f;

        while (true)
        {
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f)
                dt = 1f / 60f;

            titleProgress += titleCps * dt;
            bodyProgress += bodyCps * dt;

            if (titleTarget != null)
                titleTarget.maxVisibleCharacters = Mathf.Min(Mathf.FloorToInt(titleProgress), titleChars);

            if (bodyTarget != null)
                bodyTarget.maxVisibleCharacters = Mathf.Min(Mathf.FloorToInt(bodyProgress), bodyChars);

            bool titleDone = titleTarget == null || titleTarget.maxVisibleCharacters >= titleChars;
            bool bodyDone = bodyTarget == null || bodyTarget.maxVisibleCharacters >= bodyChars;
            if (titleDone && bodyDone)
                break;

            yield return null;
        }

        if (titleTarget != null)
            titleTarget.maxVisibleCharacters = int.MaxValue;

        if (bodyTarget != null)
            bodyTarget.maxVisibleCharacters = int.MaxValue;

        typingRoutine = null;
        RefreshEvent1Choice1Interactable();
    }

    TextMeshProUGUI GetStoryTitleText(int eventIndex)
    {
        return eventIndex switch
        {
            0 => event1StoryTitleText,
            1 => event2StoryTitleText,
            2 => event3StoryTitleText,
            3 => event4StoryTitleText,
            4 => event5StoryTitleText,
            _ => null
        };
    }

    TextMeshProUGUI GetStoryBodyText(int eventIndex)
    {
        return eventIndex switch
        {
            0 => event1StoryBodyText,
            1 => event2StoryBodyText,
            2 => event3StoryBodyText,
            3 => event4StoryBodyText,
            4 => event5StoryBodyText,
            _ => null
        };
    }

    void CacheAndApplyStoryText(int eventIndex, TextMeshProUGUI titleTarget, TextMeshProUGUI bodyTarget)
    {
        ref string titleCache = ref GetTitleCacheRef(eventIndex);
        ref string storyCache = ref GetStoryCacheRef(eventIndex);

        if (titleTarget != null)
        {
            if (string.IsNullOrEmpty(titleCache))
                titleCache = titleTarget.text;
            titleTarget.text = titleCache;
        }

        if (bodyTarget != null)
        {
            if (string.IsNullOrEmpty(storyCache))
                storyCache = bodyTarget.text;
            bodyTarget.text = storyCache;
        }
    }

    ref string GetTitleCacheRef(int eventIndex)
    {
        switch (eventIndex)
        {
            case 0: return ref event1TitleCached;
            case 1: return ref event2TitleCached;
            case 2: return ref event3TitleCached;
            case 3: return ref event4TitleCached;
            case 4: return ref event5TitleCached;
            default:
                throw new System.ArgumentOutOfRangeException(nameof(eventIndex));
        }
    }

    ref string GetStoryCacheRef(int eventIndex)
    {
        switch (eventIndex)
        {
            case 0: return ref event1StoryCached;
            case 1: return ref event2StoryCached;
            case 2: return ref event3StoryCached;
            case 3: return ref event4StoryCached;
            case 4: return ref event5StoryCached;
            default:
                throw new System.ArgumentOutOfRangeException(nameof(eventIndex));
        }
    }

    ref BodyStyleSnapshot GetBodyStyleBackupRef(int eventIndex)
    {
        switch (eventIndex)
        {
            case 0: return ref event1BodyStyleBackup;
            case 1: return ref event2BodyStyleBackup;
            case 2: return ref event3BodyStyleBackup;
            case 3: return ref event4BodyStyleBackup;
            case 4: return ref event5BodyStyleBackup;
            default:
                throw new System.ArgumentOutOfRangeException(nameof(eventIndex));
        }
    }

    // 결과 본문에 인스펙터 지정 스타일을 적용한다(원래 스타일은 1회 보관)
    void ApplyResultTextStyle(TextMeshProUGUI bodyTarget)
    {
        if (!overrideResultTextStyle || bodyTarget == null) return;

        CaptureBodyStyle(bodyTarget, ref GetBodyStyleBackupRef(currentEventIndex));

        if (resultTextStyle.font != null) bodyTarget.font = resultTextStyle.font;
        bodyTarget.fontSize = resultTextStyle.fontSize;
        bodyTarget.color = resultTextStyle.color;
        bodyTarget.fontStyle = resultTextStyle.fontStyle;
        bodyTarget.alignment = resultTextStyle.alignment;
        bodyTarget.characterSpacing = resultTextStyle.characterSpacing;
        bodyTarget.lineSpacing = resultTextStyle.lineSpacing;
        bodyTarget.paragraphSpacing = resultTextStyle.paragraphSpacing;
    }

    // 텍스트의 현재 스타일을 스냅샷에 1회 저장한다
    void CaptureBodyStyle(TextMeshProUGUI t, ref BodyStyleSnapshot s)
    {
        if (s.captured || t == null) return;
        s.font = t.font;
        s.fontSize = t.fontSize;
        s.color = t.color;
        s.fontStyle = t.fontStyle;
        s.alignment = t.alignment;
        s.characterSpacing = t.characterSpacing;
        s.lineSpacing = t.lineSpacing;
        s.paragraphSpacing = t.paragraphSpacing;
        s.captured = true;
    }

    // 저장된 스냅샷 스타일을 텍스트에 적용한다
    void ApplyBodyStyle(TextMeshProUGUI t, BodyStyleSnapshot s)
    {
        if (t == null || !s.captured) return;
        t.font = s.font;
        t.fontSize = s.fontSize;
        t.color = s.color;
        t.fontStyle = s.fontStyle;
        t.alignment = s.alignment;
        t.characterSpacing = s.characterSpacing;
        t.lineSpacing = s.lineSpacing;
        t.paragraphSpacing = s.paragraphSpacing;
    }
}
