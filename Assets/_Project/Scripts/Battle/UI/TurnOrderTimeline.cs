using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Battle.Relic;

namespace Battle.UI
{
    // 턴 예고 바 — 화면 좌측 가장자리에 몬스터별 행을 세로로 쌓는다.
    //   [패널: 몬스터 초상화 | 칼 아이콘 + 공격까지 남은 턴(또는 예상 피해)]  [방해 아이콘 + 방해까지 남은 턴]
    // 행 순서는 전투 등록 순서로 고정. 적 행동 시점은 행동 게이지(플레이어 행동마다 1단계)를 시뮬레이션해 예측한다(빙결 스택 반영).
    // 아래에는 전투형 유물 칸을 표시하고, 유물 발동 시 해당 칸을 순서대로 펄스한다.
    public class TurnOrderTimeline : MonoBehaviour
    {
        // 칼 아이콘 아래 숫자 의미
        public enum AttackNumber { TurnsLeft, Damage }

        [Header("행 프리팹")]
        [Tooltip("몬스터 한 행 프리팹(TurnOrderRowView). 비우면 아래 '행 내부' 값으로 코드에서 만든다.")]
        [SerializeField] private TurnOrderRowView rowPrefab;
        [Tooltip("행 안 글자에 쓸 TMP 머티리얼(외곽선 포함, 예: NYJUnheo SDF 2). 지정하면 아래 코드 외곽선 대신 이 머티리얼을 쓴다.")]
        [SerializeField] private Material textMaterial;
        [Tooltip("textMaterial이 없을 때 숫자 텍스트에 코드로 어두운 외곽선 적용")]
        [SerializeField] private bool numberOutline = true;
        [Tooltip("2순위(다음) 행동 묶음 투명도")]
        [SerializeField, Range(0f, 1f)] private float disruptAlpha = 0.6f;

        [Header("배치 (좌상단 기준)")]
        [Tooltip("첫 행의 좌상단 위치. x=0이면 화면 왼쪽 가장자리에 붙는다.")]
        [SerializeField] private Vector2 anchoredOffset = new Vector2(0f, -170f);
        [Tooltip("행 크기(px) — 행 프리팹을 쓰면 프리팹 루트 크기를 따른다")]
        [SerializeField] private Vector2 rowPanelSize = new Vector2(220f, 122f);
        [SerializeField] private float rowSpacing = 14f;    // 행 간격
        [SerializeField] private float slideSpeed = 12f;    // 위치 보간 속도
        [Tooltip("누르면 턴 예고 바를 숨기거나 다시 표시")]
        [SerializeField] private KeyCode hideToggleKey = KeyCode.Z;
        private bool _hiddenByUser;

        [Header("행 내부 — 프리팹 미사용 시 (패널 기준 px)")]
        [Tooltip("왼쪽 초상화 영역 폭(패널 왼쪽부터)")]
        [SerializeField] private float portraitWidth = 132f;
        [Tooltip("초상화 영역 위아래·왼쪽 안쪽 여백")]
        [SerializeField] private float portraitInset = 9f;
        [SerializeField] private Color portraitBgColor = new Color(0.13f, 0.10f, 0.08f, 0.95f);
        [SerializeField] private Color dividerColor = new Color(0.86f, 0.66f, 0.36f, 1f);
        [SerializeField] private float iconSize = 46f;      // 칼·방해 아이콘 크기
        [SerializeField] private float iconY = -36f;        // 아이콘 중심 y(행 위쪽 기준)
        [SerializeField] private float numberY = -86f;      // 숫자 중심 y
        [SerializeField] private float numberFontSize = 40f;

        [Header("공격 (칼)")]
        [Tooltip("비우면 Resources/UI/TurnOrder/TurnOrder_AttackIcon")]
        [SerializeField] private Sprite attackIcon;
        [Tooltip("칼 아래 숫자: 공격까지 남은 턴 / 예상 피해")]
        [SerializeField] private AttackNumber attackNumber = AttackNumber.TurnsLeft;
        [SerializeField] private Color attackNumberColor = Color.white;

