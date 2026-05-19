using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Battle.Card;

namespace Battle.UI
{
    /// <summary>
    /// NewSkillCard 프리팹에 부착하는 카드 표시 + 드래그 컴포넌트.
    /// 프리팹 자식 이름이 표준 규칙(Nametxt / Desctxt / IconImage / guageCost / attributeImg / Background)이면
    /// Awake에서 자동 바인딩되며, Inspector에서 직접 연결도 가능.
    /// </summary>
    public class NewCardView : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerClickHandler
    {
        [System.Serializable]
        public struct CardIconEntry
        {
            public int cardId;
            public Sprite sprite;
        }

        [Header("Inspector 자동 바인딩 (비우면 자식 이름으로 자동 탐색)")]
        [SerializeField] private Image iconImage;
        [SerializeField] private Image attributeImage;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descText;
        [SerializeField] private TMP_Text gaugeText;

        [Header("자동 바인딩 옵션")]
        [Tooltip("Awake 시 자식 이름으로 누락된 참조를 채운다.")]
        [SerializeField] private bool autoBindOnAwake = true;

        [Header("속성 이미지 매핑 (attributeImg sprite)")]
        [SerializeField] private Sprite fireElementSprite;
        [SerializeField] private Sprite waterElementSprite;
        [SerializeField] private Sprite windElementSprite;
        [SerializeField] private Sprite earthElementSprite;
        [SerializeField] private Sprite neutralElementSprite;

        [Header("카드 아이콘 매핑 (IconImage sprite — cardId 기준)")]
        [SerializeField] private List<CardIconEntry> cardIcons = new List<CardIconEntry>();
        [Tooltip("매핑이 없으면 Resources/CardIcons/{cardId}.png 자동 로드 시도.")]
        [SerializeField] private bool resourcesFallback = true;
        [Tooltip("ON이면 카드 아이콘 자리에 속성 sprite를 그대로 사용 (카드 아이콘 미완성 임시).")]
        [SerializeField] private bool useElementSpriteAsIcon = true;

        [Header("크기 — 드래그/Hover 시 일시 확대")]
        [Tooltip("드래그 중 카드 크기 배율 (홈 스케일 기준).")]
        [SerializeField] private float dragScaleMultiplier = 1.15f;
        [Tooltip("Hover(마우스 위) 시 카드 크기 배율 (홈 스케일 기준).")]
        [SerializeField] private float hoverScaleMultiplier = 1.05f;
        [Tooltip("Hover 시 카드가 위로 떠오를 거리 (UI 좌표 단위).")]
        [SerializeField] private Vector2 hoverPositionOffset = new Vector2(0f, 100f);

        [Header("색상 (sprite 매핑이 없을 때 fallback)")]
        [SerializeField] private bool tintBackgroundByElement = true;
        [Range(0f, 1f)]
        [SerializeField] private float backgroundTintAlpha = 0.35f;

        public CardInstance Card { get; private set; }
        public int SlotIndex { get; set; }
        public CardHandHUD Hud { get; set; }

        private RectTransform _rect;
        private CanvasGroup _canvasGroup;
        private Vector2 _homeAnchoredPos;
        private Transform _homeParent;
        private int _homeSiblingIndex;
        private Vector3 _homeLocalScale;
        private Color _originalBackgroundColor;
        private bool _capturedOriginalBgColor;
        private bool _isDragging;
        private bool _isHovering;
        private int _hoverSlotOriginalSibling = -1;

        void Awake()
        {
            _rect = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            if (autoBindOnAwake) AutoBind();

            // 프리팹 원본 스케일 보존 (CaptureHome 호출 전 hover 등에서도 활용)
            _homeLocalScale = _rect.localScale;

            if (backgroundImage != null && !_capturedOriginalBgColor)
            {
                _originalBackgroundColor = backgroundImage.color;
                _capturedOriginalBgColor = true;
            }
        }

        /// <summary>외부(콤보 슬롯 UI 등)에서 prefab의 element sprite를 공유받기 위한 접근자.</summary>
        public Sprite GetElementSprite(CardElement element) => element switch
        {
            CardElement.Fire    => fireElementSprite,
            CardElement.Water   => waterElementSprite,
            CardElement.Wind    => windElementSprite,
            CardElement.Earth   => earthElementSprite,
            CardElement.Neutral => neutralElementSprite,
            _ => null
        };

        void AutoBind()
        {
            if (iconImage == null) iconImage = FindChildComponent<Image>("IconImage");
            if (attributeImage == null) attributeImage = FindChildComponent<Image>("attributeImg");
            if (backgroundImage == null) backgroundImage = FindChildComponent<Image>("Background");
            if (nameText == null) nameText = FindChildComponent<TMP_Text>("Nametxt");
            if (descText == null) descText = FindChildComponent<TMP_Text>("Desctxt");
            if (gaugeText == null) gaugeText = FindChildComponent<TMP_Text>("guageCost");
        }

        T FindChildComponent<T>(string childName) where T : Component
        {
            Transform[] all = GetComponentsInChildren<Transform>(true);
            foreach (Transform t in all)
            {
                if (t == null) continue;
                if (t.name == childName)
                {
                    var comp = t.GetComponent<T>();
                    if (comp != null) return comp;
                }
            }
            return null;
        }

