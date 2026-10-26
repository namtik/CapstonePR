using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Battle.Relic;

namespace Battle.UI
{
    // 적 행동 종류 — 새 행동(방해행동 세부 종류 등)은 여기에 추가하고 actionIcons에 아이콘을 등록한다
    public enum EnemyActionKind { Attack = 0, Disrupt = 1 }

    // 행동 종류별 아이콘·색 — 인스펙터에서 언제든 추가/교체
    [Serializable]
    public class EnemyActionIcon
    {
        public EnemyActionKind kind;
        public Sprite icon;                   // 비우면 fallbackText 글자 표시
        public Color color = Color.white;     // 칸 테두리·배지 색
        public string fallbackText = "?";     // 아이콘 없을 때 글자
    }

    // 행동 순서 바 — 화면 좌측.
    // 맨 위 '현재' 칸 = 지금 행동하는 쪽(내 턴 / 행동 중인 적), 그 아래 = 몬스터별 행(초상화 + 다음 행동 칸들).
    // 행 순서는 전투 등록 순서로 고정, 가장 먼저 올 행동 칸은 빛나는 테두리로 강조한다.
    // 적 행동 시점은 행동 게이지(플레이어 행동마다 1단계)로 시뮬레이션해 예측한다(빙결 스택 반영).
    public class TurnOrderTimeline : MonoBehaviour
    {
        [Header("배치 (좌상단 기준)")]
        [SerializeField] private Vector2 anchoredOffset = new Vector2(24f, -150f); // 바 시작 위치
        [SerializeField] private float currentSize = 96f;   // '현재' 칸 크기
        [Tooltip("현재 칸 오른쪽 이름 라벨 폭")]
        [SerializeField] private float labelWidth = 90f;    // 이름 라벨 폭
        [SerializeField] private float portraitSize = 80f;  // 몬스터 행 초상화 크기
        [SerializeField] private float actionSize = 60f;    // 행동 칸 크기
        [SerializeField] private float rowSpacing = 10f;    // 행 간격
        [SerializeField] private float cellSpacing = 10f;   // 행동 칸 간격
        [SerializeField] private int actionsPerRow = 2;     // 몬스터별 표시할 다음 행동 수
        [Tooltip("몬스터 수가 적어도 이 행 수만큼 패널 높이를 고정(더 많으면 늘어남)")]
        [SerializeField] private int reservedRows = 3;      // 고정 몬스터 행 수
        [Tooltip("방해행동(게이지 절반)도 행동 칸에 표시")]
        [SerializeField] private bool showDisruptions = true;
        [SerializeField] private float slideSpeed = 12f;    // 위치 보간 속도

        [Header("행동 아이콘 (종류별)")]
        [SerializeField] private List<EnemyActionIcon> actionIcons = new List<EnemyActionIcon>
        {
            new EnemyActionIcon { kind = EnemyActionKind.Attack,  color = new Color(0.95f, 0.34f, 0.30f), fallbackText = "공" },
            new EnemyActionIcon { kind = EnemyActionKind.Disrupt, color = new Color(0.70f, 0.45f, 0.98f), fallbackText = "?" },
        };
        [Tooltip("공격 칸에 예상 피해 표시")]
        [SerializeField] private bool showDamage = true;

        [Header("다음 행동 강조")]
        [SerializeField] private Color nextGlowColor = new Color(1f, 0.80f, 0.38f); // 테두리 빛 색
        [Tooltip("가장 먼저 올 행동 칸 모서리에 붙일 표식(비우면 빛나는 테두리만).")]
        [SerializeField] private Sprite nextMarker;
        [SerializeField] private float glowPulseSpeed = 3f; // 빛 깜빡임 속도

        [Header("아이템(유물)")]
        [SerializeField] private bool showItems = true;     // 몬스터 행 아래 유물 칸 표시
        [Tooltip("전투형 유물만 표시(비전투형 — 상점 할인·최대 체력 등은 제외)")]
        [SerializeField] private bool combatItemsOnly = true;
        [SerializeField] private float itemSize = 56f;      // 아이템 칸 크기
        [SerializeField] private float itemSpacing = 8f;    // 아이템 칸 간격
        [SerializeField] private int itemsPerRow = 4;       // 한 줄 아이템 수
        [Tooltip("유물 발동 시 현재 칸에 표시되는 시간(초) — 여러 개 동시 발동 시 순서대로")]
        [SerializeField] private float itemShowSeconds = 0.9f;
        [SerializeField] private Color itemColor = new Color(0.85f, 0.72f, 0.40f); // 아이템 테두리·발동 색

        [Header("행 상태")]
        [Tooltip("다른 적이 행동 중일 때 나머지 행 투명도")]
        [SerializeField, Range(0f, 1f)] private float idleRowAlpha = 0.55f;

        [Header("스타일")]
        [Tooltip("비우면 씬의 다른 TMP 텍스트 폰트를 사용(한글 폰트 확보용).")]
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Sprite playerPortrait;     // 내 턴 초상화(비우면 '나' 글자)
        [SerializeField] private Color playerColor = new Color(0.33f, 0.70f, 1f);
        [SerializeField] private Color attackColor = new Color(0.95f, 0.34f, 0.30f); // '적 턴' 헤더 색
        [SerializeField] private Color panelColor = new Color(0f, 0f, 0f, 0.35f);