        [Header("방해 행동")]
        [SerializeField] private bool showDisruptions = true;
        [Tooltip("방해 행동 아이콘. 비우면 '방해' 글자")]
        [SerializeField] private Sprite disruptIcon;
        [Tooltip("패널 오른쪽 끝에서 방해 묶음 중심까지 거리")]
        [SerializeField] private float disruptOffsetX = 36f;
        [SerializeField] private Color disruptNumberColor = new Color(0.82f, 0.80f, 0.74f, 1f);

        [Header("패널")]
        [Tooltip("비우면 Resources/UI/TurnOrder/TurnOrder_RowPanel")]
        [SerializeField] private Sprite rowPanelSprite;

        [Header("강조")]
        [Tooltip("가장 먼저 공격할 몬스터의 칼·숫자 색")]
        [SerializeField] private Color nextColor = new Color(1f, 0.80f, 0.38f);
        [SerializeField] private float glowPulseSpeed = 3f;
        [Tooltip("행동 중인 몬스터 패널 색(곱)")]
        [SerializeField] private Color actingTint = new Color(1f, 0.72f, 0.62f, 1f);
        [Tooltip("다른 적이 행동 중일 때 나머지 행 투명도")]
        [SerializeField, Range(0f, 1f)] private float idleRowAlpha = 0.55f;

        [Header("아이템(유물)")]
        [SerializeField] private bool showItems = true;
        [Tooltip("전투형 유물만 표시(비전투형 — 상점 할인·최대 체력 등은 제외)")]
        [SerializeField] private bool combatItemsOnly = true;
        [SerializeField] private float itemsX = 0f;         // 아이템 구역 x(왼쪽 끝에 붙임)
        [SerializeField] private float itemsGap = 20f;      // 행과 아이템 사이
        [SerializeField] private float itemSize = 56f;
        [SerializeField] private float itemSpacing = 8f;
        [SerializeField] private int itemsPerRow = 2;
        [Tooltip("유물 발동 표시 간격(초) — 여러 개 동시 발동 시 순서대로. 이 동안 적 행동이 대기한다")]
        [SerializeField] private float itemShowSeconds = 0.9f;
        [SerializeField] private Color itemColor = new Color(0.85f, 0.72f, 0.40f);

        [Header("스타일")]
        [Tooltip("비우면 씬의 다른 TMP 텍스트 폰트를 사용(한글 폰트 확보용).")]
        [SerializeField] private TMP_FontAsset font;

        const int MaxSimSteps = 400; // 예측 시뮬레이션 상한(빙결 스택이 매우 클 때 무한 루프 방지)
        const string ResDir = "UI/TurnOrder/";

        // 몬스터 한 행의 예측
        struct Forecast
        {
            public int attackSteps;   // 공격까지 남은 플레이어 행동 수(-1=게이지 가득, 곧 공격 / int.MaxValue=예측 불가)
            public int attackDamage;  // 다음 공격 예상 피해
            public int disruptSteps;  // 방해까지 남은 수(int.MaxValue=없음)
        }

        class RowView
        {
            public int id;
            public EnemyController enemy;
            public RectTransform rect;
            public CanvasGroup group;
            public TurnOrderRowView view;
            public Forecast forecast;
            public bool used;
        }

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
        RectTransform _rows;
        RectTransform _items;
        TextMeshProUGUI _lockHint;
        readonly List<ItemView> _itemViews = new List<ItemView>();
        readonly List<RelicSO> _itemBuf = new List<RelicSO>();
        readonly Queue<RelicSO> _triggerQueue = new Queue<RelicSO>(); // 발동 표시 대기열
        RelicSO _showingRelic;
        float _showingUntil;

        readonly Dictionary<int, RowView> _views = new Dictionary<int, RowView>();
        readonly List<RowView> _order = new List<RowView>(); // 고정 행 순서(살아있는 행)
        readonly List<RowView> _dying = new List<RowView>();
        readonly List<EnemyController> _alive = new List<EnemyController>();
        readonly HashSet<EnemyController> _pending = new HashSet<EnemyController>();
        readonly List<int> _removeBuf = new List<int>();
        bool _built;
        float _lockFlash;

        static Sprite _roundedSprite;
        static Sprite _circleSprite;

