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
        private bool _doubleBasicCardEffects; // 유물(태초의 서 10004): 기본 카드 효과 2배 (런 지속)

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

        public bool DoubleBasicCardEffects => _doubleBasicCardEffects; // 기본 카드 효과 2배 여부
        // 기본 카드 효과 2배 활성화 (태초의 서 10004)
        public void EnableDoubleBasicCardEffects()
        {
            if (_doubleBasicCardEffects) return;
            _doubleBasicCardEffects = true;
            Debug.Log("[RunDeck] 기본 카드 효과 2배 활성화");
        }
        // 영구 공격력 보너스 누적 증가
        public void AddPermanentAttackPower(int n)
        {
            if (n == 0) return;
            _permanentAttackPower += n;
            Debug.Log($"[RunDeck] 영구 공격력 +{n} (누적 {_permanentAttackPower})");
        }

        // 카드 1장(사본)별 영구 피해 보너스(연마 410 등) — 카드 ID → [사본 번호] = 누적 피해.
        // 같은 카드가 여러 장이어도 실제로 처치한 그 1장만 오른다. 사본 번호는 런 덱에서 전투용 인스턴스를 만들 때의 순서.
        private readonly Dictionary<int, List<int>> _permanentCardDamage = new Dictionary<int, List<int>>();

        // 카드 ID의 copyIndex번째 사본의 영구 피해 보너스
        public int GetPermanentCardDamage(int cardId, int copyIndex)
        {
            if (copyIndex < 0 || !_permanentCardDamage.TryGetValue(cardId, out var list)) return 0;
            return copyIndex < list.Count ? list[copyIndex] : 0;
        }

        // 카드 ID의 copyIndex번째 사본의 영구 피해 보너스 누적 증가 (플레이어 공격력에는 영향 없음)
        public void AddPermanentCardDamage(int cardId, int copyIndex, int n)
        {
            if (n == 0 || copyIndex < 0) return;
            if (!_permanentCardDamage.TryGetValue(cardId, out var list))
                _permanentCardDamage[cardId] = list = new List<int>();
            while (list.Count <= copyIndex) list.Add(0);
            list[copyIndex] += n;
            Debug.Log($"[RunDeck] 카드 {cardId} #{copyIndex} 영구 피해 +{n} (누적 {list[copyIndex]})");
        }

        // 카드 1장이 덱에서 빠질 때 — 보너스가 가장 낮은 사본을 뺀다(강화된 카드를 잃지 않게)
        void RemoveOneCopyBonus(int cardId)
        {
            if (!_permanentCardDamage.TryGetValue(cardId, out var list) || list.Count == 0) return;
            int count = 0;
            for (int i = 0; i < _runDeck.Count; i++)
                if (_runDeck[i].cardId == cardId) count += _runDeck[i].count;
            // count는 제거 후 남은 장수. 기록이 없는 사본(보너스 0)이 남아 있으면 그쪽이 빠진 것으로 본다.
            if (list.Count <= count) return;
            int minIdx = 0;
            for (int i = 1; i < list.Count; i++) if (list[i] < list[minIdx]) minIdx = i;
            list.RemoveAt(minIdx);
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
            MarkDeckDiscovered();
        }

        // 보존 덱에서 이번 전투용 새 CardInstance 목록 생성
        public List<CardInstance> InstantiateForBattle()
        {
            EnsureSeeded();
            var cards = CardDatabase.InstantiateDeck(_runDeck);
            // 같은 카드 ID 안에서의 순서를 사본 번호로 매겨, 그 사본의 영구 피해 보너스를 붙인다
            var copyCounters = new Dictionary<int, int>();
            for (int i = 0; i < cards.Count; i++)
            {
                var c = cards[i];
                if (c == null || c.data == null) continue;
                copyCounters.TryGetValue(c.Id, out int k);
                copyCounters[c.Id] = k + 1;
                c.runCopyIndex = k;
                c.permanentDamage = GetPermanentCardDamage(c.Id, k);
            }
            return cards;
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
            Battle.UI.CodexProgress.MarkCard(cardId);

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
            _permanentCardDamage.Clear();
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
                RemoveOneCopyBonus(cardId);

                var card = CardDatabase.GetById(cardId);
                string name = card != null ? card.displayName : cardId.ToString();
                Debug.Log($"[RunDeck] 카드 제거: {name} (덱 {TotalCardCount}장)");
                return true;
            }

            return false;
        }

        // 보유 덱의 모든 카드를 랜덤한(불/물/바람/땅) 카드로 변환 (망각의 붓 10017)
        public void RandomizeAllCards()
        {
            EnsureSeeded();

            var pool = new List<int>();
            foreach (var c in CardDatabase.All)
            {
                if (c == null) continue;
                if (c.element == CardElement.Fire || c.element == CardElement.Water ||
                    c.element == CardElement.Wind || c.element == CardElement.Earth)
                    pool.Add(c.id);
            }
            if (pool.Count == 0) return;

            int total = TotalCardCount;
            var counts = new Dictionary<int, int>();
            for (int i = 0; i < total; i++)
            {
                int id = pool[Random.Range(0, pool.Count)];
                counts.TryGetValue(id, out int cur);
                counts[id] = cur + 1;
            }

            _runDeck.Clear();
            _permanentCardDamage.Clear(); // 카드가 전부 바뀌므로 사본별 강화도 초기화
            foreach (var kv in counts) _runDeck.Add(new CardDatabase.DeckEntry(kv.Key, kv.Value));
            _seeded = true;
            MarkDeckDiscovered();
            Debug.Log($"[RunDeck] 카드 전부 랜덤 변환 — {TotalCardCount}장 ({_runDeck.Count}종)");
        }

        // 현재 덱의 모든 카드를 도감에 발견 처리
        void MarkDeckDiscovered()
        {
            for (int i = 0; i < _runDeck.Count; i++)
                if (_runDeck[i].count > 0) Battle.UI.CodexProgress.MarkCard(_runDeck[i].cardId);
        }

        // 런 재시작 — 덱과 누적 상태를 기본값으로 복원
        public void ResetRun()
        {
            _runDeck.Clear();
            _seeded = false;
            _permanentAttackPower = 0;
            _permanentCardDamage.Clear();
            _basicCardStatMultiplier = 1;
            _doubleBasicCardEffects = false;
            _recentUses.Clear();
            _recentCosts.Clear();
            _awakenActivations = 0;
            _nonAwakenCardUses = 0;
            EnsureSeeded();
            Debug.Log("[RunDeck] 런 리셋 — 기본 덱으로 복원");
        }
    }
}
