using System.Collections.Generic;
using UnityEngine;
using Battle.Card;

namespace Battle
{
    /// <summary>
    /// 런(run) 단위 플레이어 덱 보존소. 전투 간 덱을 유지한다.
    /// 덱은 DeckEntry(카드ID+수량)로 저장하고, 매 전투 InstantiateForBattle()로
    /// 새 CardInstance를 만들어 NewBattleController에 주입한다.
    /// (CardInstance는 런타임 상태를 가지므로 재사용하면 오염되기 때문)
    /// SampleScene은 스테이지 토글 방식의 단일 씬이라 씬 오브젝트가 런 내내 유지된다.
    /// </summary>
    public class RunDeckState : MonoBehaviour
    {
        public static RunDeckState Instance { get; private set; }

        private readonly List<CardDatabase.DeckEntry> _runDeck = new List<CardDatabase.DeckEntry>();
        private bool _seeded;
        private int _permanentAttackPower; // 땅410 등: 런 동안 지속되는 영구 공격력 보너스

        public IReadOnlyList<CardDatabase.DeckEntry> RunDeck => _runDeck;

        /// <summary>땅410 등 영구 공격력 보너스(런 동안 지속). 전투 시작 시 Ctx.attackPowerBonus로 복원.</summary>
        public int PermanentAttackPower => _permanentAttackPower;
        public void AddPermanentAttackPower(int n)
        {
            if (n == 0) return;
            _permanentAttackPower += n;
            Debug.Log($"[RunDeck] 영구 공격력 +{n} (누적 {_permanentAttackPower})");
        }

        /// <summary>덱에 든 카드 총 장수(수량 합).</summary>
        public int TotalCardCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _runDeck.Count; i++) n += _runDeck[i].count;
                return n;
            }
        }

        void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(this); return; }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>씬에 없으면 생성해서 Instance를 보장하고 반환.</summary>
        public static RunDeckState EnsureExists()
        {
            if (Instance != null) return Instance;
            var existing = FindFirstObjectByType<RunDeckState>(FindObjectsInactive.Include);
            if (existing != null) { Instance = existing; return existing; }
            var go = new GameObject("RunDeckState");
            return go.AddComponent<RunDeckState>(); // Awake에서 Instance 설정
        }

        /// <summary>비어 있으면 기획서 기본 시작 덱(8장)으로 시드.</summary>
        public void EnsureSeeded()
        {
            if (_seeded && _runDeck.Count > 0) return;
            _runDeck.Clear();
            _runDeck.AddRange(CardDatabase.DefaultStartingDeckEntries());
            _seeded = true;
            Debug.Log($"[RunDeck] 기본 시작 덱 시드 — {TotalCardCount}장");
        }

        /// <summary>이번 전투용으로 보존 덱에서 새 CardInstance 리스트 생성.</summary>
        public List<CardInstance> InstantiateForBattle()
        {
            EnsureSeeded();
            return CardDatabase.InstantiateDeck(_runDeck);
        }

        /// <summary>보상 등으로 카드 1장 추가(같은 ID면 수량 +1).</summary>
        public void AddCard(int cardId)
        {
            EnsureSeeded();
            CardData data = CardDatabase.GetById(cardId);
            if (data == null)
            {
                Debug.LogWarning($"[RunDeck] 알 수 없는 카드 ID {cardId} 추가 무시.");
                return;
            }

            for (int i = 0; i < _runDeck.Count; i++)
            {
                if (_runDeck[i].cardId == cardId)
                {
                    var entry = _runDeck[i];
                    entry.count += 1;
                    _runDeck[i] = entry; // DeckEntry는 struct → 재할당 필요
                    Debug.Log($"[RunDeck] 카드 추가: {data.displayName} (덱 {TotalCardCount}장)");
                    return;
                }
            }

            _runDeck.Add(new CardDatabase.DeckEntry(cardId, 1));
            Debug.Log($"[RunDeck] 카드 추가: {data.displayName} (덱 {TotalCardCount}장)");
        }

        /// <summary>런 재시작 — 덱을 기본값으로 되돌림.</summary>
        public void ResetRun()
        {
            _runDeck.Clear();
            _seeded = false;
            _permanentAttackPower = 0;
            EnsureSeeded();
            Debug.Log("[RunDeck] 런 리셋 — 기본 덱으로 복원");
        }
    }
}