        const int MaxSimSteps = 400; // 예측 시뮬레이션 상한(빙결 스택이 매우 클 때 무한 루프 방지)
        const float HeaderH = 34f;

        // 예측된 행동 하나
        struct Prediction
        {
            public EnemyActionKind kind;
            public int steps;      // 몇 단계(플레이어 행동) 후 발동하는지, -1=대기 중(곧 행동)
            public int damage;     // 공격 예상 피해(-1=표시 안 함)
        }

        // 행동 칸 화면 오브젝트
        class CellView
        {
            public RectTransform rect;
            public Image glow;
            public Image frame;
            public Image icon;
            public TextMeshProUGUI fallback;
            public Image badgeBg;
            public TextMeshProUGUI badgeText;
            public RectTransform dmgPill;
            public TextMeshProUGUI dmgText;
            public Image marker;
        }

        // 몬스터 행 화면 오브젝트
        class RowView
        {
            public int id;
            public EnemyController enemy;
            public RectTransform rect;
            public CanvasGroup group;
            public Image highlight;
            public Image frame;
            public Image portrait;
            public TextMeshProUGUI initial;
            public CellView[] cells;
            public readonly List<Prediction> preds = new List<Prediction>();
            public bool used;
        }

        // 아이템(유물) 칸 화면 오브젝트
        class ItemView
        {
            public RelicSO relic;
            public RectTransform rect;
            public Image frame;
            public Image icon;
            public TextMeshProUGUI fallback;
            public Image badgeBg;
            public TextMeshProUGUI badgeText;
            public float flash;    // 발동 펄스(1→0)
        }

        RectTransform _root;
        RectTransform _rows;   // 행 컨테이너
        RectTransform _items;  // 아이템 컨테이너
        RectTransform _itemDivider;
        readonly List<ItemView> _itemViews = new List<ItemView>();
        readonly List<RelicSO> _itemBuf = new List<RelicSO>();
        readonly Queue<RelicSO> _triggerQueue = new Queue<RelicSO>(); // 현재 칸에 띄울 발동 유물 대기열
        RelicSO _showingRelic;
        float _showingUntil;
        Image _panel;
        TextMeshProUGUI _header;
        TextMeshProUGUI _lockHint;
        RectTransform _divider;

        // 현재 칸
        RectTransform _current;
        Image _curFrame;
        Image _curPortrait;
        TextMeshProUGUI _curInitial;
        Image _curBadgeBg;
        Image _curBadgeIcon;
        TextMeshProUGUI _curBadgeText;
        TextMeshProUGUI _curLabel;
        int _curId = int.MinValue;
        float _curPulse;

        readonly Dictionary<int, RowView> _views = new Dictionary<int, RowView>();
        readonly List<RowView> _order = new List<RowView>(); // 고정 행 순서(살아있는 행)
        readonly List<RowView> _dying = new List<RowView>();
        readonly List<EnemyController> _alive = new List<EnemyController>();
        readonly HashSet<EnemyController> _pending = new HashSet<EnemyController>();
        readonly List<int> _removeBuf = new List<int>();
        bool _built;
        bool _wasEnemyTurn;
        float _headerPulse;
        float _lockFlash;

        static Sprite _roundedSprite;
        static Sprite _circleSprite;

        void Awake() => Build();

        // 적 턴에 카드를 쓰려 할 때 '적 턴' 표시를 깜빡여 잠금을 알린다
        public void FlashLocked() => _lockFlash = 1f;

        // 유물 발동 — 현재 칸에 순서대로 띄우고 해당 아이템 칸을 펄스
        public void ShowRelicTrigger(RelicSO relic)
        {
            if (relic == null || !showItems) return;
            _triggerQueue.Enqueue(relic);
            for (int i = 0; i < _itemViews.Count; i++)
                if (_itemViews[i].relic == relic) _itemViews[i].flash = 1f;
        }

        // ── 매 프레임: 예측 → 배치 ─────────────────────────────
        void LateUpdate()
        {
            var nbc = NewBattleController.Instance;
            bool show = nbc != null && nbc.InBattle && EnemyCombatParty.AliveCount > 0;
            if (_root.gameObject.activeSelf != show) _root.gameObject.SetActive(show);
            if (!show) return;

            UpdateHeader(nbc);
            UpdateCurrent(nbc);
            SyncRows();
            BuildPredictions(nbc);
            SyncItems(nbc);
            ApplyLayout(nbc);
        }

