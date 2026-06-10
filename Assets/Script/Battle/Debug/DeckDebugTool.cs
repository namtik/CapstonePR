using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Battle.Card;
using Battle.Deck;

namespace Battle
{
    // [테스트 전용] 인스펙터로 덱을 보고/편집하는 디버그 도구
    public class DeckDebugTool : MonoBehaviour
    {
        [Header("덱 목록 (Card Id + 수량)")]
        [Tooltip("테스트용 덱 구성. 버튼으로 런 덱/현재 전투에 적용한다. 카드 ID는 아래 '카드 목록 콘솔 출력'으로 확인.")]
        public List<CardDatabase.DeckEntry> deckEntries = new List<CardDatabase.DeckEntry>(); // 편집용 덱 구성 목록

        [Header("시작 덱으로 사용")]
        [Tooltip("체크하면 플레이 시작(Start)에서 위 목록을 런 시작 덱으로 적용한다. (게임 시작 전 덱 세팅)")]
        public bool useAsStartingDeck = false;        // 시작 시 위 목록을 런 덱으로 적용할지 여부

        [Header("게임 중 한 장 빠른 추가")]
        [Tooltip("이 카드 ID를 '패' 또는 '뽑을 더미'에 즉시 투입(빠른 추가 버튼).")]
        public int quickAddCardId = 100;             // 빠른 추가로 투입할 카드 ID

        // 시작 시 useAsStartingDeck이 켜져 있으면 목록을 런 시작 덱으로 적용
        void Start()
        {
            if (useAsStartingDeck && deckEntries != null && deckEntries.Count > 0)
            {
                RunDeckState.EnsureExists().ReplaceDeck(deckEntries);
                Debug.Log($"[DeckDebug] 시작 덱으로 적용 — {deckEntries.Count}종");
            }
        }

        // 현재 런 덱을 편집용 목록으로 불러오기
        [ContextMenu("현재 런 덱 → 목록으로 불러오기")]
        public void LoadFromRunDeck()
        {
            var rd = RunDeckState.EnsureExists();
            deckEntries = new List<CardDatabase.DeckEntry>(rd.RunDeck);
            Debug.Log($"[DeckDebug] 런 덱 불러옴 — {deckEntries.Count}종");
        }

        // 편집용 목록을 런 덱에 적용 (다음 전투부터)
        [ContextMenu("목록 → 런 덱 적용 (다음 전투부터)")]
        public void ApplyToRunDeck()
        {
            RunDeckState.EnsureExists().ReplaceDeck(deckEntries);
            Debug.Log($"[DeckDebug] 런 덱에 적용 — {deckEntries.Count}종");
        }

        // 런 덱을 기본값으로 리셋
        [ContextMenu("런 덱 기본값으로 리셋")]
        public void ResetRunDeck()
        {
            RunDeckState.EnsureExists().ResetRun();
        }

        // 편집용 목록 전체를 현재 전투의 뽑을 더미에 추가
        [ContextMenu("목록 전부 → 현재 전투 뽑을 더미에 추가")]
        public void AddListToCurrentBattle()
        {
            var deck = ResolveBattleDeck();
            if (deck == null) { Debug.LogWarning("[DeckDebug] 진행 중인 전투가 없습니다."); return; }

            var instances = CardDatabase.InstantiateDeck(deckEntries);
            foreach (var c in instances) deck.AddToDrawShuffled(c);
            Debug.Log($"[DeckDebug] 현재 전투 뽑을 더미에 {instances.Count}장 추가");
        }

        // quickAddCardId 카드 1장을 현재 전투의 패에 빠르게 추가
        [ContextMenu("빠른 추가: 카드 1장 → 패")]
        public void AddQuickCardToHand()
        {
            var deck = ResolveBattleDeck();
            if (deck == null) { Debug.LogWarning("[DeckDebug] 진행 중인 전투가 없습니다."); return; }

            var data = CardDatabase.GetById(quickAddCardId);
            if (data == null) { Debug.LogWarning($"[DeckDebug] 알 수 없는 카드 ID {quickAddCardId}"); return; }

            var card = new CardInstance(data);
            if (!deck.AddToHand(card))
            {
                deck.AddToDrawShuffled(card);
                Debug.Log($"[DeckDebug] 패가 가득 — 뽑을 더미로: {data.displayName}");
            }
            else Debug.Log($"[DeckDebug] 패에 추가: {data.displayName}");
        }

        // quickAddCardId 카드 1장을 현재 전투의 뽑을 더미에 빠르게 추가
        [ContextMenu("빠른 추가: 카드 1장 → 뽑을 더미")]
        public void AddQuickCardToDraw()
        {
            var deck = ResolveBattleDeck();
            if (deck == null) { Debug.LogWarning("[DeckDebug] 진행 중인 전투가 없습니다."); return; }

            var data = CardDatabase.GetById(quickAddCardId);
            if (data == null) { Debug.LogWarning($"[DeckDebug] 알 수 없는 카드 ID {quickAddCardId}"); return; }

            deck.AddToDrawShuffled(new CardInstance(data));
            Debug.Log($"[DeckDebug] 뽑을 더미에 추가: {data.displayName}");
        }

        // 전체 카드 목록(ID·이름)을 콘솔에 출력
        [ContextMenu("카드 목록(ID·이름) 콘솔 출력")]
        public void DumpCardList()
        {
            CardDatabase.EnsureInit();
            var sb = new StringBuilder("[DeckDebug] 카드 목록 (ID\t이름):\n");
            foreach (var c in CardDatabase.All)
                if (c != null) sb.AppendLine($"{c.id}\t{c.displayName}");
            Debug.Log(sb.ToString());
        }

        // 진행 중인 전투의 라이브 덱을 반환 (없으면 null)
        static CardDeckSystem ResolveBattleDeck()
        {
            var nb = NewBattleController.Instance;
            return nb != null ? nb.Deck : null;
        }
    }
}
