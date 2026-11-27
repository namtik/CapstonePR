using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using TMPro;
using Battle.Card;
using Battle.Deck;
using Battle;

namespace Battle.UI
{
    // 손패 HUD — 카드 슬롯 구성·콤보/각성 UI 토글
    public partial class CardHandHUD : MonoBehaviour
    {
        public const int SLOT_COUNT = CardDeckSystem.MAX_HAND_LIMIT; // UI 슬롯 사전 생성 수(패 한도 상한)
        public const int LAYOUT_REFERENCE_COUNT = CardDeckSystem.HAND_LIMIT; // 레이아웃 기준 카드 수

        [Header("프리팹 (필수)")]
        [SerializeField] private NewCardView cardPrefab; // 카드 뷰 프리팹
        // 외부에서 카드 뷰 프리팹을 재사용하기 위한 접근자
        public NewCardView CardPrefab => cardPrefab;

        [Header("배치")]
        [Tooltip("카드 슬롯들이 정렬될 부모. 비우면 본 트랜스폼 자식으로 자동 생성.")]
        [SerializeField] private RectTransform handRoot; // 카드 슬롯 부모
        [Tooltip("드래그 중 카드가 옮겨질 상위 캔버스 레이어. 비우면 같은 캔버스의 최상단을 자동 사용.")]
        [SerializeField] private RectTransform dragLayer; // 드래그 레이어
        // 드래그 레이어 반환(없으면 handRoot)
        public RectTransform DragLayer => dragLayer != null ? dragLayer : handRoot;
        public RectTransform HandRoot => handRoot; // 손패 루트(적 행동 중 숨김용)

        // 덱(뽑을 더미)·묘지(버린 더미) UI 루트 — 수치 텍스트에서 캔버스 바로 아래 오브젝트까지 거슬러 올라간 것
        public void CollectPileRoots(List<GameObject> into)
        {
            AddCanvasChild(drawCountText, into);
            AddCanvasChild(discardCountText, into);
        }

        static void AddCanvasChild(Component c, List<GameObject> into)
        {
            if (c == null) return;
            Transform t = c.transform;
            while (t.parent != null && t.parent.GetComponent<Canvas>() == null) t = t.parent;
            if (t.parent == null) t = c.transform; // 캔버스를 못 찾으면 텍스트만
            if (!into.Contains(t.gameObject)) into.Add(t.gameObject);
        }

        [Header("레이아웃 — 슬롯 위치 자동 정렬")]
        [Tooltip("슬롯 간 간격 (x). NewSkillCard 프리팹이 scale=1, 300×400일 때 650~750 권장.")]
        [SerializeField] private Vector2 slotSpacing = new Vector2(700f, 0f); // 슬롯 간 간격
        [SerializeField] private Vector2 handAnchoredPos = new Vector2(0f, 240f); // 손패 루트 위치
        [Tooltip("손패에 놓인 카드의 기본 스케일. 프리팹 원본(상점/인벤토리)과 별개.")]
        [SerializeField] private float handCardScale = 1f; // 손패 카드 기본 스케일
        public float HandCardScale => handCardScale; // 손패 카드 기본 스케일
        [Tooltip("드래그 종료 시 스크린 Y가 이 값 이상이면 카드 사용으로 간주.")]
        [SerializeField] private float useThresholdY = 500f; // 카드 사용 판정 Y
        public float UseThresholdY => useThresholdY; // 지정 모드 진입 임계선

        [Header("적 지정 — 드래그 화살표")]
        [Tooltip("ON이면 target=ENEMY 카드를 드래그할 때 화살표로 적을 조준해야 사용된다.")]
        [SerializeField] private bool enemyTargetingEnabled = true; // 적 지정 사용 여부
        [Tooltip("화살표 체브론/화살촉. 비우면 화살표를 그리지 않고 록온 브래킷만 남긴다. 팁이 위쪽이면 방향이 맞는다.")]
        [SerializeField] private Sprite targetingArrowSprite; // 화살표 이미지
        [Tooltip("록온 코너(ㄴ자) 한 장. 비우면 기본 브래킷. 좌하단 ㄴ, 피벗은 바깥 꼭짓점.")]
        [SerializeField] private Sprite targetingBracketSprite; // 록온 브래킷 이미지

        [Header("Fan Layout — 손패 부채꼴 연출")]
        [Tooltip("ON이면 카드들이 호 형태로 회전·배치된다.")]
        [SerializeField] private bool fanLayout = true; // 부채꼴 배치 사용 여부
        [Tooltip("부채꼴 반지름. 클수록 호가 평평해짐.")]
        [SerializeField] private float fanRadius = 1800f; // 부채꼴 반지름
        [Tooltip("부채꼴 전체 각도(도). 클수록 카드 사이 간격이 넓어짐. 5장 기준 36~44 권장.")]
        [SerializeField] private float fanArcAngle = 40f; // 부채꼴 전체 각도
        [Tooltip("호 위 가장자리에서 카드들이 떨어질 깊이. 작을수록 카드들이 살짝 아래로 호를 그림.")]
        [SerializeField] private float fanVerticalDip = 50f; // 호 하강 깊이

        [Header("Hover 이웃 밀어내기 — 카드에 마우스 올리면 양옆 벌어짐")]
        [Tooltip("ON이면 손패 카드에 마우스를 올릴 때 양옆 카드가 좌우로 살짝 벌어진다.")]
        [SerializeField] private bool hoverSpreadEnabled = true; // 이웃 밀어내기 사용 여부
        [Tooltip("Hover한 카드 바로 옆 카드가 밀려나는 거리(px). 멀리 있는 카드일수록 감소.")]
        [SerializeField] private float hoverSpreadDistance = 90f; // 인접 카드 밀림 거리
        [Tooltip("Hover 카드로부터 이 칸 수까지만 밀림 영향(거리에 따라 선형 감소).")]
        [SerializeField] private int hoverSpreadRange = 2; // 밀림 영향 범위(좌우 칸 수)
        [Tooltip("밀림/복귀 애니메이션 속도. 클수록 빠르게 자리 잡음. 0이면 즉시.")]
        [SerializeField] private float hoverSpreadLerpSpeed = 14f; // 밀림 보간 속도