        // ─────────────────────────────────────────────────────────────
        // 바인딩 / 표시
        // ─────────────────────────────────────────────────────────────

        public void Bind(CardHandHUD hud, int slotIndex)
        {
            Hud = hud;
            SlotIndex = slotIndex;
        }

        public void SetCard(CardInstance card)
        {
            Card = card;
            Refresh();
        }

        public void Refresh()
        {
            if (Card == null)
            {
                gameObject.SetActive(false);
                return;
            }
            gameObject.SetActive(true);

            // 새 전투 시스템: 글로벌 속성 저주 상태를 카드 인스턴스에 동기화 (시각 표시용)
            if (Battle.NewBattleController.Instance != null)
            {
                Card.cursed = Battle.NewBattleController.Instance.IsElementCursed(Card.Element);
            }

            if (nameText != null) nameText.text = Card.data.displayName;
            if (descText != null) descText.text = Card.data.description;
            if (gaugeText != null) gaugeText.text = Card.data.gauge.ToString();

            // 속성 이미지
            ApplyAttributeSprite(Card.Element);

            // 카드 아이콘
            ApplyCardIcon(Card.Id);

            // 배경 색조 (sprite 매핑이 없거나 옵션 ON이면)
            if (backgroundImage != null && tintBackgroundByElement)
            {
                Color baseColor = _capturedOriginalBgColor ? _originalBackgroundColor : Color.white;
                Color elementColor = ColorForElement(Card.Element, Card.cursed);
                backgroundImage.color = Color.Lerp(baseColor, elementColor, backgroundTintAlpha);
            }
        }

        void ApplyAttributeSprite(CardElement element)
        {
            if (attributeImage == null) return;
            Sprite s = element switch
            {
                CardElement.Fire    => fireElementSprite,
                CardElement.Water   => waterElementSprite,
                CardElement.Wind    => windElementSprite,
                CardElement.Earth   => earthElementSprite,
                CardElement.Neutral => neutralElementSprite,
                _ => null
            };

            if (s != null)
            {
                attributeImage.sprite = s;
                attributeImage.color = Color.white;
            }
            else
            {
                // sprite 매핑이 없으면 색으로 표시
                attributeImage.color = ColorForElement(element, false);
            }
        }

        void ApplyCardIcon(int cardId)
        {
            if (iconImage == null) return;

            // 임시: 카드 아이콘 미완성 → 속성 sprite로 대체
            if (useElementSpriteAsIcon && Card != null)
            {
                Sprite elementSprite = GetElementSprite(Card.Element);
                if (elementSprite != null)
                {
                    iconImage.sprite = elementSprite;
                    iconImage.color = Color.white;
                    iconImage.enabled = true;
                    return;
                }
            }

            // Inspector 매핑 우선
            for (int i = 0; i < cardIcons.Count; i++)
            {
                if (cardIcons[i].cardId == cardId && cardIcons[i].sprite != null)
                {
                    iconImage.sprite = cardIcons[i].sprite;
                    iconImage.color = Color.white;
                    iconImage.enabled = true;
                    return;
                }
            }

            // Resources fallback
            if (resourcesFallback)
            {
                Sprite resourceSprite = Resources.Load<Sprite>($"CardIcons/{cardId}");
                if (resourceSprite != null)
                {
                    iconImage.sprite = resourceSprite;
                    iconImage.color = Color.white;
                    iconImage.enabled = true;
                    return;
                }
            }

            // 매핑이 전혀 없으면 색만 표시
            iconImage.sprite = null;
            iconImage.color = ColorForElement(Card != null ? Card.Element : CardElement.Neutral, false);
        }

        public void CaptureHome()
        {
            if (_rect == null) _rect = GetComponent<RectTransform>();
            // 호출자가 hover offset을 제거한 상태에서 호출한다고 가정 — 여기선 현재 위치를 그대로 캡처
            _homeAnchoredPos = _rect.anchoredPosition;
            _homeParent = _rect.parent;
            _homeSiblingIndex = _rect.GetSiblingIndex();
            // _homeLocalScale은 Awake에서 한 번 캡처한 베이스를 유지 (hover/drag 배율을 베이스로 저장하는 것을 막음)
        }

        public void ReturnHome()
        {
            if (_homeParent == null) return;
            _rect.SetParent(_homeParent, false);
            _rect.SetSiblingIndex(_homeSiblingIndex);
            _rect.anchoredPosition = _homeAnchoredPos;
            _rect.localScale = _homeLocalScale;
            // 슬롯이 fan layout의 회전을 가지고 있으면 localRotation=Identity가 슬롯 회전을 그대로 따름
            _rect.localRotation = Quaternion.identity;
        }

        /// <summary>외부(CardHandHUD.Refresh 등)에서 카드를 슬롯에 강제 복원할 때 스케일도 베이스로 되돌린다.</summary>
        public void ResetToHomeScale()
        {
            if (_rect == null) _rect = GetComponent<RectTransform>();
            _rect.localScale = _homeLocalScale == Vector3.zero ? Vector3.one : _homeLocalScale;
        }