        // ── 현재 칸 ─────────────────────────────────────────────
        void UpdateCurrent(NewBattleController nbc)
        {
            // 유물 발동 표시가 대기 중이면 현재 칸을 잠시 그 유물로 바꾼다(여러 개면 순서대로)
            if (_showingRelic != null && Time.unscaledTime >= _showingUntil) _showingRelic = null;
            if (_showingRelic == null && _triggerQueue.Count > 0)
            {
                _showingRelic = _triggerQueue.Dequeue();
                _showingUntil = Time.unscaledTime + itemShowSeconds;
            }

            EnemyController acting = nbc.ActingEnemy;
            bool enemyActing = acting != null && acting.IsAlive;
            int id = _showingRelic != null ? _showingRelic.GetInstanceID()
                   : enemyActing ? acting.GetInstanceID() * 2 + (nbc.ActingIsDisruption ? 1 : 0) : int.MinValue + 1;
            if (id != _curId) { _curId = id; _curPulse = 1f; }
            _curPulse = Mathf.MoveTowards(_curPulse, 0f, Time.deltaTime * 3f);

            Sprite portrait;
            string name;
            Color c;
            if (_showingRelic != null)
            {
                portrait = _showingRelic.icon;
                name = _showingRelic.DisplayLabel;
                c = itemColor;
                _curLabel.text = $"<b>{name}</b>\n<size=70%>발동</size>";
                _curBadgeBg.gameObject.SetActive(false);
            }
            else if (enemyActing)
            {
                EnemyActionKind kind = nbc.ActingIsDisruption ? EnemyActionKind.Disrupt : EnemyActionKind.Attack;
                EnemyActionIcon entry = FindIcon(kind);
                c = KindColor(kind);
                GetEnemyInfo(acting, out portrait, out name);
                _curLabel.text = $"<b>{name}</b>";

                // 행동 종류 배지(아이콘)
                _curBadgeBg.gameObject.SetActive(true);
                _curBadgeBg.color = c;
                bool hasIcon = entry != null && entry.icon != null;
                _curBadgeIcon.enabled = hasIcon;
                if (hasIcon) _curBadgeIcon.sprite = entry.icon;
                _curBadgeText.gameObject.SetActive(!hasIcon);
                if (!hasIcon) _curBadgeText.text = entry != null ? entry.fallbackText : "!";
            }
            else
            {
                portrait = playerPortrait;
                name = "나";
                c = playerColor;
                _curLabel.text = "<b>내 턴</b>";
                _curBadgeBg.gameObject.SetActive(false);
            }

            _curFrame.color = c;
            _curPortrait.enabled = portrait != null;
            if (portrait != null && _curPortrait.sprite != portrait) _curPortrait.sprite = portrait;
            _curInitial.gameObject.SetActive(portrait == null);
            if (portrait == null) _curInitial.text = string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1);

            float s = 1f + _curPulse * 0.12f;
            _current.localScale = new Vector3(s, s, 1f);
        }

        // ── 몬스터 행: 등록 순서 고정, 새 적은 뒤에 추가, 사라진 적은 페이드 아웃 ──
        void SyncRows()
        {
            foreach (var v in _views.Values) v.used = false;
            EnemyCombatParty.CollectAlive(_alive);
            for (int i = 0; i < _alive.Count; i++)
            {
                EnemyController e = _alive[i];
                int id = e.GetInstanceID();
                if (!_views.TryGetValue(id, out RowView v))
                {
                    v = CreateRow(id, e);
                    _views[id] = v;
                    _order.Add(v);
                }
                v.used = true;
            }

            _removeBuf.Clear();
            foreach (var kv in _views)
                if (!kv.Value.used) _removeBuf.Add(kv.Key);
            for (int i = 0; i < _removeBuf.Count; i++)
            {
                RowView v = _views[_removeBuf[i]];
                _views.Remove(_removeBuf[i]);
                _order.Remove(v);
                _dying.Add(v);
            }
        }

        // 각 몬스터의 다음 행동들을 게이지 시뮬레이션으로 예측
        void BuildPredictions(NewBattleController nbc)
        {
            _pending.Clear();
            foreach (EnemyController e in nbc.PendingAttackers)
                if (e != null) _pending.Add(e);

            int want = Mathf.Max(1, actionsPerRow);
            for (int r = 0; r < _order.Count; r++)
            {
                RowView row = _order[r];
                row.preds.Clear();
                EnemyController e = row.enemy;
                if (e == null) continue;
                int attacksAhead = 0;

                // 이미 게이지가 차서 공격 대기 중
                if (_pending.Contains(e))
                    row.preds.Add(new Prediction { kind = EnemyActionKind.Attack, steps = -1, damage = e.GetPredictedAttackDamage(attacksAhead++) });

                EnemyStat st = e.GetComponent<EnemyStat>();
                if (st == null) continue;
                int step = st.GaugeStep;
                int max = Mathf.Max(1, st.GaugeMaxSteps);
                bool mid = st.MidPatternTriggered;
                int frost = st.statusEffects != null && st.statusEffects.TryGetValue("frost", out int f) ? f : 0;

                // 플레이어 행동 1회마다 게이지 +1(빙결 스택이 있으면 스택만 1 소모)
                for (int k = 1; k <= MaxSimSteps && row.preds.Count < want; k++)
                {
                    if (frost > 0) { frost--; continue; }
                    step++;
                    if (step == max / 2 && !mid)
                    {
                        mid = true;
                        if (showDisruptions)
                            row.preds.Add(new Prediction { kind = EnemyActionKind.Disrupt, steps = k, damage = -1 });
                    }
                    if (step >= max && row.preds.Count < want)
                    {
                        row.preds.Add(new Prediction { kind = EnemyActionKind.Attack, steps = k, damage = e.GetPredictedAttackDamage(attacksAhead++) });
                        step = 0;
                        mid = false;
                    }
                }
            }
        }

