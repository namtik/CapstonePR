using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Battle.Card;
using Battle.Deck;

namespace Battle.UI
{
    /// <summary>
    /// 5장 손패 HUD. NewSkillCard 프리팹을 인스턴스화해서 슬롯을 구성한다.
    /// 콤보 슬롯/콤보 스킬 UI는 피버 모드 토글로 표시/숨김.
    /// </summary>
    public class CardHandHUD : MonoBehaviour
    {
        /// <summary>UI 슬롯 사전 생성 수 — 동적 패 한도(최대 MAX_HAND_LIMIT) 대응을 위해 상한 기준.</summary>
        public const int SLOT_COUNT = CardDeckSystem.MAX_HAND_LIMIT;
        /// <summary>레이아웃 기준 카드 수 — 이 값 기준으로 한 칸 간격이 정해진다(과거 SLOT_COUNT=5 호환).</summary>
        public const int LAYOUT_REFERENCE_COUNT = CardDeckSystem.HAND_LIMIT;

        [Header("프리팹 (필수)")]
        [SerializeField] private NewCardView cardPrefab;

        [Header("배치")]
        [Tooltip("카드 슬롯들이 정렬될 부모. 비우면 본 트랜스폼 자식으로 자동 생성.")]
        [SerializeField] private RectTransform handRoot;
        [Tooltip("드래그 중 카드가 옮겨질 상위 캔버스 레이어. 비우면 같은 캔버스의 최상단을 자동 사용.")]
        [SerializeField] private RectTransform dragLayer;
        public RectTransform DragLayer => dragLayer != null ? dragLayer : handRoot;

        [Header("레이아웃 — 슬롯 위치 자동 정렬")]
        [Tooltip("슬롯 간 간격 (x). NewSkillCard 프리팹이 scale=1, 300×400일 때 650~750 권장.")]
        [SerializeField] private Vector2 slotSpacing = new Vector2(700f, 0f);
        [SerializeField] private Vector2 handAnchoredPos = new Vector2(0f, 240f);
        [Tooltip("드래그 종료 시 스크린 Y가 이 값 이상이면 카드 사용으로 간주.")]
        [SerializeField] private float useThresholdY = 500f;

        [Header("Fan Layout — 손패 부채꼴 연출")]
        [Tooltip("ON이면 카드들이 호 형태로 회전·배치된다.")]
        [SerializeField] private bool fanLayout = true;
        [Tooltip("부채꼴 반지름. 클수록 호가 평평해짐.")]
        [SerializeField] private float fanRadius = 1800f;
        [Tooltip("부채꼴 전체 각도(도). 클수록 카드 사이 간격이 넓어짐. 5장 기준 36~44 권장.")]
        [SerializeField] private float fanArcAngle = 40f;
        [Tooltip("호 위 가장자리에서 카드들이 떨어질 깊이. 작을수록 카드들이 살짝 아래로 호를 그림.")]
        [SerializeField] private float fanVerticalDip = 50f;

        [Header("콤보 UI — 피버 전용")]
        [Tooltip("피버 발동 시 활성화될 콤보 슬롯 패널.")]
        [SerializeField] private GameObject comboSlotPanel;
        [Tooltip("피버 발동 시 활성화될 콤보 스킬 목록 패널.")]
        [SerializeField] private GameObject comboSkillPanel;
        [Tooltip("콤보 슬롯 3칸의 Image. 비워두면 comboSlotPanel 자식에서 자동 탐색.")]
        [SerializeField] private Image[] comboSlotImages = new Image[3];
        [SerializeField] private Color emptyComboSlotColor = new Color(0.25f, 0.25f, 0.25f, 0.4f);

        [Header("콤보 스킬 목록 (SkillListItem 프리팹)")]
        [Tooltip("콤보 스킬 한 항목을 표현할 프리팹. SkillListItem.prefab 사용.")]
        [SerializeField] private SkillListItemUI comboSkillItemPrefab;
        [Tooltip("콤보 스킬 항목들이 배치될 컨테이너. 비워두면 comboSkillPanel을 사용.")]
        [SerializeField] private RectTransform comboSkillItemContainer;

        [Header("정보 텍스트")]
        [SerializeField] private TMP_Text drawCountText;
        [SerializeField] private TMP_Text discardCountText;
        [SerializeField] private TMP_Text feverCountText;

        [Header("피버 게이지 (HP바 아래)")]
        [Tooltip("게이지 슬라이더 — 비우면 SetFeverGaugeAnchor 호출 또는 첫 갱신 시 자동 생성.")]
        [SerializeField] private Slider feverGauge;
        [Tooltip("게이지를 부착할 기준 RectTransform — 보통 Player.hpBar의 RectTransform. 비우면 NewBattleController가 런타임에 넣어줌.")]
        [SerializeField] private RectTransform feverGaugeAnchor;
        [Tooltip("게이지 자동 생성 시 크기.")]
        [SerializeField] private Vector2 feverGaugeSize = new Vector2(300f, 22f);
        [Tooltip("anchor 기준 오프셋. y 음수 = 아래(HP바 아래).")]
        [SerializeField] private Vector2 feverGaugeOffset = new Vector2(0f, -32f);
        [Tooltip("게이지 위에 표시할 라벨 (예: 14/15, READY (F), 10.0s). 비우면 게이지 자동 생성 시 함께 생성.")]
        [SerializeField] private TMP_Text feverGaugeLabel;
        [Tooltip("충전 진행 중 색.")]
        [SerializeField] private Color feverChargeColor = new Color(1f, 0.55f, 0.15f, 1f);
        [Tooltip("충전 완료(READY) 색.")]
        [SerializeField] private Color feverArmedColor = new Color(1f, 0.85f, 0.2f, 1f);
        [Tooltip("발동 중 색(카운트다운).")]
        [SerializeField] private Color feverActiveColor = new Color(0.75f, 0.35f, 1f, 1f);
        [Tooltip("READY 상태 펄스 속도(Hz). 0이면 펄스 없음.")]
        [SerializeField] private float feverArmedPulseHz = 2.5f;
        [Tooltip("READY 펄스 최소 알파(0~1).")]
        [SerializeField] private float feverArmedPulseMinAlpha = 0.55f;

