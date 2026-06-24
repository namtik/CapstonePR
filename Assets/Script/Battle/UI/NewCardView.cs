using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Battle.Card;

namespace Battle.UI
{
    // 카드 표시 + 드래그/호버/클릭을 처리하는 카드 뷰 컴포넌트
    public class NewCardView : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerClickHandler
    {
        // cardId와 아이콘 스프라이트 매핑 구조체
        [System.Serializable]
        public struct CardIconEntry
        {
            public int cardId; // 카드 ID
            public Sprite sprite; // 아이콘 스프라이트
        }

        [Header("Inspector 자동 바인딩 (비우면 자식 이름으로 자동 탐색)")]
        [SerializeField] private Image iconImage; // 카드 아이콘 이미지
        [SerializeField] private Image attributeImage; // 속성 아이콘 이미지
        [SerializeField] private Image backgroundImage; // 카드 배경 이미지
        [SerializeField] private TMP_Text nameText; // 카드 이름 텍스트
        [SerializeField] private TMP_Text descText; // 카드 설명 텍스트
        [SerializeField] private TMP_Text gaugeText; // 게이지 비용 텍스트
        [Tooltip("카드 타입(공격/스킬/파워) 텍스트. 프리팹 자식 이름 'cardType'.")]
        [SerializeField] private TMP_Text cardTypeText; // 카드 타입 텍스트
        [Tooltip("카드 타입 배경 Image. 프리팹 자식 이름 'baseCardType'.")]
        [SerializeField] private Image cardTypeBackground; // 카드 타입 배경 이미지

        [Header("자동 바인딩 옵션")]
        [Tooltip("Awake 시 자식 이름으로 누락된 참조를 채운다.")]
        [SerializeField] private bool autoBindOnAwake = true; // Awake 자동 바인딩 여부

        [Header("속성 이미지 매핑 (attributeImg sprite)")]
        [SerializeField] private Sprite fireElementSprite; // 불 속성 스프라이트
        [SerializeField] private Sprite waterElementSprite; // 물 속성 스프라이트
        [SerializeField] private Sprite windElementSprite; // 바람 속성 스프라이트
        [SerializeField] private Sprite earthElementSprite; // 땅 속성 스프라이트
        [SerializeField] private Sprite neutralElementSprite; // 무속성 스프라이트

        [Header("배경 이미지 매핑 (Background sprite — 속성별)")]
        [Tooltip("ON이면 속성에 따라 Background sprite를 바꾼다.")]
        [SerializeField] private bool useElementBackgroundSprite = true; // 속성별 배경 사용 여부
        [SerializeField] private Sprite fireBackgroundSprite; // 불 배경 스프라이트
        [SerializeField] private Sprite waterBackgroundSprite; // 물 배경 스프라이트
        [SerializeField] private Sprite windBackgroundSprite; // 바람 배경 스프라이트
        [SerializeField] private Sprite earthBackgroundSprite; // 땅 배경 스프라이트
        [SerializeField] private Sprite neutralBackgroundSprite; // 무속성 배경 스프라이트

        [Header("카드 아이콘 매핑 (IconImage sprite — cardId 기준)")]
        [SerializeField] private List<CardIconEntry> cardIcons = new List<CardIconEntry>(); // cardId별 아이콘 매핑
        [Tooltip("매핑이 없으면 Resources/CardIcons/{cardId}.png 자동 로드 시도.")]
        [SerializeField] private bool resourcesFallback = true; // 리소스 폴백 로드 여부
        [Tooltip("ON이면 카드 아이콘 자리에 속성 sprite를 그대로 사용 (카드 아이콘 미완성 임시).")]
        [SerializeField] private bool useElementSpriteAsIcon = true; // 속성 스프라이트를 아이콘 대용

        [Header("크기 — 드래그/Hover 시 일시 확대")]
        [Tooltip("드래그 중 카드 크기 배율 (홈 스케일 기준).")]
        [SerializeField] private float dragScaleMultiplier = 1.15f; // 드래그 시 배율
        [Tooltip("Hover(마우스 위) 시 카드 크기 배율 (홈 스케일 기준).")]
        [SerializeField] private float hoverScaleMultiplier = 1.05f; // 호버 시 배율
        [Tooltip("Hover 시 카드가 위로 떠오를 거리 (UI 좌표 단위).")]
        [SerializeField] private Vector2 hoverPositionOffset = new Vector2(0f, 100f); // 호버 시 상승 오프셋
        [Tooltip("덱 보기/픽커 그리드 — 제자리에서만 살짝 확대(떠오름 없음).")]
        [SerializeField] private float gridHoverScaleMultiplier = 1.06f; // 그리드 호버 배율

