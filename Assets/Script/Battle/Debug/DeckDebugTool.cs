using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Battle.Card;
using Battle.Deck;

namespace Battle
{
    /// <summary>
    /// [테스트 전용] 인스펙터로 덱을 보고/편집하는 도구.
    /// - 게임 시작 전: deckEntries를 채우고 useAsStartingDeck를 켜면 플레이 시작 시 런 시작 덱으로 적용.
    /// - 게임 중: 버튼(또는 컴포넌트 우클릭 컨텍스트 메뉴)으로 런 덱 적용 / 현재 전투에 카드 즉시 투입.
    /// 카드 ID는 "카드 목록 콘솔 출력"으로 확인. 출시 빌드 전 삭제해도 무방한 디버그 컴포넌트.
    /// </summary>
    public class DeckDebugTool : MonoBehaviour
    {
        [Header("덱 목록 (Card Id + 수량)")]
        [Tooltip("테스트용 덱 구성. 버튼으로 런 덱/현재 전투에 적용한다. 카드 ID는 아래 '카드 목록 콘솔 출력'으로 확인.")]
        public List<CardDatabase.DeckEntry> deckEntries = new List<CardDatabase.DeckEntry>();

        [Header("시작 덱으로 사용")]
        [Tooltip("체크하면 플레이 시작(Start)에서 위 목록을 런 시작 덱으로 적용한다. (게임 시작 전 덱 세팅)")]
        public bool useAsStartingDeck = false;

        [Header("게임 중 한 장 빠른 추가")]
        [Tooltip("이 카드 ID를 '패' 또는 '뽑을 더미'에 즉시 투입(빠른 추가 버튼).")]
        public int quickAddCardId = 100;

        void Start()
        {
            if (useAsStartingDeck && deckEntries != null && deckEntries.Count > 0)
            {
                RunDeckState.EnsureExists().ReplaceDeck(deckEntries);
                Debug.Log($"[DeckDebug] 시작 덱으로 적용 — {deckEntries.Count}종");
            }
        }

        // ── 런 덱 (다음 전투/시작 덱) ──────────────────────────────

        [ContextMenu("현재 런 덱 → 목록으로 불러오기")]
        public void LoadFromRunDeck()
        {
            var rd = RunDeckState.EnsureExists();
            deckEntries = new List<CardDatabase.DeckEntry>(rd.RunDeck);
            Debug.Log($"[DeckDebug] 런 덱 불러옴 — {deckEntries.Count}종");
        }

        [ContextMenu("목록 → 런 덱 적용 (다음 전투부터)")]
        public void ApplyToRunDeck()
        {
            RunDeckState.EnsureExists().ReplaceDeck(deckEntries);
            Debug.Log($"[DeckDebug] 런 덱에 적용 — {deckEntries.Count}종");
        }

        [ContextMenu("런 덱 기본값으로 리셋")]
        public void ResetRunDeck()
        {
            RunDeckState.EnsureExists().ResetRun();
        }

        // ── 현재 전투 (라이브 덱) ──────────────────────────────────

        [ContextMenu("목록 전부 → 현재 전투 뽑을 더미에 추가")]
        public void AddListToCurrentBattle()
        {
            var deck = ResolveBattleDeck();
            if (deck == null) { Debug.LogWarning("[DeckDebug] 진행 중인 전투가 없습니다."); return; }

            var instances = CardDatabase.InstantiateDeck(deckEntries);
            foreach (var c in instances) deck.AddToDrawShuffled(c);
            Debug.Log($"[DeckDebug] 현재 전투 뽑을 더미에 {instances.Count}장 추가");
        }

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

        // ── 참고용 ────────────────────────────────────────────────

        [ContextMenu("카드 목록(ID·이름) 콘솔 출력")]
        public void DumpCardList()
        {
            CardDatabase.EnsureInit();
            var sb = new StringBuilder("[DeckDebug] 카드 목록 (ID\t이름):\n");
            foreach (var c in CardDatabase.All)
                if (c != null) sb.AppendLine($"{c.id}\t{c.displayName}");
            Debug.Log(sb.ToString());
        }

        static CardDeckSystem ResolveBattleDeck()
        {
            var nb = NewBattleController.Instance;
            return nb != null ? nb.Deck : null;
        }
    }
}