        [Header("피버 입력 히스토리 (왼쪽 표시)")]
        [Tooltip("피버 동안 입력된 속성 카드 전체 히스토리가 표시될 부모. 비우면 자동 생성.")]
        [SerializeField] private RectTransform feverHistoryContainer;
        [Tooltip("히스토리 한 칸 크기.")]
        [SerializeField] private Vector2 feverHistoryItemSize = new Vector2(60f, 60f);
        [Tooltip("히스토리 칸 간 세로 간격.")]
        [SerializeField] private float feverHistoryItemSpacing = 8f;
        [Tooltip("한 열에 표시할 최대 항목 수 — 이 이상이면 오른쪽 새 열로 이동.")]
        [SerializeField] private int feverHistoryItemsPerColumn = 8;
        [Tooltip("열과 열 사이 가로 간격.")]
        [SerializeField] private float feverHistoryColumnSpacing = 8f;
        [Tooltip("히스토리 컨테이너 위치(앵커 기준). 좌측 가운데 추천.")]
        [SerializeField] private Vector2 feverHistoryAnchoredPos = new Vector2(80f, 0f);
        [Tooltip("표시할 최대 항목 수 — 초과 시 가장 오래된 것부터 숨김.")]
        [SerializeField] private int feverHistoryMaxItems = 64;

        private readonly List<NewCardView> _cardViews = new List<NewCardView>();
        private readonly List<RectTransform> _slotAnchors = new List<RectTransform>();
        private CardDeckSystem _deck;
        public System.Func<CardInstance, bool> UseCardCallback;

        // ── 피버 게이지 내부 상태 ──
        private Image _feverGaugeFill;
        private float _feverArmedPulseT;
        private bool _feverGaugeArmedState; // 펄스 적용 여부 (armed && !active)

        // ── 카드 선택 모드 (예: 불3 "패에서 카드 하나 선택해 소멸") ──
        private System.Action<CardInstance> _selectionCallback;
        private System.Func<CardInstance, bool> _selectionFilter;
        private CardInstance _selectionExcludeCard;
        public bool IsSelectionMode => _selectionCallback != null;

        [Header("선택 모드 UI")]
        [Tooltip("선택 모드 안내 텍스트. 비워두면 표시 안 함.")]
        [SerializeField] private TMP_Text selectionPromptText;
        [Tooltip("선택 모드 시 화면을 덮는 어둡게 처리 오버레이. 비우면 자동 생성.")]
        [SerializeField] private RectTransform dimOverlay;
        [Tooltip("dim 오버레이 색(알파로 어둡기 조절) — 선택/픽커 모드.")]
        [SerializeField] private Color dimColor = new Color(0f, 0f, 0f, 0.6f);
        [Tooltip("피버 모드 dim 색 — 일반 선택 모드와 다른 톤으로 구분 가능. 카드 선택 dim(dimColor)과 독립.")]
        [SerializeField] private Color feverDimColor = new Color(0.02f, 0.0f, 0.08f, 0.82f);

        void Awake()
        {
            EnsureHandRoot();
            EnsureDragLayer();
            BuildSlotAnchors();
            SetFeverMode(false); // 초기엔 콤보 UI 숨김
        }

        public void Bind(CardDeckSystem deck)
        {
            if (_deck != null) _deck.OnPileChanged -= Refresh;
            _deck = deck;
            if (_deck != null) _deck.OnPileChanged += Refresh;
            Refresh();
        }

        void OnDestroy()
        {
            if (_deck != null) _deck.OnPileChanged -= Refresh;
        }

        public void SetFeverText(string text)
        {
            if (feverCountText != null) feverCountText.text = text;
        }

        /// <summary>
        /// 피버 게이지를 부착할 기준 RectTransform을 외부에서 지정 (보통 Player.hpBar).
        /// 이미 게이지가 자동 생성됐다면 부모를 재배치한다.
        /// </summary>
        public void SetFeverGaugeAnchor(RectTransform anchor)
        {
            if (anchor == null) return;
            feverGaugeAnchor = anchor;
            EnsureFeverGauge();
            RefitFeverGaugeToAnchor();
        }

        /// <summary>
        /// 피버 상태에 따라 게이지를 갱신.
        /// chargeCur/chargeMax: 충전 단계(0~15) — armed/active=false일 때 사용.
        /// armed: 충전 완료 후 F키 대기.
        /// active: 발동 중 — timeRemaining/timeMax로 fill 계산. timeMax는 콤보 보너스로 동적 증가.
        /// </summary>
        public void UpdateFeverGauge(int chargeCur, int chargeMax, bool armed, bool active, float timeRemaining, float timeMax)
        {
            EnsureFeverGauge();
            if (feverGauge == null) return;

            float fill;
            Color color;
            string label;
            if (active)
            {
                float denom = timeMax > 0f ? timeMax : 1f;
                fill = Mathf.Clamp01(timeRemaining / denom);
                color = feverActiveColor;
                label = $"{Mathf.Max(0f, timeRemaining):F1}s";
            }
            else if (armed)
            {
                fill = 1f;
                color = feverArmedColor;
                label = "READY (F)";
            }
            else
            {
                int max = Mathf.Max(1, chargeMax);
                int cur = Mathf.Clamp(chargeCur, 0, max);
                fill = (float)cur / max;
                color = feverChargeColor;
                label = $"{cur}/{max}";
            }

            feverGauge.value = fill;
            if (_feverGaugeFill != null)
            {
                // armed 상태는 펄스가 알파를 매 프레임 갱신하므로 현재 알파 유지, 그 외엔 1.0
                float alpha = (armed && !active) ? _feverGaugeFill.color.a : 1f;
                _feverGaugeFill.color = new Color(color.r, color.g, color.b, alpha);
            }
            if (feverGaugeLabel != null) feverGaugeLabel.text = label;

            bool wasArmed = _feverGaugeArmedState;
            _feverGaugeArmedState = armed && !active;
            if (!_feverGaugeArmedState && wasArmed && _feverGaugeFill != null)
            {
                var c = _feverGaugeFill.color;
                _feverGaugeFill.color = new Color(c.r, c.g, c.b, 1f);
                _feverArmedPulseT = 0f;
            }
        }