        [Header("드로우 등장 연출")]
        [Tooltip("여러 장을 동시에 뽑을 때 카드마다 떠오르기 시작을 늦추는 간격(초). 0이면 동시에 올라옴.")]
        [SerializeField] private float drawIntroStagger = 0.05f; // 드로우 등장 지연 간격
        [Tooltip("각성 진입/종료 시 손패 카드↔부적 전환을 카드마다 시작하는 간격(초).")]
        [SerializeField] private float talismanBurnStagger = 0.06f; // 부적 전환 지연 간격
        private bool _talismanHand; // 각성 중 — 손패를 부적으로 표시
        public bool IsTalismanHand => _talismanHand; // 손패 부적 표시 여부

        // 손패 중 카드↔부적 전환 연출이 진행 중인 카드가 있는지(대기 중인 시간차 포함)
        public bool IsTalismanTransitioning
        {
            get
            {
                for (int i = 0; i < _cardViews.Count; i++)
                    if (_cardViews[i] != null && _cardViews[i].IsTalismanTransitioning) return true;
                return false;
            }
        }

        [Header("콤보 UI — 각성 전용")]
        [Tooltip("각성 발동 시 활성화될 콤보 슬롯 패널.")]
        [SerializeField] private GameObject comboSlotPanel; // 콤보 슬롯 패널
        [Tooltip("각성 발동 시 활성화될 콤보 스킬 목록 패널.")]
        [SerializeField] private GameObject comboSkillPanel; // 콤보 스킬 패널
        [Tooltip("콤보 슬롯 3칸의 프레임 Image. 비워두면 comboSlotPanel 자식에서 자동 탐색.")]
        [SerializeField] private Image[] comboSlotImages = new Image[3]; // 콤보 슬롯 프레임 3칸
        [Tooltip("원소 아이콘이 들어가는 프레임. 비우면 Resources/UI/UI_ElementSlot_Frame 를 사용.")]
        [SerializeField] private Sprite comboSlotFrameSprite; // 콤보 슬롯 프레임 스프라이트
        [Tooltip("프레임 안쪽 배경. 비우면 Resources/UI/UI_ElementSlot_BG.")]
        [SerializeField] private Sprite comboSlotBgSprite; // 콤보 슬롯 안쪽 배경
        [Tooltip("프레임 안에 표시할 불 아이콘. 비우면 Resources/UI/UI_Awake_FireIcon.")]
        [SerializeField] private Sprite comboSlotFireSprite; // 각성 슬롯 불 아이콘
        [Tooltip("프레임 안에 표시할 물 아이콘. 비우면 Resources/UI/UI_Awake_WaterIcon.")]
        [SerializeField] private Sprite comboSlotWaterSprite; // 각성 슬롯 물 아이콘
        [Tooltip("프레임 안에 표시할 바람 아이콘. 비우면 Resources/UI/UI_Awake_WindIcon.")]
        [SerializeField] private Sprite comboSlotWindSprite; // 각성 슬롯 바람 아이콘
        [Tooltip("프레임 안에 표시할 땅 아이콘. 비우면 Resources/UI/UI_Awake_LandIcon.")]
        [SerializeField] private Sprite comboSlotEarthSprite; // 각성 슬롯 땅 아이콘
        [SerializeField] private Color emptyComboSlotColor = new Color(0.25f, 0.25f, 0.25f, 0.4f); // 프레임이 없을 때 빈 칸 색

        [Header("콤보 슬롯 입력 연출 (각성 중 속성이 슬롯에 들어올 때)")]
        [Tooltip("각성 중 콤보 슬롯에 속성이 들어올 때 해당 칸을 팝(확대→복귀)시키는 연출 ON/OFF.")]
        [SerializeField] private bool comboSlotPopEnabled = true; // 콤보 슬롯 팝 연출 사용 여부
        [Tooltip("팝 시 최대 확대 배율(원본 스케일 기준). 1.5 = 50% 커졌다 돌아옴.")]
        [SerializeField] private float comboSlotPopScale = 1.5f; // 팝 최대 확대 배율
        [Tooltip("커졌다 원래대로 돌아오는 전체 시간(초).")]
        [SerializeField] private float comboSlotPopDuration = 0.28f; // 팝 전체 시간
        [Tooltip("입력 순간 칸을 흰색으로 잠깐 플래시할지 여부.")]
        [SerializeField] private bool comboSlotFlashEnabled = true; // 입력 칸 플래시 사용 여부
        [Tooltip("각성 중 콤보 칸에 속성이 들어오면 금색 프레임 주변을 그 속성 빛으로 감싼다.")]
        [SerializeField] private bool comboSlotElementVfxEnabled = true; // 콤보 슬롯 속성 테두리 이펙트

        [Header("콤보 스킬 목록 (SkillListItem 프리팹)")]
        [Tooltip("콤보 스킬 한 항목을 표현할 프리팹. SkillListItem.prefab 사용.")]
        [SerializeField] private SkillListItemUI comboSkillItemPrefab; // 콤보 스킬 항목 프리팹
        [Tooltip("콤보 스킬 항목들이 배치될 컨테이너. 비워두면 comboSkillPanel을 사용.")]
        [SerializeField] private RectTransform comboSkillItemContainer; // 콤보 스킬 항목 컨테이너

        [Header("콤보 스킬 이름")]
        [Tooltip("비우면 프리팹에 지정된 폰트를 유지한다.")]
        [SerializeField] private TMP_FontAsset comboSkillNameFont; // 콤보 이름 폰트
        [SerializeField] private float comboSkillNameFontSize = 24f; // 콤보 이름 크기
        [SerializeField] private Color comboSkillNameColor = Color.white; // 콤보 이름 색
        [SerializeField] private FontStyles comboSkillNameFontStyle = FontStyles.Bold; // 콤보 이름 스타일
        [SerializeField] private TextAlignmentOptions comboSkillNameAlignment = TextAlignmentOptions.MidlineLeft; // 콤보 이름 정렬

        [Header("콤보 스킬 쿨타임 숫자")]
        [SerializeField] private float comboSkillCooldownFontSize = 32f; // 쿨타임 숫자 크기
        [SerializeField] private Color comboSkillCooldownColor = new Color(0.808f, 0.682f, 0.404f, 1f); // #ceae67
        [SerializeField] private FontStyles comboSkillCooldownFontStyle = FontStyles.Bold; // 쿨타임 숫자 스타일

