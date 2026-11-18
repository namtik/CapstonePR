using System;
using System.Collections.Generic;
using UnityEngine;
using Battle.Card;

namespace Battle.UI
{
    // 전투 기록. 한 기록은 패널 한 칸이고, 8칸을 넘으면 왼쪽(가장 오래된 것)부터 밀어낸다.
    public static class BattleLog
    {
        public const int MaxSlots = 8;

        public enum Kind
        {
            Card,
            Draw,
            Enemy,
            Relic,
            Awaken
        }

        // 카드가 더미 사이를 움직인 곳. 팝업은 이 기록에 실제로 움직인 카드만 보여 준다.
        public enum MoveZone
        {
            Deck,
            Draw,
            Hand,
            Graveyard,
            Exile,
            Discard
        }

        public sealed class CardMove
        {
            public readonly MoveZone Zone;
            public readonly CardData Data;

            public CardMove(MoveZone zone, CardData data)
            {
                Zone = zone;
                Data = data;
            }
        }

        public sealed class Entry
        {
            public readonly int Id;
            public readonly Kind Category;
            public readonly Sprite Icon;
            public readonly Sprite Badge;
            public readonly Sprite MonsterNumber;
            public readonly bool HasNumber;
            public readonly int Number;
            public readonly string Caption;
            public readonly List<CardMove> Moves;
            public readonly List<CombatNote> Combat;
            public readonly List<Battle.ComboSkillDef> AwakenCombos = new List<Battle.ComboSkillDef>();
            public int EnemyAliveIndex = -1;
            public int AttackIndex = -1;
            public int DisruptionIndex = -1;
            public bool HasCaption => HasNumber || !string.IsNullOrEmpty(Caption);
            public bool HasMoves => Moves != null && Moves.Count > 0;
            public bool HasCombat => Combat != null && Combat.Count > 0;
            public int Slots => 1;

            public Entry(int id, Kind category, Sprite icon, Sprite badge, bool hasNumber, int number, string caption, Sprite monsterNumber, List<CardMove> moves, List<CombatNote> combat = null)
            {
                Id = id;
                Category = category;
                Icon = icon;
                Badge = badge;
                HasNumber = hasNumber;
                Number = number;
                Caption = caption;
                MonsterNumber = monsterNumber;
                Moves = moves ?? new List<CardMove>();
                Combat = combat ?? new List<CombatNote>();
            }
        }

        public enum CombatKind
        {
            Damage,
            Heal,
            Guard,
            Launcher,
            Burn,
            Freeze
        }

        public sealed class CombatNote
        {
            public readonly bool OnPlayer;
            public readonly int EnemySlot;
            public readonly CombatKind Kind;
            public readonly int Amount;
            public readonly bool Gained;

            public CombatNote(bool onPlayer, int enemySlot, CombatKind kind, int amount, bool gained)
            {
                OnPlayer = onPlayer;
                EnemySlot = enemySlot;
                Kind = kind;
                Amount = amount;
                Gained = gained;
            }
        }

        static readonly List<Entry> _entries = new List<Entry>();
        static readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();
        static readonly List<CardData> _deferredDraws = new List<CardData>();
        static readonly List<CardMove> _moveBuffer = new List<CardMove>();
        static readonly List<CombatNote> _combatBuffer = new List<CombatNote>();
        static int _nextId;
        static int _drawDeferDepth;
        static int _suppressDepth;
        static int _moveTargetId = -1;
        static int _combatTargetId = -1;
        static bool _capturingMoves;
        static bool _capturingCombat;
        static int _awakenEntryId = -1;
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
            _deferredDraws.Clear();
            _moveBuffer.Clear();
            _combatBuffer.Clear();
            _drawDeferDepth = 0;
            _suppressDepth = 0;
            _capturingMoves = false;
            _capturingCombat = false;
            _moveTargetId = -1;
            _combatTargetId = -1;
            _awakenEntryId = -1;
            if (_entries.Count == 0) return;
            _entries.Clear();
            Changed?.Invoke();
        }

        public static int RecordEnemyAttack(Sprite portrait, int damage, int monsterIndex, string enemyName, int attackIndex = -1)
        {
            if (!InBattle) return -1;
            Sprite attack = Load("UI/Tab/UI_Icon_Attack");
            if (portrait == null) portrait = attack;
            int id = Push(Kind.Enemy, portrait, attack, true, damage, enemyName, MonsterMark(monsterIndex), null, TakeCombat());
            Entry entry = Find(id);
            if (entry != null)
            {
                entry.EnemyAliveIndex = monsterIndex;
                entry.AttackIndex = attackIndex;
            }
            return id;
        }