        void Update()
        {
            UpdateFeverArmedPulse();
        }

        void UpdateFeverArmedPulse()
        {
            if (!_feverGaugeArmedState || _feverGaugeFill == null) return;
            if (feverArmedPulseHz <= 0f) return;
            _feverArmedPulseT += Time.unscaledDeltaTime * feverArmedPulseHz * Mathf.PI * 2f;
            float t = 0.5f + 0.5f * Mathf.Sin(_feverArmedPulseT);
            float a = Mathf.Lerp(Mathf.Clamp01(feverArmedPulseMinAlpha), 1f, t);
            var c = _feverGaugeFill.color;
            _feverGaugeFill.color = new Color(c.r, c.g, c.b, a);
        }

        void EnsureFeverGauge()
        {
            if (feverGauge != null)
            {
                if (_feverGaugeFill == null) _feverGaugeFill = ResolveGaugeFill(feverGauge);
                return;
            }

            // 부모 결정: anchor가 있으면 anchor의 부모(같은 레벨에 형제로 두기), 없으면 캔버스
            Transform parent = null;
            if (feverGaugeAnchor != null)
                parent = feverGaugeAnchor.parent != null ? feverGaugeAnchor.parent : (Transform)feverGaugeAnchor;
            if (parent == null)
            {
                Canvas canvas = ResolveTargetCanvas();
                parent = canvas != null ? canvas.transform : transform;
            }

            // 루트
            var go = new GameObject("FeverGauge", typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = feverGaugeSize;

            // 배경
            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(rect, false);
            var bgRect = (RectTransform)bgGo.transform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImg = bgGo.GetComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.6f);
            bgImg.raycastTarget = false;

            // Fill Area
            var fillAreaGo = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaGo.transform.SetParent(rect, false);
            var fillAreaRect = (RectTransform)fillAreaGo.transform;
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = new Vector2(2f, 2f);
            fillAreaRect.offsetMax = new Vector2(-2f, -2f);

            // Fill
            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(fillAreaRect, false);
            var fillRect = (RectTransform)fillGo.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImg = fillGo.GetComponent<Image>();
            fillImg.color = feverChargeColor;
            fillImg.raycastTarget = false;

            var slider = go.GetComponent<Slider>();
            slider.transition = Selectable.Transition.None;
            slider.interactable = false;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.fillRect = fillRect;
            slider.targetGraphic = fillImg;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
            slider.direction = Slider.Direction.LeftToRight;

            feverGauge = slider;
            _feverGaugeFill = fillImg;

            // 라벨 자동 생성
            if (feverGaugeLabel == null)
            {
                var labelGo = new GameObject("Label", typeof(RectTransform));
                labelGo.transform.SetParent(rect, false);
                var labelRect = (RectTransform)labelGo.transform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;
                var tmp = labelGo.AddComponent<TextMeshProUGUI>();
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontSize = 16;
                tmp.fontStyle = FontStyles.Bold;
                tmp.color = Color.white;
                tmp.text = "";
                tmp.raycastTarget = false;
                feverGaugeLabel = tmp;
            }

            RefitFeverGaugeToAnchor();
        }

        void RefitFeverGaugeToAnchor()
        {
            if (feverGauge == null || feverGaugeAnchor == null) return;
            var rect = (RectTransform)feverGauge.transform;
            var anchorParent = feverGaugeAnchor.parent != null
                ? feverGaugeAnchor.parent
                : (Transform)feverGaugeAnchor;
            if (rect.parent != anchorParent) rect.SetParent(anchorParent, false);

            // anchor와 같은 정렬 기준으로 맞춘 후 오프셋 적용
            rect.anchorMin = feverGaugeAnchor.anchorMin;
            rect.anchorMax = feverGaugeAnchor.anchorMax;
            rect.pivot = feverGaugeAnchor.pivot;
            rect.anchoredPosition = feverGaugeAnchor.anchoredPosition + feverGaugeOffset;
            rect.sizeDelta = feverGaugeSize;
            rect.SetAsLastSibling();
        }

        static Image ResolveGaugeFill(Slider slider)
        {
            if (slider == null) return null;
            if (slider.fillRect != null)
            {
                var img = slider.fillRect.GetComponent<Image>();
                if (img != null) return img;
            }
            return slider.GetComponentInChildren<Image>();
        }

        /// <summary>피버 활성/비활성에 따라 콤보 슬롯/스킬 UI를 토글 + dim 오버레이 처리.</summary>
        public void SetFeverMode(bool active)
        {
            if (comboSlotPanel != null) comboSlotPanel.SetActive(active);
            if (comboSkillPanel != null) comboSkillPanel.SetActive(active);
            if (feverHistoryContainer != null) feverHistoryContainer.gameObject.SetActive(active);

            if (active) ShowFeverDim();
            else HideFeverDim();
        }