        [Header("정보 텍스트")]
        [SerializeField] private TMP_Text drawCountText; // 뽑을 더미 수 텍스트
        [SerializeField] private TMP_Text discardCountText; // 버린 더미 수 텍스트
        [FormerlySerializedAs("feverCountText")]
        [SerializeField] private TMP_Text awakenCountText; // 각성 카운트 텍스트

        [Header("버린 더미 카운트 강조 (숫자 변할 때 팝)")]
        [Tooltip("버린 더미 숫자가 바뀔 때 잠깐 커졌다 줄어드는 팝 연출.")]
        [SerializeField] private bool discardPopEnabled = true; // 버린 더미 팝 연출 사용 여부
        [Tooltip("팝 시 최대 확대 배율(원본 스케일 기준). 1.6 = 60% 더 커졌다 돌아옴.")]
        [SerializeField] private float discardPopScale = 1.6f; // 팝 최대 확대 배율
        [Tooltip("커졌다 원래대로 돌아오는 전체 시간(초).")]
        [SerializeField] private float discardPopDuration = 0.35f; // 팝 전체 시간

        [Header("리셔플 카운트업 (묘지→덱 복귀 시 덱 숫자 1,2,3 떨어짐)")]
        [Tooltip("버린 더미가 덱으로 돌아갈 때 덱 숫자가 1씩 위에서 떨어지며 올라가는 연출.")]
        [SerializeField] private bool reshuffleCountUpEnabled = true; // 리셔플 카운트업 사용 여부
        [Tooltip("숫자 하나가 위에서 제자리로 떨어지는 시간(초).")]
        [SerializeField] private float reshuffleStepDuration = 0.12f; // 한 단계 낙하 시간
        [Tooltip("숫자와 숫자 사이 간격(초). 0이면 끊김 없이 연속.")]
        [SerializeField] private float reshuffleStepGap = 0.04f; // 단계 간 간격
        [Tooltip("숫자가 떨어지기 시작하는 높이(px, 제자리 기준 위쪽).")]
        [SerializeField] private float reshuffleDropHeight = 36f; // 낙하 시작 높이
        [Tooltip("착지 시 살짝 커지는 팝 배율(1=없음).")]
        [SerializeField] private float reshuffleLandScale = 1.35f; // 착지 팝 배율
        [Tooltip("카운트업으로 표시할 최대 단계 수 — 너무 많으면 길어지므로 상한(이상은 마지막에 실제값으로 정착).")]
        [SerializeField] private int reshuffleMaxSteps = 12; // 카운트업 최대 단계

        [Header("각성 게이지 (HP바 아래)")]
        [Tooltip("게이지 슬라이더 — 비우면 SetAwakenGaugeAnchor 호출 또는 첫 갱신 시 자동 생성.")]
        [FormerlySerializedAs("feverGauge")]
        [SerializeField] private Slider awakenGauge; // 각성 게이지 슬라이더
        [Tooltip("게이지를 부착할 기준 RectTransform — 보통 Player.hpBar의 RectTransform. 비우면 NewBattleController가 런타임에 넣어줌.")]
        [FormerlySerializedAs("feverGaugeAnchor")]
        [SerializeField] private RectTransform awakenGaugeAnchor; // 게이지 부착 기준
        [Tooltip("게이지 자동 생성 시 크기.")]
        [FormerlySerializedAs("feverGaugeSize")]
        [SerializeField] private Vector2 awakenGaugeSize = new Vector2(480f, 56f); // 게이지 크기
        [Tooltip("anchor 기준 오프셋. y 음수 = 아래(HP바 아래).")]
        [FormerlySerializedAs("feverGaugeOffset")]
        [SerializeField] private Vector2 awakenGaugeOffset = new Vector2(0f, -58f); // anchor 기준 오프셋
        [Tooltip("anchor(Player.hpBar) 미배선 시 게이지를 표시할 위치 — 화면 상단 중앙 기준(y 음수=아래로). hpBar 배선되면 무시.")]
        [SerializeField] private Vector2 awakenGaugeFallbackPos = new Vector2(0f, -40f); // anchor 없을 때 위치
        [Tooltip("게이지 위에 표시할 라벨 (예: 6/10, 10.0s). 비우면 게이지 자동 생성 시 함께 생성.")]
        [FormerlySerializedAs("feverGaugeLabel")]
        [SerializeField] private TMP_Text awakenGaugeLabel; // 게이지 라벨
        [Tooltip("충전 진행 중 색.")]
        [FormerlySerializedAs("feverChargeColor")]
        [SerializeField] private Color awakenChargeColor = new Color(1f, 0.55f, 0.15f, 1f); // 충전 중 색
        [Tooltip("발동 중 색(카운트다운).")]
        [FormerlySerializedAs("feverActiveColor")]
        [SerializeField] private Color awakenActiveColor = new Color(0.75f, 0.35f, 1f, 1f); // 발동 중 색
        [Header("각성 게이지 스킨")]
        [SerializeField] private Sprite awakenIconSprite; // 예전 아이콘. 새 레이아웃에서는 쓰지 않음
        [SerializeField] private Sprite awakenFillSprite; // 찬 칸 (29x16)
        [SerializeField] private Sprite awakenEmptySprite; // 빈 칸 (29x16)
        [SerializeField] private Sprite awakenFrameSprite; // 예전 프레임. 새 레이아웃에서는 쓰지 않음
        [SerializeField] private Sprite awakenTimerSprite; // 각성 중 타이머 바 (274x16)