        public static void RecordDisruption(int action, Sprite portrait, int monsterIndex, string enemyName)
        {
            if (!InBattle) return;
            Sprite icon = Load("UI/Tab/UI_Icon_Interference");
            if (portrait == null) portrait = icon;
            int id = Push(Kind.Enemy, portrait, icon, false, 0, enemyName, MonsterMark(monsterIndex));
            Entry entry = Find(id);
            if (entry == null) return;
            entry.EnemyAliveIndex = monsterIndex;
            entry.DisruptionIndex = action;
        }

        public static int RecordEnemyBurn(Sprite portrait, int damage, string enemyName)
        {
            if (!InBattle || damage <= 0) return -1;
            Sprite burn = Load("UI/Tab/UI_Icon_Burn");
            if (portrait == null) portrait = burn;
            int id = Push(Kind.Enemy, portrait, burn, true, damage, enemyName, null);
            return id;
        }

        // 카드를 쓰면 피해·방어도를 카드 이미지 한 칸에 붙인다. 둘 다 없으면 카드 이미지만.
        // 효과로 움직인 카드는 같은 목록을 나눠 갖고, 호버 팝업이 그 목록을 읽는다.
        public static int RecordCardPlay(CardData data, int damage, int guardGained)
        {
            List<CardMove> moves = TakeMoves();
            List<CombatNote> combat = TakeCombat();
            if (!InBattle || data == null || _suppressDepth > 0) return -1;
            Sprite card = LoadCard(data);
            string cardName = data.displayName;
            int id = -1;
            bool logged = false;
            if (damage > 0)
            {
                id = Push(Kind.Card, card, Load("UI/Tab/UI_Icon_Attack"), true, damage, cardName, null, moves, combat);
                logged = true;
            }
            if (guardGained > 0)
            {
                id = Push(Kind.Card, card, Load("UI/Tab/UI_Icon_Shield"), true, guardGained, cardName, null, moves, combat);
                logged = true;
            }
            if (!logged) id = Push(Kind.Card, card, null, false, 0, cardName, null, moves, combat);
            _moveTargetId = id;
            _combatTargetId = id;
            return id;
        }

        public static int MoveTargetId => _moveTargetId;

        // 이 카드의 효과가 더미를 만지는 동안 움직임을 모은다.
        public static void BeginMoveCapture()
        {
            _capturingMoves = true;
            _moveBuffer.Clear();
            BeginCombatCapture();
        }

        public static void SetMoveTarget(int entryId)
        {
            _capturingMoves = false;
            _moveTargetId = entryId;
            _capturingCombat = false;
            _combatTargetId = entryId;
        }

        public static void CloseMoveTarget()
        {
            _capturingMoves = false;
            _moveTargetId = -1;
            _moveBuffer.Clear();
            _capturingCombat = false;
            _combatTargetId = -1;
            _combatBuffer.Clear();
        }

        public static void BeginCombatCapture()
        {
            _capturingCombat = true;
            _combatBuffer.Clear();
        }

        public static void NotePlayer(CombatKind kind, int amount, bool gained)
        {
            NoteCombat(true, -1, kind, amount, gained);
        }

        public static void NoteEnemy(EnemyController enemy, CombatKind kind, int amount, bool gained)
        {
            int slot = enemy != null ? EnemyCombatParty.AliveIndexOf(enemy) : -1;
            NoteCombat(false, slot, kind, amount, gained);
        }

        public static void AppendCombat(int entryId, bool onPlayer, int enemySlot, CombatKind kind, int amount, bool gained)
        {
            if (amount < 0) return;
            if (amount == 0 && kind != CombatKind.Damage) return;
            Entry entry = Find(entryId);
            if (entry == null) return;
            entry.Combat.Add(new CombatNote(onPlayer, enemySlot, kind, amount, gained));
        }

        static void NoteCombat(bool onPlayer, int enemySlot, CombatKind kind, int amount, bool gained)
        {
            if (_suppressDepth > 0) return;
            if (amount < 0) return;
            if (amount == 0 && kind != CombatKind.Damage) return;
            var note = new CombatNote(onPlayer, enemySlot, kind, amount, gained);
            if (_capturingCombat)
            {
                _combatBuffer.Add(note);
                return;
            }
            Entry entry = Find(_combatTargetId);
            if (entry == null) return;
            entry.Combat.Add(note);
        }

        static List<CombatNote> TakeCombat()
        {
            var notes = new List<CombatNote>(_combatBuffer);
            _combatBuffer.Clear();
            _capturingCombat = false;
            return notes;
        }

