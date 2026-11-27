using System;
using System.Collections.Generic;
using UnityEngine;

namespace Battle.Card
{
    // 전체 카드/효과 정의를 로드·조회하는 정적 데이터베이스
    public static class CardDatabase
    {
        public const int FRAGMENT_1 = 501; // 파편 카드 ID 1
        public const int FRAGMENT_2 = 502; // 파편 카드 ID 2
        public const int FRAGMENT_3 = 503; // 파편 카드 ID 3

        public const int NEUTRAL_FILLER = 500; // 무속성 더미 카드 ID

        public static readonly int[] FragmentIds = { FRAGMENT_1, FRAGMENT_2, FRAGMENT_3 }; // 파편 ID 모음

        // 기본 덱 카드 ID(4속성 공격·방어 8장) — 유물(태초의 서 10004) 대상 판별용
        public static readonly HashSet<int> BasicCardIds = new HashSet<int> { 100, 101, 200, 201, 300, 301, 400, 401 };
        // 지정 ID가 기본 카드인지 여부
        public static bool IsBasicCard(int id) => BasicCardIds.Contains(id);

        // 표시용 설명 — 런 동안 바뀐 카드 수치를 실제 전투 계산(CardEffectResolver)과 같은 순서로 반영한다.
        //  1) 유물 태초의 서(10004): 기본 카드(BasicCardIds) 효과 수치 2배
        //  2) 이벤트(기본 카드 강화): BASIC 태그 카드의 피해·방어도 × BasicCardStatMultiplier
        //  3) 연마(410) 등 카드 1장별 영구 피해: 피해 수치 + 그 장의 누적값(공식으로 곱해지는 단위 수치에는 더하지 않음)
        // 효과 amount에 해당하는 숫자만 바꿔 무관한 숫자(횟수·장수 등)는 건드리지 않는다.
        public static string GetDisplayDescription(CardInstance card) =>
            card == null ? string.Empty : GetDisplayDescription(card.data, card.permanentDamage);

        // 카드 데이터만 있을 때(상점·보상 등 새 카드) — 사본별 영구 피해는 없음
        public static string GetDisplayDescription(CardData card, int permanentDamage = 0)
        {
            if (card == null) return string.Empty;
            string desc = card.description ?? string.Empty;
            if (desc.Length == 0) return desc;

            var run = Battle.RunDeckState.Instance;
            if (run == null) return desc;

            var effects = GetEffects(card.id);
            if (effects == null || effects.Count == 0) return desc;

            bool relicDouble = run.DoubleBasicCardEffects && IsBasicCard(card.id);
            int basicMult = card.HasTag("BASIC") ? Mathf.Max(1, run.BasicCardStatMultiplier) : 1;
            int permDamage = permanentDamage;
            if (!relicDouble && basicMult <= 1 && permDamage == 0) return desc;

            // 효과 순서대로 (원본 수치, 표시 수치) 목록
            var origs = new List<int>();
            var shows = new List<int>();
            bool anyChange = false;
            for (int i = 0; i < effects.Count; i++)
            {
                var e = effects[i];
                if (e == null || e.amount <= 0) continue;
                string verb = (e.doAction ?? string.Empty).Trim().ToUpperInvariant();

                int v = e.amount;
                if (relicDouble) v *= 2;
                if (basicMult > 1 && (verb == "DAMAGE" || verb == "GAIN_BLOCK")) v *= basicMult;
                if (permDamage != 0 && verb == "DAMAGE" && string.IsNullOrEmpty(e.formula)) v += permDamage;

                origs.Add(e.amount);
                shows.Add(v);
                if (v != e.amount) anyChange = true;
            }
            if (!anyChange) return desc;

            // 설명의 숫자를 앞에서부터 효과 순서와 짝지어 바꾼다.
            // (연마 "피해를 10 준다 … 피해 10씩 증가"처럼 같은 숫자가 다른 효과에 쓰여도 해당 효과 자리만 바뀜)
            int next = 0;
            return System.Text.RegularExpressions.Regex.Replace(desc, @"\d+", m =>
            {
                if (!int.TryParse(m.Value, out int n)) return m.Value;
                for (int j = next; j < origs.Count; j++)
                {
                    if (origs[j] != n) continue;
                    next = j + 1;
                    return shows[j].ToString();
                }
                return m.Value;
            });
        }

        const string CARDS_RESOURCE_PATH    = "CardDB/Cards";       // 카드 JSON 리소스 경로
        const string EFFECTS_RESOURCE_PATH  = "CardDB/CardEffects"; // 효과 JSON 리소스 경로

        private static Dictionary<int, CardData> _byId;                         // ID → 카드 데이터 맵
        private static List<CardData> _all;                                     // 전체 카드 목록
        private static Dictionary<int, List<CardEffectData>> _effectsByCard;    // 카드 ID → 효과 목록 맵

        // 최초 1회 로드 보장
        public static void EnsureInit()
        {
            if (_byId != null) return;
            LoadAll();
        }

        // ID로 카드 데이터 조회
        public static CardData GetById(int id)
        {
            EnsureInit();
            return _byId.TryGetValue(id, out var data) ? data : null;
        }

