using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;
using Battle.Card;

namespace Battle.UI
{
    // 전투 중 Tab으로 띄우는 요괴 정보 패널.
    // 위치와 글꼴은 이 컴포넌트 인스펙터 값을 따른다.
    public class BattleTabPanel : MonoBehaviour
    {
        [Tooltip("열고 닫을 프레임. CombatStage 캔버스에 배치된 UI_Tab_Frame.")]
        [SerializeField] private GameObject panelRoot;

        [Tooltip("왼쪽, 가운데, 오른쪽 요괴 정보 칸.")]
        [SerializeField] private MonsterInfoColumn[] columns;

        [Header("몬스터 이름")]
        [Tooltip("이 패널에서 조선100년체를 쓰는 글자에 적용할 머티리얼.")]
        [SerializeField] private Material chosunMaterial;
        [Tooltip("초상화 아래 이름 글꼴.")]
        [SerializeField] private TMP_FontAsset nameFont;
        [Tooltip("이름 글자 크기.")]
        [SerializeField] private float nameFontSize = 43.2f;
        [Tooltip("이름 색.")]
        [SerializeField] private Color nameColor = new Color(0.78f, 0.09f, 0.1f, 1f);
        [Tooltip("이름 정렬.")]
        [SerializeField] private TextAlignmentOptions nameAlignment = TextAlignmentOptions.Center;
        [Tooltip("초상화 아래쪽 기준 위치. X는 좌우, Y는 위로 겹치는 정도.")]
        [SerializeField] private Vector2 nameOffset = new Vector2(0f, 31.38f);
        [Tooltip("이름 칸 너비.")]
        [SerializeField] private float nameWidth = 384f;
        [Tooltip("이름 외곽선 두께. 0이면 외곽선이 없습니다.")]
        [Range(0f, 1f)]
        [SerializeField] private float nameOutlineWidth = 0f;
        [Tooltip("이름 외곽선 색.")]
        [SerializeField] private Color nameOutlineColor = Color.black;

        [Header("체력")]
        [Tooltip("체력 글꼴. 비우면 이름 글꼴을 사용합니다.")]
        [SerializeField] private TMP_FontAsset hpFont;
        [Tooltip("체력 글자 크기.")]
        [SerializeField] private float hpFontSize = 36f;
        [Tooltip("체력 숫자 위치. 초상화 아래쪽 가운데가 기준이고, X는 숫자 가운데입니다.")]
        [SerializeField] private Vector2 hpNumberOffset = new Vector2(27.36f, -29.88f);
        [Tooltip("체력 숫자 색.")]
        [SerializeField] private Color hpNumberColor = new Color(0.96f, 0.94f, 0.9f, 1f);
        [Tooltip("체력 숫자 외곽선 두께. 0이면 외곽선이 없습니다.")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("hpOutlineWidth")]
        [SerializeField] private float hpNumberOutlineWidth = 0f;
        [Tooltip("체력 숫자 외곽선 색.")]
        [FormerlySerializedAs("hpOutlineColor")]
        [SerializeField] private Color hpNumberOutlineColor = Color.black;
        [Tooltip("체력 글자 위치. 초상화 아래쪽 가운데가 기준이고, X는 글자 가운데입니다.")]
        [SerializeField] private Vector2 hpLabelOffset = new Vector2(-91.32f, -29.88f);
        [Tooltip("체력 라벨 색.")]
        [SerializeField] private Color hpLabelColor = new Color(0.91f, 0.82f, 0.63f, 1f);
        [Tooltip("체력 글자 외곽선 두께. 0이면 외곽선이 없습니다.")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("hpOutlineWidth")]
        [SerializeField] private float hpLabelOutlineWidth = 0f;
        [Tooltip("체력 글자 외곽선 색.")]
        [FormerlySerializedAs("hpOutlineColor")]
        [SerializeField] private Color hpLabelOutlineColor = Color.black;

        [Header("능력치 글자")]
        [Tooltip("공격, 흡수, 방해에 공통으로 쓸 글꼴. 비우면 각 텍스트에 이미 지정된 글꼴을 유지합니다.")]
        [SerializeField] private TMP_FontAsset statFont;
        [Tooltip("공격 수치와 흡수 수치 크기.")]
        [SerializeField] private float attackFontSize = 43.2f;
        [Tooltip("방해 문구 크기.")]
        [SerializeField] private float patternFontSize = 26.4f;
        [Tooltip("양피지 위 글자 색.")]
        [SerializeField] private Color statColor = new Color(0.1f, 0.06f, 0.04f, 1f);
        [Tooltip("공격 수치와 흡수 수치 정렬.")]
        [SerializeField] private TextAlignmentOptions attackAlignment = TextAlignmentOptions.Center;
        [Tooltip("방해 문구 정렬.")]
        [SerializeField] private TextAlignmentOptions patternAlignment = TextAlignmentOptions.MidlineLeft;
        [Tooltip("능력치 외곽선 두께. 0이면 외곽선이 없습니다.")]
        [Range(0f, 1f)]
        [SerializeField] private float statOutlineWidth = 0f;
        [Tooltip("능력치 외곽선 색.")]
        [SerializeField] private Color statOutlineColor = Color.black;

        [Header("탭 프레임")]
        [Tooltip("탭 프레임 위치. 왼쪽 위 기준.")]
        [SerializeField] private Vector2 framePosition = new Vector2(211f, -99f);
        [Tooltip("탭 프레임 크기.")]
        [SerializeField] private Vector2 frameSize = new Vector2(1499f, 804f);

        [Header("전투 기록 바")]
        [Tooltip("탭 프레임 왼쪽 위 기준 위치. 합쳐진 그림의 아래 기록 띠 위.")]
        [SerializeField] private Vector2 logBarPosition = new Vector2(87.5f, -842.6f);
        [Tooltip("기록 바 크기.")]
        [SerializeField] private Vector2 logBarSize = new Vector2(1448.4f, 120f);
        [Tooltip("기록 글자 오른쪽에서 로그가 시작되는 거리.")]
        [SerializeField] private float logContentInset = 12.8f;