        // 보유 유물 → 아이템 칸 동기화(목록이 바뀔 때만 재생성) + 카운터·발동 펄스 갱신
        void SyncItems(NewBattleController nbc)
        {
            _itemBuf.Clear();
            var mgr = RelicManager.Instance;
            if (showItems && mgr != null)
            {
                var owned = mgr.OwnedRelics;
                for (int i = 0; i < owned.Count; i++)
                {
                    RelicSO r = owned[i];
                    if (r == null || (combatItemsOnly && r.category != RelicCategory.Combat)) continue;
                    _itemBuf.Add(r);
                }
            }

            bool changed = _itemBuf.Count != _itemViews.Count;
            for (int i = 0; !changed && i < _itemBuf.Count; i++)
                changed = _itemViews[i].relic != _itemBuf[i];
            if (changed)
            {
                for (int i = 0; i < _itemViews.Count; i++) Destroy(_itemViews[i].rect.gameObject);
                _itemViews.Clear();
                int cols = Mathf.Max(1, itemsPerRow);
                for (int i = 0; i < _itemBuf.Count; i++)
                {
                    float x = (i % cols) * (itemSize + itemSpacing);
                    float y = -(i / cols) * (itemSize + itemSpacing);
                    _itemViews.Add(CreateItem(_itemBuf[i], new Vector2(x + itemSize * 0.5f, y - itemSize * 0.5f)));
                }
            }

            float dt = Time.deltaTime;
            for (int i = 0; i < _itemViews.Count; i++)
            {
                ItemView v = _itemViews[i];
                int counter = v.relic.GetBattleCounter(nbc);
                bool hasCounter = counter >= 0;
                if (v.badgeBg.gameObject.activeSelf != hasCounter) v.badgeBg.gameObject.SetActive(hasCounter);
                if (hasCounter) v.badgeText.text = counter.ToString();

                v.flash = Mathf.MoveTowards(v.flash, 0f, dt * 2f);
                v.frame.color = Color.Lerp(itemColor, Color.white, v.flash);
                float s = 1f + v.flash * 0.18f;
                v.rect.localScale = new Vector3(s, s, 1f);
            }
        }

        // 상단 '내 턴 / 적 턴' 표시 — 턴이 바뀌는 순간 펄스
        void UpdateHeader(NewBattleController nbc)
        {
            bool enemyTurn = nbc.IsEnemyTurn && !nbc.IsAwakenActive;
            if (enemyTurn != _wasEnemyTurn) { _headerPulse = 1f; _wasEnemyTurn = enemyTurn; }
            _headerPulse = Mathf.MoveTowards(_headerPulse, 0f, Time.deltaTime * 2.5f);
            _lockFlash = Mathf.MoveTowards(_lockFlash, 0f, Time.deltaTime * 2f);

            if (nbc.IsAwakenActive)
            {
                _header.text = "각성";
                _header.color = new Color(1f, 0.85f, 0.3f);
            }
            else
            {
                _header.text = enemyTurn ? "적 턴" : "내 턴";
                _header.color = Color.Lerp(enemyTurn ? attackColor : playerColor, Color.white, _lockFlash * 0.8f);
            }
            float s = 1f + _headerPulse * 0.25f + _lockFlash * 0.12f;
            _header.rectTransform.localScale = new Vector3(s, s, 1f);

            _lockHint.gameObject.SetActive(_lockFlash > 0.01f);
            if (_lockHint.gameObject.activeSelf)
                _lockHint.color = new Color(1f, 1f, 1f, _lockFlash);
        }