        [Header("드로우 등장 연출 — 아래에서 위로")]
        [Tooltip("패에 새로 들어온 카드가 아래에서 떠오르는 연출 ON/OFF.")]
        [SerializeField] private bool drawIntroEnabled = true; // 등장 연출 사용 여부
        [Tooltip("등장 연출 길이(초).")]
        [SerializeField] private float drawIntroDuration = 0.28f; // 등장 연출 길이
        [Tooltip("등장 시작 시 홈 위치에서 아래로 떨어져 있을 거리(UI 단위).")]
        [SerializeField] private float drawIntroRiseDistance = 240f; // 등장 상승 거리
        [Tooltip("등장 시작 시 카드 크기 배율(홈 스케일 기준). 1이면 크기 변화 없음.")]
        [SerializeField] private float drawIntroStartScale = 0.92f; // 등장 시작 배율

        [Header("색상 (sprite 매핑이 없을 때 fallback)")]
        [SerializeField] private bool tintBackgroundByElement = true; // 속성 색 틴트 사용 여부
        [Range(0f, 1f)]
        [SerializeField] private float backgroundTintAlpha = 0.35f; // 배경 틴트 강도
        [Tooltip("ON이면 카드 타입 배경(baseCardType)을 타입별 색으로 칠한다.")]
        [SerializeField] private bool tintCardTypeBackground = false; // 타입 배경 틴트 여부

        public CardInstance Card { get; private set; } // 현재 표시 중인 카드
        public int SlotIndex { get; set; } // 슬롯 인덱스
        public CardHandHUD Hud { get; set; } // 소유 HUD 참조

        private RectTransform _rect; // 자신의 RectTransform
        private CanvasGroup _canvasGroup; // 알파/레이캐스트 제어용
        private Vector2 _homeAnchoredPos; // 홈 위치
        private Transform _homeParent; // 홈 부모
        private int _homeSiblingIndex; // 홈 형제 인덱스
        private Vector3 _homeLocalScale; // 홈 스케일
        private Color _originalBackgroundColor; // 원본 배경 색
        private bool _capturedOriginalBgColor; // 원본 배경 색 캡처 여부
        private bool _isDragging; // 드래그 중 여부
        private bool _isHovering; // 호버 중 여부
        private int _hoverSlotOriginalSibling = -1; // 호버 전 형제 인덱스
        private Coroutine _drawIntroCo; // 등장 연출 코루틴
        private bool _playingIntro; // 등장 연출 진행 중 여부
        public bool IsPlayingDrawIntro => _playingIntro; // 등장 연출 진행 중 여부

        // 참조 바인딩과 원본 스케일/배경 색을 캡처
        void Awake()
        {
            _rect = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            if (autoBindOnAwake) AutoBind();

            // 프리팹 원본 스케일 보존
            _homeLocalScale = _rect.localScale;

            if (backgroundImage != null && !_capturedOriginalBgColor)
            {
                _originalBackgroundColor = backgroundImage.color;
                _capturedOriginalBgColor = true;
            }
        }

        // 비활성화 시 연출/드래그 상태를 정리해 재사용을 안전하게 함
        void OnDisable()
        {
            // 코루틴이 멈추므로 등장 연출 상태를 정리
            if (_drawIntroCo != null) { StopCoroutine(_drawIntroCo); _drawIntroCo = null; }
            _playingIntro = false;

            // OnEndDrag 누락으로 클릭이 막히는 것을 막기 위해 레이캐스트 복구
            _isDragging = false;
            if (_canvasGroup != null) _canvasGroup.blocksRaycasts = true;
        }

        // 속성에 대응하는 prefab 스프라이트를 반환
        public Sprite GetElementSprite(CardElement element) => element switch
        {
            CardElement.Fire    => fireElementSprite,
            CardElement.Water   => waterElementSprite,
            CardElement.Wind    => windElementSprite,
            CardElement.Earth   => earthElementSprite,
            CardElement.Neutral => neutralElementSprite,
            _ => null
        };

