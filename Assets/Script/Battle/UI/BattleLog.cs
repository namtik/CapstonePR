using System;
using System.Collections.Generic;
using UnityEngine;
using Battle.Card;

namespace Battle.UI
{
    // 전투 기록. UI_Info 는 1칸, 숫자와 함께 보이는 기록은 2칸. 합이 10칸을 넘으면 왼쪽(가장 오래된 것)부터 밀어낸다.
    public static class BattleLog
    {
        public const int MaxSlots = 10;

        public sealed class Entry
        {
            public readonly int Id;
            public readonly Sprite Icon;
            public readonly Sprite Badge;
            public readonly bool HasNumber;
            public readonly int Number;
            public int Slots => (HasNumber || Badge != null) ? 2 : 1;

            public Entry(int id, Sprite icon, Sprite badge, bool hasNumber, int number)
            {
                Id = id;
                Icon = icon;
                Badge = badge;
                HasNumber = hasNumber;
                Number = number;
            }
        }

        static readonly List<Entry> _entries = new List<Entry>();
        static readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();
        static int _nextId;
        static bool _logDraws = true;
        static bool _logGuard = true;

        public static IReadOnlyList<Entry> Entries => _entries;
        public static event Action Changed;

        public static bool InBattle
        {
            get
            {
                NewBattleController battle = NewBattleController.Instance;
                return battle != null && battle.InBattle;
            }
        }

        public static void Clear()
        {
            if (_entries.Count == 0) return;
            _entries.Clear();
            Changed?.Invoke();
        }

        public static void RecordEnemyAttack(Sprite portrait, int damage)
        {
            if (!InBattle) return;
            Sprite attack = Load("UI/Tab/UI_Icon_Attack");
            if (portrait == null) portrait = attack;
            Push(portrait, attack, true, damage);
        }

        public static void RecordDisruption()
        {
            if (!InBattle) return;
            Push(Load("UI/Tab/UI_Icon_Interference"), null, false, 0);
        }

        public static void RecordDisruption(int number)
        {
            if (!InBattle) return;
            Push(Load("UI/Tab/UI_Icon_Interference"), null, true, number);
        }

        public static void RecordEnemyBurn(Sprite portrait, int damage)
        {
            if (!InBattle || damage <= 0) return;
            Sprite burn = Load("UI/Tab/UI_Icon_Burn");
            if (portrait == null) portrait = burn;
            Push(portrait, burn, true, damage);
        }

        // 카드를 쓰면 피해·방어도를 카드 이미지 한 칸에 붙인다. 둘 다 없으면 카드 이미지만.
        public static void RecordCardPlay(CardData data, int damage, int guardGained)
        {
            if (!InBattle || data == null) return;
            Sprite card = LoadCard(data);
            bool logged = false;
            if (damage > 0)
            {
                Push(card, Load("UI/Tab/UI_Icon_Attack"), true, damage);
                logged = true;
            }
            if (guardGained > 0)
            {
                Push(card, Load("UI/Tab/UI_Icon_Shield"), true, guardGained);
                logged = true;
            }
            if (!logged) Push(card, null, false, 0);
        }

        // 카드 효과 처리 중에는 방어도를 따로 남기지 않고, 카드 기록에 합친다.
        public static void SetGuardLogging(bool enabled)
        {
            _logGuard = enabled;
        }

        // 전투 시작 패 드로우처럼 기록에서 빼야 할 때만 끈다.
        public static void SetDrawLogging(bool enabled)
        {
            _logDraws = enabled;
        }

        public static void RecordDraw(CardData data)
        {
            if (!_logDraws || !InBattle || data == null) return;
            Push(LoadCard(data), Load("UI/Tab/UI_Icon_Draw"), false, 0);
        }

        public static void RecordGuard(int amount)
        {
            if (!_logGuard || !InBattle || amount <= 0) return;
            Push(Load("UI/Tab/UI_Icon_Shield"), null, true, amount);
        }

        public static void RecordRelic(Sprite icon)
        {
            if (!InBattle || icon == null) return;
            Push(icon, null, false, 0);
        }

        static void Push(Sprite icon, Sprite badge, bool hasNumber, int number)
        {
            int cost = (hasNumber || badge != null) ? 2 : 1;
            while (_entries.Count > 0 && UsedSlots() + cost > MaxSlots)
                _entries.RemoveAt(0);

            _entries.Add(new Entry(++_nextId, icon, badge, hasNumber, number));
            Changed?.Invoke();
        }

        static int UsedSlots()
        {
            int used = 0;
            for (int i = 0; i < _entries.Count; i++) used += _entries[i].Slots;
            return used;
        }

        static Sprite LoadCard(CardData data)
        {
            if (data == null || string.IsNullOrEmpty(data.skillImg)) return null;
            return Load("CardIcons/" + data.skillImg);
        }

        static Sprite Load(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            Sprite sprite;
            if (_sprites.TryGetValue(path, out sprite) && sprite != null) return sprite;
            sprite = Resources.Load<Sprite>(path);
            if (sprite != null) _sprites[path] = sprite;
            return sprite;
        }
    }
}