        // 행 배치·내용 갱신, 사라지는 행 처리, 패널 크기 맞춤
        void ApplyLayout(NewBattleController nbc)
        {
            float dt = Time.deltaTime;
            float t = 1f - Mathf.Exp(-slideSpeed * dt);
            EnemyController acting = nbc.ActingEnemy;

            // 가장 먼저 올 행동 = 남은 단계가 가장 적은 칸(동률이면 위 행 우선)
            int nextRow = -1, nextCell = -1, best = int.MaxValue;
            for (int r = 0; r < _order.Count; r++)
            {
                var preds = _order[r].preds;
                for (int c = 0; c < preds.Count; c++)
                    if (preds[c].steps < best) { best = preds[c].steps; nextRow = r; nextCell = c; }
            }
            float glow = 0.55f + 0.45f * Mathf.Sin(Time.time * glowPulseSpeed);

            float y = 0f;
            for (int r = 0; r < _order.Count; r++)
            {
                RowView row = _order[r];
                bool isActing = acting != null && row.enemy == acting;

                Vector2 target = new Vector2(0f, y);
                if (row.group.alpha <= 0f) row.rect.anchoredPosition = target - new Vector2(0f, 20f); // 새 행은 아래에서 등장
                row.rect.anchoredPosition = Vector2.Lerp(row.rect.anchoredPosition, target, t);
                float targetAlpha = acting != null && !isActing ? idleRowAlpha : 1f;
                row.group.alpha = Mathf.MoveTowards(row.group.alpha, targetAlpha, dt * 5f);
                FillRow(row, isActing, nbc.ActingIsDisruption, r == nextRow ? nextCell : -1, glow);

                y -= portraitSize + rowSpacing;
            }

            // 죽은 적의 행 — 왼쪽으로 빠지며 사라지고, 아래 행들이 위로 당겨진다
            for (int i = _dying.Count - 1; i >= 0; i--)
            {
                RowView v = _dying[i];
                v.group.alpha = Mathf.MoveTowards(v.group.alpha, 0f, dt * 3f);
                v.rect.anchoredPosition += new Vector2(-220f * dt, 0f);
                if (v.group.alpha <= 0f)
                {
                    Destroy(v.rect.gameObject);
                    _dying.RemoveAt(i);
                }
            }

            float rowsTop = HeaderH + currentSize + rowSpacing * 2f;
            // 몬스터 수와 관계없이 최대 행 수만큼 자리를 잡아 패널 크기·아이템 위치를 고정
            int rowSlots = Mathf.Max(_order.Count, reservedRows);
            float rowsH = rowSlots > 0 ? rowSlots * (portraitSize + rowSpacing) - rowSpacing : 0f;
            float targetH = rowsTop + rowsH + 10f;

            // 아이템 구역 — 몬스터 행 아래 구분선 다음, 행이 줄면 함께 위로 당겨진다
            bool hasItems = _itemViews.Count > 0;
            if (_items.gameObject.activeSelf != hasItems) _items.gameObject.SetActive(hasItems);
            _itemDivider.gameObject.SetActive(hasItems);
            if (hasItems)
            {
                int cols = Mathf.Max(1, itemsPerRow);
                int lines = (_itemViews.Count + cols - 1) / cols;
                float itemsTop = rowsTop + rowsH + rowSpacing * 2f;
                Vector2 ip = new Vector2(0f, -itemsTop);
                _items.anchoredPosition = _items.anchoredPosition == Vector2.zero ? ip : Vector2.Lerp(_items.anchoredPosition, ip, t);
                _itemDivider.anchoredPosition = new Vector2(0f, _items.anchoredPosition.y + rowSpacing);
                targetH = itemsTop + lines * (itemSize + itemSpacing) - itemSpacing + 10f;
            }

            float w = Mathf.Max(currentSize + labelWidth, portraitSize + Mathf.Max(1, actionsPerRow) * (actionSize + cellSpacing));
            if (hasItems) w = Mathf.Max(w, Mathf.Max(1, itemsPerRow) * (itemSize + itemSpacing) - itemSpacing);
            w += 22f;
            _divider.sizeDelta = new Vector2(w - 22f, 2f);
            _itemDivider.sizeDelta = _divider.sizeDelta;
            Vector2 size = _panel.rectTransform.sizeDelta;
            _panel.rectTransform.sizeDelta = new Vector2(w, size.y <= 0f ? targetH : Mathf.Lerp(size.y, targetH, t));
        }

        // 한 행의 초상화·행동 칸 채우기
        void FillRow(RowView row, bool isActing, bool actingDisrupt, int nextCell, float glow)
        {
            GetEnemyInfo(row.enemy, out Sprite portrait, out string name);
            row.portrait.enabled = portrait != null;
            if (portrait != null && row.portrait.sprite != portrait) row.portrait.sprite = portrait;
            row.initial.gameObject.SetActive(portrait == null);
            if (portrait == null) row.initial.text = name.Substring(0, 1);

            // 행동 중인 행 — 배경 강조 + 테두리 행동 색
            Color actColor = KindColor(actingDisrupt ? EnemyActionKind.Disrupt : EnemyActionKind.Attack);
            Color hl = isActing ? new Color(actColor.r, actColor.g, actColor.b, 0.35f) : new Color(1f, 1f, 1f, 0f);
            row.highlight.color = Color.Lerp(row.highlight.color, hl, 0.25f);
            row.frame.color = isActing ? actColor : new Color(0.75f, 0.75f, 0.8f, 0.9f);

            for (int c = 0; c < row.cells.Length; c++)
            {
                CellView cell = row.cells[c];
                bool has = c < row.preds.Count;
                if (cell.rect.gameObject.activeSelf != has) cell.rect.gameObject.SetActive(has);
                if (!has) continue;
                FillCell(cell, row.preds[c], c == nextCell, glow);
            }
        }

        void FillCell(CellView cell, Prediction p, bool isNext, float glow)
        {
            EnemyActionIcon entry = FindIcon(p.kind);
            Color c = KindColor(p.kind);
            cell.frame.color = c;

            bool hasIcon = entry != null && entry.icon != null;
            cell.icon.enabled = hasIcon;
            if (hasIcon && cell.icon.sprite != entry.icon) cell.icon.sprite = entry.icon;
            cell.fallback.gameObject.SetActive(!hasIcon);
            if (!hasIcon) { cell.fallback.text = entry != null ? entry.fallbackText : "?"; cell.fallback.color = c; }

            cell.badgeBg.color = c;
            cell.badgeText.text = p.steps < 0 ? "!" : p.steps.ToString();

            bool dmg = showDamage && p.damage >= 0;
            if (cell.dmgPill.gameObject.activeSelf != dmg) cell.dmgPill.gameObject.SetActive(dmg);
            if (dmg) cell.dmgText.text = p.damage.ToString();

            // 다음 행동 강조 — 빛나는 테두리 + (선택) 표식
            cell.glow.gameObject.SetActive(isNext);
            if (isNext) cell.glow.color = new Color(nextGlowColor.r, nextGlowColor.g, nextGlowColor.b, glow);
            bool mark = isNext && nextMarker != null;
            cell.marker.gameObject.SetActive(mark);
            if (mark && cell.marker.sprite != nextMarker) cell.marker.sprite = nextMarker;
            float s = isNext ? 1f + 0.04f * glow : 1f;
            cell.rect.localScale = new Vector3(s, s, 1f);
        }

