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
    // 이벤트1 선택1(도박): 재화 소모 후 확률로 보상을 지급하고 결과 본문을 반환
    EventResult ExecuteEvent1Choice1()
    {
        if (MoneyManager.Instance == null)
        {
            Debug.LogWarning("[EventStage] MoneyManager가 없어 이벤트1 버튼1 보상을 처리하지 못했습니다.");
            return EventResult.TextOnly(resultTexts.event1Choice1.failureBody);
        }

        bool spent = MoneyManager.Instance.SpendMoney(event1Button1Cost);
        if (!spent)
        {
            Debug.Log($"[EventStage] 재화 부족으로 이벤트1 버튼1 실패 (필요: {event1Button1Cost})");
            return EventResult.TextOnly(resultTexts.event1Choice1.failureBody);
        }

        float chance = Mathf.Clamp01(event1Button1WinChance);
        bool won = Random.value < chance;
        if (won)
        {
            MoneyManager.Instance.AddMoney(event1Button1Reward);
            Debug.Log($"[EventStage] 이벤트1 버튼1 성공: -{event1Button1Cost}, +{event1Button1Reward}");
            return EventResult.WithGold(resultTexts.event1Choice1.successBody, event1Button1Reward);
        }

        Debug.Log($"[EventStage] 이벤트1 버튼1 실패: -{event1Button1Cost}, 획득 없음");
        return EventResult.TextOnly(resultTexts.event1Choice1.failureBody);
    }

    // 이벤트1 선택2(회복): 최대 체력 비율만큼 회복하고 결과 본문을 반환
    string ExecuteEvent1Choice2()
    {
        Player player = Player.Resolve(true);
        if (player == null)
        {
            Debug.LogWarning("[EventStage] Player를 찾지 못해 회복을 수행하지 못했습니다.");
            return resultTexts.event1Choice2Body;
        }

        int healAmount = Mathf.Max(1, Mathf.RoundToInt(player.maxHp * Mathf.Clamp01(event1Button2HealPercent)));
        player.Heal(healAmount);
        Debug.Log($"[EventStage] 이벤트1 버튼2: 최대체력 비율 회복 +{healAmount}");
        return resultTexts.event1Choice2Body;
    }

    // 이벤트1 선택3: 속성 카드와 탈진 카드를 덱에 추가하고 결과 본문을 반환
    string ExecuteEvent1Choice3()
    {
        // 이벤트2 선택1과 같이 획득 가능한 전체 속성 카드에서 무작위(기존 풀은 기본 공격 카드뿐)
        int elementalId = PickRandomObtainableElementCardId();
        if (elementalId <= 0) elementalId = PickRandomCardId(elementalCardPool); // 후보가 없을 때만 기존 풀
        AddCardToRunDeck(elementalId, "이벤트1 버튼3 속성카드");
        AddCardToRunDeck(exhaustionCardId, "이벤트1 버튼3 탈진카드");
        return resultTexts.event1Choice3Body;
    }

    // 이벤트2 선택1: 속성 카드를 덱에 추가하고 결과 본문을 반환
    // elementalCardPool(씬 값 100/200/300/400)은 기본 공격 카드뿐이라, 획득 가능한 전체 속성 카드에서 무작위로 뽑는다.
    string ExecuteEvent2Choice1()
    {
        int elementalId = PickRandomObtainableElementCardId();
        if (elementalId <= 0) elementalId = PickRandomCardId(elementalCardPool); // 후보가 없을 때만 기존 풀
        AddCardToRunDeck(elementalId, "이벤트2 버튼1 속성카드");
        return resultTexts.event2Choice1Body;
    }

    // 획득 가능한 4속성 카드 전체(전투 보상·상점과 같은 기준: 기본·파편·임시 카드 제외)에서 무작위 1장
    static int PickRandomObtainableElementCardId()
    {
        CardDatabase.EnsureInit();
        var candidates = new List<int>();
        IReadOnlyList<CardData> all = CardDatabase.All;
        for (int i = 0; i < all.Count; i++)
        {
            CardData c = all[i];
            if (c == null) continue;
            if (c.element != CardElement.Fire && c.element != CardElement.Water
                && c.element != CardElement.Wind && c.element != CardElement.Earth) continue;
            if (c.HasTag("BASIC") || c.HasTag("FRAGMENT") || c.HasTag("TEMPORARY_ON_CREATE")) continue;
            candidates.Add(c.id);
        }
        return candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)] : -1;
    }

    // 이벤트2 선택2(복귀): 별도 효과 없이 결과 본문을 반환
    string ExecuteEvent2Choice2()
    {
        Debug.Log("[EventStage] 이벤트2 버튼2: 맵으로 복귀");
        return resultTexts.event2Choice2Body;
    }

    // 이벤트4 선택1: 보유 카드 중 무작위 N장 제거
    EventResult ExecuteEvent4Choice1()
    {
        List<string> removedNames = RemoveRandomCardsFromRunDeck(event4RemoveCardCount);
        string body = resultTexts.event4Choice1Body;

        if (removedNames.Count > 0 && !string.IsNullOrEmpty(event4RemovedCardsFormat))
        {
            string removalLine = string.Format(
                event4RemovedCardsFormat.TrimStart('\n', '\r'),
                string.Join(", ", removedNames));
            body += "\n\n" + removalLine;
        }

        Debug.Log($"[EventStage] 이벤트4 버튼1: 카드 {removedNames.Count}장 제거");
        return EventResult.TextOnly(body);
    }

    // 이벤트4 선택2: 효과 없음 (결과 텍스트만)
    string ExecuteEvent4Choice2()
    {
        return resultTexts.event4Choice2Body;
    }

    // 런 덱에서 무작위로 count장 제거하고 제거된 카드 이름 목록을 반환
    List<string> RemoveRandomCardsFromRunDeck(int count)
    {
        var removedNames = new List<string>();
        if (count <= 0)
            return removedNames;

        RunDeckState deck = RunDeckState.EnsureExists();
        deck.EnsureSeeded();

        var pool = new List<int>();
        IReadOnlyList<CardDatabase.DeckEntry> entries = deck.RunDeck;
        for (int i = 0; i < entries.Count; i++)
        {
            CardDatabase.DeckEntry entry = entries[i];
            if (entry.count <= 0)
                continue;

            for (int c = 0; c < entry.count; c++)
                pool.Add(entry.cardId);
        }

        int removeCount = Mathf.Min(count, pool.Count);
        for (int i = 0; i < removeCount; i++)
        {
            int pickIndex = Random.Range(0, pool.Count);
            int cardId = pool[pickIndex];
            pool.RemoveAt(pickIndex);

            if (!deck.TryRemoveCard(cardId))
                continue;

            CardData cardData = CardDatabase.GetById(cardId);
            removedNames.Add(cardData != null ? cardData.displayName : cardId.ToString());
        }

        return removedNames;
    }

    string GrantRandomUnownedCombo(string context)
    {
        List<ComboSkillDef> all = ComboSkillDatabase.BuildOwnedCombos();
        if (all == null || all.Count == 0)
        {
            Debug.LogWarning($"[EventStage] {context} - 콤보 DB가 비어 있습니다.");
            return null;
        }

        HashSet<int> ownedRefIds = CollectOwnedComboRefIds();
        var candidates = new List<ComboSkillDef>();
        for (int i = 0; i < all.Count; i++)
        {
            ComboSkillDef skill = all[i];
            if (skill == null)
                continue;

            if (ownedRefIds.Contains(skill.refComboId))
                continue;

            candidates.Add(skill);
        }

        if (candidates.Count == 0)
            candidates = all;

        ComboSkillDef picked = candidates[Random.Range(0, candidates.Count)];
        if (picked == null)
        {
            Debug.LogWarning($"[EventStage] {context} - 지급할 콤보를 고르지 못했습니다.");
            return null;
        }

        NewBattleController battle = NewBattleController.Instance
            ?? FindFirstObjectByType<NewBattleController>(FindObjectsInactive.Include);

        if (battle == null)
        {
            Debug.LogWarning($"[EventStage] {context} - NewBattleController를 찾지 못해 콤보를 지급하지 못했습니다.");
            return null;
        }

        battle.AddOwnedCombo(picked.refComboId);
        Debug.Log($"[EventStage] {context} 콤보 획득: {picked.displayName} (ref={picked.refComboId})");
        return picked.displayName;
    }

    HashSet<int> CollectOwnedComboRefIds()
    {
        var result = new HashSet<int>();
        NewBattleController battle = NewBattleController.Instance
            ?? FindFirstObjectByType<NewBattleController>(FindObjectsInactive.Include);

        if (battle == null || battle.OwnedComboSkills == null)
            return result;

        for (int i = 0; i < battle.OwnedComboSkills.Count; i++)
        {
            ComboSkillDef skill = battle.OwnedComboSkills[i];
            if (skill == null)
                continue;

            result.Add(skill.refComboId);
        }

        return result;
    }

    // 주어진 카드 ID를 검증한 뒤 런 덱에 추가한다
    void AddCardToRunDeck(int cardId, string context)
    {
        if (cardId <= 0)
        {
            Debug.LogWarning($"[EventStage] {context} - 유효한 카드 ID가 없습니다.");
            return;
        }

        CardData cardData = CardDatabase.GetById(cardId);
        if (cardData == null)
        {
            Debug.LogWarning($"[EventStage] {context} - 카드 ID {cardId}를 찾을 수 없습니다.");
            return;
        }

        RunDeckState.EnsureExists().AddCard(cardId);
        _pendingGainedCardIds.Add(cardId); // 결과 화면에 카드로 띄운다
        Debug.Log($"[EventStage] {context} 획득: {cardData.displayName}({cardId})");
    }

    // 풀에서 데이터베이스에 존재하는 카드 ID 하나를 무작위로 고른다
    int PickRandomCardId(List<int> pool)
    {
        if (pool == null || pool.Count == 0)
            return -1;

        var candidates = new List<int>(pool.Count);
        for (int i = 0; i < pool.Count; i++)
        {
            int cardId = pool[i];
            if (CardDatabase.GetById(cardId) != null)
                candidates.Add(cardId);
        }

        if (candidates.Count == 0)
            return -1;

        return candidates[Random.Range(0, candidates.Count)];
    }
}
