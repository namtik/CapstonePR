using System.Collections.Generic;
using UnityEngine;

namespace Battle.Card
{
    /// <summary>
    /// 카드 22종(속성 19 + 파편 3) 전체 정의. 카드DB 엑셀과 1:1 대응.
    /// 시작 덱 구성(보유 수량)은 별도 — DefaultPrototypeDeck() 또는 외부에서 주입.
    /// </summary>
    public static class CardDatabase
    {
        public const int FRAGMENT_1 = 422;
        public const int FRAGMENT_2 = 423;
        public const int FRAGMENT_3 = 424;

        private static Dictionary<int, CardData> _byId;
        private static List<CardData> _all;

        public static void EnsureInit()
        {
            if (_byId != null) return;
            BuildAll();
        }

        public static CardData GetById(int id)
        {
            EnsureInit();
            return _byId.TryGetValue(id, out var data) ? data : null;
        }

        public static IReadOnlyList<CardData> All
        {
            get { EnsureInit(); return _all; }
        }

        public static readonly int[] FragmentIds = { FRAGMENT_1, FRAGMENT_2, FRAGMENT_3 };

        // ─────────────────────────────────────────────────────────────
        // 시작 덱 구성
        // ─────────────────────────────────────────────────────────────

        [System.Serializable]
        public struct DeckEntry
        {
            public int cardId;
            public int count;

            public DeckEntry(int cardId, int count)
            {
                this.cardId = cardId;
                this.count = count;
            }
        }

        /// <summary>
        /// PDF 전투시스템 문서 [프로토타입 간 덱 구성] 기준 시작 덱(20장).
        /// </summary>
        public static List<DeckEntry> DefaultPrototypeDeckEntries()
        {
            return new List<DeckEntry>
            {
                // 불 — 8장
                new DeckEntry(104, 1), // 불1
                new DeckEntry(107, 1), // 불2
                new DeckEntry(112, 1), // 불3
                new DeckEntry(115, 2), // 불4
                new DeckEntry(116, 1), // 불5
                new DeckEntry(118, 1), // 불6
                new DeckEntry(121, 1), // 불7

                // 물 — 3장
                new DeckEntry(201, 1), // 물1
                new DeckEntry(208, 1), // 물2
                new DeckEntry(216, 1), // 물3

                // 바람 — 4장
                new DeckEntry(312, 2), // 바람1
                new DeckEntry(320, 1), // 바람2
                new DeckEntry(321, 1), // 바람3

                // 땅 — 5장
                new DeckEntry(400, 1), // 땅1
                new DeckEntry(411, 2), // 땅2
                new DeckEntry(414, 1), // 땅3
                new DeckEntry(420, 1), // 땅4
            };
        }

        /// <summary>DeckEntry 리스트를 실제 CardInstance 리스트로 변환.</summary>
        public static List<CardInstance> InstantiateDeck(IList<DeckEntry> entries)
        {
            EnsureInit();
            var list = new List<CardInstance>();
            if (entries == null) return list;

            foreach (var entry in entries)
            {
                if (entry.count <= 0) continue;
                var data = GetById(entry.cardId);
                if (data == null)
                {
                    Debug.LogWarning($"[CardDatabase] 알 수 없는 카드 ID {entry.cardId} 무시.");
                    continue;
                }
                for (int i = 0; i < entry.count; i++)
                    list.Add(new CardInstance(data));
            }
            return list;
        }

        /// <summary>기본 프로토타입 덱(20장)을 CardInstance 리스트로 생성.</summary>
        public static List<CardInstance> BuildDefaultPrototypeDeck()
        {
            return InstantiateDeck(DefaultPrototypeDeckEntries());
        }

        // ─────────────────────────────────────────────────────────────
        // 파편 생성
        // ─────────────────────────────────────────────────────────────

        public static CardInstance CreateFragmentInstance()
        {
            EnsureInit();
            int pick = FragmentIds[Random.Range(0, FragmentIds.Length)];
            return new CardInstance(_byId[pick], transient: true);
        }

