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
        [Tooltip("카드 타입(공격/스킬/파워) 텍스트. 프리팹 자식 이름 'cardType'.")]
        [SerializeField] private TMP_Text cardTypeText;
        [Tooltip("카드 타입 배경 Image. 프리팹 자식 이름 'baseCardType'.")]
        [SerializeField] private Image cardTypeBackground;

        [Header("자동 바인딩 옵션")]
        [Tooltip("Awake 시 자식 이름으로 누락된 참조를 채운다.")]
        [SerializeField] private bool autoBindOnAwake = true;

        [Header("속성 이미지 매핑 (attributeImg sprite)")]
        [SerializeField] private Sprite fireElementSprite;
        [SerializeField] private Sprite waterElementSprite;
        [SerializeField] private Sprite windElementSprite;
        [SerializeField] private Sprite earthElementSprite;
        [SerializeField] private Sprite neutralElementSprite;

        [Header("배경 이미지 매핑 (Background sprite — 속성별)")]
        [Tooltip("ON이면 속성에 따라 Background sprite를 바꾼다.")]
        [SerializeField] private bool useElementBackgroundSprite = true;
        [SerializeField] private Sprite fireBackgroundSprite;
        [SerializeField] private Sprite waterBackgroundSprite;
        [SerializeField] private Sprite windBackgroundSprite;
        [SerializeField] private Sprite earthBackgroundSprite;
        [SerializeField] private Sprite neutralBackgroundSprite;

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

        [Header("드로우 등장 연출 — 아래에서 위로")]
        [Tooltip("패에 새로 들어온 카드가 아래에서 떠오르는 연출 ON/OFF.")]
        [SerializeField] private bool drawIntroEnabled = true;
        [Tooltip("등장 연출 길이(초).")]
        [SerializeField] private float drawIntroDuration = 0.28f;
        [Tooltip("등장 시작 시 홈 위치에서 아래로 떨어져 있을 거리(UI 단위).")]
        [SerializeField] private float drawIntroRiseDistance = 240f;
        [Tooltip("등장 시작 시 카드 크기 배율(홈 스케일 기준). 1이면 크기 변화 없음.")]
        [SerializeField] private float drawIntroStartScale = 0.92f;

        [Header("색상 (sprite 매핑이 없을 때 fallback)")]
        [SerializeField] private bool tintBackgroundByElement = true;
        [Range(0f, 1f)]
        [SerializeField] private float backgroundTintAlpha = 0.35f;
        [Tooltip("ON이면 카드 타입 배경(baseCardType)을 타입별 색으로 칠한다.")]
        [SerializeField] private bool tintCardTypeBackground = false;

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
        private Coroutine _drawIntroCo;
        private bool _playingIntro;
        /// <summary>드로우 등장 연출 진행 중 — CardHandHUD가 슬롯 위치 리셋을 건너뛰는 판단에 사용.</summary>
        public bool IsPlayingDrawIntro => _playingIntro;

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

        void OnDisable()
        {
            // 비활성화되면 Unity가 코루틴을 멈추므로 등장 연출 상태를 정리 — 다음 활성화 시 깨끗한 상태 보장
            if (_drawIntroCo != null) { StopCoroutine(_drawIntroCo); _drawIntroCo = null; }
            _playingIntro = false;
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
            if (cardTypeText == null) cardTypeText = FindChildComponent<TMP_Text>("cardType");
            if (cardTypeBackground == null) cardTypeBackground = FindChildComponent<Image>("baseCardType");
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
            // 카드가 바뀌면 hover/drag 같은 transient 상태 리셋 — 카드 사용 후 잔여 상태로
            // 인한 손패 정렬 어긋남 방지(특히 더블클릭 사용 직후 마우스가 같은 위치에 있을 때).
            if (Card != card)
            {
                CancelDrawIntro(); // 재사용되는 뷰에 남아있던 등장 연출 정리
                _isHovering = false;
                _isDragging = false;
                if (_hoverSlotOriginalSibling >= 0 && transform.parent != null)
                {
                    transform.parent.SetSiblingIndex(_hoverSlotOriginalSibling);
                    _hoverSlotOriginalSibling = -1;
                }
            }
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
            // 등장 연출 중이 아니면 알파를 항상 1로 — 이전 연출 잔여로 카드가 투명하게 남는 것 방지
            if (!_playingIntro && _canvasGroup != null) _canvasGroup.alpha = 1f;

            // 새 전투 시스템: 글로벌 속성 저주 상태를 카드 인스턴스에 동기화 (시각 표시용)
            if (Battle.NewBattleController.Instance != null)
            {
                Card.cursed = Battle.NewBattleController.Instance.IsElementCursed(Card.Element);
            }

            if (nameText != null) nameText.text = Card.data.displayName;
            if (descText != null) descText.text = Card.data.description;
            if (gaugeText != null) gaugeText.text = Card.data.gauge.ToString();

            // 카드 타입 (공격/스킬/파워)
            if (cardTypeText != null) cardTypeText.text = CardTypeName(Card.Type);
            if (tintCardTypeBackground && cardTypeBackground != null)
                cardTypeBackground.color = ColorForCardType(Card.Type);

            // 속성 이미지
            ApplyAttributeSprite(Card.Element);

            // 카드 아이콘
            ApplyCardIcon(Card.Id);

            // 배경 이미지(속성별 sprite 우선, 없으면 색 틴트)
            ApplyBackgroundSprite(Card.Element);
        }

        void ApplyBackgroundSprite(CardElement element)
        {
            if (backgroundImage == null) return;

            bool cursed = Card != null && Card.cursed;

            // 속성별 배경 sprite 우선
            if (useElementBackgroundSprite)
            {
                Sprite s = GetBackgroundSprite(element);
                if (s != null)
                {
                    backgroundImage.sprite = s;
                    // 저주 시 보라 틴트로 구분, 평소엔 원본 색(흰색)
                    backgroundImage.color = cursed ? new Color(0.7f, 0.45f, 0.75f, 1f) : Color.white;
                    return;
                }
            }

            // sprite 매핑이 없으면 색 틴트 fallback
            if (tintBackgroundByElement)
            {
                Color baseColor = _capturedOriginalBgColor ? _originalBackgroundColor : Color.white;
                Color elementColor = ColorForElement(element, Card != null && Card.cursed);
                backgroundImage.color = Color.Lerp(baseColor, elementColor, backgroundTintAlpha);
            }
        }

        Sprite GetBackgroundSprite(CardElement element) => element switch
        {
            CardElement.Fire    => fireBackgroundSprite,
            CardElement.Water   => waterBackgroundSprite,
            CardElement.Wind    => windBackgroundSprite,
            CardElement.Earth   => earthBackgroundSprite,
            CardElement.Neutral => neutralBackgroundSprite,
            _ => null
        };

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

            // 1순위: DB의 SkillImg 컬럼 기반 (Resources/CardIcons/{skillImg})
            if (Card != null && !string.IsNullOrEmpty(Card.data.skillImg))
            {
                Sprite sp = Resources.Load<Sprite>($"CardIcons/{Card.data.skillImg}");
                if (sp != null)
                {
                    iconImage.sprite = sp;
                    iconImage.color = Color.white;
                    iconImage.enabled = true;
                    return;
                }
            }

            // 2순위: Inspector 매핑
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

            // 3순위: Resources/CardIcons/{cardId} (구버전 호환)
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

            // 4순위(임시): 카드 아이콘 미완성 → 속성 sprite로 대체
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

            // 마지막 fallback: 속성 색만 표시
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

        // ─────────────────────────────────────────────────────────────
        // 드로우 등장 연출 (아래에서 위로 떠오름)
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 패에 새로 들어온 카드를 홈 위치 아래에서 위로 떠오르게 한다.
        /// CaptureHome() 직후(홈 좌표가 확정된 상태)에 호출해야 한다.
        /// delay: 연속 드로우 시 카드마다 시작을 늦춰 차례로 올라오는 느낌(stagger).
        /// </summary>
        public void PlayDrawIntro(float delay = 0f)
        {
            if (_rect == null) _rect = GetComponent<RectTransform>();
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();

            // 연출 비활성 또는 오브젝트 비활성(빈 슬롯) → 그냥 홈에 고정
            if (!drawIntroEnabled || !isActiveAndEnabled)
            {
                SettleIntro();
                return;
            }

            if (_drawIntroCo != null) StopCoroutine(_drawIntroCo);

            // 부채꼴 슬롯은 회전돼 있으므로, 화면 기준 '수직 아래' 오프셋을 슬롯 로컬 좌표로 변환
            // (가장자리 카드도 비스듬하지 않고 똑바로 위로 올라오도록).
            Vector2 below = ComputeBelowOffset();

            // 시작 상태를 즉시 적용 — delay(stagger) 동안 홈에서 깜빡이지 않도록 미리 아래에 숨긴다
            _playingIntro = true;
            Vector3 baseScale = _homeLocalScale == Vector3.zero ? Vector3.one : _homeLocalScale;
            _rect.anchoredPosition = _homeAnchoredPos + below;
            _rect.localScale = baseScale * drawIntroStartScale;
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;

            _drawIntroCo = StartCoroutine(DrawIntroRoutine(delay, baseScale, below));
        }

        /// <summary>화면 기준 '수직 아래로 drawIntroRiseDistance'를 현재 슬롯(부모) 회전을 상쇄한 로컬 오프셋으로 변환.</summary>
        Vector2 ComputeBelowOffset()
        {
            float slotZ = (_rect != null && _rect.parent != null) ? _rect.parent.localEulerAngles.z : 0f;
            float rad = -slotZ * Mathf.Deg2Rad;
            float ry = -drawIntroRiseDistance; // 화면 기준 아래 방향
            // Rot(rad) * (0, ry)
            return new Vector2(-ry * Mathf.Sin(rad), ry * Mathf.Cos(rad));
        }

        System.Collections.IEnumerator DrawIntroRoutine(float delay, Vector3 baseScale, Vector2 below)
        {
            // stagger 대기 (이 동안 카드는 아래에서 알파 0으로 숨어 있음)
            while (delay > 0f)
            {
                delay -= Time.unscaledDeltaTime;
                yield return null;
            }

            Vector2 start = _homeAnchoredPos + below;
            float dur = Mathf.Max(0.01f, drawIntroDuration);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / dur;
                float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f); // ease-out cubic — 빠르게 올라와 부드럽게 정착
                _rect.anchoredPosition = Vector2.LerpUnclamped(start, _homeAnchoredPos, e);
                _rect.localScale = baseScale * Mathf.LerpUnclamped(drawIntroStartScale, 1f, e);
                if (_canvasGroup != null) _canvasGroup.alpha = Mathf.Clamp01(e);
                yield return null;
            }

            _drawIntroCo = null;
            SettleIntro();
        }

        /// <summary>등장 연출을 즉시 끝내고 홈 위치/스케일/알파로 고정.</summary>
        void SettleIntro()
        {
            _playingIntro = false;
            if (_rect != null)
            {
                _rect.anchoredPosition = _homeAnchoredPos;
                _rect.localScale = _homeLocalScale == Vector3.zero ? Vector3.one : _homeLocalScale;
            }
            if (_canvasGroup != null) _canvasGroup.alpha = 1f;
        }

        /// <summary>진행 중인 등장 연출을 취소(드래그/hover 시작·카드 교체 등)하고 홈으로 즉시 정착.</summary>
        void CancelDrawIntro()
        {
            if (_drawIntroCo != null) { StopCoroutine(_drawIntroCo); _drawIntroCo = null; }
            if (_playingIntro) SettleIntro();
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

        static string CardTypeName(CardType type) => type switch
        {
            CardType.Attack => "공격",
            CardType.Skill  => "스킬",
            CardType.Power  => "파워",
            _ => ""
        };

        static Color ColorForCardType(CardType type) => type switch
        {
            CardType.Attack => new Color(0.90f, 0.40f, 0.35f, 1f), // 빨강
            CardType.Skill  => new Color(0.40f, 0.65f, 0.90f, 1f), // 파랑
            CardType.Power  => new Color(0.75f, 0.55f, 0.90f, 1f), // 보라
            _ => Color.white
        };

        // ─────────────────────────────────────────────────────────────
        // 드래그
        // ─────────────────────────────────────────────────────────────

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (Card == null || Hud == null) return;
            CancelDrawIntro(); // 등장 연출 중 잡으면 즉시 홈으로 정착 후 드래그

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
            // 선택 모드에서 선택 불가(필터/제외) 카드는 hover 강조하지 않음 — 잘못된 시각 피드백 방지
            if (Hud != null && Hud.IsSelectionMode && !Hud.IsCardSelectable(Card)) return;
            CancelDrawIntro(); // hover 시작하면 등장 연출을 끝내고 hover 표현으로 전환
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

            // 선택/픽커 모드: 단일 클릭으로 선택 처리
            if (Hud.IsSelectionMode || Hud.IsPickerMode)
            {
                Hud.OnCardClicked(this);
                return;
            }

            // 일반 모드: 더블클릭(clickCount>=2)으로 카드 사용
            if (eventData.clickCount >= 2)
            {
                Hud.TryUseFromClick(this);
            }
        }

        void ApplyScale(float multiplier)
        {
            if (_rect == null) return;
            Vector3 baseScale = _homeLocalScale == Vector3.zero ? Vector3.one : _homeLocalScale;
            _rect.localScale = baseScale * multiplier;
        }

        /// <summary>
        /// 손패로 복귀/갱신 시 raycast 차단을 해제해 항상 클릭 가능하게 한다.
        /// 드래그(OnBeginDrag)나 각성 중 비차단 연출 churn으로 OnEndDrag가 누락되면
        /// blocksRaycasts=false가 남아 그 카드뷰가 다음 전투로 재사용될 때 클릭 불가가 된다.
        /// CardHandHUD.Refresh가 매 손패 갱신마다 호출.
        /// </summary>
        public void EnsureRaycastable()
        {
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup != null) _canvasGroup.blocksRaycasts = true;
        }

    }
}
