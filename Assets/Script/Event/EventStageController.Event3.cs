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
    void OnEvent3InitialChoice1Clicked() => OnEvent3InitialChoice(1);

    void OnEvent3InitialChoice2Clicked() => OnEvent3InitialChoice(2);

    void OnEvent3Branch1Choice1Clicked() => OnEvent3BranchChoice(1, 1);

    void OnEvent3Branch1Choice2Clicked() => OnEvent3BranchChoice(1, 2);

    void OnEvent3Branch2Choice1Clicked() => OnEvent3BranchChoice(2, 1);

    void OnEvent3Branch2Choice2Clicked() => OnEvent3BranchChoice(2, 2);

    // 이벤트3 1차 선택: 중간 결과 텍스트 + 2차 버튼 노출
    void OnEvent3InitialChoice(int branch)
    {
        if (currentEventIndex != 2 || phase != EventPhase.Story || event3Phase != Event3Phase.Initial)
            return;

        event3SelectedBranch = branch;
        event3Phase = Event3Phase.BranchIntro;

        SetEvent3InitialButtonsActive(false);

        string introBody = branch == 1
            ? resultTexts.event3Branch1IntroBody
            : resultTexts.event3Branch2IntroBody;

        PlayEvent3BranchIntro(introBody, branch);
    }

    // 이벤트3 2차 선택: 최종 결과 + 기능 적용 후 맵 복귀 대기
    void OnEvent3BranchChoice(int branch, int subChoice)
    {
        if (currentEventIndex != 2 || phase != EventPhase.Story || event3Phase != Event3Phase.BranchIntro)
            return;
        if (branch != event3SelectedBranch)
            return;

        SetEvent3BranchButtonsActive(branch, false);
        event3Phase = Event3Phase.FinalResult;

        EventResult result = ExecuteEvent3FinalChoice(branch, subChoice);
        PlayResultText(result.body, result.goldReward);
    }

    void ResetEvent3Flow()
    {
        event3Phase = Event3Phase.Initial;
        event3SelectedBranch = 0;

        TextMeshProUGUI title = event3StoryTitleText;
        if (title != null)
            title.gameObject.SetActive(true);

        SetEvent3InitialButtonsActive(true);
        SetEvent3BranchButtonsActive(1, false);
        SetEvent3BranchButtonsActive(2, false);
    }

    void SetEvent3InitialButtonsActive(bool active)
    {
        if (event3Choice1Button != null)
            event3Choice1Button.gameObject.SetActive(active);

        if (event3Choice2Button != null)
            event3Choice2Button.gameObject.SetActive(active);
    }

    void SetEvent3BranchButtonsActive(int branch, bool active)
    {
        if (branch == 1)
        {
            if (event3Branch1Choice1Button != null)
                event3Branch1Choice1Button.gameObject.SetActive(active);
            if (event3Branch1Choice2Button != null)
                event3Branch1Choice2Button.gameObject.SetActive(active);
        }
        else if (branch == 2)
        {
            if (event3Branch2Choice1Button != null)
                event3Branch2Choice1Button.gameObject.SetActive(active);
            if (event3Branch2Choice2Button != null)
                event3Branch2Choice2Button.gameObject.SetActive(active);
        }
    }

    // 1차 선택 후 중간 텍스트를 표시하고 해당 분기 2차 버튼을 연다
    void PlayEvent3BranchIntro(string body, int branch)
    {
        if (event3StoryTitleText != null)
            event3StoryTitleText.gameObject.SetActive(false);

        TextMeshProUGUI bodyTarget = event3StoryBodyText;
        if (bodyTarget == null)
        {
            SetEvent3BranchButtonsActive(branch, true);
            return;
        }

        ApplyResultTextStyle(bodyTarget);

        if (typingRoutine != null)
        {
            StopCoroutine(typingRoutine);
            typingRoutine = null;
        }

        bodyTarget.text = body ?? string.Empty;
        SetEvent3BranchButtonsActive(branch, true);

        if (!useTypewriter)
        {
            bodyTarget.maxVisibleCharacters = int.MaxValue;
            return;
        }

        typingRoutine = StartCoroutine(Event3BranchIntroTypeRoutine(bodyTarget));
    }

    System.Collections.IEnumerator Event3BranchIntroTypeRoutine(TextMeshProUGUI bodyTarget)
    {
        bodyTarget.ForceMeshUpdate();
        int total = bodyTarget.textInfo.characterCount;
        bodyTarget.maxVisibleCharacters = 0;

        if (total <= 0)
        {
            bodyTarget.maxVisibleCharacters = int.MaxValue;
            typingRoutine = null;
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
    }

    // 이벤트3 2차 선택별 최종 효과 + 결과 본문
    EventResult ExecuteEvent3FinalChoice(int branch, int subChoice)
    {
        if (branch == 1 && subChoice == 1)
            return ExecuteEvent3_1_1();
        if (branch == 1 && subChoice == 2)
            return ExecuteEvent3_1_2();
        if (branch == 2 && subChoice == 1)
            return ExecuteEvent3_2_1();
        if (branch == 2 && subChoice == 2)
            return ExecuteEvent3_2_2();

        return default;
    }

    // 1-1: HP -10, 무작위 미보유 콤보 스킬 획득
    EventResult ExecuteEvent3_1_1()
    {
        Player player = Player.Resolve(true);
        if (player != null)
        {
            player.TakeDamage(Event3Branch1Choice1HpLoss, "self_loss");
            Debug.Log($"[EventStage] 이벤트3 1-1: HP -{Event3Branch1Choice1HpLoss}");
        }
        else
        {
            Debug.LogWarning("[EventStage] Player를 찾지 못해 HP 차감을 건너뜁니다.");
        }

        string body = resultTexts.event3Branch1Choice1Body;
        string comboName = GrantRandomUnownedCombo("이벤트3 1-1");
        if (!string.IsNullOrEmpty(comboName) && !string.IsNullOrEmpty(event3Branch1Choice1ComboFormat))
            body += string.Format(event3Branch1Choice1ComboFormat, comboName);

        return EventResult.TextOnly(body);
    }

    // 1-2: 효과 없음 (결과 텍스트만)
    EventResult ExecuteEvent3_1_2()
    {
        return EventResult.TextOnly(resultTexts.event3Branch1Choice2Body);
    }

    // 2-1: 골드 획득
    EventResult ExecuteEvent3_2_1()
    {
        int reward = 0;
        if (event3Branch2Choice1Reward > 0 && MoneyManager.Instance != null)
        {
            MoneyManager.Instance.AddMoney(event3Branch2Choice1Reward);
            reward = event3Branch2Choice1Reward;
            Debug.Log($"[EventStage] 이벤트3 2-1: +{event3Branch2Choice1Reward} 골드");
        }

        return EventResult.WithGold(resultTexts.event3Branch2Choice1Body, reward);
    }

    // 2-2: 오염된 원소 카드(id504) 획득
    EventResult ExecuteEvent3_2_2()
    {
        AddCardToRunDeck(Event3PollutedElementCardId, "이벤트3 2-2 오염된 원소 카드");
        return EventResult.TextOnly(resultTexts.event3Branch2Choice2Body);
    }
}
