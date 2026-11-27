using System.Collections.Generic;
using UnityEngine;

namespace Battle.UI
{
    // 도감 발견 기록 — 런이 끝나도 유지되도록 PlayerPrefs에 저장한다(발견 순서 유지)
    public static class CodexProgress
    {
        static readonly DiscoveryList Relics = new DiscoveryList("Codex.DiscoveredRelics"); // 유물 id
        static readonly DiscoveryList Cards = new DiscoveryList("Codex.DiscoveredCards");   // 카드 id
        static readonly DiscoveryList Combos = new DiscoveryList("Codex.DiscoveredCombos"); // 콤보 refComboId

        // ── 유물 ──
        public static int DiscoveredRelicCount => Relics.Count;
        public static bool IsRelicDiscovered(string relicId) => Relics.Contains(relicId);

        // 이미지 전용 임시 유물(sprite_*)은 기록하지 않는다
        public static void MarkRelic(string relicId)
        {
            if (relicId != null && relicId.StartsWith("sprite_")) return;
            Relics.Add(relicId);
        }

        // ── 부적(카드) ──
        public static bool IsCardDiscovered(int cardId) => Cards.Contains(cardId.ToString());
        public static void MarkCard(int cardId) => Cards.Add(cardId.ToString());
        // 발견 순번(0부터, 미발견이면 int.MaxValue) — 도감 획득순 정렬용
        public static int CardDiscoveryOrder(int cardId) => Cards.OrderOf(cardId.ToString());

        // ── 사방비급(콤보) ──
        public static bool IsComboDiscovered(int refComboId) => Combos.Contains(refComboId.ToString());
        public static void MarkCombo(int refComboId) => Combos.Add(refComboId.ToString());

        // 발견 기록 초기화(디버그용)
        public static void ResetRelics() => Relics.Clear();
        public static void ResetAll()
        {
            Relics.Clear();
            Cards.Clear();
            Combos.Clear();
        }

        // PlayerPrefs 키 하나에 ';'로 이어 저장하는 발견 목록(최초 접근 시 로드)
        class DiscoveryList
        {
            const char Separator = ';';

            readonly string _key;
            List<string> _order;        // 발견 순서
            Dictionary<string, int> _index; // id → 발견 순번

            public DiscoveryList(string key) { _key = key; }

            public int Count { get { Load(); return _order.Count; } }

            public bool Contains(string id)
            {
                if (string.IsNullOrWhiteSpace(id)) return false;
                Load();
                return _index.ContainsKey(id);
            }

            public int OrderOf(string id)
            {
                Load();
                return id != null && _index.TryGetValue(id, out int i) ? i : int.MaxValue;
            }

            public void Add(string id)
            {
                if (string.IsNullOrWhiteSpace(id)) return;
                Load();
                if (_index.ContainsKey(id)) return;
                _index[id] = _order.Count;
                _order.Add(id);
                PlayerPrefs.SetString(_key, string.Join(Separator.ToString(), _order));
                PlayerPrefs.Save();
            }

            public void Clear()
            {
                Load();
                _order.Clear();
                _index.Clear();
                PlayerPrefs.DeleteKey(_key);
                PlayerPrefs.Save();
            }

            void Load()
            {
                if (_order != null) return;
                _order = new List<string>();
                _index = new Dictionary<string, int>();
                foreach (var id in PlayerPrefs.GetString(_key, string.Empty).Split(Separator))
                {
                    if (string.IsNullOrWhiteSpace(id) || _index.ContainsKey(id)) continue;
                    _index[id] = _order.Count;
                    _order.Add(id);
                }
            }
        }
    }
}