        void Awake() => Build();

        // 적 턴에 카드를 쓰려 할 때 잠금 안내를 깜빡인다
        public void FlashLocked() => _lockFlash = 1f;

        // 유물 발동 표시가 진행 중이거나 대기 중인지 — 적 행동은 이게 끝난 뒤 시작한다
        public bool IsShowingRelicTrigger =>
            isActiveAndEnabled && showItems
            && (_triggerQueue.Count > 0 || (_showingRelic != null && Time.unscaledTime < _showingUntil));

        // 유물 발동 — 순서대로 해당 아이템 칸을 펄스
        public void ShowRelicTrigger(RelicSO relic)
        {
            if (relic == null || !showItems) return;
            _triggerQueue.Enqueue(relic);
        }

        // ── 매 프레임: 예측 → 배치 ─────────────────────────────
        void LateUpdate()
        {
            if (Input.GetKeyDown(hideToggleKey)) _hiddenByUser = !_hiddenByUser;

            var nbc = NewBattleController.Instance;
            bool show = !_hiddenByUser && nbc != null && nbc.InBattle && EnemyCombatParty.AliveCount > 0;
            if (_root.gameObject.activeSelf != show) _root.gameObject.SetActive(show);
            if (!show) return;

            UpdateRelicTrigger();
            UpdateLockHint();
            SyncRows();
            BuildForecasts(nbc);
            SyncItems(nbc);
            ApplyLayout(nbc);
        }

        // 발동 대기열 — itemShowSeconds 간격으로 하나씩 꺼내 아이템 칸을 펄스
        void UpdateRelicTrigger()
        {
            if (_showingRelic != null && Time.unscaledTime >= _showingUntil) _showingRelic = null;
            if (_showingRelic != null || _triggerQueue.Count == 0) return;
            _showingRelic = _triggerQueue.Dequeue();
            _showingUntil = Time.unscaledTime + itemShowSeconds;
            for (int i = 0; i < _itemViews.Count; i++)
                if (_itemViews[i].relic == _showingRelic) _itemViews[i].flash = 1f;
        }

