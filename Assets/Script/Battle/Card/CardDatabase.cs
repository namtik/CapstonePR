using System;
using System.Collections.Generic;
using UnityEngine;

namespace Battle.Card
{
    /// <summary>
    /// 카드 전체 정의 (Resources/CardDB/Cards.json 기반).
    /// 기획자가 Card_DB.xlsx → Tools/ConvertCardDB.py 로 JSON을 갱신하면 게임에 반영.
    /// 효과 실행은 CardEffectResolver가 id로 분기 (미구현 카드는 효과 없음).
    /// </summary>
    public static class CardDatabase
    {
        // 파편 ID (Card_DB.xlsx 기준 — 501/502/503)
        public const int FRAGMENT_1 = 501;
        public const int FRAGMENT_2 = 502;
        public const int FRAGMENT_3 = 503;

        /// <summary>방해 행동 등으로 덱에 삽입되는 무속성 더미 카드 ID.</summary>
        public const int NEUTRAL_FILLER = 500;

        public static readonly int[] FragmentIds = { FRAGMENT_1, FRAGMENT_2, FRAGMENT_3 };

        const string CARDS_RESOURCE_PATH    = "CardDB/Cards";
        const string EFFECTS_RESOURCE_PATH  = "CardDB/CardEffects";

        private static Dictionary<int, CardData> _byId;
        private static List<CardData> _all;
        private static Dictionary<int, List<CardEffectData>> _effectsByCard;

        public static void EnsureInit()
        {
            if (_byId != null) return;
            LoadAll();
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

        public static IReadOnlyList<CardEffectData> GetEffects(int cardId)
        {
            EnsureInit();
            return _effectsByCard.TryGetValue(cardId, out var list)
                ? list
                : (IReadOnlyList<CardEffectData>)Array.Empty<CardEffectData>();
        }

        // ─────────────────────────────────────────────────────────────
        // 시작 덱 구성
        // ─────────────────────────────────────────────────────────────

        [Serializable]
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
        // 파편/무속성 생성
        // ─────────────────────────────────────────────────────────────

        public static CardInstance CreateFragmentInstance()
        {
            EnsureInit();
            int pick = FragmentIds[UnityEngine.Random.Range(0, FragmentIds.Length)];
            return new CardInstance(_byId[pick], transient: true);
        }

        public static CardInstance CreateFragmentInstance(int fragmentId)
        {
            EnsureInit();
            if (!_byId.ContainsKey(fragmentId)) return null;
            return new CardInstance(_byId[fragmentId], transient: true);
        }

        /// <summary>무속성 더미 카드(500) 생성 — 방해 행동용.</summary>
        public static CardInstance CreateNeutralFillerInstance()
        {
            EnsureInit();
            return _byId.TryGetValue(NEUTRAL_FILLER, out var data)
                ? new CardInstance(data, transient: true)
                : null;
        }

        // ─────────────────────────────────────────────────────────────
        // JSON 로드
        // ─────────────────────────────────────────────────────────────

        static void LoadAll()
        {
            LoadCards();
            LoadEffects();
        }

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

        // JsonUtility 파싱용 DTO (Cards.json 스키마와 1:1).
        [Serializable]
        class CardJsonList
        {
            public List<CardJson> cards = new List<CardJson>();
        }

        [Serializable]
        class CardJson
        {
            public int id;
            public string name;
            public string element;
            public string type;
            public int gauge;
            public string rarity;
            public string[] tags;
            public bool comboSlot;
            public string effectName;
            public string skillImg;
            public string description;
        }
    }
}
