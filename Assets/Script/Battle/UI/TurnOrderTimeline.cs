using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Battle.UI
{
    // 붕괴:스타레일식 행동 순서 바 — 화면 좌측 세로열.
    // 맨 위 '현재' 칸 = 지금 행동하는 쪽(내 턴 / 행동 중인 적), 그 아래 = 앞으로 행동할 적들의 순서.
    // 적 행동 시점은 행동 게이지(플레이어 행동마다 1단계)로 시뮬레이션해 예측한다(빙결 스택 반영).
    // 항목은 '어느 적의 몇 번째 사이클 행동인지'로 식별되어, 순서가 바뀌면 위치가 부드럽게 미끄러진다.
    public class TurnOrderTimeline : MonoBehaviour
    {
        enum EntryKind { Player = 0, Attack = 1, Disrupt = 2 }

        [Header("배치 (좌상단 기준)")]
        [SerializeField] private Vector2 anchoredOffset = new Vector2(24f, -150f); // 바 시작 위치
        [SerializeField] private float currentSize = 96f;   // '현재' 칸 크기
        [SerializeField] private float entrySize = 64f;     // 예정 칸 크기
        [SerializeField] private float spacing = 10f;       // 칸 간격
        [SerializeField] private int maxUpcoming = 6;       // 표시할 예정 행동 수
        [Tooltip("방해행동(게이지 절반)도 순서 바에 별도 항목으로 표시")]
        [SerializeField] private bool showDisruptions = true;
        [SerializeField] private float slideSpeed = 12f;    // 위치 보간 속도

        [Header("스타일")]
        [Tooltip("비우면 씬의 다른 TMP 텍스트 폰트를 사용(한글 폰트 확보용).")]
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private Sprite playerPortrait;     // 내 턴 초상화(비우면 '나' 글자)
        [SerializeField] private Color playerColor = new Color(0.33f, 0.70f, 1f);
        [SerializeField] private Color attackColor = new Color(0.95f, 0.34f, 0.30f);
        [SerializeField] private Color disruptColor = new Color(0.70f, 0.45f, 0.98f);
        [SerializeField] private Color panelColor = new Color(0f, 0f, 0f, 0.35f);

        const long PlayerKey = -1;
        const int MaxSimSteps = 400; // 예측 시뮬레이션 상한(빙결 스택이 매우 클 때 무한 루프 방지)

        // 순서 바 한 칸의 논리 데이터
        struct Slot
        {
            public long key;
            public EntryKind kind;
            public EnemyController enemy;
            public int steps;      // 몇 단계(플레이어 행동) 후 발동하는지, -1=대기 중
        }

        // 순서 바 한 칸의 화면 오브젝트
        class EntryView
        {
            public long key;
            public RectTransform rect;
            public CanvasGroup group;
            public Image frame;
            public Image portrait;
            public TextMeshProUGUI initial;
            public RectTransform badge;
            public Image badgeBg;
            public TextMeshProUGUI badgeText;
            public TextMeshProUGUI label;
            public bool used;
            public bool dying;
        }

        RectTransform _root;
        RectTransform _entries; // 칸 컨테이너(패널 위, 헤더 아래 순서)
        Image _panel;
        TextMeshProUGUI _header;
        TextMeshProUGUI _lockHint;
        RectTransform _divider;
        readonly Dictionary<long, EntryView> _views = new Dictionary<long, EntryView>();
        readonly List<EntryView> _dying = new List<EntryView>();
        readonly List<Slot> _slots = new List<Slot>();
        readonly List<EnemyController> _alive = new List<EnemyController>();
        readonly List<long> _removeBuf = new List<long>();
        bool _built;
        bool _wasEnemyTurn;
        float _headerPulse;
        float _lockFlash;

        static Sprite _roundedSprite;
        static Sprite _circleSprite;

        void Awake() => Build();

        // 적 턴에 카드를 쓰려 할 때 '적 턴' 표시를 깜빡여 잠금을 알린다
        public void FlashLocked() => _lockFlash = 1f;

        // ── 매 프레임: 슬롯 계산 → 배치 ─────────────────────────────
        void LateUpdate()
        {
            var nbc = NewBattleController.Instance;
            bool show = nbc != null && nbc.InBattle && EnemyCombatParty.AliveCount > 0;
            if (_root.gameObject.activeSelf != show) _root.gameObject.SetActive(show);
            if (!show) return;

            BuildSlots(nbc);
            UpdateHeader(nbc);
            ApplyLayout();
        }

        // 현재 칸 + 공격 대기열 + 게이지 예측으로 순서를 만든다
        void BuildSlots(NewBattleController nbc)
        {
            _slots.Clear();

            // 1) 현재 칸
            EnemyController acting = nbc.ActingEnemy;
            if (acting != null && acting.IsAlive)
            {
                EnemyStat st = acting.GetComponent<EnemyStat>();
                int cycle = st != null ? st.CycleCount : 0;
                // 공격은 게이지가 찬 순간 사이클이 넘어가므로 직전 사이클 키 = 순서 바에서 올라온 그 항목
                long key = nbc.ActingIsDisruption ? MakeKey(acting, cycle, EntryKind.Disrupt)
                                                  : MakeKey(acting, cycle - 1, EntryKind.Attack);
                _slots.Add(new Slot { key = key, kind = nbc.ActingIsDisruption ? EntryKind.Disrupt : EntryKind.Attack, enemy = acting, steps = 0 });
            }
            else
            {
                _slots.Add(new Slot { key = PlayerKey, kind = EntryKind.Player, steps = 0 });
            }

            // 2) 이미 게이지가 차서 공격 대기 중인 적
            foreach (EnemyController e in nbc.PendingAttackers)
            {
                if (e == null || !e.IsAlive || _slots.Count > maxUpcoming) continue;
                EnemyStat st = e.GetComponent<EnemyStat>();
                int cycle = st != null ? st.CycleCount : 0;
                _slots.Add(new Slot { key = MakeKey(e, cycle - 1, EntryKind.Attack), kind = EntryKind.Attack, enemy = e, steps = -1 });
            }

            // 3) 행동 게이지 시뮬레이션 — 플레이어 행동 1회마다 모든 적 게이지 +1(빙결 스택이 있으면 스택만 1 소모)
            EnemyCombatParty.CollectAlive(_alive);
            int n = _alive.Count;
            if (n == 0) return;
            int[] step = new int[n], max = new int[n], frost = new int[n], cycleArr = new int[n];
            bool[] mid = new bool[n];
            for (int i = 0; i < n; i++)
            {
                EnemyStat st = _alive[i].GetComponent<EnemyStat>();
                if (st == null) { max[i] = int.MaxValue; continue; }
                step[i] = st.GaugeStep;
                max[i] = Mathf.Max(1, st.GaugeMaxSteps);
                mid[i] = st.MidPatternTriggered;
                cycleArr[i] = st.CycleCount;
                frost[i] = st.statusEffects != null && st.statusEffects.TryGetValue("frost", out int f) ? f : 0;
            }

            for (int k = 1; k <= MaxSimSteps && _slots.Count <= maxUpcoming; k++)
            {
                for (int i = 0; i < n && _slots.Count <= maxUpcoming; i++)
                {
                    if (max[i] == int.MaxValue) continue;
                    if (frost[i] > 0) { frost[i]--; continue; }
                    step[i]++;
                    if (step[i] == max[i] / 2 && !mid[i])
                    {
                        mid[i] = true;
                        if (showDisruptions)
                            _slots.Add(new Slot { key = MakeKey(_alive[i], cycleArr[i], EntryKind.Disrupt), kind = EntryKind.Disrupt, enemy = _alive[i], steps = k });
                    }
                    if (step[i] >= max[i])
                    {
                        _slots.Add(new Slot { key = MakeKey(_alive[i], cycleArr[i], EntryKind.Attack), kind = EntryKind.Attack, enemy = _alive[i], steps = k });
                        step[i] = 0;
                        mid[i] = false;
                        cycleArr[i]++;
                    }
                }
            }
        }

        static long MakeKey(EnemyController e, int cycle, EntryKind kind)
        {
            long id = (uint)e.GetInstanceID();
            return (id << 24) | ((long)(cycle & 0x3FFFFF) << 2) | (long)kind;
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

        // 슬롯 목록대로 칸 생성/재사용, 목표 위치로 보간
        void ApplyLayout()
        {
            foreach (var v in _views.Values) v.used = false;

            float headerH = 34f;
            float y = -headerH;
            float colW = currentSize;
            float dt = Time.deltaTime;
            float t = 1f - Mathf.Exp(-slideSpeed * dt);

            for (int i = 0; i < _slots.Count; i++)
            {
                Slot s = _slots[i];
                bool isCurrent = i == 0;
                float size = isCurrent ? currentSize : entrySize;

                if (!_views.TryGetValue(s.key, out EntryView v))
                {
                    v = CreateEntry(s.key);
                    _views[s.key] = v;
                    // 새 칸은 목표 위치 아래쪽에서 페이드 인
                    v.rect.anchoredPosition = new Vector2((colW - size) * 0.5f, y - size - 20f);
                    v.rect.sizeDelta = new Vector2(size, size);
                    v.group.alpha = 0f;
                }
                v.used = true;
                Fill(v, s, isCurrent);

                Vector2 target = new Vector2((colW - size) * 0.5f, y);
                v.rect.anchoredPosition = Vector2.Lerp(v.rect.anchoredPosition, target, t);
                v.rect.sizeDelta = Vector2.Lerp(v.rect.sizeDelta, new Vector2(size, size), t);
                v.group.alpha = Mathf.MoveTowards(v.group.alpha, 1f, dt * 5f);
                v.rect.SetSiblingIndex(Mathf.Max(0, _entries.childCount - 1 - i)); // 위 칸이 아래 칸 위에 그려지도록

                y -= size + spacing;
                if (isCurrent)
                {
                    _divider.anchoredPosition = new Vector2(0f, y + spacing * 0.5f);
                    y -= spacing;
                }
            }

            // 순서에서 빠진 칸 — 왼쪽으로 빠지며 사라짐
            _removeBuf.Clear();
            foreach (var kv in _views)
                if (!kv.Value.used) _removeBuf.Add(kv.Key);
            for (int i = 0; i < _removeBuf.Count; i++)
            {
                EntryView v = _views[_removeBuf[i]];
                _views.Remove(_removeBuf[i]);
                v.dying = true;
                _dying.Add(v);
            }
            for (int i = _dying.Count - 1; i >= 0; i--)
            {
                EntryView v = _dying[i];
                v.group.alpha = Mathf.MoveTowards(v.group.alpha, 0f, dt * 4f);
                v.rect.anchoredPosition += new Vector2(-220f * dt, 0f);
                if (v.group.alpha <= 0f)
                {
                    Destroy(v.rect.gameObject);
                    _dying.RemoveAt(i);
                }
            }

            float panelH = -y + 8f;
            _panel.rectTransform.sizeDelta = new Vector2(colW + 150f, panelH);
        }

        // 칸 내용(초상화·색·남은 단계·이름) 채우기
        void Fill(EntryView v, Slot s, bool isCurrent)
        {
            Color c = s.kind == EntryKind.Player ? playerColor : s.kind == EntryKind.Attack ? attackColor : disruptColor;
            v.frame.color = isCurrent ? c : Color.Lerp(c, new Color(0.2f, 0.2f, 0.2f), 0.25f);

            Sprite portrait = null;
            string name;
            if (s.kind == EntryKind.Player)
            {
                portrait = playerPortrait;
                name = "나";
            }
            else
            {
                EnemyStat st = s.enemy != null ? s.enemy.GetComponent<EnemyStat>() : null;
                portrait = st != null && st.Data != null ? st.Data.enemySprite : null;
                name = st != null && st.Data != null && !string.IsNullOrEmpty(st.Data.enemyName) ? st.Data.enemyName : "적";
            }

            v.portrait.enabled = portrait != null;
            if (portrait != null && v.portrait.sprite != portrait) v.portrait.sprite = portrait;
            v.initial.gameObject.SetActive(portrait == null);
            if (portrait == null) v.initial.text = name.Substring(0, 1);
            v.initial.fontSize = isCurrent ? 40f : 28f;

            string kindText = s.kind == EntryKind.Attack ? "공격" : s.kind == EntryKind.Disrupt ? "방해" : "";
            if (isCurrent)
            {
                v.badge.gameObject.SetActive(false);
                v.label.text = s.kind == EntryKind.Player
                    ? "<b>내 턴</b>\n<size=75%>카드를 사용하세요</size>"
                    : $"<b>{name}</b>\n<size=75%>{kindText} 중</size>";
                v.label.fontSize = 22f;
            }
            else
            {
                v.badge.gameObject.SetActive(true);
                v.badgeBg.color = c;
                v.badgeText.text = s.steps < 0 ? "!" : s.steps.ToString();
                v.label.text = s.steps < 0
                    ? $"{name}\n<size=80%><color=#{ColorUtility.ToHtmlStringRGB(c)}>{kindText} 대기</color></size>"
                    : $"{name}\n<size=80%><color=#{ColorUtility.ToHtmlStringRGB(c)}>{kindText}</color> · {s.steps}행동 후</size>";
                v.label.fontSize = 17f;
            }
        }

        // ── UI 생성 ──────────────────────────────────────────────
        void Build()
        {
            if (_built) return;
            _built = true;
            if (font == null)
            {
                var anyText = FindFirstObjectByType<TMP_Text>(FindObjectsInactive.Include);
                if (anyText != null) font = anyText.font;
            }

            var rootGo = new GameObject("TurnOrderBar", typeof(RectTransform));
            _root = (RectTransform)rootGo.transform;
            _root.SetParent(transform, false);
            _root.anchorMin = _root.anchorMax = new Vector2(0f, 1f);
            _root.pivot = new Vector2(0f, 1f);
            _root.anchoredPosition = anchoredOffset;
            _root.sizeDelta = new Vector2(currentSize, 10f);

            // 반투명 배경 패널(좌측에서 우측으로 옅어지는 느낌은 색으로만)
            _panel = NewImage("Panel", _root, RoundedSprite(), panelColor);
            _panel.raycastTarget = false;
            var pr = _panel.rectTransform;
            pr.anchorMin = pr.anchorMax = new Vector2(0f, 1f);
            pr.pivot = new Vector2(0f, 1f);
            pr.anchoredPosition = new Vector2(-10f, 8f);

            var div = NewImage("Divider", _root, null, new Color(1f, 1f, 1f, 0.35f));
            _divider = div.rectTransform;
            _divider.anchorMin = _divider.anchorMax = new Vector2(0f, 1f);
            _divider.pivot = new Vector2(0f, 0.5f);
            _divider.sizeDelta = new Vector2(currentSize + 120f, 2f);

            var entriesGo = new GameObject("Entries", typeof(RectTransform));
            _entries = (RectTransform)entriesGo.transform;
            _entries.SetParent(_root, false);
            _entries.anchorMin = _entries.anchorMax = new Vector2(0f, 1f);
            _entries.pivot = new Vector2(0f, 1f);
            _entries.anchoredPosition = Vector2.zero;
            _entries.sizeDelta = Vector2.zero;

            _header = NewText("Header", _root, 26f, TextAlignmentOptions.Left);
            var hr = _header.rectTransform;
            hr.anchorMin = hr.anchorMax = new Vector2(0f, 1f);
            hr.pivot = new Vector2(0f, 1f);
            hr.anchoredPosition = Vector2.zero;
            hr.sizeDelta = new Vector2(160f, 30f);
            _header.fontStyle = FontStyles.Bold;

            _lockHint = NewText("LockHint", _root, 16f, TextAlignmentOptions.Left);
            var lr = _lockHint.rectTransform;
            lr.anchorMin = lr.anchorMax = new Vector2(0f, 1f);
            lr.pivot = new Vector2(0f, 1f);
            lr.anchoredPosition = new Vector2(84f, -6f);
            lr.sizeDelta = new Vector2(180f, 24f);
            _lockHint.text = "적 행동 중에는 사용 불가";
            _lockHint.gameObject.SetActive(false);
        }

        EntryView CreateEntry(long key)
        {
            var v = new EntryView { key = key };
            var go = new GameObject("Entry", typeof(RectTransform), typeof(CanvasGroup));
            v.rect = (RectTransform)go.transform;
            v.rect.SetParent(_entries, false);
            v.rect.anchorMin = v.rect.anchorMax = new Vector2(0f, 1f);
            v.rect.pivot = new Vector2(0f, 1f);
            v.group = go.GetComponent<CanvasGroup>();
            v.group.blocksRaycasts = false;
            v.group.interactable = false;

            // 테두리(행동 종류 색)
            v.frame = NewImage("Frame", v.rect, RoundedSprite(), Color.white);
            Stretch(v.frame.rectTransform, 0f);

            // 초상화 마스크(둥근 사각) + 초상화
            var maskImg = NewImage("Mask", v.rect, RoundedSprite(), new Color(0.12f, 0.12f, 0.16f, 1f));
            Stretch(maskImg.rectTransform, 4f);
            maskImg.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            v.portrait = NewImage("Portrait", maskImg.rectTransform, null, Color.white);
            v.portrait.preserveAspect = true;
            Stretch(v.portrait.rectTransform, 0f);

            v.initial = NewText("Initial", maskImg.rectTransform, 28f, TextAlignmentOptions.Center);
            Stretch(v.initial.rectTransform, 0f);
            v.initial.fontStyle = FontStyles.Bold;

            // 남은 단계 배지(우하단 원)
            var badgeImg = NewImage("Badge", v.rect, CircleSprite(), Color.white);
            v.badgeBg = badgeImg;
            v.badge = badgeImg.rectTransform;
            v.badge.anchorMin = v.badge.anchorMax = new Vector2(1f, 0f);
            v.badge.pivot = new Vector2(0.5f, 0.5f);
            v.badge.anchoredPosition = new Vector2(-4f, 4f);
            v.badge.sizeDelta = new Vector2(26f, 26f);
            v.badgeText = NewText("Steps", v.badge, 16f, TextAlignmentOptions.Center);
            Stretch(v.badgeText.rectTransform, 0f);
            v.badgeText.fontStyle = FontStyles.Bold;

            // 오른쪽 이름/설명
            v.label = NewText("Label", v.rect, 17f, TextAlignmentOptions.MidlineLeft);
            var lr = v.label.rectTransform;
            lr.anchorMin = new Vector2(1f, 0f);
            lr.anchorMax = new Vector2(1f, 1f);
            lr.pivot = new Vector2(0f, 0.5f);
            lr.anchoredPosition = new Vector2(10f, 0f);
            lr.sizeDelta = new Vector2(140f, 0f);
            v.label.textWrappingMode = TextWrappingModes.NoWrap;
            v.label.overflowMode = TextOverflowModes.Overflow;
            return v;
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