        [Header("각성 입력 히스토리 (왼쪽 표시)")]
        [Tooltip("OFF면 각성 입력 히스토리(왼쪽 세로 격자)를 표시하지 않는다. 콤보 슬롯/스킬 목록에는 영향 없음.")]
        [SerializeField] private bool awakenHistoryEnabled = true; // 입력 히스토리 표시 여부
        [Tooltip("각성 동안 입력된 속성 카드 전체 히스토리가 표시될 부모. 비우면 자동 생성.")]
        [FormerlySerializedAs("feverHistoryContainer")]
        [SerializeField] private RectTransform awakenHistoryContainer; // 입력 히스토리 부모
        [Tooltip("히스토리 한 칸 크기.")]
        [FormerlySerializedAs("feverHistoryItemSize")]
        [SerializeField] private Vector2 awakenHistoryItemSize = new Vector2(60f, 60f); // 히스토리 칸 크기
        [Tooltip("히스토리 칸 간 세로 간격.")]
        [FormerlySerializedAs("feverHistoryItemSpacing")]
        [SerializeField] private float awakenHistoryItemSpacing = 8f; // 히스토리 세로 간격
        [Tooltip("한 열에 표시할 최대 항목 수 — 이 이상이면 오른쪽 새 열로 이동.")]
        [FormerlySerializedAs("feverHistoryItemsPerColumn")]
        [SerializeField] private int awakenHistoryItemsPerColumn = 8; // 한 열 최대 항목 수
        [Tooltip("열과 열 사이 가로 간격.")]
        [FormerlySerializedAs("feverHistoryColumnSpacing")]
        [SerializeField] private float awakenHistoryColumnSpacing = 8f; // 열 간 가로 간격
        [Tooltip("히스토리 컨테이너 위치(앵커 기준). 좌측 가운데 추천.")]
        [FormerlySerializedAs("feverHistoryAnchoredPos")]
        [SerializeField] private Vector2 awakenHistoryAnchoredPos = new Vector2(80f, 0f); // 히스토리 컨테이너 위치
        [Tooltip("표시할 최대 항목 수 — 초과 시 가장 오래된 것부터 숨김.")]
        [FormerlySerializedAs("feverHistoryMaxItems")]
        [SerializeField] private int awakenHistoryMaxItems = 64; // 히스토리 최대 항목 수

        private readonly List<NewCardView> _cardViews = new List<NewCardView>(); // 슬롯별 카드 뷰 풀
        private readonly List<RectTransform> _slotAnchors = new List<RectTransform>(); // 슬롯 앵커 목록
        private readonly List<Vector2> _slotBasePos = new List<Vector2>(); // 스프레드 미적용 기준 위치
        private float[] _slotSpreadCurrent; // 슬롯별 현재 적용된 좌우 밀림(px)
        private float[] _slotSpreadTarget; // 슬롯별 목표 좌우 밀림(px)
        private int _hoverSpreadSlot = -1; // 현재 hover로 밀어내는 기준 슬롯(-1=없음)
        private readonly HashSet<CardInstance> _prevHandSet = new HashSet<CardInstance>(); // 직전 패 구성(신규 판별용)
        private CardDeckSystem _deck; // 바인딩된 덱 시스템
        public System.Func<CardInstance, bool> UseCardCallback; // 카드 사용 콜백

        private int _prevDiscardCount = int.MinValue; // 직전 표시한 버린 더미 수
        private Coroutine _discardPopCo; // 버린 더미 팝 코루틴
        private Vector3 _discardCountBaseScale = Vector3.one; // 버린 더미 텍스트 기준 스케일
        private bool _discardCountBaseCaptured; // 버린 더미 기준 캡처 여부

        private Coroutine _drawCountCo; // 리셔플 카운트업 코루틴
        private bool _drawCountAnimating; // 카운트업 진행 중 여부
        private Vector2 _drawCountHomePos; // 덱 수 텍스트 홈 위치
        private Vector3 _drawCountBaseScale = Vector3.one; // 덱 수 텍스트 기준 스케일
        private bool _drawCountBaseCaptured; // 덱 수 기준 캡처 여부
        public System.Action SelectionClosedCallback; // 선택/픽커 완료 직후 콜백

        private Image _awakenGaugeFill; // 각성 게이지 채움 이미지
        private readonly List<Image> _awakenSegments = new List<Image>(); // 각성 칸 이미지
        private RectTransform _awakenSegmentRoot; // 칸들이 들어갈 영역
        private Image _awakenTimerImage; // 각성 중 타이머 바
        private int _awakenSegmentCount; // 현재 생성된 칸 수
        private bool _awakenSkinReady; // 커스텀 스킨 적용 여부
        const string AwakenSpriteResourcePath = "UI/AwakenGauge/"; // Resources 스킨 경로
        // HP 프레임과 같은 왼쪽 위 기준. 표: (94+27n, 65), n = 0~9
        const float AwakenCellX = 94f;
        const float AwakenCellStep = 27f;
        const float AwakenCellY = 65f;
        const float AwakenCellW = 29f;
        const float AwakenCellH = 16f;
        const float AwakenTimerX = 93f;
        const float AwakenTimerY = 65f;
        const float AwakenTimerW = 274f;
        const float AwakenTimerH = 16f;

        private System.Action<CardInstance> _selectionCallback; // 선택 모드 콜백
        private System.Func<CardInstance, bool> _selectionFilter; // 선택 가능 카드 필터
        private CardInstance _selectionExcludeCard; // 선택 제외 카드
        private System.Action<IList<CardInstance>> _slotConfirmCallback; // 빈 칸 방식 확정 콜백(여러 장)
        public bool IsSelectionMode => _selectionCallback != null || _slotConfirmCallback != null; // 선택 모드 여부

        [Header("카드 선택 오버레이 (프리팹)")]
        [Tooltip("카드 선택 효과 공용 UI(부적을 선택하세요·띠·빈 칸·확인). 비우면 기존 딤+안내 텍스트 방식.")]
        [SerializeField] private CardSelectOverlay selectOverlayPrefab; // 선택 오버레이 프리팹
        private CardSelectOverlay _selectOverlay; // 생성된 오버레이 인스턴스
        private CardInstance[] _slotPicks;        // Slots 모드 칸별 선택 카드
        private NewCardView[] _slotPreviews;      // 칸에 표시 중인 카드 미리보기
        private readonly List<NewCardView> _slotHiddenHandViews = new List<NewCardView>(); // 칸에 넣어 숨긴 손패 뷰
        private bool _usingSlotSelection;         // 현재 선택 모드가 빈 칸 방식인지
        private bool _usingPilePicker;            // 현재 픽커가 더미 패널 방식인지
        private readonly List<Button> _activeSlotButtons = new List<Button>(); // 이번 선택의 빈 칸들