        // 자식 이름 규칙으로 누락된 참조를 자동 연결
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

        // 이름이 일치하는 자식에서 컴포넌트를 탐색
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

        // HUD와 슬롯 인덱스를 설정
        public void Bind(CardHandHUD hud, int slotIndex)
        {
            Hud = hud;
            SlotIndex = slotIndex;
        }

        // 표시할 카드를 설정하고 상호작용 상태를 정상화
        public void SetCard(CardInstance card)
        {
            // 재배치 시 클릭이 막히지 않도록 레이캐스트 복구
            _isDragging = false;
            if (_canvasGroup != null) _canvasGroup.blocksRaycasts = true;

            // 카드가 바뀌면 hover 등 일시 상태를 리셋
            if (Card != card)
            {
                CancelDrawIntro(); // 남아있던 등장 연출 정리
                _isHovering = false;
                if (_hoverSlotOriginalSibling >= 0 && transform.parent != null)
                {
                    transform.parent.SetSiblingIndex(_hoverSlotOriginalSibling);
                    _hoverSlotOriginalSibling = -1;
                }
            }
            Card = card;
            Refresh();
        }

        // CardData만으로 카드 외형(아이콘/속성/타입/텍스트)을 갱신
        public void ApplyShopPreview(CardData cardData, Sprite iconOverride = null)
        {
            if (cardData == null) return;

            if (nameText != null) nameText.text = cardData.displayName;
            if (descText != null) descText.text = cardData.description;
            if (gaugeText != null) gaugeText.text = EffectiveGaugeCost(cardData.gauge).ToString();

            if (cardTypeText != null) cardTypeText.text = CardTypeName(cardData.type);
            if (tintCardTypeBackground && cardTypeBackground != null)
                cardTypeBackground.color = ColorForCardType(cardData.type);

            ApplyAttributeSprite(cardData.element);
            ApplyBackgroundSprite(cardData.element);

            if (iconImage != null)
            {
                if (iconOverride != null)
                {
                    iconImage.sprite = iconOverride;
                    iconImage.color = Color.white;
                    iconImage.enabled = true;
                }
                else
                {
                    Sprite sp = null;
                    if (!string.IsNullOrEmpty(cardData.skillImg))
                        sp = Resources.Load<Sprite>($"CardIcons/{cardData.skillImg}");

                    if (sp != null)
                    {
                        iconImage.sprite = sp;
                        iconImage.color = Color.white;
                        iconImage.enabled = true;
                    }
                    else
                    {
                        Sprite elementSprite = GetElementSprite(cardData.element);
                        iconImage.sprite = elementSprite;
                        iconImage.color = elementSprite != null ? Color.white : ColorForElement(cardData.element, false);
                        iconImage.enabled = true;
                    }
                }
            }
        }

        // 유물(독주 10013 등) 코스트 가감을 반영한 표시용 게이지 코스트.
        // 실제 사용 비용(NewBattleController) 계산과 동일하게 0 하한으로 클램프한다.
        static int EffectiveGaugeCost(int baseGauge)
        {
            var rm = Battle.Relic.RelicManager.Instance;
            int delta = rm != null ? rm.GetGaugeCostDelta() : 0;
            return delta != 0 ? Mathf.Max(0, baseGauge + delta) : baseGauge;
        }

        // 현재 카드 데이터로 텍스트/속성/아이콘/배경을 갱신
        public void Refresh()
        {
            if (Card == null)
            {
                gameObject.SetActive(false);
                return;
            }
            gameObject.SetActive(true);
            // 등장 연출 중이 아니면 알파를 1로 고정
            if (!_playingIntro && _canvasGroup != null) _canvasGroup.alpha = 1f;

            // 글로벌 속성 저주 상태를 카드에 동기화
            if (Battle.NewBattleController.Instance != null)
            {
                Card.cursed = Battle.NewBattleController.Instance.IsElementCursed(Card.Element);
            }

            if (nameText != null) nameText.text = Card.data.displayName;
            if (descText != null) descText.text = Card.data.description;
            if (gaugeText != null) gaugeText.text = EffectiveGaugeCost(Card.data.gauge).ToString();

            // 카드 타입 표시
            if (cardTypeText != null) cardTypeText.text = CardTypeName(Card.Type);
            if (tintCardTypeBackground && cardTypeBackground != null)
                cardTypeBackground.color = ColorForCardType(Card.Type);

            // 속성 이미지 적용
            ApplyAttributeSprite(Card.Element);

            // 카드 아이콘 적용
            ApplyCardIcon(Card.Id);

            // 배경 이미지 적용
            ApplyBackgroundSprite(Card.Element);
        }