        [Header("파워 바")]
        [Tooltip("탭 프레임 왼쪽 위 기준 위치. 합쳐진 그림의 위 파워 띠 위.")]
        [SerializeField] private Vector2 powerBarPosition = new Vector2(82f, -2f);
        [Tooltip("파워 바 크기.")]
        [SerializeField] private Vector2 powerBarSize = new Vector2(1357f, 99f);
        [Tooltip("파워 글자 오른쪽에서 첫 카드가 시작되는 거리.")]
        [SerializeField] private float powerCardInset = 12f;
        [Tooltip("파워 카드 사이 간격.")]
        [SerializeField] private float powerCardGap = 10f;

        [Header("정보 카드")]
        [Tooltip("요괴 정보 카드 크기. 세 장에 같이 적용됩니다.")]
        [SerializeField] private Vector2 cardSize = new Vector2(409.2f, 645.6f);
        [Tooltip("왼쪽 카드 위치.")]
        [SerializeField] private Vector2 leftColumnPosition = new Vector2(204.6f, -322.8f);
        [Tooltip("가운데 카드 위치.")]
        [SerializeField] private Vector2 centerColumnPosition = new Vector2(685.8f, -322.8f);
        [Tooltip("오른쪽 카드 위치.")]
        [SerializeField] private Vector2 rightColumnPosition = new Vector2(1167f, -322.8f);

        [Header("초상화")]
        [Tooltip("카드 위쪽 가운데 기준 위치.")]
        [SerializeField] private Vector2 portraitPosition = new Vector2(0f, -14.4f);
        [Tooltip("초상화 칸 크기. 빨간 테두리와 검은 배경이 이 칸을 채웁니다.")]
        [SerializeField] private Vector2 portraitSize = new Vector2(369.6f, 248.4f);
        [Tooltip("적 그림이 빨간 테두리를 가리지 않도록 안쪽으로 들이는 정도.")]
        [SerializeField] private float portraitInset = 4.8f;

        [Header("공격 줄")]
        [Tooltip("카드 왼쪽 위 기준 위치.")]
        [SerializeField] private Vector2 attackBarPosition = new Vector2(41.4f, -378.6f);
        [Tooltip("공격 줄 크기.")]
        [SerializeField] private Vector2 attackBarSize = new Vector2(325.56f, 58.44f);
        [SerializeField] private Vector2 attackSlot0Position = new Vector2(87.24f, -20.4f);
        [SerializeField] private Vector2 attackSlot0Size = new Vector2(81f, 58.44f);
        [SerializeField] private Vector2 attackSlot1Position = new Vector2(168.24f, -20.4f);
        [SerializeField] private Vector2 attackSlot1Size = new Vector2(81.84f, 58.44f);
        [SerializeField] private Vector2 attackSlot2Position = new Vector2(250.08f, -20.4f);
        [SerializeField] private Vector2 attackSlot2Size = new Vector2(75.6f, 58.44f);

        [Header("흡수 줄")]
        [Tooltip("카드 왼쪽 위 기준 위치.")]
        [SerializeField] private Vector2 absorbBarPosition = new Vector2(41.4f, -449.64f);
        [SerializeField] private Vector2 absorbBarSize = new Vector2(325.56f, 58.44f);
        [Tooltip("흡수 줄 안에서의 글자 위치.")]
        [SerializeField] private Vector2 absorbTextPosition = new Vector2(87.24f, -24f);
        [SerializeField] private Vector2 absorbTextSize = new Vector2(238.32f, 58.44f);

        [Header("방해 줄")]
        [Tooltip("카드 왼쪽 위 기준 위치.")]
        [SerializeField] private Vector2 patternBarPosition = new Vector2(41.4f, -520.68f);
        [SerializeField] private Vector2 patternBarSize = new Vector2(325.56f, 58.44f);
        [Tooltip("방해 줄 안에서의 글자 위치.")]
        [SerializeField] private Vector2 patternTextPosition = new Vector2(87.24f, -26.4f);
        [SerializeField] private Vector2 patternTextSize = new Vector2(238.32f, 58.44f);

        [Header("방해 기록")]
        [Tooltip("방해행동 기록에 커서를 올렸을 때, 적 번호 자리에 뜨는 글꼴. 비우면 이름 글꼴을 사용합니다.")]
        [SerializeField] private TMP_FontAsset disruptionFont;
        [Tooltip("방해 기록 글자 크기.")]
        [SerializeField] private float disruptionFontSize = 43.2f;
        [Tooltip("방해 기록 글자 색.")]
        [SerializeField] private Color disruptionColor = new Color(0.96f, 0.94f, 0.9f, 1f);
        [Tooltip("방해 기록 글자 정렬.")]
        [SerializeField] private TextAlignmentOptions disruptionAlignment = TextAlignmentOptions.Center;
        [Tooltip("적 번호 자리 가운데에서 얼마나 옮길지. X는 오른쪽, Y는 위입니다.")]
        [SerializeField] private Vector2 disruptionOffset = new Vector2(37.68f, 21.96f);
        [Tooltip("방해 기록 글자 칸 크기.")]
        [SerializeField] private Vector2 disruptionLabelSize = new Vector2(144f, 48f);
        [Tooltip("방해 기록 외곽선 두께. 0이면 외곽선이 없습니다.")]
        [Range(0f, 1f)]
        [SerializeField] private float disruptionOutlineWidth = 0f;
        [Tooltip("방해 기록 외곽선 색.")]
        [SerializeField] private Color disruptionOutlineColor = Color.black;

        readonly List<EnemyController> _alive = new List<EnemyController>();
        readonly StringBuilder _label = new StringBuilder();
        Sprite _portraitFrame;
        Sprite _attackRing;
        Sprite _attackBright;
        int _highlightAlive = -1;
        int _highlightAttack = -1;
        int _disruptionAlive = -1;
        int _disruptionAction = -1;