        [Header("선택 모드 UI")]
        [Tooltip("선택 모드 안내 텍스트. 비워두면 표시 안 함.")]
        [SerializeField] private TMP_Text selectionPromptText; // 선택 모드 안내 텍스트
        [Tooltip("선택 모드 시 화면을 덮는 어둡게 처리 오버레이. 비우면 자동 생성.")]
        [SerializeField] private RectTransform dimOverlay; // 화면 딤 오버레이
        [Tooltip("dim 오버레이 색(알파로 어둡기 조절) — 선택/픽커 모드.")]
        [SerializeField] private Color dimColor = new Color(0f, 0f, 0f, 0.6f); // 선택/픽커 딤 색
        [Tooltip("각성 딤의 가장자리 색. 알파는 가장자리가 얼마나 어두운지. 중심은 이 색이 걷혀 화면이 밝게 남는다.")]
        [FormerlySerializedAs("feverDimColor")]
        [SerializeField] private Color awakenDimColor = new Color(0.02f, 0.0f, 0.08f, 0.82f); // 각성 딤 가장자리 색
        [Tooltip("밝은 중심이 유지되는 반경. 0이면 중심부터 어두워지고, 1이면 가장 가까운 화면 가장자리까지 밝다.")]
        [SerializeField, Range(0f, 1f)] private float awakenDimBrightRadius = 0.18f; // 각성 딤 밝은 중심 반경
        [Tooltip("완전히 어두워지는 지점. 1이면 화면 가장자리에서 가장 어둡고, 작을수록 어두운 영역이 중심 쪽으로 넓어진다.")]
        [SerializeField, Range(0.05f, 1.5f)] private float awakenDimFadeEnd = 1f; // 각성 딤이 가장 어두워지는 거리

        private Material _awakenDimMaterial; // 각성 전용 방사형 딤 머티리얼
        private bool _awakenDimActive; // 각성 딤이 켜져 있는지

        [Header("더미/덱 보기 — 닫기")]
        [Tooltip("비워 두면 런타임에 화면 하단 중앙에 생성.")]
        [SerializeField] private Button viewerCloseButton;
        [SerializeField] private string viewerCloseButtonLabel = "\uB2EB\uAE30"; // 닫기
        [SerializeField] private Vector2 viewerCloseButtonSize = new Vector2(200f, 56f);
        [SerializeField] private Vector2 viewerCloseButtonAnchoredPos = new Vector2(0f, 52f);
        [SerializeField] private Sprite viewerCloseButtonSprite;
        [SerializeField] private Color viewerCloseButtonImageColor = new Color(0.12f, 0.12f, 0.16f, 0.96f);
        [SerializeField] private TMP_FontAsset viewerCloseButtonFont;
        [SerializeField] private float viewerCloseButtonFontSize = 24f;
        [SerializeField] private FontStyles viewerCloseButtonFontStyle = FontStyles.Bold;
        [SerializeField] private Color viewerCloseButtonFontColor = Color.white;

        // 손패 루트/드래그 레이어/슬롯 셋업 및 초기 상태 구성
        void Awake()
        {
            EnsureHandRoot();
            EnsureDragLayer();
            EnsureTargetArrow();
            BuildSlotAnchors();
            SetAwakenMode(false);
            SetupPileClickHandlers();
        }

        [Header("더미 보기 패널 (프리팹)")]
        [Tooltip("덱/묘지/런 덱 보기 패널. 비우면 기존 런타임 생성 방식(픽커 공용 UI)으로 폴백.")]
        [SerializeField] private PileViewerPanel pileViewerPanel;

        private bool _viewerMode; // 더미 보기 모드 여부
        private BattleTabPanel _tabPanel; // Tab 패널(더미 보기와 동시 표시 방지용)
        private int _viewerPile = -1; // 보는 더미(0=뽑을, 1=버린, -2=런 덱)
        public bool IsViewerMode => _viewerMode; // 더미 보기 모드 여부

        const int RunDeckViewerPile = -2; // menubar 덱 보기 식별자

        // 뽑을/버린 더미 카운트 텍스트에 클릭 핸들러 연결
        void SetupPileClickHandlers()
        {
            AttachPileButton(drawCountText, "Deck", () => TogglePileViewer(0));
            AttachPileButton(discardCountText, "Grave", () => TogglePileViewer(1));
        }

        // 덱 시스템을 바인딩하고 이벤트 구독을 갱신
        public void Bind(CardDeckSystem deck)
        {
            if (_deck != null)
            {
                _deck.OnPileChanged -= Refresh;
                _deck.OnReshuffled -= HandleReshuffled;
            }
            _deck = deck;
            if (_deck != null)
            {
                _deck.OnPileChanged += Refresh;
                _deck.OnReshuffled += HandleReshuffled;
            }
            Refresh();
        }

        // 덱 이벤트 구독을 해제
        void OnDestroy()
        {
            if (_deck != null)
            {
                _deck.OnPileChanged -= Refresh;
                _deck.OnReshuffled -= HandleReshuffled;
            }
            if (_awakenDimMaterial != null)
                Destroy(_awakenDimMaterial);
        }

        public RectTransform AwakenGaugeRect =>
            awakenGauge != null ? awakenGauge.transform as RectTransform : null;

        private RectTransform _awakenMarkerLayer; // 각성 표식 레이어(딤 바로 위, 다른 UI 아래)
        // 각성 표식을 생성할 레이어를 외부에 노출(딤 바로 위 정렬은 ShowAwakenDim에서 보장)
        public RectTransform AwakenMarkerLayer => _awakenMarkerLayer;

        const string AwakenDimShaderResourcePath = "UI/AwakenRadialDim"; // Resources 셰이더 경로

        private readonly List<Image> _awakenHistoryViews = new List<Image>(); // 입력 히스토리 뷰 풀

        const string ComboSlotFrameResourcePath = "UI/UI_ElementSlot_Frame"; // 프레임 스프라이트 경로
        const string ComboSlotBgResourcePath = "UI/UI_ElementSlot_BG"; // 프레임 안쪽 배경 경로
        const float ComboSlotIconInset = 0.033f; // 150px 프레임의 6px 테두리 안쪽에 맞닿는 여백

        Sprite _comboSlotFrameSpriteResolved; // 한 번 찾은 프레임 스프라이트
        bool _comboSlotFrameLookupFailed; // 프레임을 못 찾으면 경고는 한 번만
        Sprite _comboSlotBgSpriteResolved; // 한 번 찾은 안쪽 배경 스프라이트
        bool _comboSlotBgLookupFailed; // 배경을 못 찾으면 경고는 한 번만

        readonly HashSet<string> _missingComboSlotSprites = new HashSet<string>(); // 못 찾은 아이콘 경로