        void ShowFeverDim()
        {
            EnsureDimOverlay();
            if (dimOverlay != null)
            {
                // 피버 전용 색으로 변경
                var img = dimOverlay.GetComponent<Image>();
                if (img != null) img.color = feverDimColor;
                dimOverlay.gameObject.SetActive(true);
                dimOverlay.SetAsLastSibling();
            }
            // 손패/콤보 UI를 dim 위로 — dim 위에 있어야 어두워지지 않음
            if (handRoot != null) handRoot.SetAsLastSibling();
            if (comboSlotPanel != null) comboSlotPanel.transform.SetAsLastSibling();
            if (comboSkillPanel != null) comboSkillPanel.transform.SetAsLastSibling();
            if (feverHistoryContainer != null) feverHistoryContainer.SetAsLastSibling();
            if (feverCountText != null) feverCountText.transform.SetAsLastSibling();
        }

        void HideFeverDim()
        {
            if (dimOverlay != null)
            {
                dimOverlay.gameObject.SetActive(false);
                // 다음 선택/픽커 모드를 위해 색을 원래 dimColor로 복구
                var img = dimOverlay.GetComponent<Image>();
                if (img != null) img.color = dimColor;
            }
            // handRoot 시블링 위치 복원 (다른 UI보다 너무 위에 있지 않도록)
            if (handRoot != null) handRoot.SetAsLastSibling();
        }

        // ─────────────────────────────────────────────────────────────
        // 피버 입력 히스토리 (왼쪽 세로 표시)
        // ─────────────────────────────────────────────────────────────

        private readonly List<Image> _feverHistoryViews = new List<Image>();