        public bool IsVisible => panelRoot != null && panelRoot.activeSelf;

        CardHandHUD _handHud; // 더미 보기 창과 동시 표시 방지용

        // 플레이 시작 시 닫아 둔다. 에디트 모드의 배치 상태는 유지된다.
        void Awake()
        {
            Hide();
        }

        // 일시정지 중에도 인스펙터 수정이 바로 화면에 반영된다.
        void OnValidate()
        {
            if (!Application.isPlaying) return;
            RefreshEnemyPortraits();
        }

        void Update()
        {
            if (!IsVisible) return;
            // Tab이 열린 채 인벤토리/설정(Esc)/도감이 열리면 Tab 패널을 닫아 두 창이 겹치지 않게 한다
            if (IsOtherOverlayOpen) { Hide(); return; }
            RefreshEnemyPortraits();
        }

        // Tab 패널과 동시에 띄우지 않을 전역 창(인벤토리·설정/사운드·도감)이 열려 있는지
        static bool IsOtherOverlayOpen =>
            InventoryPanelController.IsOpen || SettingPanel.IsOverlayOpen || CodexPanelController.IsOpen;

        public void Toggle()
        {
            if (IsVisible) Hide();
            else Show();
        }

        public void Show()
        {
            if (panelRoot == null) return;
            // 인벤토리/설정(Esc)/도감이 떠 있는 동안에는 Tab 패널을 열지 않는다
            if (IsOtherOverlayOpen) return;
            // 정보 창은 한 번에 하나 — 더미(묘지/덱) 보기가 열려 있으면 닫고 Tab 패널을 연다
            if (_handHud == null) _handHud = FindFirstObjectByType<CardHandHUD>();
            if (_handHud != null && _handHud.IsViewerMode) _handHud.CloseViewer();
            transform.SetAsLastSibling();
            panelRoot.SetActive(true);
            RefreshEnemyPortraits();
        }

        public void Hide()
        {
            if (panelRoot == null) return;
            panelRoot.SetActive(false);
        }

        // 한 마리면 가운데, 두 마리면 왼쪽·가운데, 세 마리면 왼쪽·가운데·오른쪽.
        public void RefreshEnemyPortraits()
        {
            if (columns == null) return;

            EnemyCombatParty.CollectAlive(_alive);
            ApplyFrameLayout();
            for (int i = 0; i < columns.Length; i++)
            {
                MonsterInfoColumn column = columns[i];
                if (column == null) continue;

                int enemyIndex = EnemyIndexForSlot(i, _alive.Count);
                EnemyStat stat = null;
                if (enemyIndex >= 0 && enemyIndex < _alive.Count)
                    stat = _alive[enemyIndex].GetComponent<EnemyStat>();

                ApplyColumn(column, stat, stat != null && stat.Data != null ? enemyIndex : -1, i);
            }
        }

        void ApplyColumn(MonsterInfoColumn column, EnemyStat stat, int enemyIndex, int columnIndex)
        {
            EnemyData data = stat != null ? stat.Data : null;
            bool show = data != null;
            if (column.group != null)
            {
                column.group.alpha = show ? 1f : 0f;
                column.group.blocksRaycasts = show;
            }

            ApplyInspectorLayout(column, columnIndex);
            EnsurePortraitFrame(column);

            if (column.enemyImage != null)
            {
                Sprite sprite = show ? data.enemySprite : null;
                column.enemyImage.sprite = sprite;
                column.enemyImage.enabled = sprite != null;
                column.enemyImage.preserveAspect = true;
                column.enemyImage.color = Color.white;
            }

            ApplySlotNumber(column);
            ApplyDisruptionLabel(column, enemyIndex);
            ApplyName(column, show ? data.enemyName : string.Empty, show);
            ApplyHp(column, stat, show);
            ApplyAttack(column, data, show, enemyIndex);
            ApplyStatStyle(column.absorbText, false);
            ApplyStatStyle(column.interferenceText, true);
            SetText(column.absorbText, show ? data.흡수수치.ToString() : string.Empty);
            SetText(column.interferenceText, show ? FormatInterference(data) : string.Empty);
        }

        void ApplyFrameLayout()
        {
            if (panelRoot == null) return;
            RectTransform frame = panelRoot.transform as RectTransform;
            if (frame == null) return;
            frame.anchoredPosition = framePosition;
            frame.sizeDelta = frameSize;
            ApplyLogBar(frame);
            ApplyPowerBar(frame);
        }

        void ApplyLogBar(RectTransform frame)
        {
            RectTransform bar = frame.Find("BattleLogFrame") as RectTransform;
            if (bar == null) return;
            SetBarRect(bar, logBarPosition, logBarSize);

            Image image = bar.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = null;
                image.color = new Color(1f, 1f, 1f, 0f);
                image.type = Image.Type.Simple;
                image.preserveAspect = false;
            }

            BattleLogView view = bar.GetComponent<BattleLogView>();
            if (view != null) view.ApplyContentInset(logContentInset);
        }

        void ApplyPowerBar(RectTransform frame)
        {
            RectTransform bar = frame.Find("PowerBar") as RectTransform;
            if (bar == null)
            {
                GameObject go = new GameObject("PowerBar", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(RectMask2D));
                go.layer = frame.gameObject.layer;
                go.transform.SetParent(frame, false);
                bar = go.GetComponent<RectTransform>();
            }

            SetBarRect(bar, powerBarPosition, powerBarSize);
            Image image = bar.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = null;
                image.color = new Color(1f, 1f, 1f, 0f);
                image.type = Image.Type.Simple;
                image.preserveAspect = false;
                image.raycastTarget = false;
            }