        private Coroutine[] _comboSlotPopCos;   // 콤보 슬롯 칸별 팝 코루틴
        private Vector3[] _comboSlotBaseScales; // 콤보 슬롯 칸별 기준 스케일

        private readonly List<SkillListItemUI> _comboSkillItems = new List<SkillListItemUI>(); // 콤보 스킬 항목 풀

        // 저주가 걸린 속성의 손패 카드를 한 번 떠오르게 한다
        public void NudgeHandCardsOfElement(CardElement element)
        {
            for (int i = 0; i < _cardViews.Count; i++)
            {
                var view = _cardViews[i];
                if (view == null || view.Card == null || !view.gameObject.activeInHierarchy) continue;
                if (view.Card.Element != element) continue;
                view.PlayNudge();
            }
        }

        // 속성에 대응하는 표시 색을 반환
        static Color ColorForElement(CardElement element) => element switch
        {
            CardElement.Fire    => new Color(0.95f, 0.55f, 0.40f, 1f),
            CardElement.Water   => new Color(0.50f, 0.75f, 0.95f, 1f),
            CardElement.Wind    => new Color(0.60f, 0.90f, 0.60f, 1f),
            CardElement.Earth   => new Color(0.85f, 0.70f, 0.45f, 1f),
            CardElement.Neutral => new Color(0.70f, 0.70f, 0.70f, 1f),
            _ => new Color(0.5f, 0.5f, 0.5f, 1f)
        };

        // 현재 손패에 맞춰 카드 뷰/슬롯/카운트 텍스트를 갱신
        public void Refresh()
        {
            if (_deck == null) return;

            var hand = _deck.Hand;
            // 손패 수에 맞춰 슬롯 위치 재계산
            PositionSlotAnchorsForCount(hand.Count);

            int newCardOrder = 0; // 이번에 새로 들어온 카드 순번(stagger 계산용)

            for (int i = 0; i < SLOT_COUNT; i++)
            {
                NewCardView view = i < _cardViews.Count ? _cardViews[i] : null;
                if (view == null && cardPrefab != null && i < _slotAnchors.Count)
                {
                    view = Instantiate(cardPrefab, _slotAnchors[i]);
                    view.Bind(this, i);
                    if (_talismanHand) view.SetTalismanMode(true); // 각성 중 새로 만든 슬롯도 부적
                    _cardViews.Add(view);
                }
                if (view == null) continue;

                // 등장 연출 중이 아니면 카드 뷰를 슬롯으로 복원
                if (i < _slotAnchors.Count && !view.IsPlayingDrawIntro)
                {
                    RectTransform vrect = (RectTransform)view.transform;
                    vrect.SetParent(_slotAnchors[i], false);
                    vrect.anchorMin = new Vector2(0.5f, 0.5f);
                    vrect.anchorMax = new Vector2(0.5f, 0.5f);
                    vrect.pivot = new Vector2(0.5f, 0.5f);
                    vrect.anchoredPosition = Vector2.zero;
                    vrect.localRotation = Quaternion.identity; // 슬롯의 fan 회전을 따름
                    view.ResetToHomeScale();
                }

                CardInstance card = i < hand.Count ? hand[i] : null;
                bool isNewToHand = card != null && !_prevHandSet.Contains(card);
                view.SetCard(card);

                // 연출 중이 아닐 때만 홈 좌표 재캡처
                if (!view.IsPlayingDrawIntro)
                    view.CaptureHome();

                // 새로 들어온 카드만 등장 연출 재생
                if (isNewToHand)
                    view.PlayDrawIntro(newCardOrder++ * Mathf.Max(0f, drawIntroStagger));
            }

            // 다음 Refresh의 신규 판별용으로 현재 패를 스냅샷
            _prevHandSet.Clear();
            for (int i = 0; i < hand.Count; i++)
                if (hand[i] != null) _prevHandSet.Add(hand[i]);

            // 카운트업 중이 아닐 때만 덱 수 텍스트 갱신
            if (drawCountText != null && !_drawCountAnimating) drawCountText.text = $"{_deck.DrawCount}";
            if (discardCountText != null)
            {
                int discardCount = _deck.DiscardCount;
                discardCountText.text = $"{discardCount}";
                // 값이 실제로 바뀐 경우에만 팝 강조
                if (_prevDiscardCount != int.MinValue && discardCount != _prevDiscardCount)
                    PlayDiscardPop();
                _prevDiscardCount = discardCount;
            }
        }

        // 버린 더미 숫자 변화 시 팝 강조를 시작
        void PlayDiscardPop()
        {
            if (!discardPopEnabled || discardCountText == null) return;

            // 기준 스케일은 최초 1회만 캡처
            if (!_discardCountBaseCaptured)
            {
                _discardCountBaseScale = discardCountText.transform.localScale;
                _discardCountBaseCaptured = true;
            }

            if (_discardPopCo != null) StopCoroutine(_discardPopCo);
            _discardPopCo = StartCoroutine(DiscardPopRoutine());
        }

        // 버린 더미 텍스트를 커졌다 줄이는 팝 코루틴
        System.Collections.IEnumerator DiscardPopRoutine()
        {
            if (discardCountText == null) { _discardPopCo = null; yield break; }

            Transform tr = discardCountText.transform;
            Vector3 peak = _discardCountBaseScale * Mathf.Max(1f, discardPopScale);
            float dur = Mathf.Max(0.01f, discardPopDuration);
            const float upPortion = 0.35f; // 앞 35%는 확대, 나머지는 복귀 구간

            float t = 0f;
            while (t < dur)
            {
                if (discardCountText == null) { _discardPopCo = null; yield break; }
                t += Time.deltaTime;
                float n = Mathf.Clamp01(t / dur);

                // 0→peak(ease-out) 후 peak→base(선형) 엔벨로프
                float env = n < upPortion
                    ? 1f - (1f - n / upPortion) * (1f - n / upPortion)
                    : 1f - (n - upPortion) / (1f - upPortion);
                env = Mathf.Clamp01(env);

                tr.localScale = Vector3.Lerp(_discardCountBaseScale, peak, env);
                yield return null;
            }

            tr.localScale = _discardCountBaseScale;
            _discardPopCo = null;
        }