        public static void NoteMove(MoveZone zone, CardInstance card)
        {
            if (_suppressDepth > 0 || card == null || card.data == null) return;
            var move = new CardMove(zone, card.data);
            if (_capturingMoves)
            {
                _moveBuffer.Add(move);
                return;
            }
            Entry entry = Find(_moveTargetId);
            if (entry == null) return;
            entry.Moves.Add(move);
        }

        public static Entry Find(int id)
        {
            for (int i = 0; i < _entries.Count; i++)
                if (_entries[i].Id == id) return _entries[i];
            return null;
        }

        static List<CardMove> TakeMoves()
        {
            var moves = new List<CardMove>(_moveBuffer);
            _moveBuffer.Clear();
            _capturingMoves = false;
            return moves;
        }

        // 카드 효과 처리 중에는 방어도를 따로 남기지 않고, 카드 기록에 합친다.
        public static void SetGuardLogging(bool enabled)
        {
            _logGuard = enabled;
        }

        // 각성 진입처럼, 켜 둔 동안 생기는 기록은 남기지 않는다.
        public static void BeginSuppress()
        {
            _suppressDepth++;
        }

        public static void EndSuppress()
        {
            if (_suppressDepth > 0) _suppressDepth--;
        }

        // 전투 시작 패 드로우처럼 기록에서 빼야 할 때만 끈다.
        public static void SetDrawLogging(bool enabled)
        {
            _logDraws = enabled;
        }

        // 카드 사용 로그 뒤로 보내기 위해 드로우 기록을 보류한다.
        public static void BeginDeferDraws()
        {
            _drawDeferDepth++;
        }

        public static void EndDeferDraws()
        {
            if (_drawDeferDepth > 0) _drawDeferDepth--;
        }

        // 보류한 효과 드로우는 로그에 남기지 않는다. 뽑힌 카드는 그 카드 기록의 팝업에만 나온다.
        public static void FlushDeferredDraws()
        {
            _deferredDraws.Clear();
        }

        public static void RecordDraw(CardData data, bool playerCommand = false)
        {
            if (!_logDraws || !InBattle || data == null) return;
            // 카드 효과가 뽑은 것은 그 카드의 팝업으로만 보여 준다.
            if (_drawDeferDepth > 0 || _capturingMoves || _moveTargetId >= 0) return;
            PushDraw(data, playerCommand);
        }

        static void PushDraw(CardData data, bool playerCommand)
        {
            if (!InBattle || data == null) return;
            string caption = playerCommand ? "드로우" : data.displayName;
            var moves = new List<CardMove> { new CardMove(MoveZone.Draw, data) };
            Push(Kind.Draw, null, null, false, 0, caption, null, moves);
        }

        public static void RecordGuard(int amount)
        {
            if (!_logGuard || !InBattle || amount <= 0) return;
            Push(Kind.Card, Load("UI/Tab/UI_Icon_Shield"), null, true, amount, "방어도", null);
        }

        public static void RecordAwaken(Sprite icon)
        {
            if (!InBattle) return;
            if (icon == null) icon = Load("UI/Tab/각성");
            if (icon == null) icon = Load("UI/AwakenGauge/awaken_icon");
            _awakenEntryId = Push(Kind.Awaken, icon, null, false, 0, "각성", null);
        }

        public static void NoteAwakenCombo(Battle.ComboSkillDef skill)
        {
            if (skill == null) return;
            Entry entry = Find(_awakenEntryId);
            if (entry == null) return;
            entry.AwakenCombos.Add(skill);
        }

        public static void RecordRelic(Sprite icon, string relicName)
        {
            if (!InBattle || icon == null) return;
            Push(Kind.Relic, icon, null, false, 0, relicName, null);
        }

        static Sprite MonsterMark(int monsterIndex)
        {
            if (monsterIndex < 0 || monsterIndex > 2) return null;
            return Load("UI/Tab/UI_IconNum" + (monsterIndex + 1));
        }

        static int Push(Kind category, Sprite icon, Sprite badge, bool hasNumber, int number, string caption, Sprite monsterNumber, List<CardMove> moves = null, List<CombatNote> combat = null)
        {
            if (_suppressDepth > 0) return -1;
            while (_entries.Count > 0 && UsedSlots() + 1 > MaxSlots)
                _entries.RemoveAt(0);

            int id = ++_nextId;
            _entries.Add(new Entry(id, category, icon, badge, hasNumber, number, caption, monsterNumber, moves, combat));
            Changed?.Invoke();
            return id;
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