        EnemyActionIcon FindIcon(EnemyActionKind kind)
        {
            if (actionIcons == null) return null;
            for (int i = 0; i < actionIcons.Count; i++)
                if (actionIcons[i] != null && actionIcons[i].kind == kind) return actionIcons[i];
            return null;
        }

        Color KindColor(EnemyActionKind kind)
        {
            EnemyActionIcon e = FindIcon(kind);
            return e != null ? e.color : Color.white;
        }

        static void GetEnemyInfo(EnemyController e, out Sprite portrait, out string name)
        {
            EnemyStat st = e != null ? e.GetComponent<EnemyStat>() : null;
            portrait = st != null && st.Data != null ? st.Data.enemySprite : null;
            name = st != null && st.Data != null && !string.IsNullOrEmpty(st.Data.enemyName) ? st.Data.enemyName : "적";
        }

        // ── UI 생성 ──────────────────────────────────────────────
        void Build()
        {
            if (_built) return;
            _built = true;

            // 씬에 빈 오브젝트(일반 Transform)로 배치되거나 스케일 0으로 들어가도 화면 전체 기준으로 동작하도록 보정
            if (!(transform is RectTransform))
            {
                var self = gameObject.AddComponent<RectTransform>();
                self.anchorMin = Vector2.zero;
                self.anchorMax = Vector2.one;
                self.offsetMin = Vector2.zero;
                self.offsetMax = Vector2.zero;
            }
            if (transform.localScale == Vector3.zero) transform.localScale = Vector3.one;

            if (font == null)
            {
                var anyText = FindFirstObjectByType<TMP_Text>(FindObjectsInactive.Include);
                if (anyText != null) font = anyText.font;
            }

            var rootGo = new GameObject("TurnOrderBar", typeof(RectTransform));
            _root = (RectTransform)rootGo.transform;
            _root.SetParent(transform, false);
            TopLeft(_root);
            _root.anchoredPosition = anchoredOffset;
            _root.sizeDelta = new Vector2(currentSize, 10f);

            // 반투명 배경 패널 — 행 수에 맞춰 높이가 줄고 늘어난다
            _panel = NewImage("Panel", _root, RoundedSprite(), panelColor);
            TopLeft(_panel.rectTransform);
            _panel.rectTransform.anchoredPosition = new Vector2(-10f, 8f);

            _header = NewText("Header", _root, 26f, TextAlignmentOptions.Left);
            var hr = _header.rectTransform;
            TopLeft(hr);
            hr.anchoredPosition = Vector2.zero;
            hr.sizeDelta = new Vector2(160f, 30f);
            _header.fontStyle = FontStyles.Bold;

            _lockHint = NewText("LockHint", _root, 16f, TextAlignmentOptions.Left);
            var lr = _lockHint.rectTransform;
            TopLeft(lr);
            lr.anchoredPosition = new Vector2(84f, -6f);
            lr.sizeDelta = new Vector2(180f, 24f);
            _lockHint.text = "적 행동 중에는 사용 불가";
            _lockHint.gameObject.SetActive(false);

            BuildCurrent();

            var div = NewImage("Divider", _root, null, new Color(1f, 1f, 1f, 0.35f));
            _divider = div.rectTransform;
            TopLeft(_divider);
            _divider.pivot = new Vector2(0f, 0.5f);
            _divider.anchoredPosition = new Vector2(0f, -(HeaderH + currentSize + rowSpacing));
            _divider.sizeDelta = new Vector2(Mathf.Max(currentSize + labelWidth, portraitSize + actionsPerRow * (actionSize + cellSpacing)), 2f);

            var rowsGo = new GameObject("Rows", typeof(RectTransform));
            _rows = (RectTransform)rowsGo.transform;
            _rows.SetParent(_root, false);
            TopLeft(_rows);
            _rows.anchoredPosition = new Vector2(0f, -(HeaderH + currentSize + rowSpacing * 2f));
            _rows.sizeDelta = Vector2.zero;

            var idiv = NewImage("ItemDivider", _root, null, new Color(1f, 1f, 1f, 0.35f));
            _itemDivider = idiv.rectTransform;
            TopLeft(_itemDivider);
            _itemDivider.pivot = new Vector2(0f, 0.5f);
            _itemDivider.gameObject.SetActive(false);

            var itemsGo = new GameObject("Items", typeof(RectTransform));
            _items = (RectTransform)itemsGo.transform;
            _items.SetParent(_root, false);
            TopLeft(_items);
            _items.sizeDelta = Vector2.zero;
            itemsGo.SetActive(false);
        }