        static Color ColorForElement(CardElement element, bool cursed)
        {
            if (cursed) return new Color(0.4f, 0.1f, 0.4f, 1f);
            return element switch
            {
                CardElement.Fire    => new Color(0.95f, 0.55f, 0.40f, 1f),
                CardElement.Water   => new Color(0.50f, 0.75f, 0.95f, 1f),
                CardElement.Wind    => new Color(0.60f, 0.90f, 0.60f, 1f),
                CardElement.Earth   => new Color(0.85f, 0.70f, 0.45f, 1f),
                CardElement.Neutral => new Color(0.70f, 0.70f, 0.70f, 1f),
                _ => new Color(0.5f, 0.5f, 0.5f, 1f)
            };
        }

        // ─────────────────────────────────────────────────────────────
        // 드래그
        // ─────────────────────────────────────────────────────────────

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (Card == null || Hud == null) return;

            // hover 상태였다면 hover offset과 슬롯 sibling을 먼저 원래대로 복원
            if (_isHovering)
            {
                ApplyHoverOffset(false);
                if (transform.parent != null && _hoverSlotOriginalSibling >= 0)
                {
                    transform.parent.SetSiblingIndex(_hoverSlotOriginalSibling);
                    _hoverSlotOriginalSibling = -1;
                }
            }

            CaptureHome();
            _isDragging = true;

            // dragLayer를 캔버스 최상단으로 끌어올리고 카드를 거기로 이동 (worldPositionStays=true로 시각 위치 유지)
            RectTransform dragLayer = Hud.DragLayer;
            if (dragLayer != null)
            {
                dragLayer.SetAsLastSibling();
                _rect.SetParent(dragLayer, true);
                _rect.SetAsLastSibling();
            }

            _canvasGroup.blocksRaycasts = false;
            ApplyScale(dragScaleMultiplier);
            // 드래그 중엔 카드를 똑바로 (fan layout의 회전 제거)
            _rect.localRotation = Quaternion.identity;
            // 위치는 그대로 — 카드는 슬롯 시각 위치에서 시작, 이후 OnDrag의 delta로 마우스를 따라감
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (Card == null) return;

            // PointerEventData.delta는 픽셀 이동량 → Canvas Scaler 보정 후 anchoredPosition에 누적
            RectTransform parentRect = _rect.parent as RectTransform;
            if (parentRect == null) return;

            Canvas canvas = parentRect.GetComponentInParent<Canvas>();
            float scaleFactor = canvas != null ? canvas.scaleFactor : 1f;
            if (scaleFactor <= 0f) scaleFactor = 1f;

            _rect.anchoredPosition += eventData.delta / scaleFactor;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (Card == null) return;
            _canvasGroup.blocksRaycasts = true;
            _isDragging = false;

            bool used = Hud != null && Hud.TryUseFromDrag(this, eventData);
            if (!used)
            {
                ReturnHome();
                // hover 상태가 유지될 수 있으므로 hover 표현을 다시 반영
                if (_isHovering)
                {
                    ApplyScale(hoverScaleMultiplier);
                    ApplyHoverOffset(true);
                    if (transform.parent != null)
                    {
                        _hoverSlotOriginalSibling = transform.parent.GetSiblingIndex();
                        transform.parent.SetAsLastSibling();
                    }
                }
            }
            // used인 경우 CardHandHUD.Refresh가 슬롯/스케일을 다시 캡처
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovering = true;
            if (Card == null || _isDragging) return;
            ApplyScale(hoverScaleMultiplier);
            ApplyHoverOffset(true);

            // 슬롯 자체를 부모(handRoot) 안에서 마지막 sibling으로 → 다른 카드보다 앞에 렌더
            if (transform.parent != null)
            {
                _hoverSlotOriginalSibling = transform.parent.GetSiblingIndex();
                transform.parent.SetAsLastSibling();
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovering = false;
            if (_isDragging) return;
            ApplyScale(1f);
            ApplyHoverOffset(false);

            // 원래 sibling 순서로 복귀
            if (transform.parent != null && _hoverSlotOriginalSibling >= 0)
            {
                transform.parent.SetSiblingIndex(_hoverSlotOriginalSibling);
                _hoverSlotOriginalSibling = -1;
            }
        }

        void ApplyHoverOffset(bool hovering)
        {
            if (_rect == null) return;
            _rect.anchoredPosition = hovering ? _homeAnchoredPos + hoverPositionOffset : _homeAnchoredPos;
        }

        // ─────────────────────────────────────────────────────────────
        // 클릭 (카드 선택 모드)
        // ─────────────────────────────────────────────────────────────

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Card == null || Hud == null) return;
            if (_isDragging) return; // 드래그 중인 클릭은 무시
            // 선택 모드면 Hud에 알림
            Hud.OnCardClicked(this);
        }

        void ApplyScale(float multiplier)
        {
            if (_rect == null) return;
            Vector3 baseScale = _homeLocalScale == Vector3.zero ? Vector3.one : _homeLocalScale;
            _rect.localScale = baseScale * multiplier;
        }

    }
}