        public void UpdateFeverInputHistory(IList<CardElement> history)
        {
            EnsureFeverHistoryContainer();
            if (feverHistoryContainer == null) return;

            int total = history != null ? history.Count : 0;
            int max = Mathf.Max(1, feverHistoryMaxItems);
            // 표시 슬라이스: 최근 max개만(오래된 게 잘림)
            int start = Mathf.Max(0, total - max);
            int visible = total - start;

            // 풀 확장
            while (_feverHistoryViews.Count < visible)
            {
                var go = new GameObject($"FevHist_{_feverHistoryViews.Count}", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(feverHistoryContainer, false);
                var rect = (RectTransform)go.transform;
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = feverHistoryItemSize;
                var img = go.GetComponent<Image>();
                img.raycastTarget = false;
                _feverHistoryViews.Add(img);
            }

            // 활성/비활성/내용 갱신 — 한 열에 itemsPerColumn개씩, 초과 시 오른쪽 새 열로 wrap
            int itemsPerCol = Mathf.Max(1, feverHistoryItemsPerColumn);
            float colW = feverHistoryItemSize.x + feverHistoryColumnSpacing;
            float rowH = feverHistoryItemSize.y + feverHistoryItemSpacing;
            for (int i = 0; i < _feverHistoryViews.Count; i++)
            {
                var img = _feverHistoryViews[i];
                if (i < visible)
                {
                    img.gameObject.SetActive(true);
                    var rect = (RectTransform)img.transform;
                    int col = i / itemsPerCol;
                    int row = i % itemsPerCol;
                    rect.anchoredPosition = new Vector2(col * colW, -row * rowH);

                    var element = history[start + i];
                    var sp = cardPrefab != null ? cardPrefab.GetElementSprite(element) : null;
                    if (sp != null)
                    {
                        img.sprite = sp;
                        img.color = Color.white;
                    }
                    else
                    {
                        img.sprite = null;
                        img.color = ColorForElement(element);
                    }
                }
                else
                {
                    img.gameObject.SetActive(false);
                }
            }
        }

        void EnsureFeverHistoryContainer()
        {
            if (feverHistoryContainer != null) return;
            Canvas canvas = ResolveTargetCanvas();
            Transform parent = canvas != null ? canvas.transform : transform;

            var go = new GameObject("FeverHistoryContainer", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            feverHistoryContainer = (RectTransform)go.transform;
            // 좌측 중앙 정렬 (anchor를 좌측에, pivot도 좌측에)
            feverHistoryContainer.anchorMin = new Vector2(0f, 0.5f);
            feverHistoryContainer.anchorMax = new Vector2(0f, 0.5f);
            feverHistoryContainer.pivot = new Vector2(0f, 0.5f);
            feverHistoryContainer.anchoredPosition = feverHistoryAnchoredPos;
            feverHistoryContainer.sizeDelta = new Vector2(feverHistoryItemSize.x, 600f);
            // 기본적으로 active로 생성 — SetFeverMode가 visibility 제어
            // (자동 생성 타이밍이 ActivateFever 안의 UpdateFeverInputHistory 호출이라 active 상태가 맞음)
        }

        /// <summary>콤보 슬롯 3칸을 현재 입력 시퀀스로 갱신. 빈 슬롯은 회색.</summary>
        public void UpdateComboSlot(IList<CardElement> input)
        {
            EnsureComboSlotImagesBound();

            if (comboSlotImages == null || comboSlotImages.Length == 0)
            {
                Debug.LogWarning("[CardHandHUD] UpdateComboSlot: comboSlotImages가 비어있고 자동 탐색도 실패. ComboSlotPanel의 자식 Image 3개를 인스펙터에 연결하세요.");
                return;
            }

            for (int i = 0; i < comboSlotImages.Length; i++)
            {
                var img = comboSlotImages[i];
                if (img == null) continue;

                if (input != null && i < input.Count)
                {
                    Sprite s = cardPrefab != null ? cardPrefab.GetElementSprite(input[i]) : null;
                    if (s != null)
                    {
                        img.sprite = s;
                        img.color = Color.white;
                    }
                    else
                    {
                        img.sprite = null;
                        img.color = ColorForElement(input[i]);
                    }
                    img.enabled = true;
                }
                else
                {
                    img.sprite = null;
                    img.color = emptyComboSlotColor;
                    img.enabled = true;
                }
            }
        }

        /// <summary>인스펙터에 콤보 슬롯 Image가 안 연결됐으면 comboSlotPanel 자식에서 Image 3개를 자동 탐색.</summary>
        void EnsureComboSlotImagesBound()
        {
            bool needBind = comboSlotImages == null || comboSlotImages.Length < 3
                || comboSlotImages[0] == null || comboSlotImages[1] == null || comboSlotImages[2] == null;
            if (!needBind) return;
            if (comboSlotPanel == null) return;

            var collected = new List<Image>();
            // 직계 자식 우선
            for (int i = 0; i < comboSlotPanel.transform.childCount && collected.Count < 3; i++)
            {
                var img = comboSlotPanel.transform.GetChild(i).GetComponent<Image>();
                if (img != null) collected.Add(img);
            }
            // 부족하면 모든 후손 검색
            if (collected.Count < 3)
            {
                var all = comboSlotPanel.GetComponentsInChildren<Image>(true);
                foreach (var img in all)
                {
                    if (img == null) continue;
                    if (img.gameObject == comboSlotPanel) continue; // 패널 자체는 제외
                    if (collected.Contains(img)) continue;
                    collected.Add(img);
                    if (collected.Count >= 3) break;
                }
            }

            if (collected.Count >= 3)
            {
                comboSlotImages = new Image[3] { collected[0], collected[1], collected[2] };
                Debug.Log("[CardHandHUD] 콤보 슬롯 Image 3개 자동 바인딩 완료.");
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 콤보 스킬 목록 — SkillListItem 프리팹 인스턴스화
        // ─────────────────────────────────────────────────────────────

        private readonly List<SkillListItemUI> _comboSkillItems = new List<SkillListItemUI>();

        /// <summary>콤보 스킬 목록을 SkillListItem 프리팹으로 표시. 발동된 스킬은 반투명.</summary>
        public void UpdateComboSkillList(IList<ComboSkillDef> skills, HashSet<int> activatedIndices)
        {
            if (comboSkillItemPrefab == null)
            {
                Debug.LogWarning("[CardHandHUD] comboSkillItemPrefab이 미연결 — 콤보 스킬 목록 표시 불가.");
                return;
            }

            RectTransform container = comboSkillItemContainer != null
                ? comboSkillItemContainer
                : (comboSkillPanel != null ? comboSkillPanel.transform as RectTransform : null);

            if (container == null)
            {
                Debug.LogWarning("[CardHandHUD] 콤보 스킬 컨테이너가 없음 — comboSkillItemContainer 또는 comboSkillPanel 연결 필요.");
                return;
            }

            // 필요 만큼 인스턴스화
            int needed = skills != null ? skills.Count : 0;
            while (_comboSkillItems.Count < needed)
            {
                var item = Instantiate(comboSkillItemPrefab, container);
                _comboSkillItems.Add(item);
            }

            // 속성 sprite는 NewSkillCard prefab의 매핑을 공유
            Sprite fireSp  = cardPrefab != null ? cardPrefab.GetElementSprite(CardElement.Fire)  : null;
            Sprite waterSp = cardPrefab != null ? cardPrefab.GetElementSprite(CardElement.Water) : null;
            Sprite windSp  = cardPrefab != null ? cardPrefab.GetElementSprite(CardElement.Wind)  : null;
            Sprite earthSp = cardPrefab != null ? cardPrefab.GetElementSprite(CardElement.Earth) : null;

            // 표시 갱신
            for (int i = 0; i < _comboSkillItems.Count; i++)
            {
                var item = _comboSkillItems[i];
                if (item == null) continue;

                if (i < needed && skills[i] != null)
                {
                    item.gameObject.SetActive(true);
                    bool activated = activatedIndices != null && activatedIndices.Contains(i);
                    item.SetupForCombo(skills[i], activated, fireSp, waterSp, windSp, earthSp);
                }
                else
                {
                    item.gameObject.SetActive(false);
                }
            }
        }

        static Color ColorForElement(CardElement element) => element switch
        {
            CardElement.Fire    => new Color(0.95f, 0.55f, 0.40f, 1f),
            CardElement.Water   => new Color(0.50f, 0.75f, 0.95f, 1f),
            CardElement.Wind    => new Color(0.60f, 0.90f, 0.60f, 1f),
            CardElement.Earth   => new Color(0.85f, 0.70f, 0.45f, 1f),
            CardElement.Neutral => new Color(0.70f, 0.70f, 0.70f, 1f),
            _ => new Color(0.5f, 0.5f, 0.5f, 1f)
        };

        // ─────────────────────────────────────────────────────────────
        // 손패 갱신
        // ─────────────────────────────────────────────────────────────

        public void Refresh()
        {
            if (_deck == null) return;

            var hand = _deck.Hand;
            // 손 패 갯수에 맞춰 슬롯 위치 재계산 — 카드가 적으면 가운데로 모임
            PositionSlotAnchorsForCount(hand.Count);

            for (int i = 0; i < SLOT_COUNT; i++)
            {
                NewCardView view = i < _cardViews.Count ? _cardViews[i] : null;
                if (view == null && cardPrefab != null && i < _slotAnchors.Count)
                {
                    view = Instantiate(cardPrefab, _slotAnchors[i]);
                    view.Bind(this, i);
                    _cardViews.Add(view);
                }
                if (view == null) continue;

                // 사용된 카드 view가 DragLayer로 옮겨졌을 수 있으므로 매번 슬롯으로 복원
                if (i < _slotAnchors.Count)
                {
                    RectTransform vrect = (RectTransform)view.transform;
                    vrect.SetParent(_slotAnchors[i], false);
                    vrect.anchorMin = new Vector2(0.5f, 0.5f);
                    vrect.anchorMax = new Vector2(0.5f, 0.5f);
                    vrect.pivot = new Vector2(0.5f, 0.5f);
                    vrect.anchoredPosition = Vector2.zero;
                    vrect.localRotation = Quaternion.identity; // 슬롯의 fan 회전을 그대로 따름
                    view.ResetToHomeScale();
                }

                CardInstance card = i < hand.Count ? hand[i] : null;
                view.SetCard(card);
                view.CaptureHome();
            }

            if (drawCountText != null) drawCountText.text = $"{_deck.DrawCount}";
            if (discardCountText != null) discardCountText.text = $"{_deck.DiscardCount}";
        }

        public bool TryUseFromDrag(NewCardView view, PointerEventData ev)
        {
            if (view.Card == null) return false;
            // 선택/픽커 모드에서는 드래그 사용 금지
            if (IsSelectionMode || IsPickerMode) return false;
            if (UseCardCallback == null) return false;
            if (ev.position.y < useThresholdY) return false;
            return UseCardCallback(view.Card);
        }

        /// <summary>더블클릭으로 카드 사용 (드래그 없이도 사용 가능).</summary>
        public bool TryUseFromClick(NewCardView view)
        {
            if (view == null || view.Card == null) return false;
            if (IsSelectionMode || IsPickerMode) return false;
            if (UseCardCallback == null) return false;
            return UseCardCallback(view.Card);
        }

        // ─────────────────────────────────────────────────────────────
        // 카드 선택 모드 (불3 등)
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 패에서 카드 1장 직접 선택을 받는다. filter가 null이면 모든 카드 허용.
        /// excludeCard는 제외(예: 효과를 시전한 카드 자기 자신).
        /// </summary>
        public void EnterSelectionMode(string promptMessage, System.Action<CardInstance> callback,
            System.Func<CardInstance, bool> filter = null, CardInstance excludeCard = null)
        {
            _selectionCallback = callback;
            _selectionFilter = filter;
            _selectionExcludeCard = excludeCard;

            // 화면 어둡게 + 손패를 오버레이 위로 올려 강조
            EnsureDimOverlay();
            if (dimOverlay != null)
            {
                dimOverlay.gameObject.SetActive(true);
                dimOverlay.SetAsLastSibling();           // 오버레이를 다른 UI 위로
            }
            if (handRoot != null) handRoot.SetAsLastSibling(); // 손패를 오버레이보다 더 위로

            if (selectionPromptText != null)
            {
                selectionPromptText.text = promptMessage;
                selectionPromptText.gameObject.SetActive(true);
                selectionPromptText.transform.SetAsLastSibling(); // 안내문도 오버레이 위로
            }
        }

        public void ExitSelectionMode()
        {
            _selectionCallback = null;
            _selectionFilter = null;
            _selectionExcludeCard = null;

            if (dimOverlay != null) dimOverlay.gameObject.SetActive(false);

            if (selectionPromptText != null)
                selectionPromptText.gameObject.SetActive(false);
        }

        // ─────────────────────────────────────────────────────────────
        // 카드 픽커 모드 (버린 더미/뽑을 더미/파편 풀 등에서 1장 선택)
        // ─────────────────────────────────────────────────────────────

        private RectTransform _pickerRoot;
        private readonly List<NewCardView> _pickerViews = new List<NewCardView>();
        private System.Action<CardInstance> _pickerCallback;
        public bool IsPickerMode => _pickerCallback != null;

        /// <summary>
        /// 임의의 카드 목록에서 1장을 선택받는다. 임시 카드 뷰를 그리드로 깔고 클릭 시 콜백 발화.
        /// </summary>
        public void EnterCardPickerMode(string promptMessage, IList<CardInstance> cards,
            System.Action<CardInstance> callback)
        {
            if (cards == null || cards.Count == 0)
            {
                callback?.Invoke(null);
                return;
            }
            if (cardPrefab == null)
            {
                Debug.LogWarning("[CardHandHUD] EnterCardPickerMode — cardPrefab 미설정. 무작위 자동 선택 폴백.");
                callback?.Invoke(cards[Random.Range(0, cards.Count)]);
                return;
            }

            _pickerCallback = callback;

            EnsureDimOverlay();
            EnsurePickerRoot();

            // 순서: handRoot(맨 뒤) → dim → 안내문 → pickerRoot(최상단)
            if (handRoot != null) handRoot.SetAsFirstSibling();
            if (dimOverlay != null)
            {
                dimOverlay.gameObject.SetActive(true);
                dimOverlay.SetAsLastSibling();
            }
            if (selectionPromptText != null)
            {
                selectionPromptText.text = promptMessage;
                selectionPromptText.gameObject.SetActive(true);
                selectionPromptText.transform.SetAsLastSibling();
            }
            _pickerRoot.gameObject.SetActive(true);
            _pickerRoot.SetAsLastSibling();
            BuildPickerCards(cards);
        }

        public void ExitPickerMode()
        {
            _pickerCallback = null;
            ClearPickerViews();
            if (_pickerRoot != null) _pickerRoot.gameObject.SetActive(false);
            if (dimOverlay != null) dimOverlay.gameObject.SetActive(false);
            if (selectionPromptText != null) selectionPromptText.gameObject.SetActive(false);
            // 손패가 SetAsFirstSibling 됐던 것 복원 — 최상단으로 다시 올림
            if (handRoot != null) handRoot.SetAsLastSibling();
        }

        void EnsurePickerRoot()
        {
            if (_pickerRoot != null) return;
            Canvas canvas = ResolveTargetCanvas();
            Transform parent = canvas != null ? canvas.transform : transform;

            var go = new GameObject("CardPickerRoot", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            _pickerRoot = (RectTransform)go.transform;
            _pickerRoot.anchorMin = new Vector2(0.5f, 0.5f);
            _pickerRoot.anchorMax = new Vector2(0.5f, 0.5f);
            _pickerRoot.pivot = new Vector2(0.5f, 0.5f);
            _pickerRoot.anchoredPosition = Vector2.zero;
            _pickerRoot.sizeDelta = new Vector2(1600f, 600f);
        }

        void ClearPickerViews()
        {
            for (int i = 0; i < _pickerViews.Count; i++)
            {
                if (_pickerViews[i] != null) Destroy(_pickerViews[i].gameObject);
            }
            _pickerViews.Clear();
        }

        void BuildPickerCards(IList<CardInstance> cards)
        {
            ClearPickerViews();
            const int maxPerRow = 6;
            const float colSpacing = 350f;
            const float rowSpacing = 440f;

            int total = cards.Count;
            int rows = Mathf.CeilToInt(total / (float)maxPerRow);
            for (int i = 0; i < total; i++)
            {
                int row = i / maxPerRow;
                int col = i % maxPerRow;
                int cardsInRow = (row == rows - 1) ? (total - row * maxPerRow) : maxPerRow;
                float rowWidth = colSpacing * (cardsInRow - 1);
                float x = -rowWidth * 0.5f + col * colSpacing;
                float y = (rows - 1) * rowSpacing * 0.5f - row * rowSpacing;

                var view = Instantiate(cardPrefab, _pickerRoot);
                view.Bind(this, -1); // 슬롯 인덱스 -1 = 픽커 뷰
                var vrect = (RectTransform)view.transform;
                vrect.anchorMin = new Vector2(0.5f, 0.5f);
                vrect.anchorMax = new Vector2(0.5f, 0.5f);
                vrect.pivot = new Vector2(0.5f, 0.5f);
                vrect.anchoredPosition = new Vector2(x, y);
                vrect.localRotation = Quaternion.identity;
                view.SetCard(cards[i]);
                view.CaptureHome();
                _pickerViews.Add(view);
            }
        }

        bool TryHandlePickerClick(NewCardView view)
        {
            if (_pickerCallback == null) return false;
            if (view == null || view.Card == null) return true;
            if (!_pickerViews.Contains(view)) return true; // 픽커 뷰만 유효

            var cb = _pickerCallback;
            CardInstance picked = view.Card;
            ExitPickerMode();
            cb?.Invoke(picked);
            return true;
        }

        /// <summary>선택 모드용 전체 화면 어둡게 처리 오버레이 생성(캔버스 안에).</summary>
        void EnsureDimOverlay()
        {
            if (dimOverlay != null) return;

            Canvas canvas = ResolveTargetCanvas();
            Transform parent = canvas != null ? canvas.transform : transform;

            var go = new GameObject("SelectionDimOverlay", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var img = go.GetComponent<Image>();
            img.color = dimColor;
            img.raycastTarget = true; // 오버레이 뒤쪽 UI 클릭 차단

            dimOverlay = rect;
            dimOverlay.gameObject.SetActive(false);
        }

        /// <summary>NewCardView가 클릭됐을 때 호출. 픽커/선택 모드면 콜백 발동.</summary>
        public void OnCardClicked(NewCardView view)
        {
            // 픽커 모드 우선
            if (TryHandlePickerClick(view)) return;

            if (!IsSelectionMode) return;
            if (view == null || view.Card == null) return;
            if (_selectionExcludeCard != null && view.Card == _selectionExcludeCard) return;
            if (_selectionFilter != null && !_selectionFilter(view.Card)) return;

            var cb = _selectionCallback;
            CardInstance selected = view.Card;
            ExitSelectionMode();
            cb?.Invoke(selected);
        }

        // ─────────────────────────────────────────────────────────────
        // 루트/슬롯 자동 셋업
        // ─────────────────────────────────────────────────────────────

        /// <summary>UI 좌표계를 보장하기 위해 캔버스를 우선순위대로 탐색.</summary>
        Canvas ResolveTargetCanvas()
        {
            // 1) CardHandHUD 자신의 부모 캔버스 (CardHandHUD가 캔버스 안일 때)
            Canvas canvas = GetComponentInParent<Canvas>();
            // 2) handRoot가 인스펙터로 연결되어 있으면 그 캔버스
            if (canvas == null && handRoot != null) canvas = handRoot.GetComponentInParent<Canvas>();
            // 3) dragLayer가 연결되어 있으면 그 캔버스
            if (canvas == null && dragLayer != null) canvas = dragLayer.GetComponentInParent<Canvas>();
            // 4) 씬 전체 캔버스 검색
            if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
            return canvas;
        }

        void EnsureHandRoot()
        {
            if (handRoot != null) return;

            // CardHandHUD가 캔버스 밖에 있어도 슬롯이 보이도록 캔버스 안에 강제 생성
            Canvas canvas = ResolveTargetCanvas();
            Transform parent = canvas != null ? canvas.transform : transform;
            if (canvas == null)
                Debug.LogWarning("[CardHandHUD] 씬에 Canvas가 없어 handRoot가 캔버스 밖에 만들어집니다 — UI가 렌더되지 않을 수 있습니다.");

            var go = new GameObject("HandRoot", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            handRoot = (RectTransform)go.transform;
            handRoot.anchorMin = new Vector2(0.5f, 0f);
            handRoot.anchorMax = new Vector2(0.5f, 0f);
            handRoot.pivot = new Vector2(0.5f, 0f);
            handRoot.anchoredPosition = handAnchoredPos;
            handRoot.sizeDelta = new Vector2(slotSpacing.x * SLOT_COUNT, 400f);
        }

        void EnsureDragLayer()
        {
            if (dragLayer != null) return;

            // CardHandHUD가 캔버스 밖에 있더라도 dragLayer는 반드시 캔버스 안에 만들어야
            // ScreenPointToWorldPointInRectangle 좌표 변환이 정확하게 동작
            Canvas canvas = ResolveTargetCanvas();
            Transform parent = canvas != null ? canvas.transform : transform;
            if (canvas == null)
                Debug.LogWarning("[CardHandHUD] 씬에 Canvas가 없어 dragLayer가 캔버스 밖에 만들어집니다 — 드래그 좌표가 어긋날 수 있습니다.");

            var go = new GameObject("DragLayer", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            dragLayer = (RectTransform)go.transform;
            dragLayer.anchorMin = Vector2.zero;
            dragLayer.anchorMax = Vector2.one;
            dragLayer.offsetMin = Vector2.zero;
            dragLayer.offsetMax = Vector2.zero;
            dragLayer.SetAsLastSibling();
        }

        void BuildSlotAnchors()
        {
            _slotAnchors.Clear();
            if (fanLayout) BuildFanSlotAnchors();
            else BuildLinearSlotAnchors();
        }

        /// <summary>활성 카드 갯수에 맞춰 슬롯 위치를 재계산. 카드가 적을 때 가운데 정렬.</summary>
        void PositionSlotAnchorsForCount(int activeCount)
        {
            if (_slotAnchors == null || _slotAnchors.Count == 0) return;
            if (fanLayout) PositionFanForCount(activeCount);
            else PositionLinearForCount(activeCount);
        }

        void PositionFanForCount(int activeCount)
        {
            int total = _slotAnchors.Count;
            // 활성 카드 사이 간격은 기준 카드 수(=HAND_LIMIT=5) 기준 유지.
            // 전체 호는 카드 수에 비례해서 줄어듦 → 자연스러운 중앙 정렬.
            float perStep = fanArcAngle / Mathf.Max(1, LAYOUT_REFERENCE_COUNT - 1);
            float effectiveArc = activeCount > 1 ? perStep * (activeCount - 1) : 0f;
            float startAngle = -effectiveArc * 0.5f;
            float angleStep = activeCount > 1 ? effectiveArc / (activeCount - 1) : 0f;

            for (int i = 0; i < total; i++)
            {
                var rect = _slotAnchors[i];
                if (rect == null) continue;

                if (i < activeCount)
                {
                    float angleDeg = activeCount > 1 ? startAngle + i * angleStep : 0f;
                    float angleRad = angleDeg * Mathf.Deg2Rad;
                    float x = Mathf.Sin(angleRad) * fanRadius;
                    float y = (Mathf.Cos(angleRad) - 1f) * fanRadius - fanVerticalDip;
                    rect.anchoredPosition = new Vector2(x, y);
                    rect.localRotation = Quaternion.Euler(0f, 0f, -angleDeg);
                }
                else
                {
                    // 비활성 슬롯 — 화면 밖으로 (카드 view가 같이 따라가지만 SetActive(false)되어 안 보임)
                    rect.anchoredPosition = new Vector2(0f, -10000f);
                    rect.localRotation = Quaternion.identity;
                }
            }
        }

        void PositionLinearForCount(int activeCount)
        {
            int total = _slotAnchors.Count;
            float totalWidth = activeCount > 1 ? slotSpacing.x * (activeCount - 1) : 0f;
            float startX = -totalWidth * 0.5f;

            for (int i = 0; i < total; i++)
            {
                var rect = _slotAnchors[i];
                if (rect == null) continue;
                if (i < activeCount)
                    rect.anchoredPosition = new Vector2(startX + i * slotSpacing.x, slotSpacing.y);
                else
                    rect.anchoredPosition = new Vector2(0f, -10000f);
            }
        }

        void BuildLinearSlotAnchors()
        {
            float totalWidth = slotSpacing.x * (SLOT_COUNT - 1);
            float startX = -totalWidth * 0.5f;

            for (int i = 0; i < SLOT_COUNT; i++)
            {
                var slotGo = new GameObject($"Slot_{i}", typeof(RectTransform));
                slotGo.transform.SetParent(handRoot, false);
                var rect = (RectTransform)slotGo.transform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(startX + i * slotSpacing.x, slotSpacing.y);
                rect.sizeDelta = Vector2.zero;
                _slotAnchors.Add(rect);
            }
        }

        /// <summary>카드 게임 손패 연출 — 호(arc) 위에 카드들을 회전·배치.</summary>
        void BuildFanSlotAnchors()
        {
            int n = SLOT_COUNT;
            float startAngle = -fanArcAngle * 0.5f;
            float angleStep = n > 1 ? fanArcAngle / (n - 1) : 0f;

            // 호의 가장 위쪽 점이 (0, 0)이 되도록 — 원의 중심은 (0, -fanRadius)
            for (int i = 0; i < n; i++)
            {
                float angleDeg = startAngle + i * angleStep;
                float angleRad = angleDeg * Mathf.Deg2Rad;

                // 원 위의 점: 중심에서 위로 fanRadius 떨어진 호
                float x = Mathf.Sin(angleRad) * fanRadius;
                float y = (Mathf.Cos(angleRad) - 1f) * fanRadius - fanVerticalDip;

                var slotGo = new GameObject($"Slot_{i}", typeof(RectTransform));
                slotGo.transform.SetParent(handRoot, false);
                var rect = (RectTransform)slotGo.transform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(x, y);
                rect.localRotation = Quaternion.Euler(0f, 0f, -angleDeg); // 호 방향으로 회전
                rect.sizeDelta = Vector2.zero;
                _slotAnchors.Add(rect);
            }
        }
    }
}