        // 아이템(유물) 칸 — 아이콘 + 우하단 카운터 배지
        ItemView CreateItem(RelicSO relic, Vector2 center)
        {
            var v = new ItemView { relic = relic };
            var go = new GameObject("Item", typeof(RectTransform));
            v.rect = (RectTransform)go.transform;
            v.rect.SetParent(_items, false);
            TopLeft(v.rect);
            v.rect.pivot = new Vector2(0.5f, 0.5f);
            v.rect.sizeDelta = new Vector2(itemSize, itemSize);
            v.rect.anchoredPosition = center;

            v.frame = NewImage("Frame", v.rect, RoundedSprite(), itemColor);
            Stretch(v.frame.rectTransform, 0f);
            var bg = NewImage("Bg", v.rect, RoundedSprite(), new Color(0.12f, 0.12f, 0.16f, 1f));
            Stretch(bg.rectTransform, 3f);

            v.icon = NewImage("Icon", v.rect, relic.icon, Color.white);
            v.icon.type = Image.Type.Simple;
            v.icon.preserveAspect = true;
            v.icon.enabled = relic.icon != null;
            Stretch(v.icon.rectTransform, 6f);
            v.fallback = NewText("Fallback", v.rect, 22f, TextAlignmentOptions.Center);
            Stretch(v.fallback.rectTransform, 0f);
            v.fallback.fontStyle = FontStyles.Bold;
            string label = relic.DisplayLabel;
            v.fallback.text = relic.icon == null && !string.IsNullOrEmpty(label) ? label.Substring(0, 1) : "";

            v.badgeBg = NewImage("Badge", v.rect, CircleSprite(), itemColor);
            var b = v.badgeBg.rectTransform;
            b.anchorMin = b.anchorMax = new Vector2(1f, 0f);
            b.anchoredPosition = new Vector2(-2f, 2f);
            b.sizeDelta = new Vector2(24f, 24f);
            v.badgeText = NewText("Count", b, 16f, TextAlignmentOptions.Center);
            Stretch(v.badgeText.rectTransform, 0f);
            v.badgeText.fontStyle = FontStyles.Bold;
            v.badgeText.color = new Color(0.1f, 0.08f, 0.05f);
            v.badgeBg.gameObject.SetActive(false);
            return v;
        }

        // 맨 위 '현재' 칸(지금 행동하는 쪽)
        void BuildCurrent()
        {
            var go = new GameObject("Current", typeof(RectTransform));
            _current = (RectTransform)go.transform;
            _current.SetParent(_root, false);
            TopLeft(_current);
            _current.pivot = new Vector2(0.5f, 0.5f);
            _current.sizeDelta = new Vector2(currentSize, currentSize);
            _current.anchoredPosition = new Vector2(currentSize * 0.5f, -HeaderH - currentSize * 0.5f);

            BuildPortrait(_current, 40f, out _curFrame, out _curPortrait, out _curInitial);

            _curBadgeBg = NewImage("Badge", _current, CircleSprite(), Color.white);
            var b = _curBadgeBg.rectTransform;
            b.anchorMin = b.anchorMax = new Vector2(1f, 0f);
            b.anchoredPosition = new Vector2(-6f, 6f);
            b.sizeDelta = new Vector2(34f, 34f);
            _curBadgeIcon = NewImage("Icon", b, null, Color.white);
            _curBadgeIcon.preserveAspect = true;
            Stretch(_curBadgeIcon.rectTransform, 5f);
            _curBadgeText = NewText("Text", b, 18f, TextAlignmentOptions.Center);
            Stretch(_curBadgeText.rectTransform, 0f);
            _curBadgeText.fontStyle = FontStyles.Bold;

            _curLabel = NewText("Label", _current, 22f, TextAlignmentOptions.MidlineLeft);
            var lr = _curLabel.rectTransform;
            lr.anchorMin = new Vector2(1f, 0f);
            lr.anchorMax = new Vector2(1f, 1f);
            lr.pivot = new Vector2(0f, 0.5f);
            lr.anchoredPosition = new Vector2(10f, 0f);
            lr.sizeDelta = new Vector2(labelWidth, 0f);
            _curLabel.textWrappingMode = TextWrappingModes.NoWrap;
            _curLabel.overflowMode = TextOverflowModes.Overflow;
        }

        RowView CreateRow(int id, EnemyController enemy)
        {
            var v = new RowView { id = id, enemy = enemy };
            var go = new GameObject("Row", typeof(RectTransform), typeof(CanvasGroup));
            v.rect = (RectTransform)go.transform;
            v.rect.SetParent(_rows, false);
            TopLeft(v.rect);
            v.rect.sizeDelta = new Vector2(portraitSize + actionsPerRow * (actionSize + cellSpacing), portraitSize);
            v.group = go.GetComponent<CanvasGroup>();
            v.group.alpha = 0f;
            v.group.blocksRaycasts = false;
            v.group.interactable = false;

            // 행동 중 배경 강조
            v.highlight = NewImage("Highlight", v.rect, RoundedSprite(), new Color(1f, 1f, 1f, 0f));
            Stretch(v.highlight.rectTransform, -5f);

            var pGo = new GameObject("Portrait", typeof(RectTransform));
            var pr = (RectTransform)pGo.transform;
            pr.SetParent(v.rect, false);
            TopLeft(pr);
            pr.sizeDelta = new Vector2(portraitSize, portraitSize);
            BuildPortrait(pr, 32f, out v.frame, out v.portrait, out v.initial);

            int n = Mathf.Max(1, actionsPerRow);
            v.cells = new CellView[n];
            for (int i = 0; i < n; i++)
            {
                float x = portraitSize + cellSpacing + i * (actionSize + cellSpacing);
                // 칸은 초상화 세로 중앙선에 맞춰 정렬
                v.cells[i] = CreateCell(v.rect, new Vector2(x + actionSize * 0.5f, -portraitSize * 0.5f));
            }
            return v;
        }