        // 리셔플 시 카운트업 연출을 시작
        void HandleReshuffled(int count)
        {
            if (!reshuffleCountUpEnabled || drawCountText == null || count <= 0) return;
            if (_drawCountCo != null) StopCoroutine(_drawCountCo);
            _drawCountCo = StartCoroutine(DrawCountUpRoutine(count));
        }

        // 덱 수를 1씩 위에서 떨어뜨리며 올리는 카운트업 코루틴
        System.Collections.IEnumerator DrawCountUpRoutine(int count)
        {
            var rt = drawCountText.transform as RectTransform;
            if (rt == null) { _drawCountCo = null; yield break; }

            // 홈 위치/스케일은 최초 1회만 캡처
            if (!_drawCountBaseCaptured)
            {
                _drawCountHomePos = rt.anchoredPosition;
                _drawCountBaseScale = rt.localScale;
                _drawCountBaseCaptured = true;
            }

            _drawCountAnimating = true; // 진행 중 Refresh의 덮어쓰기 차단

            int steps = Mathf.Min(count, Mathf.Max(1, reshuffleMaxSteps));
            Vector3 landPeak = _drawCountBaseScale * Mathf.Max(1f, reshuffleLandScale);
            float stepDur = Mathf.Max(0.01f, reshuffleStepDuration);
            Vector2 dropStart = _drawCountHomePos + Vector2.up * reshuffleDropHeight;

            for (int v = 1; v <= steps; v++)
            {
                if (drawCountText == null) break;
                drawCountText.text = v.ToString();

                float t = 0f;
                while (t < stepDur)
                {
                    if (drawCountText == null) break;
                    t += Time.deltaTime;
                    float n = Mathf.Clamp01(t / stepDur);
                    float ease = 1f - (1f - n) * (1f - n); // ease-out 낙하
                    rt.anchoredPosition = Vector2.Lerp(dropStart, _drawCountHomePos, ease);

                    // 착지 직전 마지막 25%에 삼각 엔벨로프로 팝
                    float popEnv = 0f;
                    if (n >= 0.75f) { float m = (n - 0.75f) / 0.25f; popEnv = 1f - Mathf.Abs(2f * m - 1f); }
                    rt.localScale = Vector3.Lerp(_drawCountBaseScale, landPeak, Mathf.Clamp01(popEnv));
                    yield return null;
                }

                rt.anchoredPosition = _drawCountHomePos;
                rt.localScale = _drawCountBaseScale;

                if (reshuffleStepGap > 0f) yield return new WaitForSeconds(reshuffleStepGap);
            }

            // 연출 종료 후 실제 현재 덱 수로 정착
            if (drawCountText != null) drawCountText.text = $"{(_deck != null ? _deck.DrawCount : steps)}";
            rt.anchoredPosition = _drawCountHomePos;
            rt.localScale = _drawCountBaseScale;
            _drawCountAnimating = false;
            _drawCountCo = null;
        }

        public EnemyController PendingEnemyTarget { get; private set; }

        TargetArrowView _targetArrow; // 지정 화살표 뷰

        void EnsureTargetArrow()
        {
            if (_targetArrow != null) return;
            EnsureDragLayer();
            _targetArrow = TargetArrowView.EnsureOn(dragLayer);
            if (_targetArrow != null)
                _targetArrow.SetCustomSprites(targetingArrowSprite, targetingBracketSprite);
        }

        [Header("카드 사용 연출 — 중앙 표시")]
        [Tooltip("카드 사용 시 화면 중앙에 카드를 잠깐 띄웠다 사라지게 하는 연출 ON/OFF.")]
        [SerializeField] private bool cardUsePresentEnabled = true; // 중앙 표시 연출 사용 여부
        [Tooltip("각성 중 카드 사용 시 중앙 표시 연출을 켤지 여부. OFF면 각성 중에는 중앙 카드 연출을 생략(효과음은 유지). 일반 사용에는 영향 없음.")]
        [SerializeField] private bool awakenCardUsePresentEnabled = true; // 각성 중 중앙 표시 연출 사용 여부
        [Tooltip("중앙 표시 위치(캔버스 중앙 기준 오프셋). y 양수 = 중앙보다 위.")]
        [SerializeField] private Vector2 cardUsePresentPos = new Vector2(0f, 60f); // 중앙 표시 위치
        [Tooltip("중앙 표시 시 카드 크기 배율(프리팹 기본 스케일 기준).")]
        [SerializeField] private float cardUsePresentScale = 1.5f; // 중앙 표시 크기 배율
        [Tooltip("등장(커지며 나타남) 시간(초).")]
        [SerializeField] private float cardUsePresentEnterTime = 0.15f; // 등장 시간
        [Tooltip("중앙에서 머무는 시간(초).")]
        [SerializeField] private float cardUsePresentHold = 0.35f; // 유지 시간
        [Tooltip("퇴장(사라짐) 시간(초). 이 직전에 효과가 발동된다.")]
        [SerializeField] private float cardUsePresentExitTime = 0.2f; // 퇴장 시간

        private Coroutine _interruptablePresentCo; // 교체 가능(각성용) 연출 코루틴
        private NewCardView _interruptablePresentView; // 교체 가능 연출 카드 뷰
        private Coroutine _normalPresentCo; // 일반(차단) 연출 코루틴
        private NewCardView _normalPresentView; // 일반 연출 카드 뷰

        // 사용한 카드를 중앙에 띄웠다 사라뜨리고 사라질 때 onDisappear 호출
        public void PlayCardUsePresentation(CardInstance card, System.Action onDisappear, bool interruptable = false)
        {
            if (card != null) SfxManager.Instance?.PlayCardUse();
            // interruptable == true 는 각성 중 카드 사용 경로 — 전용 토글로 중앙 연출만 생략(효과음은 위에서 이미 재생)
            bool presentEnabled = cardUsePresentEnabled && (!interruptable || awakenCardUsePresentEnabled);
            if (!presentEnabled || card == null || cardPrefab == null || !isActiveAndEnabled)
            {
                onDisappear?.Invoke();
                return;
            }

            if (interruptable)
            {
                // 직전 각성용 연출을 정리하고 새 카드로 교체
                if (_interruptablePresentCo != null) StopCoroutine(_interruptablePresentCo);
                if (_interruptablePresentView != null) Destroy(_interruptablePresentView.gameObject);
                _interruptablePresentView = null;
                _interruptablePresentCo = StartCoroutine(CardUsePresentationRoutine(card, onDisappear, true));
            }
            else
            {
                _normalPresentCo = StartCoroutine(CardUsePresentationRoutine(card, onDisappear, false));
            }
        }