        // 속성에 맞는 배경 스프라이트/색을 적용
        void ApplyBackgroundSprite(CardElement element)
        {
            if (backgroundImage == null) return;

            bool cursed = Card != null && Card.cursed;

            // 속성별 배경 스프라이트 우선
            if (useElementBackgroundSprite)
            {
                Sprite s = GetBackgroundSprite(element);
                if (s != null)
                {
                    backgroundImage.sprite = s;
                    // 저주 시 보라 틴트, 평소엔 흰색
                    backgroundImage.color = cursed ? new Color(0.7f, 0.45f, 0.75f, 1f) : Color.white;
                    return;
                }
            }

            // 매핑이 없으면 색 틴트로 폴백
            if (tintBackgroundByElement)
            {
                Color baseColor = _capturedOriginalBgColor ? _originalBackgroundColor : Color.white;
                Color elementColor = ColorForElement(element, Card != null && Card.cursed);
                backgroundImage.color = Color.Lerp(baseColor, elementColor, backgroundTintAlpha);
            }
        }

        // 속성에 대응하는 배경 스프라이트를 반환
        Sprite GetBackgroundSprite(CardElement element) => element switch
        {
            CardElement.Fire    => fireBackgroundSprite,
            CardElement.Water   => waterBackgroundSprite,
            CardElement.Wind    => windBackgroundSprite,
            CardElement.Earth   => earthBackgroundSprite,
            CardElement.Neutral => neutralBackgroundSprite,
            _ => null
        };