        CellView CreateCell(RectTransform parent, Vector2 center)
        {
            var c = new CellView();
            var go = new GameObject("Action", typeof(RectTransform));
            c.rect = (RectTransform)go.transform;
            c.rect.SetParent(parent, false);
            TopLeft(c.rect);
            c.rect.pivot = new Vector2(0.5f, 0.5f);
            c.rect.sizeDelta = new Vector2(actionSize, actionSize);
            c.rect.anchoredPosition = center;

            // 다음 행동 강조 빛(칸보다 살짝 크게)
            c.glow = NewImage("Glow", c.rect, RoundedSprite(), nextGlowColor);
            Stretch(c.glow.rectTransform, -5f);
            c.glow.gameObject.SetActive(false);

            c.frame = NewImage("Frame", c.rect, RoundedSprite(), Color.white);
            Stretch(c.frame.rectTransform, 0f);
            var bg = NewImage("Bg", c.rect, RoundedSprite(), new Color(0.12f, 0.12f, 0.16f, 1f));
            Stretch(bg.rectTransform, 3f);

            c.icon = NewImage("Icon", c.rect, null, Color.white);
            c.icon.preserveAspect = true;
            Stretch(c.icon.rectTransform, 9f);
            c.fallback = NewText("Fallback", c.rect, 24f, TextAlignmentOptions.Center);
            Stretch(c.fallback.rectTransform, 0f);
            c.fallback.fontStyle = FontStyles.Bold;

            // 남은 단계 배지(우하단 원)
            c.badgeBg = NewImage("Badge", c.rect, CircleSprite(), Color.white);
            var b = c.badgeBg.rectTransform;
            b.anchorMin = b.anchorMax = new Vector2(1f, 0f);
            b.anchoredPosition = new Vector2(-2f, 2f);
            b.sizeDelta = new Vector2(26f, 26f);
            c.badgeText = NewText("Steps", b, 17f, TextAlignmentOptions.Center);
            Stretch(c.badgeText.rectTransform, 0f);
            c.badgeText.fontStyle = FontStyles.Bold;

            // 예상 피해(좌상단 알약)
            var pill = NewImage("Damage", c.rect, CircleSprite(), new Color(0f, 0f, 0f, 0.75f));
            c.dmgPill = pill.rectTransform;
            c.dmgPill.anchorMin = c.dmgPill.anchorMax = new Vector2(0f, 1f);
            c.dmgPill.anchoredPosition = new Vector2(4f, -4f);
            c.dmgPill.sizeDelta = new Vector2(30f, 20f);
            c.dmgText = NewText("Value", c.dmgPill, 14f, TextAlignmentOptions.Center);
            Stretch(c.dmgText.rectTransform, 0f);
            c.dmgText.fontStyle = FontStyles.Bold;

            // 다음 행동 표식(우상단, 스프라이트 지정 시)
            c.marker = NewImage("Marker", c.rect, null, Color.white);
            c.marker.preserveAspect = true;
            var m = c.marker.rectTransform;
            m.anchorMin = m.anchorMax = new Vector2(1f, 1f);
            m.anchoredPosition = new Vector2(-2f, -2f);
            m.sizeDelta = new Vector2(24f, 24f);
            c.marker.gameObject.SetActive(false);
            return c;
        }

        // 테두리 + 둥근 마스크 초상화 + 이니셜(초상화 없을 때)
        void BuildPortrait(RectTransform parent, float initialSize, out Image frame, out Image portrait, out TextMeshProUGUI initial)
        {
            frame = NewImage("Frame", parent, RoundedSprite(), Color.white);
            Stretch(frame.rectTransform, 0f);

            var maskImg = NewImage("Mask", parent, RoundedSprite(), new Color(0.12f, 0.12f, 0.16f, 1f));
            Stretch(maskImg.rectTransform, 4f);
            maskImg.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            portrait = NewImage("Portrait", maskImg.rectTransform, null, Color.white);
            portrait.preserveAspect = true;
            Stretch(portrait.rectTransform, 0f);

            initial = NewText("Initial", maskImg.rectTransform, initialSize, TextAlignmentOptions.Center);
            Stretch(initial.rectTransform, 0f);
            initial.fontStyle = FontStyles.Bold;
        }

        static void TopLeft(RectTransform r)
        {
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0f, 1f);
        }

        static Image NewImage(string name, Transform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        TextMeshProUGUI NewText(string name, Transform parent, float size, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            if (font != null) t.font = font;
            t.fontSize = size;
            t.alignment = align;
            t.color = Color.white;
            t.raycastTarget = false;
            t.richText = true;
            return t;
        }

        static void Stretch(RectTransform r, float inset)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(inset, inset);
            r.offsetMax = new Vector2(-inset, -inset);
        }

        // 코드 생성 둥근 사각/원 스프라이트(별도 에셋 없이 동작)
        static Sprite RoundedSprite() => _roundedSprite != null ? _roundedSprite : (_roundedSprite = MakeRounded(64, 14f));
        static Sprite CircleSprite() => _circleSprite != null ? _circleSprite : (_circleSprite = MakeRounded(64, 32f));

        static Sprite MakeRounded(int size, float radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave,
            };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float cx = x + 0.5f, cy = y + 0.5f;
                    float dx = Mathf.Max(radius - cx, 0f, cx - (size - radius));
                    float dy = Mathf.Max(radius - cy, 0f, cy - (size - radius));
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    byte a = (byte)(Mathf.Clamp01(radius - d + 0.5f) * 255f);
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            float b = Mathf.Min(radius, size * 0.5f - 1f);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }
    }
}