            FillPowerCards(bar);
        }

        static void SetBarRect(RectTransform bar, Vector2 position, Vector2 size)
        {
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(0f, 1f);
            bar.pivot = new Vector2(0f, 1f);
            bar.localScale = Vector3.one;
            bar.anchoredPosition = position;
            bar.sizeDelta = size;
        }

        Sprite _cardLogPanel;

        void FillPowerCards(RectTransform bar)
        {
            NewBattleController battle = NewBattleController.Instance;
            IReadOnlyList<CardInstance> powers = battle != null && battle.Deck != null ? battle.Deck.PowerField : null;
            int count = powers != null ? powers.Count : 0;
            float width = BattleLogView.PanelWidth;
            float height = BattleLogView.PanelHeight;
            float gap = Mathf.Max(0f, powerCardGap);
            float y = -(Mathf.Max(height, powerBarSize.y) - height) * 0.5f;
            if (_cardLogPanel == null) _cardLogPanel = Resources.Load<Sprite>("UI/Tab/카드 기록 패널");

            int slot = 0;
            for (int i = count - 1; i >= 0; i--)
            {
                CardInstance card = powers[i];
                if (card == null || card.data == null) continue;
                RectTransform slotRect = EnsurePowerSlot(bar, slot);
                slotRect.anchorMin = new Vector2(0f, 1f);
                slotRect.anchorMax = new Vector2(0f, 1f);
                slotRect.pivot = new Vector2(0f, 1f);
                slotRect.anchoredPosition = new Vector2(powerCardInset + slot * (width + gap), y);
                slotRect.sizeDelta = new Vector2(width, height);

                Image plate = slotRect.GetComponent<Image>();
                plate.sprite = null;
                plate.color = new Color(1f, 1f, 1f, 0f);
                plate.raycastTarget = true;

                Image back = EnsurePowerImage(slotRect, "Back");
                PlacePowerPart(back.rectTransform, 0f, BattleLogView.BackWidth, BattleLogView.BackHeight);
                back.sprite = null;
                back.color = new Color(0.45f, 0.45f, 0.45f, 1f);
                back.raycastTarget = false;

                Image icon = EnsurePowerImage(slotRect, "Icon");
                PlacePowerPart(icon.rectTransform, (width - BattleLogView.IconWidth) * 0.5f, BattleLogView.IconWidth, BattleLogView.IconHeight);
                Sprite sprite = LoadPowerSprite(card);
                icon.sprite = sprite;
                icon.color = Color.white;
                icon.preserveAspect = false;
                icon.enabled = sprite != null;
                icon.raycastTarget = false;

                Image frame = EnsurePowerImage(slotRect, "Frame");
                PlacePowerPart(frame.rectTransform, 0f, width, height);
                frame.sprite = _cardLogPanel;
                frame.color = Color.white;
                frame.preserveAspect = false;
                frame.enabled = _cardLogPanel != null;
                frame.raycastTarget = false;

                ApplyPowerCaption(slotRect, card.data.displayName);
                back.transform.SetSiblingIndex(0);
                icon.transform.SetSiblingIndex(1);
                frame.transform.SetSiblingIndex(2);

                PowerCardHover hover = slotRect.GetComponent<PowerCardHover>();
                if (hover == null) hover = slotRect.gameObject.AddComponent<PowerCardHover>();
                hover.Card = card;
                slotRect.gameObject.SetActive(true);
                slot++;
            }

            for (int i = 0; i < bar.childCount; i++)
            {
                Transform child = bar.GetChild(i);
                if (!child.name.StartsWith("PowerCard_")) continue;
                int index;
                if (!int.TryParse(child.name.Substring("PowerCard_".Length), out index)) continue;
                if (index >= slot) child.gameObject.SetActive(false);
            }
        }

        static RectTransform EnsurePowerSlot(RectTransform bar, int index)
        {
            string slotName = "PowerCard_" + index;
            Transform found = bar.Find(slotName);
            if (found != null) return (RectTransform)found;

            GameObject go = new GameObject(slotName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = bar.gameObject.layer;
            go.transform.SetParent(bar, false);
            return go.GetComponent<RectTransform>();
        }

        static Image EnsurePowerImage(RectTransform slot, string childName)
        {
            Transform found = slot.Find(childName);
            if (found != null) return found.GetComponent<Image>();

            GameObject go = new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = slot.gameObject.layer;
            go.transform.SetParent(slot, false);
            return go.GetComponent<Image>();
        }

        static void PlacePowerPart(RectTransform rect, float x, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(x, 0f);
        }

        void ApplyPowerCaption(RectTransform slot, string value)
        {
            Transform oldName = slot.Find("Name");
            if (oldName != null) oldName.gameObject.SetActive(false);

            Transform found = slot.Find("Label");
            TMP_Text label = found != null ? found.GetComponent<TMP_Text>() : null;
            if (label == null)
            {
                GameObject textGo = new GameObject("Label", typeof(RectTransform));
                textGo.layer = slot.gameObject.layer;
                textGo.transform.SetParent(slot, false);
                label = textGo.AddComponent<TextMeshProUGUI>();
            }

            label.gameObject.SetActive(true);
            label.transform.SetAsLastSibling();
            RectTransform rect = label.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.localScale = Vector3.one;
            rect.sizeDelta = new Vector2(136f, 20f);
            rect.anchoredPosition = new Vector2(8f, -82f);
            if (label.text != value) label.text = value ?? string.Empty;
            ApplyChosunFont(label, nameFont);
            label.fontSize = 16f;
            label.color = new Color(0.96f, 0.93f, 0.86f, 1f);
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            ApplyOutline(label, 0.15f, Color.black);
        }

        static Sprite LoadPowerSprite(CardInstance card)
        {
            if (card == null || card.data == null) return null;
            if (!string.IsNullOrEmpty(card.data.skillImg))
            {
                Sprite named = Resources.Load<Sprite>("CardIcons/" + card.data.skillImg);
                if (named != null) return named;
            }
            return Resources.Load<Sprite>("CardIcons/" + card.data.id);
        }

        void ApplyInspectorLayout(MonsterInfoColumn column, int columnIndex)
        {
            RectTransform card = column.group != null ? column.group.transform as RectTransform : null;
            if (card != null)
            {
                card.sizeDelta = cardSize;
                card.anchoredPosition = ColumnPosition(columnIndex);
            }

            RectTransform portrait = card != null ? card.Find("UI_EnemyPortrait") as RectTransform : null;
            if (portrait != null)
            {
                portrait.anchorMin = new Vector2(0.5f, 1f);
                portrait.anchorMax = new Vector2(0.5f, 1f);
                portrait.pivot = new Vector2(0.5f, 1f);
                portrait.anchoredPosition = portraitPosition;
                portrait.sizeDelta = portraitSize;

                RectTransform enemy = portrait.Find("EnemyImage") as RectTransform;
                if (enemy != null)
                {
                    float inset = Mathf.Max(0f, portraitInset);
                    enemy.anchorMin = Vector2.zero;
                    enemy.anchorMax = Vector2.one;
                    enemy.offsetMin = new Vector2(inset, inset);
                    enemy.offsetMax = new Vector2(-inset, -inset);
                    enemy.pivot = new Vector2(0.5f, 0.5f);
                }
            }

            PlaceBarRect(card, "AttackIcon", attackBarPosition, attackBarSize);
            PlaceBarRect(card, "AbsorbIcon", absorbBarPosition, absorbBarSize);
            PlaceBarRect(card, "InterferenceIcon", patternBarPosition, patternBarSize);
            PlaceTextRect(card, "AbsorbIcon", "AbsorbText", absorbTextPosition, absorbTextSize);
            PlaceTextRect(card, "InterferenceIcon", "InterferenceText", patternTextPosition, patternTextSize);
        }

        Vector2 ColumnPosition(int columnIndex)
        {
            if (columnIndex == 1) return centerColumnPosition;
            if (columnIndex == 2) return rightColumnPosition;
            return leftColumnPosition;
        }

        static void PlaceBarRect(RectTransform card, string barName, Vector2 position, Vector2 size)
        {
            if (card == null) return;
            RectTransform bar = card.Find(barName) as RectTransform;
            if (bar == null) return;
            Image icon = bar.GetComponent<Image>();
            if (icon != null) icon.enabled = false;
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(0f, 1f);
            bar.pivot = new Vector2(0f, 1f);
            bar.anchoredPosition = position;
            bar.sizeDelta = size;
        }

        static void PlaceTextRect(RectTransform card, string barName, string textName, Vector2 position, Vector2 size)
        {
            if (card == null) return;
            RectTransform bar = card.Find(barName) as RectTransform;
            if (bar == null) return;
            RectTransform text = bar.Find(textName) as RectTransform;
            if (text == null) return;
            text.anchorMin = new Vector2(0f, 1f);
            text.anchorMax = new Vector2(0f, 1f);
            text.pivot = new Vector2(0f, 1f);
            text.anchoredPosition = position;
            text.sizeDelta = size;
        }

        void ApplyAttack(MonsterInfoColumn column, EnemyData data, bool show, int enemyIndex)
        {
            TMP_Text[] slots = EnsureAttackSlots(column);
            int[] sequence = show ? data.attackSequence : null;
            int count = sequence != null ? sequence.Length : 0;
            if (slots == null) return;

            int selected = -1;
            if (show && enemyIndex >= 0 && enemyIndex == _highlightAlive && _highlightAttack >= 0 && count > 0)
                selected = _highlightAttack < 2 || count <= 3 ? _highlightAttack % count : 2;
            if (selected >= slots.Length) selected = slots.Length - 1;

            for (int i = 0; i < slots.Length; i++)
            {
                TMP_Text slot = slots[i];
                if (slot == null) continue;
                bool on = i < count;
                slot.gameObject.SetActive(on);
                if (!on) continue;
                string value = i < 2 || count <= 3
                    ? sequence[i].ToString()
                    : JoinTail(sequence, i);
                if (slot.text != value) slot.text = value;
                ApplyStatStyle(slot, false);
                SetSlotAboveDim(slot, selected == i);
            }

            if (column.attackText != null && column.attackText.gameObject.activeSelf)
                column.attackText.gameObject.SetActive(false);

            TMP_Text ringTarget = selected >= 0 && selected < slots.Length ? slots[selected] : null;
            if (ringTarget != null && !ringTarget.gameObject.activeSelf) ringTarget = null;
            PlaceAttackRing(slots, ringTarget);
        }

        static void SetSlotAboveDim(TMP_Text slot, bool above)
        {
            if (slot == null) return;
            Canvas canvas = slot.GetComponent<Canvas>();
            if (!above)
            {
                if (canvas != null) canvas.overrideSorting = false;
                return;
            }

            Canvas root = slot.canvas != null ? slot.canvas.rootCanvas : null;
            if (canvas == null) canvas = slot.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = (root != null ? root.sortingOrder : 0) + 50;
            canvas.additionalShaderChannels |=
                AdditionalCanvasShaderChannels.TexCoord1
                | AdditionalCanvasShaderChannels.Normal
                | AdditionalCanvasShaderChannels.Tangent;
        }

        void PlaceAttackRing(TMP_Text[] slots, TMP_Text slot)
        {
            if (slots == null) return;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null || slots[i] == slot) continue;
                HideMark(slots[i].transform, "AttackRing");
                HideMark(slots[i].transform, "AttackBright");
            }
            if (slot == null) return;

            float diameter = Mathf.Max(slot.rectTransform.sizeDelta.y, 36f) * 1.15f;
            PlaceMark(slot, "AttackRing", _attackRing != null ? _attackRing : (_attackRing = Resources.Load<Sprite>("UI/Tab/AttackRing")), diameter, Color.white, false);
            PlaceMark(slot, "AttackBright", BrightCircle(), diameter * 0.72f, new Color(1f, 0.96f, 0.88f, 0.72f), true);
        }

        static void HideMark(Transform parent, string markName)
        {
            Transform mark = parent.Find(markName);
            if (mark != null) mark.gameObject.SetActive(false);
        }

        void PlaceMark(TMP_Text slot, string markName, Sprite sprite, float diameter, Color color, bool behindText)
        {
            Transform found = slot.transform.Find(markName);
            Image image;
            RectTransform rect;
            if (found == null)
            {
                var go = new GameObject(markName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.layer = slot.gameObject.layer;
                go.transform.SetParent(slot.transform, false);
                image = go.GetComponent<Image>();
                image.raycastTarget = false;
                rect = go.GetComponent<RectTransform>();
            }
            else
            {
                found.gameObject.SetActive(true);
                image = found.GetComponent<Image>();
                rect = found as RectTransform;
            }

            if (image != null)
            {
                image.sprite = sprite;
                image.color = color;
                image.preserveAspect = true;
                image.raycastTarget = false;
            }
            if (rect == null) return;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(diameter, diameter);
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;

            Canvas canvas = rect.GetComponent<Canvas>();
            if (behindText)
            {
                if (canvas == null) canvas = rect.gameObject.AddComponent<Canvas>();
                Canvas root = slot.canvas != null ? slot.canvas.rootCanvas : null;
                canvas.overrideSorting = true;
                canvas.sortingOrder = (root != null ? root.sortingOrder : 0) + 40;
            }
            else if (canvas != null)
            {
                canvas.overrideSorting = false;
            }
            rect.SetAsLastSibling();
        }

        Sprite BrightCircle()
        {
            if (_attackBright != null) return _attackBright;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            float radius = size * 0.5f - 0.5f;
            var center = new Vector2(radius, radius);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float edge = Vector2.Distance(new Vector2(x, y), center) / radius;
                    float alpha = Mathf.Clamp01(1f - Mathf.InverseLerp(0.72f, 1f, edge));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            _attackBright = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _attackBright;
        }

        public void HighlightEnemyAttack(int aliveIndex, int attackIndex)
        {
            _highlightAlive = aliveIndex;
            _highlightAttack = attackIndex;
            RefreshEnemyPortraits();
        }

        public void ClearAttackHighlight()
        {
            if (_highlightAlive < 0 && _highlightAttack < 0) return;
            _highlightAlive = -1;
            _highlightAttack = -1;
            RefreshEnemyPortraits();
        }

        static string JoinTail(int[] sequence, int start)
        {
            var builder = new StringBuilder();
            for (int i = start; i < sequence.Length; i++)
            {
                if (builder.Length > 0) builder.Append(' ');
                builder.Append(sequence[i]);
            }
            return builder.ToString();
        }

        TMP_Text[] EnsureAttackSlots(MonsterInfoColumn column)
        {
            if (column.attackText == null) return column.attackSlots;
            Transform bar = column.attackText.transform.parent;
            if (bar == null) return column.attackSlots;

            if (column.attackSlots == null || column.attackSlots.Length != 3)
                column.attackSlots = new TMP_Text[3];

            Vector2[] positions = { attackSlot0Position, attackSlot1Position, attackSlot2Position };
            Vector2[] sizes = { attackSlot0Size, attackSlot1Size, attackSlot2Size };
            for (int i = 0; i < 3; i++)
            {
                TMP_Text slot = column.attackSlots[i];
                if (slot == null)
                {
                    Transform found = bar.Find("AttackSlot" + i);
                    if (found != null) slot = found.GetComponent<TMP_Text>();
                }
                if (slot == null)
                {
                    GameObject go = new GameObject("AttackSlot" + i, typeof(RectTransform));
                    go.layer = bar.gameObject.layer;
                    go.transform.SetParent(bar, false);
                    slot = go.AddComponent<TextMeshProUGUI>();
                    ApplyChosunFont(slot, column.attackText.font);
                }

                column.attackSlots[i] = slot;
                RectTransform rect = slot.rectTransform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = positions[i];
                rect.sizeDelta = sizes[i];
                ApplyStatStyle(slot, false);
            }

            return column.attackSlots;
        }

        void ApplyHp(MonsterInfoColumn column, EnemyStat stat, bool show)
        {
            TMP_Text word = EnsureHpLabel(column);
            TMP_Text numbers = EnsureHp(column);
            if (word != null) word.gameObject.SetActive(show);
            if (numbers != null) numbers.gameObject.SetActive(show);
            if (!show || stat == null || word == null || numbers == null) return;

            int current = Mathf.RoundToInt(stat.currentHp);
            int max = Mathf.RoundToInt(stat.maxHp);
            ApplyHpPiece(word, "체력", hpLabelColor, hpLabelOutlineWidth, hpLabelOutlineColor);
            ApplyHpPiece(numbers, current + "/" + max, hpNumberColor, hpNumberOutlineWidth, hpNumberOutlineColor);

            float height = Mathf.Max(24f, hpFontSize * 2f);
            float width = Mathf.Max(80f, nameWidth);
            PlaceHpPiece(word.rectTransform, hpLabelOffset, width, height);
            PlaceHpPiece(numbers.rectTransform, hpNumberOffset, width, height);
        }

        void ApplyHpPiece(TMP_Text label, string value, Color color, float outlineWidth, Color outlineColor)
        {
            ApplyChosunFont(label, hpFont != null ? hpFont : nameFont);

            if (label.text != value) label.text = value;
            label.fontSize = Mathf.Max(8f, hpFontSize);
            label.color = color;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.enableAutoSizing = false;
            label.richText = false;
            label.raycastTarget = false;
            label.margin = Vector4.zero;
            if (!label.extraPadding) label.extraPadding = true;
            ApplyOutline(label, outlineWidth, outlineColor);
        }

        static void PlaceHpPiece(RectTransform rect, Vector2 position, float width, float height)
        {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(Mathf.Max(8f, width), height);
            rect.anchoredPosition = position;
        }

        TMP_Text EnsureHp(MonsterInfoColumn column)
        {
            column.hpText = EnsureColumnText(column, column.hpText, "HpText");
            return column.hpText;
        }

        TMP_Text EnsureHpLabel(MonsterInfoColumn column)
        {
            column.hpLabelText = EnsureColumnText(column, column.hpLabelText, "HpLabel");
            return column.hpLabelText;
        }

        static TMP_Text EnsureColumnText(MonsterInfoColumn column, TMP_Text current, string objectName)
        {
            if (current != null) return current;
            if (column.enemyImage == null) return null;
            Transform parent = column.enemyImage.transform.parent;
            if (parent == null) return null;

            Transform found = parent.Find(objectName);
            if (found != null) current = found.GetComponent<TMP_Text>();
            if (current != null) return current;

            GameObject go = new GameObject(objectName, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.SetAsLastSibling();
            return go.AddComponent<TextMeshProUGUI>();
        }

        void EnsurePortraitFrame(MonsterInfoColumn column)
        {
            if (column.enemyImage == null) return;
            Transform parent = column.enemyImage.transform.parent;
            if (parent == null) return;

            if (_portraitFrame == null)
                _portraitFrame = Resources.Load<Sprite>("UI/Tab/UI_Tab_PortraitFrame");

            Transform found = parent.Find("PortraitFrame");
            Image frame = found != null ? found.GetComponent<Image>() : null;
            if (frame == null)
            {
                GameObject go = new GameObject("PortraitFrame", typeof(RectTransform));
                go.layer = parent.gameObject.layer;
                go.transform.SetParent(parent, false);
                frame = go.AddComponent<Image>();
            }

            Image parentImage = parent.GetComponent<Image>();
            if (parentImage != null) parentImage.enabled = false;

            frame.sprite = _portraitFrame;
            frame.enabled = _portraitFrame != null;
            frame.color = Color.white;
            frame.raycastTarget = false;
            frame.preserveAspect = false;
            frame.type = Image.Type.Simple;

            RectTransform rect = frame.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            if (frame.transform.GetSiblingIndex() > column.enemyImage.transform.GetSiblingIndex())
                frame.transform.SetSiblingIndex(column.enemyImage.transform.GetSiblingIndex());
        }

        void ApplyStatStyle(TMP_Text label, bool pattern)
        {
            if (label == null) return;
            ApplyChosunFont(label, statFont);
            label.color = statColor;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.enableAutoSizing = false;
            label.fontSize = Mathf.Max(8f, pattern ? patternFontSize : attackFontSize);
            label.alignment = pattern ? patternAlignment : attackAlignment;
            ApplyOutline(label, statOutlineWidth, statOutlineColor);
        }

        // 초상화 왼쪽 위 번호는 쓰지 않는다.
        void ApplySlotNumber(MonsterInfoColumn column)
        {
            Image number = column.slotNumber;
            if (number == null && column.enemyImage != null)
            {
                Transform found = column.enemyImage.transform.parent.Find("SlotNumber");
                if (found != null) number = found.GetComponent<Image>();
                column.slotNumber = number;
            }
            if (number != null) number.gameObject.SetActive(false);
        }

        public void ShowDisruptionLabel(int aliveIndex, int action)
        {
            _disruptionAlive = aliveIndex;
            _disruptionAction = action;
            RefreshEnemyPortraits();
        }

        public void ClearDisruptionLabel()
        {
            if (_disruptionAlive < 0 && _disruptionAction < 0) return;
            _disruptionAlive = -1;
            _disruptionAction = -1;
            RefreshEnemyPortraits();
        }

        void ApplyDisruptionLabel(MonsterInfoColumn column, int enemyIndex)
        {
            bool show = enemyIndex >= 0 && enemyIndex == _disruptionAlive && _disruptionAction >= 0;
            TMP_Text label = EnsureDisruptionLabel(column);
            if (label == null) return;
            label.gameObject.SetActive(show);
            SetSlotAboveDim(label, show);
            if (!show) return;

            string value = DisruptionName(_disruptionAction);
            if (label.text != value) label.text = value;
            TMP_FontAsset font = disruptionFont != null ? disruptionFont : nameFont != null ? nameFont : statFont;
            ApplyChosunFont(label, font);
            label.fontSize = Mathf.Max(8f, disruptionFontSize);
            label.color = disruptionColor;
            label.alignment = disruptionAlignment;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            ApplyOutline(label, disruptionOutlineWidth, disruptionOutlineColor);

            RectTransform number = column.slotNumber != null ? column.slotNumber.rectTransform : null;
            RectTransform rect = label.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            Vector2 corner = number != null ? number.anchoredPosition : new Vector2(14.4f, -10f);
            Vector2 size = number != null ? number.sizeDelta : new Vector2(44f, 61f);
            rect.sizeDelta = new Vector2(Mathf.Max(8f, disruptionLabelSize.x), Mathf.Max(8f, disruptionLabelSize.y));
            rect.anchoredPosition = corner + new Vector2(size.x * 0.5f, -size.y * 0.5f) + disruptionOffset;
            rect.SetAsLastSibling();
        }

        TMP_Text EnsureDisruptionLabel(MonsterInfoColumn column)
        {
            if (column.enemyImage == null && column.slotNumber == null) return null;
            Transform parent = column.slotNumber != null
                ? column.slotNumber.transform.parent
                : column.enemyImage.transform.parent;
            if (parent == null) return null;
            Transform found = parent.Find("DisruptionLabel");
            if (found != null) return found.GetComponent<TMP_Text>();
            GameObject go = new GameObject("DisruptionLabel", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            TMP_Text label = go.AddComponent<TextMeshProUGUI>();
            label.raycastTarget = false;
            return label;
        }

        static string DisruptionName(int action)
        {
            switch (action)
            {
                case 0: return "저주";
                case 1: return "탈진";
                case 2: return "흡수";
                case 3: return "강화";
                case 4: return "회복";
                case 5: return "버리기";
                default: return string.Empty;
            }
        }

        // 조선100년체 글꼴을 넣고, 지정된 SDF 머티리얼을 같이 씌운다.
        void ApplyChosunFont(TMP_Text label, TMP_FontAsset font)
        {
            if (label == null || font == null) return;
            if (label.font != font) label.font = font;
            if (!UsesChosun(font) || chosunMaterial == null) return;
            if (label.fontSharedMaterial != chosunMaterial)
                label.fontSharedMaterial = chosunMaterial;
        }

        bool UsesChosun(TMP_FontAsset font)
        {
            return font != null && (font == nameFont || font == hpFont || font == statFont || font == disruptionFont);
        }

        static void ApplyOutline(TMP_Text label, float width, Color color)
        {
            // 막 만든 글자는 재질과 캔버스 렌더러가 아직 없다. 그때 외곽선을 넣으면 예외가 난다.
            if (label == null || label.font == null || label.fontSharedMaterial == null || label.canvasRenderer == null)
                return;

            float outline = Mathf.Clamp01(width);
            if (!Mathf.Approximately(label.outlineWidth, outline)) label.outlineWidth = outline;
            Color32 outlineColor = color;
            Color32 current = label.outlineColor;
            if (current.r != outlineColor.r || current.g != outlineColor.g
                || current.b != outlineColor.b || current.a != outlineColor.a)
                label.outlineColor = outlineColor;
        }

        void ApplyName(MonsterInfoColumn column, string value, bool show)
        {
            TMP_Text label = EnsureName(column);
            if (label == null) return;

            label.gameObject.SetActive(show);
            if (!show) return;

            if (label.text != value) label.text = value ?? string.Empty;
            ApplyChosunFont(label, nameFont);
            label.fontSize = Mathf.Max(8f, nameFontSize);
            label.color = nameColor;
            label.alignment = nameAlignment;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            ApplyOutline(label, nameOutlineWidth, nameOutlineColor);

            RectTransform rect = label.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(Mathf.Max(40f, nameWidth), Mathf.Max(16f, label.fontSize + 10f));
            rect.anchoredPosition = nameOffset;
        }

        TMP_Text EnsureName(MonsterInfoColumn column)
        {
            if (column.nameText != null) return column.nameText;
            if (column.enemyImage == null) return null;

            Transform parent = column.enemyImage.transform.parent;
            if (parent == null) return null;
            Transform found = parent.Find("MonsterName");
            if (found != null) column.nameText = found.GetComponent<TMP_Text>();
            if (column.nameText != null) return column.nameText;

            GameObject go = new GameObject("MonsterName", typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            go.transform.SetAsLastSibling();
            column.nameText = go.AddComponent<TextMeshProUGUI>();
            return column.nameText;
        }

        static void SetText(TMP_Text label, string value)
        {
            if (label == null) return;
            if (label.text != value) label.text = value;
        }

        string FormatInterference(EnemyData data)
        {
            DisruptionAction[] actions = data.disruptionActions;
            _label.Length = 0;
            if (actions == null || actions.Length == 0)
            {
                AppendAction(_label, DisruptionAction.Curse);
                _label.Append(", ");
                AppendAction(_label, DisruptionAction.Exhaust);
                _label.Append(", ");
                AppendAction(_label, DisruptionAction.Absorb);
                _label.Append(", ");
                AppendAction(_label, DisruptionAction.Enhance);
                _label.Append(", ");
                AppendAction(_label, DisruptionAction.Recover);
                _label.Append(", ");
                AppendAction(_label, DisruptionAction.Discard);
                return _label.ToString();
            }

            for (int i = 0; i < actions.Length; i++)
            {
                if (i > 0) _label.Append(", ");
                AppendAction(_label, actions[i]);
            }
            return _label.ToString();
        }

        static void AppendAction(StringBuilder builder, DisruptionAction action)
        {
            switch (action)
            {
                case DisruptionAction.Curse: builder.Append("저주"); break;
                case DisruptionAction.Exhaust: builder.Append("탈진"); break;
                case DisruptionAction.Absorb: builder.Append("흡수"); break;
                case DisruptionAction.Enhance: builder.Append("강화"); break;
                case DisruptionAction.Recover: builder.Append("회복"); break;
                case DisruptionAction.Discard: builder.Append("버리기"); break;
                default: builder.Append(action.ToString()); break;
            }
        }

        // 전투 배치와 같다. 1마리는 가운데, 2마리는 좌우, 3마리는 왼·가운데·오른.
        static int EnemyIndexForSlot(int slot, int enemyCount)
        {
            if (enemyCount <= 0 || slot < 0 || slot > 2) return -1;
            for (int i = 0; i < enemyCount; i++)
            {
                int side = BattleStageController.PartySideSign(i, enemyCount);
                int column = side < 0 ? 0 : side > 0 ? 2 : 1;
                if (column == slot) return i;
            }
            return -1;
        }

        public bool TryGetFrameRect(out RectTransform frame)
        {
            frame = panelRoot != null ? panelRoot.transform as RectTransform : null;
            return frame != null && frame.gameObject.activeInHierarchy;
        }

        public bool TryGetEnemyPortrait(int aliveIndex, out RectTransform portrait)
        {
            portrait = null;
            if (columns == null || aliveIndex < 0) return false;
            EnemyCombatParty.CollectAlive(_alive);
            int count = _alive.Count;
            if (aliveIndex >= count) return false;
            int side = BattleStageController.PartySideSign(aliveIndex, count);
            int column = side < 0 ? 0 : side > 0 ? 2 : 1;
            if (column >= columns.Length || columns[column] == null || columns[column].group == null) return false;
            RectTransform card = columns[column].group.transform as RectTransform;
            if (card == null) return false;
            portrait = card.Find("UI_EnemyPortrait") as RectTransform;
            if (portrait == null) portrait = card;
            return portrait.gameObject.activeInHierarchy;
        }

        [System.Serializable]
        public class MonsterInfoColumn
        {
            public CanvasGroup group;
            public Image enemyImage;
            public Image slotNumber;
            public TMP_Text attackText;
            public TMP_Text absorbText;
            public TMP_Text interferenceText;
            public TMP_Text nameText;
            public TMP_Text hpText;
            public TMP_Text hpLabelText;
            public TMP_Text[] attackSlots;
        }
    }
}
