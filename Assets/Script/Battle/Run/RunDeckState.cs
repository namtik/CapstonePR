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

        [Header("시작 덱 (인스펙터 편집)")]
        [Tooltip("런 시작 덱. 항목을 넣으면 이 덱으로 시작하고, 비우면 기본 8장(DefaultStartingDeckEntries)을 쓴다.\n" +
                 "cardId = 카드 번호(불 100~, 물 200~, 바람 300~, 땅 400~), count = 장수.\n" +
                 "변경은 새 런/리셋부터 반영된다. (인스펙터로 편집하려면 RunDeckState를 씬에 미리 배치)")]
        [SerializeField] private List<CardDatabase.DeckEntry> startingDeck = new List<CardDatabase.DeckEntry>();

        private readonly List<CardDatabase.DeckEntry> _runDeck = new List<CardDatabase.DeckEntry>();
        private bool _seeded;
        private int _permanentAttackPower; // 땅410 등: 런 동안 지속되는 영구 공격력 보너스

        // ── 적 AI용 런 플레이어 프로파일 (실제 플레이 패턴: 카테고리 사용빈도 + 평균 코스트 + 각성) ──
        private const int PROFILE_WINDOW = 15;
        private readonly Queue<Battle.AI.CardCategory> _recentUses = new Queue<Battle.AI.CardCategory>();
        private readonly Queue<int> _recentCosts = new Queue<int>();
        private int _awakenActivations;
        private int _nonAwakenCardUses;

        public IReadOnlyList<CardDatabase.DeckEntry> RunDeck => _runDeck;

        /// <summary>땅410 등 영구 공격력 보너스(런 동안 지속). 전투 시작 시 Ctx.attackPowerBonus로 복원.</summary>
        public int PermanentAttackPower => _permanentAttackPower;
        public void AddPermanentAttackPower(int n)
        {
            if (n == 0) return;
            _permanentAttackPower += n;
            Debug.Log($"[RunDeck] 영구 공격력 +{n} (누적 {_permanentAttackPower})");
        }

        /// <summary>최근 사용 윈도우에서 해당 카테고리 사용 비율 = 실제 플레이 빈도.</summary>
        float CategoryUseRatio(Battle.AI.CardCategory cat)
        {
            if (_recentUses.Count == 0) return 0f;
            int n = 0;
            foreach (var c in _recentUses) if (c == cat) n++;
            return n / (float)_recentUses.Count;
        }
        public float BurnUseRatio => CategoryUseRatio(Battle.AI.CardCategory.Burn);       // f1
        public float FragmentUseRatio => CategoryUseRatio(Battle.AI.CardCategory.Fragment); // f2
        public float ChainUseRatio => CategoryUseRatio(Battle.AI.CardCategory.Chain);      // f3
        /// <summary>f4 방어성향 = 최근 사용 중 방어/회복 비율(실제 플레이).</summary>
        public float DefenseWindowRatio => CategoryUseRatio(Battle.AI.CardCategory.Defense);
        /// <summary>최근 사용 카드 평균 코스트(게이지). 저코스트일수록 빠른 각성 빌드업(피버 지향).</summary>
        public float AvgUsedCost
        {
            get
            {
                if (_recentCosts.Count == 0) return 1f;
                int sum = 0;
                foreach (var c in _recentCosts) sum += c;
                return sum / (float)_recentCosts.Count;
            }
        }
        public int AwakenActivations => _awakenActivations;
        public int NonAwakenCardUses => _nonAwakenCardUses;

        /// <summary>일반(비각성) 카드 1장 사용 기록 — 카테고리·코스트 윈도우 갱신 + 비각성 사용 누적.</summary>
        public void RecordCardUse(Battle.AI.CardCategory category, int cost)
        {
            _recentUses.Enqueue(category);
            while (_recentUses.Count > PROFILE_WINDOW) _recentUses.Dequeue();
            _recentCosts.Enqueue(Mathf.Max(0, cost));
            while (_recentCosts.Count > PROFILE_WINDOW) _recentCosts.Dequeue();
            _nonAwakenCardUses++;
        }

        /// <summary>각성(피버) 발동 1회 기록.</summary>
        public void RecordAwakenActivation() => _awakenActivations++;

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

        /// <summary>비어 있으면 시작 덱으로 시드. 인스펙터 startingDeck이 있으면 그걸, 없으면 기본 8장.</summary>
        public void EnsureSeeded()
        {
            if (_seeded && _runDeck.Count > 0) return;
            _runDeck.Clear();
            if (startingDeck != null && startingDeck.Count > 0)
            {
                _runDeck.AddRange(startingDeck);
                Debug.Log($"[RunDeck] 인스펙터 시작 덱 시드 — {TotalCardCount}장");
            }
            else
            {
                _runDeck.AddRange(CardDatabase.DefaultStartingDeckEntries());
                Debug.Log($"[RunDeck] 기본 시작 덱 시드 — {TotalCardCount}장");
            }
            _seeded = true;
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
            _recentUses.Clear();
            _recentCosts.Clear();
            _awakenActivations = 0;
            _nonAwakenCardUses = 0;
            EnsureSeeded();
            Debug.Log("[RunDeck] 런 리셋 — 기본 덱으로 복원");
        }
    }
}