        // 전체 카드 목록
        public static IReadOnlyList<CardData> All
        {
            get { EnsureInit(); return _all; }
        }

        // 카드 ID로 효과 목록 조회
        public static IReadOnlyList<CardEffectData> GetEffects(int cardId)
        {
            EnsureInit();
            return _effectsByCard.TryGetValue(cardId, out var list)
                ? list
                : (IReadOnlyList<CardEffectData>)Array.Empty<CardEffectData>();
        }

        // 사용 시 적 1마리를 직접 지정해야 하는 카드인지(ALL_ENEMIES/RANDOM_ENEMY/PLAYER는 false)
        public static bool RequiresEnemyTarget(int cardId)
        {
            var effects = GetEffects(cardId);
            if (effects == null || effects.Count == 0) return false;
            for (int i = 0; i < effects.Count; i++)
            {
                var e = effects[i];
                if (e == null) continue;
                string when = (e.when ?? "").Trim().ToUpperInvariant();
                if (when.Length > 0 && when != "ON_USE" && when != "ON_USE_BEFORE_GAUGE") continue;
                string tgt = (e.target ?? "").Trim().ToUpperInvariant();
                if (tgt == "ENEMY") return true;
            }
            return false;
        }

        // 덱 구성 한 항목(카드 ID + 수량)
        [Serializable]
        public struct DeckEntry
        {
            public int cardId; // 카드 ID
            public int count;  // 수량

            // 카드 ID와 수량으로 생성
            public DeckEntry(int cardId, int count)
            {
                this.cardId = cardId;
                this.count = count;
            }
        }

        // 테스트 전용 프로토타입 기본 덱 구성 반환
        public static List<DeckEntry> DefaultPrototypeDeckEntries()
        {
            return new List<DeckEntry>
            {
                new DeckEntry(106, 1),
                new DeckEntry(107, 1),
                new DeckEntry(115, 1),
                new DeckEntry(116, 1),
                new DeckEntry(118, 1),
                new DeckEntry(203, 2),
                new DeckEntry(207, 1),
                new DeckEntry(211, 1),
                new DeckEntry(216, 1),
                new DeckEntry(218, 1),
                new DeckEntry(220, 1),
                new DeckEntry(221, 1),
                new DeckEntry(308, 2),
                new DeckEntry(312, 1),
                new DeckEntry(313, 2),
                new DeckEntry(318, 1),
                new DeckEntry(320, 1),
            };
        }

        // 실제 게임 런이 사용하는 기본 시작 덱(4속성 8장) 반환
        public static List<DeckEntry> DefaultStartingDeckEntries()
        {
            return new List<DeckEntry>
            {
                new DeckEntry(100, 1),
                new DeckEntry(101, 1),
                new DeckEntry(200, 1),
                new DeckEntry(201, 1),
                new DeckEntry(300, 1),
                new DeckEntry(301, 1),
                new DeckEntry(400, 1),
                new DeckEntry(401, 1),
            };
        }

        // DeckEntry 목록을 CardInstance 목록으로 변환
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

        // 프로토타입 기본 덱을 인스턴스화해 반환
        public static List<CardInstance> BuildDefaultPrototypeDeck()
        {
            return InstantiateDeck(DefaultPrototypeDeckEntries());
        }

        // 무작위 파편 카드 인스턴스 생성
        public static CardInstance CreateFragmentInstance()
        {
            EnsureInit();
            int pick = FragmentIds[UnityEngine.Random.Range(0, FragmentIds.Length)];
            return new CardInstance(_byId[pick], transient: true);
        }

        // 지정 ID의 파편 카드 인스턴스 생성
        public static CardInstance CreateFragmentInstance(int fragmentId)
        {
            EnsureInit();
            if (!_byId.ContainsKey(fragmentId)) return null;
            return new CardInstance(_byId[fragmentId], transient: true);
        }

        public const int SLOW = 506; // 둔화 카드 ID(가마이타치 특이사항, 콤보 1019 제거 대상)

        // 지정 ID 카드의 이번 전투 한정 인스턴스 생성(없는 ID면 null)
        public static CardInstance CreateTransientInstance(int id)
        {
            EnsureInit();
            return _byId.TryGetValue(id, out var data) ? new CardInstance(data, transient: true) : null;
        }

        // 무속성 더미 카드 인스턴스 생성
        public static CardInstance CreateNeutralFillerInstance()
        {
            EnsureInit();
            return _byId.TryGetValue(NEUTRAL_FILLER, out var data)
                ? new CardInstance(data, transient: true)
                : null;
        }

        public const int CHARM = 505; // 매혹 카드 ID(구미호 특이사항). 사용 시 적 행동 게이지가 가득 참. (501=금속 파편과 충돌 방지로 505 사용)

        // 매혹 카드 인스턴스 생성(런타임 정의 — JSON에 없으면 코드로 구성)
        public static CardInstance CreateCharmInstance()
        {
            EnsureInit();
            if (!_byId.TryGetValue(CHARM, out var data))
            {
                data = new CardData(CHARM, "매혹", CardElement.Neutral, CardType.Skill, 0,
                    "사용 시 적 행동 게이지가 가득 찬다.", comboSlot: false);
                _byId[CHARM] = data;
            }
            return new CardInstance(data, transient: true);
        }