        void UpdateLockHint()
        {
            _lockFlash = Mathf.MoveTowards(_lockFlash, 0f, Time.deltaTime * 2f);
            bool on = _lockFlash > 0.01f;
            if (_lockHint.gameObject.activeSelf != on) _lockHint.gameObject.SetActive(on);
            if (on) _lockHint.color = new Color(1f, 1f, 1f, _lockFlash);
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

        // 각 몬스터의 다음 공격·다음 방해를 게이지 시뮬레이션으로 예측
        void BuildForecasts(NewBattleController nbc)
        {
            _pending.Clear();
            foreach (EnemyController e in nbc.PendingAttackers)
                if (e != null) _pending.Add(e);

            for (int r = 0; r < _order.Count; r++)
            {
                RowView row = _order[r];
                EnemyController e = row.enemy;
                var f = new Forecast { attackSteps = int.MaxValue, attackDamage = -1, disruptSteps = int.MaxValue };
                if (e != null)
                {
                    f.attackDamage = e.GetPredictedAttackDamage(0);
                    if (_pending.Contains(e)) f.attackSteps = -1; // 이미 게이지가 차서 공격 대기 중

                    EnemyStat st = e.GetComponent<EnemyStat>();
                    if (st != null)
                    {
                        int step = st.GaugeStep;
                        int max = Mathf.Max(1, st.GaugeMaxSteps);
                        bool mid = st.MidPatternTriggered;
                        int frost = st.statusEffects != null && st.statusEffects.TryGetValue("frost", out int fr) ? fr : 0;

                        // 플레이어 행동 1회마다 게이지 +1(빙결 스택이 있으면 스택만 1 소모)
                        for (int k = 1; k <= MaxSimSteps; k++)
                        {
                            bool needAttack = f.attackSteps == int.MaxValue;
                            bool needDisrupt = showDisruptions && f.disruptSteps == int.MaxValue;
                            if (!needAttack && !needDisrupt) break;

                            if (frost > 0) { frost--; continue; }
                            step++;
                            if (step == st.DisruptionStep && !mid)
                            {
                                mid = true;
                                if (needDisrupt) f.disruptSteps = k;
                            }
                            if (step >= max)
                            {
                                if (needAttack) f.attackSteps = k;
                                step = 0;
                                mid = false;
                            }
                        }
                    }
                }
                row.forecast = f;
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

        // 행 배치·내용 갱신, 사라지는 행 처리, 아이템 위치
        void ApplyLayout(NewBattleController nbc)
        {
            float dt = Time.deltaTime;
            float t = 1f - Mathf.Exp(-slideSpeed * dt);
            EnemyController acting = nbc.ActingEnemy;

            // 전체에서 가장 먼저 올 행동이 있는 행(동률이면 위 행 우선) — 그 행의 1순위 칸을 강조
            int nextRow = -1, best = int.MaxValue;
            for (int r = 0; r < _order.Count; r++)
            {
                int first = FirstSteps(_order[r].forecast);
                if (first < best) { best = first; nextRow = r; }
            }
            float glow = 0.5f + 0.5f * Mathf.Sin(Time.time * glowPulseSpeed);

            float y = 0f;
            for (int r = 0; r < _order.Count; r++)
            {
                RowView row = _order[r];
                bool isActing = acting != null && row.enemy == acting;

                Vector2 target = new Vector2(0f, y);
                if (row.group.alpha <= 0f) row.rect.anchoredPosition = target - new Vector2(40f, 0f); // 새 행은 왼쪽에서 등장
                row.rect.anchoredPosition = Vector2.Lerp(row.rect.anchoredPosition, target, t);
                float targetAlpha = acting != null && !isActing ? idleRowAlpha : 1f;
                row.group.alpha = Mathf.MoveTowards(row.group.alpha, targetAlpha, dt * 5f);
                FillRow(row, isActing, r == nextRow, glow);

                y -= RowHeight + rowSpacing;
            }

            // 죽은 적의 행 — 왼쪽으로 빠지며 사라지고, 아래 행들이 위로 당겨진다
            for (int i = _dying.Count - 1; i >= 0; i--)
            {
                RowView v = _dying[i];
                v.group.alpha = Mathf.MoveTowards(v.group.alpha, 0f, dt * 3f);
                v.rect.anchoredPosition += new Vector2(-260f * dt, 0f);
                if (v.group.alpha <= 0f)
                {
                    Destroy(v.rect.gameObject);
                    _dying.RemoveAt(i);
                }
            }

            // 아이템 구역 — 마지막 행 아래, 행이 줄면 함께 위로 당겨진다
            bool hasItems = _itemViews.Count > 0;
            if (_items.gameObject.activeSelf != hasItems) _items.gameObject.SetActive(hasItems);
            if (hasItems)
            {
                float rowsH = _order.Count > 0 ? _order.Count * (RowHeight + rowSpacing) - rowSpacing : 0f;
                Vector2 ip = new Vector2(itemsX, -(rowsH + itemsGap));
                _items.anchoredPosition = _items.anchoredPosition == Vector2.zero ? ip : Vector2.Lerp(_items.anchoredPosition, ip, t);
            }
        }

        // 행 높이(프리팹이면 프리팹 루트 높이)
        float RowHeight => rowPrefab != null ? ((RectTransform)rowPrefab.transform).sizeDelta.y : rowPanelSize.y;

        // 한 행 채우기(프리팹에 없는 요소는 건너뜀)
        void FillRow(RowView row, bool isActing, bool isNext, float glow)
        {
            TurnOrderRowView v = row.view;
            GetEnemyInfo(row.enemy, out Sprite portrait, out string name);
            if (v.portrait != null)
            {
                v.portrait.enabled = portrait != null;
                if (portrait != null && v.portrait.sprite != portrait) v.portrait.sprite = portrait;
            }
            if (v.initial != null)
            {
                v.initial.gameObject.SetActive(portrait == null);
                if (portrait == null) v.initial.text = string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1);
            }

            // 행동 중 — 패널 색 + 살짝 커짐
            if (v.panel != null) v.panel.color = Color.Lerp(v.panel.color, isActing ? actingTint : Color.white, 0.25f);
            float s = isActing ? 1.04f : 1f;
            row.rect.localScale = Vector3.Lerp(row.rect.localScale, new Vector3(s, s, 1f), 0.25f);

            // 행동 두 개를 가까운 순서로 — 1순위(초상화 옆), 2순위(패널 밖)
            Forecast f = row.forecast;
            bool hasAttack = f.attackSteps != int.MaxValue;
            bool hasDisrupt = showDisruptions && f.disruptSteps != int.MaxValue;
            bool attackFirst = hasAttack && (!hasDisrupt || f.attackSteps <= f.disruptSteps);

            bool hasPrimary = hasAttack || hasDisrupt;
            bool primaryIsAttack = attackFirst || !hasDisrupt;
            bool hasSecondary = hasAttack && hasDisrupt;

            SetSlotVisible(v.primaryIcon, v.primaryNumber, v.primaryFallback, hasPrimary);
            if (hasPrimary)
                FillSlot(v.primaryIcon, v.primaryNumber, v.primaryFallback, primaryIsAttack, f, isNext, glow);

            if (v.secondaryGroup != null && v.secondaryGroup.activeSelf != hasSecondary) v.secondaryGroup.SetActive(hasSecondary);
            if (hasSecondary)
                FillSlot(v.secondaryIcon, v.secondaryNumber, v.secondaryFallback, !primaryIsAttack, f, false, glow);
        }

        // 가장 먼저 올 행동까지 남은 수(공격 대기 중이면 -1)
        int FirstSteps(Forecast f)
        {
            int d = showDisruptions ? f.disruptSteps : int.MaxValue;
            return Mathf.Min(f.attackSteps, d);
        }

        static void SetSlotVisible(Image icon, TMP_Text number, TMP_Text fallback, bool on)
        {
            if (icon != null && icon.gameObject.activeSelf != on) icon.gameObject.SetActive(on);
            if (number != null && number.gameObject.activeSelf != on) number.gameObject.SetActive(on);
            if (!on && fallback != null) fallback.gameObject.SetActive(false);
        }

        // 행동 칸 하나 채우기 — 종류에 맞는 아이콘·숫자·색
        void FillSlot(Image icon, TMP_Text number, TMP_Text fallback, bool isAttack, Forecast f, bool highlight, float glow)
        {
            Sprite sprite = isAttack ? attackIcon : disruptIcon;
            bool hasIcon = sprite != null;
            if (icon != null)
            {
                icon.enabled = hasIcon;
                if (hasIcon && icon.sprite != sprite) icon.sprite = sprite;
                icon.color = highlight ? Color.Lerp(Color.white, nextColor, 0.5f * glow) : Color.white;
                float ss = highlight ? 1f + 0.08f * glow : 1f;
                icon.rectTransform.localScale = new Vector3(ss, ss, 1f);
            }
            if (fallback != null)
            {
                fallback.gameObject.SetActive(!hasIcon);
                if (!hasIcon) fallback.text = isAttack ? "공격" : "방해";
            }

            if (number != null)
            {
                int steps = isAttack ? f.attackSteps : f.disruptSteps;
                string text = steps < 0 ? "!" : steps.ToString();
                if (isAttack && attackNumber == AttackNumber.Damage) text = f.attackDamage >= 0 ? f.attackDamage.ToString() : "-";
                number.text = text;
                Color baseColor = isAttack ? attackNumberColor : disruptNumberColor;
                number.color = highlight ? Color.Lerp(baseColor, nextColor, 0.6f + 0.4f * glow) : baseColor;
            }
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
            // 행 프리팹 아이콘에 넣어 둔 스프라이트를 행동 종류별 아이콘으로 사용(인스펙터 값 우선)
            if (rowPrefab != null)
            {
                if (attackIcon == null && rowPrefab.primaryIcon != null) attackIcon = rowPrefab.primaryIcon.sprite;
                if (disruptIcon == null && rowPrefab.secondaryIcon != null) disruptIcon = rowPrefab.secondaryIcon.sprite;
            }
            if (rowPanelSprite == null) rowPanelSprite = Resources.Load<Sprite>(ResDir + "TurnOrder_RowPanel");

            if (attackIcon == null) attackIcon = Resources.Load<Sprite>(ResDir + "TurnOrder_AttackIcon");

            var rootGo = new GameObject("TurnOrderBar", typeof(RectTransform));
            _root = (RectTransform)rootGo.transform;
            _root.SetParent(transform, false);
            TopLeft(_root);
            _root.anchoredPosition = anchoredOffset;
            _root.sizeDelta = Vector2.zero;

            _lockHint = NewText("LockHint", _root, 18f, TextAlignmentOptions.Left);
            var lr = _lockHint.rectTransform;
            TopLeft(lr);
            lr.anchoredPosition = new Vector2(itemsX, 30f);
            lr.sizeDelta = new Vector2(260f, 26f);
            _lockHint.text = "적 행동 중에는 사용 불가";
            _lockHint.gameObject.SetActive(false);

            var rowsGo = new GameObject("Rows", typeof(RectTransform));
            _rows = (RectTransform)rowsGo.transform;
            _rows.SetParent(_root, false);
            TopLeft(_rows);
            _rows.anchoredPosition = Vector2.zero;
            _rows.sizeDelta = Vector2.zero;

            var itemsGo = new GameObject("Items", typeof(RectTransform));
            _items = (RectTransform)itemsGo.transform;
            _items.SetParent(_root, false);
            TopLeft(_items);
            _items.sizeDelta = Vector2.zero;
            itemsGo.SetActive(false);
        }

        RowView CreateRow(int id, EnemyController enemy)
        {
            var row = new RowView { id = id, enemy = enemy };
            row.view = rowPrefab != null ? Instantiate(rowPrefab, _rows, false) : BuildRowView();
            row.rect = (RectTransform)row.view.transform;
            TopLeft(row.rect);
            row.rect.localScale = Vector3.one;
            row.group = row.view.GetComponent<CanvasGroup>();
            row.group.alpha = 0f;
            row.group.blocksRaycasts = false;
            row.group.interactable = false;
            // 글자 머티리얼(외곽선) — 지정 시 행 안 모든 TMP에 적용, 없으면 숫자만 코드 외곽선
            if (textMaterial != null)
            {
                foreach (var t in row.view.GetComponentsInChildren<TMP_Text>(true))
                    t.fontSharedMaterial = textMaterial;
            }
            else if (numberOutline)
            {
                ApplyOutline(row.view.primaryNumber);
                ApplyOutline(row.view.secondaryNumber);
            }

            // 방해(다음 행동) 묶음은 살짝 투명하게
            if (row.view.secondaryGroup != null)
            {
                var dcg = row.view.secondaryGroup.GetComponent<CanvasGroup>();
                if (dcg == null) dcg = row.view.secondaryGroup.AddComponent<CanvasGroup>();
                dcg.alpha = disruptAlpha;
                dcg.blocksRaycasts = false;
                dcg.interactable = false;
            }
            return row;
        }

        static void ApplyOutline(TMP_Text t)
        {
            if (t == null) return;
            t.outlineWidth = 0.18f;
            t.outlineColor = new Color32(30, 18, 8, 255);
        }

        // 행 프리팹이 없을 때 — 같은 구성의 행을 코드로 만든다
        TurnOrderRowView BuildRowView()
        {
            var go = new GameObject("Row", typeof(RectTransform), typeof(CanvasGroup));
            var rect = (RectTransform)go.transform;
            rect.SetParent(_rows, false);
            TopLeft(rect);
            rect.sizeDelta = rowPanelSize;
            var v = go.AddComponent<TurnOrderRowView>();
            BuildRowContents(v, rect);
            return v;
        }

        void BuildRowContents(TurnOrderRowView v, RectTransform root)
        {
            // 패널(이미지 없으면 둥근 사각)
            v.panel = NewImage("Panel", root, rowPanelSprite != null ? rowPanelSprite : RoundedSprite(),
                rowPanelSprite != null ? Color.white : new Color(0.25f, 0.18f, 0.12f, 0.95f));
            Stretch(v.panel.rectTransform, 0f);

            // 왼쪽 초상화 영역(어두운 배경 + 마스크 초상화)
            float h = rowPanelSize.y;
            var bg = NewImage("PortraitBg", root, null, portraitBgColor);
            var bgr = bg.rectTransform;
            TopLeft(bgr);
            bgr.anchoredPosition = new Vector2(0f, -portraitInset);
            bgr.sizeDelta = new Vector2(portraitWidth, h - portraitInset * 2f);
            bg.gameObject.AddComponent<RectMask2D>();

            v.portrait = NewImage("Portrait", bgr, null, Color.white);
            v.portrait.preserveAspect = true;
            Stretch(v.portrait.rectTransform, 4f);
            v.initial = NewText("Initial", bgr, 40f, TextAlignmentOptions.Center);
            Stretch(v.initial.rectTransform, 0f);
            v.initial.fontStyle = FontStyles.Bold;

            // 세로 구분선
            var div = NewImage("Divider", root, null, dividerColor);
            var dr = div.rectTransform;
            TopLeft(dr);
            dr.anchoredPosition = new Vector2(portraitWidth, -portraitInset);
            dr.sizeDelta = new Vector2(3f, h - portraitInset * 2f);

            // 1순위 행동(패널 오른쪽 영역 가운데) — 아이콘·글자·숫자
            float rightCenter = portraitWidth + (rowPanelSize.x - portraitWidth) * 0.5f;
            v.primaryIcon = NewImage("PrimaryIcon", root, attackIcon, Color.white);
            v.primaryIcon.preserveAspect = true;
            PlaceCenter(v.primaryIcon.rectTransform, new Vector2(rightCenter, iconY), new Vector2(iconSize, iconSize));
            v.primaryIcon.enabled = attackIcon != null;
            v.primaryFallback = NewText("PrimaryLabel", root, 18f, TextAlignmentOptions.Center);
            PlaceCenter(v.primaryFallback.rectTransform, new Vector2(rightCenter, iconY), new Vector2(iconSize + 20f, iconSize));
            v.primaryFallback.gameObject.SetActive(false);

            v.primaryNumber = NewNumber("PrimaryNumber", root, new Vector2(rightCenter, numberY), attackNumberColor);

            // 2순위 행동 묶음(패널 바깥 오른쪽)
            var dg = new GameObject("Secondary", typeof(RectTransform));
            var dgr = (RectTransform)dg.transform;
            dgr.SetParent(root, false);
            TopLeft(dgr);
            dgr.anchoredPosition = new Vector2(rowPanelSize.x + disruptOffsetX, 0f);
            dgr.sizeDelta = Vector2.zero;
            v.secondaryGroup = dg;

            v.secondaryIcon = NewImage("SecondaryIcon", dgr, disruptIcon, Color.white);
            v.secondaryIcon.preserveAspect = true;
            PlaceCenter(v.secondaryIcon.rectTransform, new Vector2(0f, iconY), new Vector2(iconSize, iconSize));
            v.secondaryFallback = NewText("SecondaryLabel", dgr, 18f, TextAlignmentOptions.Center);
            PlaceCenter(v.secondaryFallback.rectTransform, new Vector2(0f, iconY), new Vector2(iconSize + 20f, iconSize));
            v.secondaryFallback.text = "방해";
            v.secondaryFallback.color = disruptNumberColor;
            v.secondaryNumber = NewNumber("SecondaryNumber", dgr, new Vector2(0f, numberY), disruptNumberColor);
        }

        // 숫자 텍스트(외곽선으로 배경 위에서도 읽히게)
        TextMeshProUGUI NewNumber(string name, Transform parent, Vector2 center, Color color)
        {
            var t = NewText(name, parent, numberFontSize, TextAlignmentOptions.Center);
            PlaceCenter(t.rectTransform, center, new Vector2(80f, numberFontSize + 8f));
            t.fontStyle = FontStyles.Bold;
            t.color = color;
            return t;

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

        static void TopLeft(RectTransform r)
        {
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0f, 1f);
        }

        // 좌상단 앵커 기준으로 중심 좌표·크기 지정
        static void PlaceCenter(RectTransform r, Vector2 center, Vector2 size)
        {
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = center;
            r.sizeDelta = size;
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
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
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