        // 카드 사용 중앙 연출(등장~퇴장)이 아직 진행 중인지 — 적 행동은 이게 끝난 뒤 시작한다
        public bool IsCardUsePresenting => _normalPresentCo != null || _interruptablePresentCo != null;

        private RectTransform _pickerRoot;       // 픽커 중앙 컨테이너(ScrollRect)
        private RectTransform _pickerViewport;   // 픽커 마스크 영역
        private RectTransform _pickerContent;    // 픽커 카드들이 들어가는 가변 높이 컨테이너
        private readonly List<NewCardView> _pickerViews = new List<NewCardView>(); // 픽커 카드 뷰 목록
        private System.Action<CardInstance> _pickerCallback; // 픽커 선택 콜백
        private Button _viewerCloseButtonRuntime; // 더미/덱 보기 닫기 버튼(런타임 또는 Inspector)
        private Image _viewerCloseButtonImage;
        private TextMeshProUGUI _viewerCloseButtonLabelText;
        public bool IsPickerMode => _pickerCallback != null; // 픽커 모드 여부

        [Header("카드 픽커 — 그리드/스크롤")]
        [Tooltip("픽커 한 행에 표시할 최대 카드 수.")]
        [SerializeField] private int pickerColumns = 4; // 한 행 최대 카드 수
        [Tooltip("픽커 카드 가로 간격(중심→중심, 픽셀). 카드 폭 300 기준 360~520 권장.")]
        [SerializeField] private float pickerColumnSpacing = 480f; // 픽커 가로 간격
        [Tooltip("픽커 카드 세로 간격(중심→중심, 픽셀). 카드 높이 400 기준 480~640 권장.")]
        [SerializeField] private float pickerRowSpacing = 620f; // 픽커 세로 간격
        [Tooltip("픽커 viewport 최소 크기. BuildPickerCards가 카드 폭·열 수에 맞춰 가로를 자동 확장한다.")]
        [SerializeField] private Vector2 pickerViewportSize = new Vector2(1800f, 900f); // 픽커 뷰포트 크기
        [Tooltip("x=좌우, y=상하 패딩 — 양쪽 열·첫/마지막 행이 viewport 가장자리에 붙지 않도록.")]
        [SerializeField] private Vector2 pickerContentPadding = new Vector2(80f, 240f); // 픽커 컨텐츠 패딩
        [Tooltip("마우스 휠 스크롤 감도.")]
        [SerializeField] private float pickerScrollSensitivity = 60f; // 스크롤 감도

        void OnValidate()
        {
            if (viewerCloseButton != null)
            {
                _viewerCloseButtonRuntime = viewerCloseButton;
                _viewerCloseButtonImage = null;
                _viewerCloseButtonLabelText = null;
                CacheViewerCloseButtonParts();
                ApplyViewerCloseButtonStyle();
                return;
            }

            if (Application.isPlaying && _viewerCloseButtonRuntime != null)
                ApplyViewerCloseButtonStyle();
        }

        public void OnCardClicked(NewCardView view)
        {
            // 더미 보기 중 카드 클릭 — 프리팹 패널은 인벤토리처럼 X 버튼으로만 닫음, 폴백 UI는 클릭으로 닫기
            if (_viewerMode)
            {
                if (pileViewerPanel == null) CloseViewer();
                return;
            }

            // 픽커 모드 우선 처리
            if (TryHandlePickerClick(view)) return;

            if (!IsSelectionMode) return;
            if (view == null || view.Card == null) return;
            if (_selectionExcludeCard != null && view.Card == _selectionExcludeCard) return;
            if (_selectionFilter != null && !_selectionFilter(view.Card)) return;

            // 빈 칸 방식 — 손패 카드를 칸에 넣기만 하고 확인 버튼으로 확정
            if (_usingSlotSelection)
            {
                SlotHandCard(view);
                return;
            }

            var cb = _selectionCallback;
            CardInstance selected = view.Card;
            ExitSelectionMode();
            cb?.Invoke(selected);
            SelectionClosedCallback?.Invoke(); // 선택 후 보류 각성 발동 트리거
        }

        // UI 좌표계를 보장할 대상 캔버스를 우선순위대로 탐색
        Canvas ResolveTargetCanvas()

        {
            // 1) 자신의 부모 캔버스
            Canvas canvas = GetComponentInParent<Canvas>();
            // 2) handRoot의 부모 캔버스
            if (canvas == null && handRoot != null) canvas = handRoot.GetComponentInParent<Canvas>();
            // 3) dragLayer의 부모 캔버스
            if (canvas == null && dragLayer != null) canvas = dragLayer.GetComponentInParent<Canvas>();
            // 4) 씬 전체 검색
            if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
            return canvas;
        }

        public bool IsHandBlocked => TabPanelOpen;

        bool TabPanelOpen
        {
            get
            {
                if (_tabPanel == null) _tabPanel = FindFirstObjectByType<BattleTabPanel>(FindObjectsInactive.Include);
                return _tabPanel != null && _tabPanel.IsVisible;
            }
        }

        // 밀림 값을 목표로 부드럽게 보간하고 슬롯 위치에 반영
        void Update()
        {
            ApplyTabHandBlock();
            if (_awakenDimActive)
                ApplyAwakenDimGradient();

            if (!hoverSpreadEnabled || _slotSpreadCurrent == null) return;

            bool changed = false;
            float k = hoverSpreadLerpSpeed <= 0f
                ? 1f
                : 1f - Mathf.Exp(-hoverSpreadLerpSpeed * Time.unscaledDeltaTime);
            for (int i = 0; i < _slotSpreadCurrent.Length; i++)
            {
                float target = i < _slotSpreadTarget.Length ? _slotSpreadTarget[i] : 0f;
                float cur = _slotSpreadCurrent[i];
                if (Mathf.Abs(cur - target) < 0.05f)
                {
                    if (cur != target) { _slotSpreadCurrent[i] = target; changed = true; }
                    continue;
                }
                _slotSpreadCurrent[i] = Mathf.Lerp(cur, target, k);
                changed = true;
            }
            if (changed) ApplyAllSlotSpread();
        }

    }
}
