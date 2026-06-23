using System.Collections.Generic;
using UnityEngine;
using Battle.Card;

namespace Battle
{
    // 런 단위로 플레이어 덱을 전투 간 보존하는 컴포넌트
    public class RunDeckState : MonoBehaviour
    {
        public static RunDeckState Instance { get; private set; } // 싱글톤 인스턴스

        [Header("시작 덱 (인스펙터 편집)")]
        [Tooltip("런 시작 덱. 항목을 넣으면 이 덱으로 시작하고, 비우면 기본 8장(DefaultStartingDeckEntries)을 쓴다.\n" +
                 "cardId = 카드 번호(불 100~, 물 200~, 바람 300~, 땅 400~), count = 장수.\n" +
                 "변경은 새 런/리셋부터 반영된다. (인스펙터로 편집하려면 RunDeckState를 씬에 미리 배치)")]
        [SerializeField] private List<CardDatabase.DeckEntry> startingDeck = new List<CardDatabase.DeckEntry>(); // 인스펙터 시작 덱

        private readonly List<CardDatabase.DeckEntry> _runDeck = new List<CardDatabase.DeckEntry>(); // 현재 런 덱
        private bool _seeded;               // 시작 덱 시드 완료 여부
        private int _permanentAttackPower;  // 런 동안 지속되는 영구 공격력 보너스
        private int _basicCardStatMultiplier = 1; // BASIC 태그 카드 DAMAGE/GAIN_BLOCK 배율

        private const int PROFILE_WINDOW = 15; // 최근 사용 프로파일 윈도우 크기
        private readonly Queue<Battle.AI.CardCategory> _recentUses = new Queue<Battle.AI.CardCategory>(); // 최근 사용 카테고리 큐
        private readonly Queue<int> _recentCosts = new Queue<int>(); // 최근 사용 코스트 큐
        private int _awakenActivations;     // 각성 발동 누적 수
        private int _nonAwakenCardUses;     // 비각성 카드 사용 누적 수

        public IReadOnlyList<CardDatabase.DeckEntry> RunDeck => _runDeck; // 현재 런 덱 읽기 전용 뷰

        public int PermanentAttackPower => _permanentAttackPower; // 영구 공격력 보너스
        public int BasicCardStatMultiplier => _basicCardStatMultiplier; // BASIC 카드 공격/방어 배율

        // BASIC 태그 카드의 DAMAGE·GAIN_BLOCK 수치 배율을 2배로 누적
        public void DoubleBasicCardStats()
        {
            _basicCardStatMultiplier *= 2;
            Debug.Log($"[RunDeck] BASIC 카드 공격/방어 배율 x{_basicCardStatMultiplier}");
        }
        // 영구 공격력 보너스 누적 증가
        public void AddPermanentAttackPower(int n)
        {
            if (n == 0) return;
            _permanentAttackPower += n;
            Debug.Log($"[RunDeck] 영구 공격력 +{n} (누적 {_permanentAttackPower})");
        }

        // 최근 사용 윈도우에서 해당 카테고리 사용 비율 계산
        float CategoryUseRatio(Battle.AI.CardCategory cat)
        {
            if (_recentUses.Count == 0) return 0f;
            int n = 0;
            foreach (var c in _recentUses) if (c == cat) n++;
            return n / (float)_recentUses.Count;
        }
        public float BurnUseRatio => CategoryUseRatio(Battle.AI.CardCategory.Burn);          // 화상 카드 사용 비율
        public float FragmentUseRatio => CategoryUseRatio(Battle.AI.CardCategory.Fragment);  // 파편 카드 사용 비율
        public float ChainUseRatio => CategoryUseRatio(Battle.AI.CardCategory.Chain);        // 연쇄 카드 사용 비율
        public float DefenseWindowRatio => CategoryUseRatio(Battle.AI.CardCategory.Defense); // 방어/회복 카드 사용 비율
        // 최근 사용 카드 평균 코스트(게이지)
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
        public int AwakenActivations => _awakenActivations; // 각성 발동 누적 수
        public int NonAwakenCardUses => _nonAwakenCardUses; // 비각성 카드 사용 누적 수