        public static CardInstance CreateFragmentInstance(int fragmentId)
        {
            EnsureInit();
            if (!_byId.ContainsKey(fragmentId)) return null;
            return new CardInstance(_byId[fragmentId], transient: true);
        }

        // ─────────────────────────────────────────────────────────────
        // 정의
        // ─────────────────────────────────────────────────────────────

        static void BuildAll()
        {
            _all = new List<CardData>
            {
                // ── 불 ──
                new CardData(104, "불1", CardElement.Fire, CardType.Attack, 1, "소멸, 피해를 10 준다."),
                new CardData(107, "불2", CardElement.Fire, CardType.Attack, 2, "피해를 n 만큼 준다. (n = 화상 보유량)"),
                new CardData(112, "불3", CardElement.Fire, CardType.Skill,  1, "화상을 15 부여한다. 패에서 카드를 하나 선택하여 소멸한다."),
                new CardData(115, "불4", CardElement.Fire, CardType.Skill,  2, "소멸, 화상을 20 부여한다."),
                new CardData(116, "불5", CardElement.Fire, CardType.Skill,  3, "적이 보유한 화상만큼 화상 부여한다."),
                new CardData(118, "불6", CardElement.Fire, CardType.Power,  2, "적에게 피해를 줄 때 마다 화상을 1 부여한다."),
                new CardData(121, "불7", CardElement.Fire, CardType.Power,  3, "카드가 소멸될 때 마다 적에게 화상을 5 부여한다."),

                // ── 물 ──
                new CardData(201, "물1", CardElement.Water, CardType.Skill, 1, "방어도를 3 얻는다."),
                new CardData(208, "물2", CardElement.Water, CardType.Skill, 1, "체력을 4 회복한다."),
                new CardData(216, "물3", CardElement.Water, CardType.Skill, 3, "패에 있는 카드를 모두 버리고 버린 만큼 카드를 뽑는다."),

                // ── 바람 ──
                new CardData(312, "바람1", CardElement.Wind, CardType.Skill, 1, "카드를 2장 드로우 한다."),
                new CardData(320, "바람2", CardElement.Wind, CardType.Power, 3, "모든 공격 카드의 타격 횟수가 1번씩 늘어난다."),
                new CardData(321, "바람3", CardElement.Wind, CardType.Power, 3, "카드를 사용할 때 마다 적에게 피해를 3 준다."),

                // ── 땅 ──
                new CardData(400, "땅1", CardElement.Earth, CardType.Skill, 1, "방어도를 3 얻는다."),
                new CardData(411, "땅2", CardElement.Earth, CardType.Skill, 2, "방어도를 5 얻는다. 무작위 파편 카드 1장을 뽑을 카드 더미에 섞어 넣는다."),
                new CardData(414, "땅3", CardElement.Earth, CardType.Skill, 3, "소멸, 파편을 하나 선택하여 같은 이름의 카드를 버린 카드 더미에 10장 섞어 넣는다."),
                new CardData(420, "땅4", CardElement.Earth, CardType.Power, 3, "이번 전투 동안 땅 속성 카드를 사용할 때 무작위 파편 카드를 버린 카드 더미에 추가한다."),

                // ── 파편 (전투 한정 생성) ──
                new CardData(FRAGMENT_1, "파편1", CardElement.Neutral, CardType.Skill,  1, "소멸, 방어도를 3 얻는다. 카드를 1장 드로우한다."),
                new CardData(FRAGMENT_2, "파편2", CardElement.Neutral, CardType.Attack, 1, "소멸, 피해를 5 준다. 카드를 1장 드로우한다."),
                new CardData(FRAGMENT_3, "파편3", CardElement.Neutral, CardType.Attack, 1, "소멸, 피해를 8 준다. 방어도를 5 얻는다."),
            };

            _byId = new Dictionary<int, CardData>(_all.Count);
            foreach (var card in _all) _byId[card.id] = card;
        }
    }
}