        // 카드/효과 JSON 전체 로드
        static void LoadAll()
        {
            LoadCards();
            LoadEffects();
        }

        // Cards.json 로드 및 파싱
        static void LoadCards()
        {
            _all = new List<CardData>();
            _byId = new Dictionary<int, CardData>();

            var asset = Resources.Load<TextAsset>(CARDS_RESOURCE_PATH);
            if (asset == null)
            {
                Debug.LogError($"[CardDatabase] Resources/{CARDS_RESOURCE_PATH}.json 을 찾을 수 없음. " +
                               "Tools/ConvertCardDB.py 를 실행했는지 확인.");
                return;
            }

            CardJsonList payload;
            try
            {
                payload = JsonUtility.FromJson<CardJsonList>(asset.text);
            }
            catch (Exception e)
            {
                Debug.LogError($"[CardDatabase] Cards.json 파싱 실패: {e.Message}");
                return;
            }

            if (payload?.cards == null) return;

            foreach (var raw in payload.cards)
            {
                var card = new CardData(
                    raw.id,
                    raw.name,
                    ParseElement(raw.element),
                    ParseType(raw.type),
                    raw.gauge,
                    raw.description,
                    raw.tags ?? Array.Empty<string>(),
                    raw.comboSlot,
                    raw.effectName ?? "",
                    raw.skillImg ?? "",
                    ParseRarity(raw.rarity)
                );
                _all.Add(card);
                _byId[card.id] = card;
            }
        }

        // CardEffects.json 로드 및 파싱
        static void LoadEffects()
        {
            _effectsByCard = new Dictionary<int, List<CardEffectData>>();
            var asset = Resources.Load<TextAsset>(EFFECTS_RESOURCE_PATH);
            if (asset == null)
            {
                Debug.LogWarning($"[CardDatabase] Resources/{EFFECTS_RESOURCE_PATH}.json 누락 — 효과 데이터 없이 진행.");
                return;
            }

            CardEffectDataList payload;
            try
            {
                payload = JsonUtility.FromJson<CardEffectDataList>(asset.text);
            }
            catch (Exception e)
            {
                Debug.LogError($"[CardDatabase] CardEffects.json 파싱 실패: {e.Message}");
                return;
            }

            if (payload?.effects == null) return;

            foreach (var eff in payload.effects)
            {
                if (!_effectsByCard.TryGetValue(eff.cardId, out var list))
                {
                    list = new List<CardEffectData>();
                    _effectsByCard[eff.cardId] = list;
                }
                list.Add(eff);
            }
        }

        // 문자열을 CardElement로 파싱
        static CardElement ParseElement(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return CardElement.Neutral;
            switch (raw.Trim().ToUpperInvariant())
            {
                case "FIRE":     return CardElement.Fire;
                case "WATER":    return CardElement.Water;
                case "WIND":     return CardElement.Wind;
                case "EARTH":    return CardElement.Earth;
                case "FRAGMENT": return CardElement.Fragment;
                case "NEUTRAL":  return CardElement.Neutral;
                default:
                    Debug.LogWarning($"[CardDatabase] 알 수 없는 Element '{raw}' → Neutral 처리");
                    return CardElement.Neutral;
            }
        }

        // 문자열을 CardType으로 파싱
        static CardType ParseType(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return CardType.Skill;
            switch (raw.Trim().ToUpperInvariant())
            {
                case "ATTACK": return CardType.Attack;
                case "SKILL":  return CardType.Skill;
                case "POWER":  return CardType.Power;
                default:
                    Debug.LogWarning($"[CardDatabase] 알 수 없는 CardType '{raw}' → Skill 처리");
                    return CardType.Skill;
            }
        }

        // 문자열을 CardRarity로 파싱
        static CardRarity ParseRarity(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return CardRarity.Normal;
            switch (raw.Trim().ToUpperInvariant())
            {
                case "NORMAL": return CardRarity.Normal;
                case "RARE":   return CardRarity.Rare;
                case "EPIC":   return CardRarity.Epic;
                default:
                    Debug.LogWarning($"[CardDatabase] 알 수 없는 Rarity '{raw}' → Normal 처리");
                    return CardRarity.Normal;
            }
        }

        // Cards.json 파싱용 DTO 래퍼
        [Serializable]
        class CardJsonList
        {
            public List<CardJson> cards = new List<CardJson>(); // 카드 원본 리스트
        }

        // Cards.json 한 카드 원본 스키마
        [Serializable]
        class CardJson
        {
            public int id;             // 카드 ID
            public string name;        // 이름
            public string element;     // 속성 문자열
            public string type;        // 유형 문자열
            public int gauge;          // 게이지 코스트
            public string rarity;      // 등급 문자열
            public string[] tags;      // 태그 목록
            public bool comboSlot;     // 콤보 슬롯 여부
            public string effectName;  // 효과명
            public string skillImg;    // 스킬 이미지명
            public string description; // 설명
        }
    }
}