        // 속성 아이콘 스프라이트(없으면 색)를 적용
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
                // 매핑이 없으면 색으로 표시
                attributeImage.color = ColorForElement(element, false);
            }
        }

        // 우선순위에 따라 카드 아이콘 스프라이트를 적용
        void ApplyCardIcon(int cardId)
        {
            if (iconImage == null) return;

            // 1순위: DB의 SkillImg 기반 로드
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

            // 2순위: 인스펙터 매핑
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

            // 3순위: Resources 경로 로드(구버전 호환)
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

            // 4순위: 속성 스프라이트로 대체
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

            // 마지막 폴백: 속성 색만 표시
            iconImage.sprite = null;
            iconImage.color = ColorForElement(Card != null ? Card.Element : CardElement.Neutral, false);
        }

        // 현재 위치/부모/형제 인덱스를 홈으로 캡처
        public void CaptureHome()
        {
            if (_rect == null) _rect = GetComponent<RectTransform>();
            // 현재 위치를 그대로 홈으로 캡처
            _homeAnchoredPos = _rect.anchoredPosition;
            _homeParent = _rect.parent;
            _homeSiblingIndex = _rect.GetSiblingIndex();
            // _homeLocalScale은 Awake의 베이스를 유지
        }

        // 캡처한 홈 위치/부모/스케일로 복원
        public void ReturnHome()
        {
            if (_homeParent == null) return;
            _rect.SetParent(_homeParent, false);
            _rect.SetSiblingIndex(_homeSiblingIndex);
            _rect.anchoredPosition = _homeAnchoredPos;
            _rect.localScale = _homeLocalScale;
            // 슬롯의 fan 회전을 그대로 따름
            _rect.localRotation = Quaternion.identity;
        }

        // 스케일을 홈 베이스 값으로 되돌림
        public void ResetToHomeScale()
        {
            if (_rect == null) _rect = GetComponent<RectTransform>();
            _rect.localScale = _homeLocalScale == Vector3.zero ? Vector3.one : _homeLocalScale;
        }

        // 새로 들어온 카드를 아래에서 위로 떠오르게 하는 등장 연출 시작
        public void PlayDrawIntro(float delay = 0f)
        {
            if (_rect == null) _rect = GetComponent<RectTransform>();
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();

            // 연출 비활성 또는 오브젝트 비활성이면 홈에 고정
            if (!drawIntroEnabled || !isActiveAndEnabled)
            {
                SettleIntro();
                return;
            }

            if (_drawIntroCo != null) StopCoroutine(_drawIntroCo);

            // 화면 기준 아래 오프셋을 슬롯 로컬 좌표로 변환
            Vector2 below = ComputeBelowOffset();

            // stagger 동안 깜빡이지 않도록 시작 상태를 즉시 적용
            _playingIntro = true;
            Vector3 baseScale = _homeLocalScale == Vector3.zero ? Vector3.one : _homeLocalScale;
            _rect.anchoredPosition = _homeAnchoredPos + below;
            _rect.localScale = baseScale * drawIntroStartScale;
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;

            _drawIntroCo = StartCoroutine(DrawIntroRoutine(delay, baseScale, below));
        }

        // 화면 기준 아래 방향 상승 거리를 슬롯 로컬 오프셋으로 변환
        Vector2 ComputeBelowOffset()
        {
            float slotZ = (_rect != null && _rect.parent != null) ? _rect.parent.localEulerAngles.z : 0f;
            float rad = -slotZ * Mathf.Deg2Rad;
            float ry = -drawIntroRiseDistance; // 화면 기준 아래 방향
            // 회전 행렬 적용
            return new Vector2(-ry * Mathf.Sin(rad), ry * Mathf.Cos(rad));
        }

        // 지연 후 아래에서 홈까지 이동·확대·페이드인하는 등장 코루틴
        System.Collections.IEnumerator DrawIntroRoutine(float delay, Vector3 baseScale, Vector2 below)
        {
            // stagger 대기(카드는 아래에서 알파 0으로 숨음)
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
                float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f); // ease-out cubic
                _rect.anchoredPosition = Vector2.LerpUnclamped(start, _homeAnchoredPos, e);
                _rect.localScale = baseScale * Mathf.LerpUnclamped(drawIntroStartScale, 1f, e);
                if (_canvasGroup != null) _canvasGroup.alpha = Mathf.Clamp01(e);
                yield return null;
            }

            _drawIntroCo = null;
            SettleIntro();
        }

        // 등장 연출을 즉시 끝내고 홈 위치/스케일/알파로 고정
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

        // 진행 중인 등장 연출을 취소하고 홈으로 즉시 정착
        void CancelDrawIntro()
        {
            if (_drawIntroCo != null) { StopCoroutine(_drawIntroCo); _drawIntroCo = null; }
            if (_playingIntro) SettleIntro();
        }

        // 속성/저주 여부에 대응하는 색을 반환
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

        // 카드 타입의 표시 이름을 반환
        static string CardTypeName(CardType type) => type switch
        {
            CardType.Attack => "공격",
            CardType.Skill  => "스킬",
            CardType.Power  => "파워",
            _ => ""
        };

        // 카드 타입에 대응하는 색을 반환
        static Color ColorForCardType(CardType type) => type switch
        {
            CardType.Attack => new Color(0.90f, 0.40f, 0.35f, 1f), // 빨강
            CardType.Skill  => new Color(0.40f, 0.65f, 0.90f, 1f), // 파랑
            CardType.Power  => new Color(0.75f, 0.55f, 0.90f, 1f), // 보라
            _ => Color.white
        };

        // 드래그 시작 시 홈 캡처·드래그 레이어 이동·확대를 처리
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (Card == null || Hud == null) return;
            CancelDrawIntro(); // 등장 연출 중이면 즉시 홈으로 정착

            // hover 상태였으면 오프셋/형제 순서를 먼저 복원
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

            // 드래그 레이어를 최상단으로 올리고 카드를 이동
            RectTransform dragLayer = Hud.DragLayer;
            if (dragLayer != null)
            {
                dragLayer.SetAsLastSibling();
                _rect.SetParent(dragLayer, true);
                _rect.SetAsLastSibling();
            }

            _canvasGroup.blocksRaycasts = false;
            ApplyScale(dragScaleMultiplier);
            // 드래그 중엔 회전 제거
            _rect.localRotation = Quaternion.identity;
        }

        // 드래그 이동량을 스케일 보정해 위치에 누적
        public void OnDrag(PointerEventData eventData)
        {
            if (Card == null || Hud == null) return;

            // delta를 캔버스 스케일로 보정해 누적
            RectTransform parentRect = _rect.parent as RectTransform;
            if (parentRect == null) return;

            Canvas canvas = parentRect.GetComponentInParent<Canvas>();
            float scaleFactor = canvas != null ? canvas.scaleFactor : 1f;
            if (scaleFactor <= 0f) scaleFactor = 1f;

            _rect.anchoredPosition += eventData.delta / scaleFactor;
        }

        // 드래그 종료 시 사용 시도, 실패하면 홈으로 복귀
        public void OnEndDrag(PointerEventData eventData)
        {
            if (Card == null || Hud == null) return;
            _canvasGroup.blocksRaycasts = true;
            _isDragging = false;

            bool used = Hud != null && Hud.TryUseFromDrag(this, eventData);
            if (!used)
            {
                ReturnHome();
                // hover 상태면 hover 표현을 다시 반영
                if (_isHovering)
                {
                    ResolveHoverPresentation(out float scaleMultiplier, out Vector2 positionOffset);
                    ApplyScale(scaleMultiplier);
                    ApplyHoverOffset(positionOffset != Vector2.zero);
                    if (transform.parent != null)
                    {
                        _hoverSlotOriginalSibling = transform.parent.GetSiblingIndex();
                        transform.parent.SetAsLastSibling();
                    }
                }
            }
        }

        // 덱 보기/픽커 그리드 — 팝업(상승) 없이 제자리 호버만
        bool IsGridHoverCard()
        {
            return Hud != null && SlotIndex < 0 && (Hud.IsViewerMode || Hud.IsPickerMode);
        }

        void ResolveHoverPresentation(out float scaleMultiplier, out Vector2 positionOffset)
        {
            if (IsGridHoverCard())
            {
                scaleMultiplier = gridHoverScaleMultiplier;
                positionOffset = Vector2.zero;
                return;
            }

            scaleMultiplier = hoverScaleMultiplier;
            positionOffset = hoverPositionOffset;
        }

        // 호버 진입 — 그리드는 제자리 확대, 손패는 확대+상승
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Hud == null) return;
            if (Card == null || _isDragging) return;

            _isHovering = true;
            CancelDrawIntro();
            ResolveHoverPresentation(out float scaleMultiplier, out Vector2 positionOffset);
            ApplyScale(scaleMultiplier);
            ApplyHoverOffset(positionOffset != Vector2.zero);

            if (transform.parent != null)
            {
                _hoverSlotOriginalSibling = transform.parent.GetSiblingIndex();
                transform.parent.SetAsLastSibling();
            }
        }

        // 호버 종료 시 크기/위치/형제 순서를 복원
        public void OnPointerExit(PointerEventData eventData)
        {
            if (Hud == null) return;
            _isHovering = false;
            if (_isDragging) return;
            ApplyScale(1f);
            ApplyHoverOffset(false);

            // 원래 형제 순서로 복귀
            if (transform.parent != null && _hoverSlotOriginalSibling >= 0)
            {
                transform.parent.SetSiblingIndex(_hoverSlotOriginalSibling);
                _hoverSlotOriginalSibling = -1;
            }
        }

        // 호버 오프셋을 적용/해제
        void ApplyHoverOffset(bool hovering)
        {
            if (_rect == null) return;
            if (IsGridHoverCard())
            {
                _rect.anchoredPosition = _homeAnchoredPos;
                return;
            }

            _rect.anchoredPosition = hovering ? _homeAnchoredPos + hoverPositionOffset : _homeAnchoredPos;
        }

        // 클릭 처리(선택/픽커는 단일 클릭, 일반은 더블클릭 사용)
        public void OnPointerClick(PointerEventData eventData)
        {
            if (Card == null || Hud == null) return;
            if (_isDragging) return; // 드래그 중 클릭 무시

            // 선택/픽커 모드는 단일 클릭으로 선택
            if (Hud.IsSelectionMode || Hud.IsPickerMode)
            {
                Hud.OnCardClicked(this);
                return;
            }

            // 일반 모드는 더블클릭으로 사용
            if (eventData.clickCount >= 2)
            {
                Hud.TryUseFromClick(this);
            }
        }

        // 홈 베이스 스케일에 배율을 곱해 적용
        void ApplyScale(float multiplier)
        {
            if (_rect == null) return;
            Vector3 baseScale = _homeLocalScale == Vector3.zero ? Vector3.one : _homeLocalScale;
            _rect.localScale = baseScale * multiplier;
        }

    }
}
