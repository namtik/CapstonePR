using System.Collections.Generic;
using UnityEngine;

namespace Battle.Card
{
    /// <summary>
    /// 카드 전체 정의 (카드DBtest.xlsx 기준, ID 100~424).
    /// 시작 덱 구성(보유 수량)은 별도 — DefaultPrototypeDeck() 또는 외부에서 주입.
    /// 효과 실행은 CardEffectResolver가 id로 분기 (미구현 카드는 효과 없음).
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

        /// <summary>테스트 기본 덱 (사용자 지정 — 불/물/바람 일부).</summary>
        public static List<DeckEntry> DefaultPrototypeDeckEntries()
        {
            return new List<DeckEntry>
            {
                // 불
                new DeckEntry(106, 1),
                new DeckEntry(107, 1),
                new DeckEntry(115, 1),
                new DeckEntry(116, 1),
                new DeckEntry(118, 1),
                // 물
                new DeckEntry(203, 2),
                new DeckEntry(207, 1),
                new DeckEntry(211, 1),
                new DeckEntry(216, 1),
                new DeckEntry(218, 1),
                new DeckEntry(220, 1),
                new DeckEntry(221, 1),
                // 바람
                new DeckEntry(308, 2),
                new DeckEntry(312, 1),
                new DeckEntry(313, 2),
                new DeckEntry(318, 1),
                new DeckEntry(320, 1),
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
        // 정의 — 카드DBtest.xlsx 1:1 대응
        // ─────────────────────────────────────────────────────────────

        static void BuildAll()
        {
            _all = new List<CardData>
            {
                // ── 불 (100~121) ──
                new CardData(100, "불1",  CardElement.Fire, CardType.Attack, 1, "피해를 5 준다."),
                new CardData(101, "불2",  CardElement.Fire, CardType.Skill,  1, "방어도를 3 얻는다."),
                new CardData(102, "불3",  CardElement.Fire, CardType.Attack, 1, "피해를 5 준다, 화상을 5 부여한다."),
                new CardData(103, "불4",  CardElement.Fire, CardType.Attack, 1, "피해를 6 준다, 패에서 카드를 하나 선택하여 소멸한다."),
                new CardData(104, "불5",  CardElement.Fire, CardType.Attack, 1, "소멸, 피해를 10 준다."),
                new CardData(105, "불6",  CardElement.Fire, CardType.Attack, 1, "피해를 15 준다. 자신에게 피해를 3 입힌다."),
                new CardData(106, "불7",  CardElement.Fire, CardType.Attack, 2, "피해를 10 준다. 대상의 화상이 10 이상일 시 한번 더 발동한다."),
                new CardData(107, "불8",  CardElement.Fire, CardType.Attack, 2, "피해를 n 만큼 준다. (n = 화상 보유량)"),
                new CardData(108, "불9",  CardElement.Fire, CardType.Attack, 2, "피해를 15 준다. 현재 체력이 50% 미만이라면 피해가 2배가 된다."),
                new CardData(109, "불10", CardElement.Fire, CardType.Attack, 3, "피해를 15 준다, 화상을 15 부여한다. 이 카드가 소멸 시에도 효과를 발동한다."),
                new CardData(110, "불11", CardElement.Fire, CardType.Attack, 3, "소멸, 피해 15 손에 있는 모든 카드를 소멸 시킨다."),
                new CardData(111, "불12", CardElement.Fire, CardType.Attack, 3, "피해를 25 준다."),
                new CardData(112, "불13", CardElement.Fire, CardType.Skill,  1, "화상을 15 부여한다, 패에서 카드를 하나 선택하여 소멸한다."),
                new CardData(113, "불14", CardElement.Fire, CardType.Skill,  1, "화상을 8 부여한다."),
                new CardData(114, "불15", CardElement.Fire, CardType.Skill,  2, "현재 체력의 절반을 잃는다. 잃은 체력의 2배만큼 화상을 부여한다."),
                new CardData(115, "불16", CardElement.Fire, CardType.Skill,  2, "소멸, 화상을 20 부여한다."),
                new CardData(116, "불17", CardElement.Fire, CardType.Skill,  3, "적이 보유한 화상 만큼 화상 부여한다."),
                new CardData(117, "불18", CardElement.Fire, CardType.Power,  1, "플레이어 공격력 +1을 얻는다."),
                new CardData(118, "불19", CardElement.Fire, CardType.Power,  2, "적에게 피해를 줄 때 마다 화상을 1 부여한다."),
                new CardData(119, "불20", CardElement.Fire, CardType.Power,  2, "공격이나 스킬 카드의 효과로 화상이 부여될 때 마다 적에게 피해를 5 준다."),
                new CardData(120, "불21", CardElement.Fire, CardType.Power,  2, "현재 체력이 25% 이하라면 피해량이 2배가 된다."),
                new CardData(121, "불22", CardElement.Fire, CardType.Power,  3, "카드가 소멸될 때 마다 적에게 화상을 5 부여한다."),

                // ── 물 (200~221) ──
                new CardData(200, "물1",  CardElement.Water, CardType.Attack, 1, "피해를 5 준다."),
                new CardData(201, "물2",  CardElement.Water, CardType.Skill,  1, "방어도를 3 얻는다."),
                new CardData(202, "물3",  CardElement.Water, CardType.Attack, 1, "피해를 3 준다, 체력을 2 회복한다."),
                new CardData(203, "물4",  CardElement.Water, CardType.Attack, 1, "피해를 5 준다. 체력이 가득차 있다면 카드를 2장 뽑는다."),
                new CardData(204, "물5",  CardElement.Water, CardType.Attack, 2, "피해를 15 준다. 이 카드를 복사하여 버린 카드 더미에 추가한다."),
                new CardData(205, "물6",  CardElement.Water, CardType.Attack, 2, "소멸, 피해를 10 준다. 체력을 3 회복한다."),
                new CardData(206, "물7",  CardElement.Water, CardType.Attack, 3, "피해를 10 준다. 뽑을 카드 더미에 있는 카드 중 1장을 선택하여 패로 가져온다."),
                new CardData(207, "물8",  CardElement.Water, CardType.Skill,  1, "카드를 1장 뽑고 카드 1장을 패에서 선택하여 버린다."),
                new CardData(208, "물9",  CardElement.Water, CardType.Skill,  1, "체력을 4 회복한다."),
                new CardData(209, "물10", CardElement.Water, CardType.Skill,  1, "패에 있는 무속성 카드 선택하여 소멸한다."),
                new CardData(210, "물11", CardElement.Water, CardType.Skill,  1, "버린 카드 더미에 있는 카드 중 하나를 선택하여 뽑을 카드 더미 맨위로 올린다."),
                new CardData(211, "물12", CardElement.Water, CardType.Skill,  2, "소멸, 이 카드를 제외한 이번 전투에서 마지막으로 사용한 카드와 동일한 효과를 한 번 더 사용한다."),
                new CardData(212, "물13", CardElement.Water, CardType.Skill,  2, "체력을 10 회복한다. 패를 모두 버린다."),
                new CardData(213, "물14", CardElement.Water, CardType.Skill,  2, "소멸, 카드를 하나 선택하여 복사하여 패로 가져온다."),
                new CardData(214, "물15", CardElement.Water, CardType.Skill,  3, "소멸, 플레이어에게 적용된 저주를 모두 해제한다."),
                new CardData(215, "물16", CardElement.Water, CardType.Skill,  3, "일회성, 체력을 10 회복한다."),
                new CardData(216, "물17", CardElement.Water, CardType.Skill,  3, "패에 있는 카드를 모두 버리고 버린 만큼 카드를 뽑는다."),
                new CardData(217, "물18", CardElement.Water, CardType.Power,  1, "피격 시 체력을 1 회복한다."),
                new CardData(218, "물19", CardElement.Water, CardType.Power,  2, "플레이어가 회복될 때 마다 카드를 1장 드로우한다."),
                new CardData(219, "물20", CardElement.Water, CardType.Power,  2, "저주가 해제될 때 마다 체력을 3 회복한다."),
                new CardData(220, "물21", CardElement.Water, CardType.Power,  3, "카드의 효과로 카드를 버릴 때 마다 카드를 1장 드로우한다."),
                new CardData(221, "물22", CardElement.Water, CardType.Power,  3, "물 속성 카드를 사용할 때 마다 체력을 1 회복한다."),

                // ── 바람 (300~321) ──
                new CardData(300, "바람1",  CardElement.Wind, CardType.Attack, 1, "피해를 5 준다."),
                new CardData(301, "바람2",  CardElement.Wind, CardType.Skill,  1, "방어도를 3 얻는다."),
                new CardData(302, "바람3",  CardElement.Wind, CardType.Attack, 0, "피해를 3 준다."),
                new CardData(303, "바람4",  CardElement.Wind, CardType.Attack, 1, "피해를 3 X n 준다. (n='연타' 라는 이름의 카드를 사용한 횟수)"),
                new CardData(304, "바람5",  CardElement.Wind, CardType.Attack, 1, "피해를 3 X 3 준다."),
                new CardData(305, "바람6",  CardElement.Wind, CardType.Attack, 1, "피해를 5 준다. 카드를 1장 드로우한다."),
                new CardData(306, "바람7",  CardElement.Wind, CardType.Attack, 2, "피해를 2 X 3 준다, 연쇄를 10 얻는다."),
                new CardData(307, "바람8",  CardElement.Wind, CardType.Attack, 2, "피해를 3 X 4 준다. 이전 행동에 공격 카드를 사용했다면 한 번 더 발동한다."),
                new CardData(308, "바람9",  CardElement.Wind, CardType.Attack, 2, "피해를 1 X 3 준다, 카드를 2장 드로우한다."),
                new CardData(309, "바람10", CardElement.Wind, CardType.Attack, 3, "소멸, 피해를 2 준다. 연쇄를 모두 사용할 때 까지 반복한다."),
                new CardData(310, "바람11", CardElement.Wind, CardType.Attack, 3, "피해를 3 X 5 준다. 이 카드가 드로우 된 즉시 사용하면 행동 게이지를 소모하지 않는다."),
                new CardData(311, "바람12", CardElement.Wind, CardType.Attack, 3, "피해를 5 X 5 준다."),
                new CardData(312, "바람13", CardElement.Wind, CardType.Skill,  1, "카드를 2장 드로우 한다."),
                new CardData(313, "바람14", CardElement.Wind, CardType.Skill,  2, "다음에 사용하는 카드는 행동 게이지를 소모하지 않는다."),
                new CardData(314, "바람15", CardElement.Wind, CardType.Skill,  2, "연쇄를 20 얻는다."),
                new CardData(315, "바람16", CardElement.Wind, CardType.Skill,  3, "연쇄를 n 만큼 얻는다. (n = 이번 전투에서 사용한 공격 카드 수)"),
                new CardData(316, "바람17", CardElement.Wind, CardType.Skill,  3, "소멸 드로우 4"),
                new CardData(317, "바람18", CardElement.Wind, CardType.Power,  1, "연쇄의 피해량이 1 증가한다."),
                new CardData(318, "바람19", CardElement.Wind, CardType.Power,  2, "피해를 줄 때 마다 연쇄를 1 획득한다."),
                new CardData(319, "바람20", CardElement.Wind, CardType.Power,  3, "적 공격 이후 첫번째로 사용하는 카드는 행동게이지를 소모하지 않는다."),
                new CardData(320, "바람21", CardElement.Wind, CardType.Power,  3, "모든 공격 카드의 타격 횟수가 1번씩 늘어난다."),
                new CardData(321, "바람22", CardElement.Wind, CardType.Power,  3, "카드를 사용할 때 마다 적에게 피해를 3 준다."),

                // ── 땅 (400~421) ──
                new CardData(400, "땅1",  CardElement.Earth, CardType.Attack, 1, "피해를 5 준다."),
                new CardData(401, "땅2",  CardElement.Earth, CardType.Skill,  1, "방어도를 3 얻는다."),
                new CardData(402, "땅3",  CardElement.Earth, CardType.Attack, 1, "피해를 n 만큼 준다. (n = 보유한 방어도)"),
                new CardData(403, "땅4",  CardElement.Earth, CardType.Attack, 1, "피해를 n 만큼 준다. (n = 보유한 방어도)"),
                new CardData(404, "땅5",  CardElement.Earth, CardType.Attack, 2, "피해를 10 준다. 방어도를 보유하고 있지 않다면 방어도를 10 얻는다."),
                new CardData(405, "땅6",  CardElement.Earth, CardType.Attack, 3, "피해를 n 만큼 준다. (n = 패로 들어온 이후 진행된 게이지 수)"),
                new CardData(406, "땅7",  CardElement.Earth, CardType.Attack, 3, "피해를 n 만큼 준다. (n = 이번 전투에서 사용한 파편 카드 수)"),
                new CardData(407, "땅8",  CardElement.Earth, CardType.Skill,  1, "방어도를 5 얻는다."),
                new CardData(408, "땅9",  CardElement.Earth, CardType.Skill,  1, "무작위 파편 카드 3장을 버린 카드 더미에 추가한다."),
                new CardData(409, "땅10", CardElement.Earth, CardType.Skill,  1, "파편 카드 1장을 선택하여 손에 추가한다."),
                new CardData(410, "땅11", CardElement.Earth, CardType.Skill,  2, "뽑을 카드 더미에 서로 다른 파편 3장을 섞어 넣는다."),
                new CardData(411, "땅12", CardElement.Earth, CardType.Skill,  2, "방어도를 5 얻는다, 무작위 파편 카드 1장을 뽑을 카드 더미에 섞어 넣는다."),
                new CardData(412, "땅13", CardElement.Earth, CardType.Skill,  2, "방어도를 n 만큼 얻는다. (n=이번 전투에서 사용한 파편 카드 수)"),
                new CardData(413, "땅14", CardElement.Earth, CardType.Skill,  3, "각 종류별 파편 카드를 손에 추가한다."),
                new CardData(414, "땅15", CardElement.Earth, CardType.Skill,  3, "소멸, 파편을 하나 선택하여 같은 이름의 카드를 버린 카드 더미에 10장 섞어 넣는다."),
                new CardData(415, "땅16", CardElement.Earth, CardType.Skill,  3, "방어도를 n 만큼 획득한다. (n = 패로 들어온 이후 진행된 게이지 수)"),
                new CardData(416, "땅17", CardElement.Earth, CardType.Skill,  3, "소멸, 방어도를 20 얻는다."),
                new CardData(417, "땅18", CardElement.Earth, CardType.Power,  1, "방어도가 소모될 때 마다 무작위 파편 카드를 버린 카드 더미에 추가한다."),
                new CardData(418, "땅19", CardElement.Earth, CardType.Power,  2, "패 최대 한도가 1 늘어난다."),
                new CardData(419, "땅20", CardElement.Earth, CardType.Power,  2, "카드를 사용할 때 마다 방어도를 1 얻는다."),
                new CardData(420, "땅21", CardElement.Earth, CardType.Power,  3, "이번 전투 동안 땅 속성 카드를 사용할 때 무작위 파편 카드를 버린 카드 더미에 추가한다."),
                new CardData(421, "땅22", CardElement.Earth, CardType.Power,  3, "파편 카드 사용 시 버린 카드 더미에 무작위 파편 카드를 1장을 추가한다."),

                // ── 파편 (422~424, 무속성, 전투 한정 생성) ──
                new CardData(FRAGMENT_1, "파편1", CardElement.Neutral, CardType.Skill,  1, "소멸, 방어도를 3 얻는다. 카드를 1장 드로우한다."),
                new CardData(FRAGMENT_2, "파편2", CardElement.Neutral, CardType.Attack, 1, "소멸, 피해를 5 준다. 카드를 1장 드로우한다."),
                new CardData(FRAGMENT_3, "파편3", CardElement.Neutral, CardType.Attack, 1, "소멸, 피해를 8 준다. 방어도를 5 얻는다."),
            };

            _byId = new Dictionary<int, CardData>(_all.Count);
            foreach (var card in _all) _byId[card.id] = card;
        }
    }
}