        // 일반 카드 1장 사용 기록(윈도우 갱신 + 누적)
        public void RecordCardUse(Battle.AI.CardCategory category, int cost)
        {
            _recentUses.Enqueue(category);
            while (_recentUses.Count > PROFILE_WINDOW) _recentUses.Dequeue();
            _recentCosts.Enqueue(Mathf.Max(0, cost));
            while (_recentCosts.Count > PROFILE_WINDOW) _recentCosts.Dequeue();
            _nonAwakenCardUses++;
        }

        // 각성 발동 1회 기록
        public void RecordAwakenActivation() => _awakenActivations++;

        // 덱에 든 카드 총 장수
        public int TotalCardCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _runDeck.Count; i++) n += _runDeck[i].count;
                return n;
            }
        }

        // 싱글톤 인스턴스 설정(중복 시 자기 파괴)
        void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(this); return; }
        }

        // 인스턴스 참조 해제
        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // 씬에 없으면 생성해 인스턴스 보장 후 반환
        public static RunDeckState EnsureExists()
        {
            if (Instance != null) return Instance;
            var existing = FindFirstObjectByType<RunDeckState>(FindObjectsInactive.Include);
            if (existing != null) { Instance = existing; return existing; }
            var go = new GameObject("RunDeckState");
            return go.AddComponent<RunDeckState>();
        }

        // 비어 있으면 시작 덱으로 시드
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

        // 보존 덱에서 이번 전투용 새 CardInstance 목록 생성
        public List<CardInstance> InstantiateForBattle()
        {
            EnsureSeeded();
            return CardDatabase.InstantiateDeck(_runDeck);
        }

        // 카드 1장 추가(같은 ID면 수량 증가)
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
                    _runDeck[i] = entry;
                    Debug.Log($"[RunDeck] 카드 추가: {data.displayName} (덱 {TotalCardCount}장)");
                    return;
                }
            }

            _runDeck.Add(new CardDatabase.DeckEntry(cardId, 1));
            Debug.Log($"[RunDeck] 카드 추가: {data.displayName} (덱 {TotalCardCount}장)");
        }

        // 런 덱을 주어진 엔트리들로 통째로 교체(테스트용)
        public void ReplaceDeck(IEnumerable<CardDatabase.DeckEntry> entries)
        {
            _runDeck.Clear();
            if (entries != null)
            {
                foreach (var e in entries)
                {
                    if (e.count <= 0) continue;
                    if (CardDatabase.GetById(e.cardId) == null)
                    {
                        Debug.LogWarning($"[RunDeck] 알 수 없는 카드 ID {e.cardId} 무시(ReplaceDeck).");
                        continue;
                    }
                    _runDeck.Add(e);
                }
            }
            _seeded = true;
            Debug.Log($"[RunDeck] 덱 교체 — {TotalCardCount}장 ({_runDeck.Count}종)");
        }

        // 현재 런 덱 보유 카드 ID 집합 반환
        public HashSet<int> GetOwnedCardIds()
        {
            EnsureSeeded();
            var ids = new HashSet<int>();
            for (int i = 0; i < _runDeck.Count; i++)
            {
                if (_runDeck[i].count > 0)
                    ids.Add(_runDeck[i].cardId);
            }
            return ids;
        }

        // 지정 카드 1장 제거(수량 0이면 엔트리 삭제)
        public bool TryRemoveCard(int cardId)
        {
            EnsureSeeded();

            for (int i = 0; i < _runDeck.Count; i++)
            {
                if (_runDeck[i].cardId != cardId) continue;

                var entry = _runDeck[i];
                if (entry.count <= 0) return false;

                entry.count -= 1;
                if (entry.count <= 0)
                    _runDeck.RemoveAt(i);
                else
                    _runDeck[i] = entry;

                var card = CardDatabase.GetById(cardId);
                string name = card != null ? card.displayName : cardId.ToString();
                Debug.Log($"[RunDeck] 카드 제거: {name} (덱 {TotalCardCount}장)");
                return true;
            }

            return false;
        }

        // 런 재시작 — 덱과 누적 상태를 기본값으로 복원
        public void ResetRun()
        {
            _runDeck.Clear();
            _seeded = false;
            _permanentAttackPower = 0;
            _basicCardStatMultiplier = 1;
            _recentUses.Clear();
            _recentCosts.Clear();
            _awakenActivations = 0;
            _nonAwakenCardUses = 0;
            EnsureSeeded();
            Debug.Log("[RunDeck] 런 리셋 — 기본 덱으로 복원");
        }
    }
}
