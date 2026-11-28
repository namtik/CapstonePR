using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using Battle.Card;
using Battle.Deck;
using Battle.Relic;
using Battle.UI;
using Sashindo.VFX;

namespace Battle
{
    // 새 전투 시스템 진입점 (5장 손패·드래그 사용·각성·콤보·적 AI 방해)
    public partial class NewBattleController : MonoBehaviour, Battle.Relic.IRelicBattleContext
    {
        public const int AWAKEN_ACTIVATION_INPUT = 10;                  // 각성 발동에 필요한 속성 카드 사용 횟수
        public const float AWAKEN_DURATION_SECONDS = 7f;               // 각성 기본 지속 시간(초)
        public const float COMBO_BONUS_SECONDS = 0.1f;                 // 콤보 1회 성공 시 추가 시간(초)
        public const int COMBO_REUSE_CARD_USES = 5;                    // 콤보 재사용까지 필요한 각성 중 카드 사용 횟수
        public const int START_DRAW = 5;                              // 전투 시작 시 드로우 수

        public static NewBattleController Instance { get; private set; } // 싱글톤 인스턴스

        [Header("UI")]
        [SerializeField] private CardHandHUD handHud;                 // 손패 HUD
        [Tooltip("카드 사용 시 EffectName(예: Fire_ATK)에 해당하는 스프라이트 시트 애니메이션을 재생. " +
                 "비우면 자동으로 캔버스에 생성. 시트가 없으면 조용히 스킵.")]
        [SerializeField] private CardEffectOverlay effectOverlay;     // 카드 효과 이펙트 오버레이
        [Tooltip("플레이어 피격 시 화면 빨간 플래시. 비우면 자동 생성.")]
        [SerializeField] private PlayerDamageOverlay damageOverlay;   // 플레이어 피격 화면 오버레이
        [Tooltip("전투 중 Tab으로 띄우는 요괴 정보 패널. CombatStage 캔버스에 배치. 비우면 씬에서 찾는다.")]
        [SerializeField] private BattleTabPanel tabPanel;             // 요괴 정보 탭 패널
        [Tooltip("각성 동안 화면 상단에 남은 시간을 표시할 전용 게이지. 씬에 직접 만들어 연결(코드로 생성하지 않음). 비우면 표시 생략.")]
        [SerializeField] private AwakenTimerGauge awakenTimerGauge;   // 상단 각성 타이머 게이지

        [Header("보유 콤보 스킬 (각성 전용 / Inspector 편집)")]
        [SerializeField] private List<ComboSkillDef> ownedComboSkills = new List<ComboSkillDef>(); // 보유 콤보 스킬 목록
        public IReadOnlyList<ComboSkillDef> OwnedComboSkills => ownedComboSkills; // 보유 콤보 읽기 전용 노출

        [Header("콤보 스킬 DB")]
        [Tooltip("ON이면 Resources/ComboDB의 데이터 드리븐 콤보를 사용 (Inspector의 ownedComboSkills를 덮어씀). " +
                 "OFF면 위 Inspector 수동 리스트 사용.")]
        [SerializeField] private bool useComboDatabase = true;        // 콤보 DB 사용 여부
        [Tooltip("보유할 콤보 ID(효과별 커맨드 1개). 비우면 보유 콤보 없음(미선택 시작). 아래 커스텀 인스펙터에서 효과->커맨드로 편집.")]
        [SerializeField] private List<int> ownedComboIds = new List<int>(); // 보유 콤보 ID(행=효과+커맨드) 목록
        [Tooltip("[테스트] 콤보 보상 UI 전까지 수동 획득용 — 이 RefComboID(1000~1019)를 컨텍스트 메뉴 '콤보 1개 추가'로 보유에 더함.")]
        [SerializeField] private int debugAddComboRefId = 1000;       // 디버그 수동 콤보 추가용 RefComboID

        // 깨달음으로 바꾼 커맨드 순서 — 전투 시작 시 DB 기본값을 덮어쓴다
        struct ComboSlotOverride
        {
            public int refComboId;
            public CardElement slot1;
            public CardElement slot2;
            public CardElement slot3;
        }

        readonly List<ComboSlotOverride> _comboSlotOverrides = new List<ComboSlotOverride>();

        [Header("각성 콤보 이펙트")]
        [Tooltip("각성 종료 시 Damage 콤보가 적에게 들어갈 때 재생할 이펙트 이름. " +
                 "Resources/CardEffects/{이름}.png 시트를 찾아 적 위치에서 재생. " +
                 "비우거나 시트가 없으면 조용히 스킵.")]
        [FormerlySerializedAs("feverDamageEffectName")]
        [SerializeField] private string awakenDamageEffectName = "Fever_ATK"; // 각성 데미지 콤보 이펙트 이름
        [Tooltip("Damage 콤보가 N개 동시에 발동될 때 이펙트가 한 곳에 겹쳐 보이지 않도록 좌우/상하로 분산.\n" +
                 "X = 콤보 간 가로 간격, Y = zigzag 세로 진폭. 0으로 두면 분산 없이 한 곳.")]
        [FormerlySerializedAs("feverDamageEffectSpread")]
        [SerializeField] private Vector2 awakenDamageEffectSpread = new Vector2(220f, 80f); // 데미지 이펙트 분산 간격
        [Tooltip("각성 종료 시 데미지 콤보 1건당 더해질 셰이크 강도 배율. 콤보가 많을수록 더 세게 흔들림.\n" +
                 "0이면 셰이크 비활성.")]
        [FormerlySerializedAs("feverShakeIntensityPerHit")]
        [SerializeField] private float awakenShakeIntensityPerHit = 0.6f; // 콤보 1건당 셰이크 강도 배율
        [Tooltip("각성 셰이크 강도 상한 — 콤보가 너무 많아도 이 값을 넘지 않음.")]
        [FormerlySerializedAs("feverShakeIntensityMax")]
        [SerializeField] private float awakenShakeIntensityMax = 2.0f; // 셰이크 강도 상한

        [Header("각성 콤보 표식 순차 폭발")]
        [Tooltip("ON이면 각성 중 콤보 성공마다 몬스터 주위에 표식을 띄우고, 종료 시 표식을 하나씩 차례로 터뜨린다. " +
                 "OFF면 기존처럼 종료 시 한 번에 일괄 발동.")]
        [SerializeField] private bool awakenMarkerSequence = true;     // 표식 순차 폭발 사용 여부
        [Tooltip("표식을 몬스터 중심에서 배치할 기본 반경(px). 범위를 키우려면 이 값을 올려라.")]
        [SerializeField] private float awakenMarkerRadius = 230f;      // 표식 배치 반경
        [Tooltip("표식이 많아질수록 반경을 키우는 증가량(px/개). 0이면 고정 반경.")]
        [SerializeField] private float awakenMarkerRadiusGrowth = 14f; // 표식 반경 증가량
        [Tooltip("표식이 퍼질 최대 반경(px) — 이 값으로 범위 상한 제한. 0 이하면 무제한.")]
        [SerializeField] private float awakenMarkerMaxRadius = 0f;     // 표식 최대 반경(범위 상한)
        [Tooltip("표식 무리의 중심을 몬스터 기준에서 추가로 이동(px). 표식 뜨는 위치 미세조정.")]
        [SerializeField] private Vector2 awakenMarkerCenterOffset = Vector2.zero; // 표식 중심 오프셋
        [Tooltip("표식 배치 세로 눌림 비율 — 1=정원, <1이면 가로로 넓은 타원(화면에 자연스럽게 둘러싸이는 느낌).")]
        [Range(0.3f, 1f)]
        [SerializeField] private float awakenMarkerVerticalSquash = 0.72f; // 표식 세로 눌림
        [Tooltip("사방비급 성립 시 몬스터 주변에 맺히는 금빛 파티클의 크기.")]
        [SerializeField, Min(.1f)] private float awakenComboParticleScale = 1.32f;
        [Tooltip("각성 종료 후 표식이 하나씩 터지는 간격(초).")]
        [SerializeField] private float awakenExplosionInterval = 0.18f; // 순차 폭발 간격

        [Header("상태이상 연출")]
        [Tooltip("적에게 빙결(frost)이 부여될 때마다 적 위치에서 재생할 이펙트 이름. " +
                 "Resources/CardEffects/{이름} 프리팹/시트를 찾음. 비우거나 에셋이 없으면 조용히 스킵.")]
        [SerializeField] private string frostAppliedEffectName = "Frost_EFF"; // 빙결 부여 이펙트 이름

        [Header("각성 화면 효과")]
        [Tooltip("각성 동안 3D 스테이지 카메라 앞에 유지될 프리팹(Hovl Screen wind 등). UI가 아니라 카메라에 붙여 시야만 덮는다. 비우면 화면 효과 없음.")]
        [SerializeField] private GameObject awakenScreenEffectPrefab; // 각성 화면 효과 프리팹
        [Tooltip("각성 진입 연출. 비워 두면 씬의 연출 또는 패키지 기본 프리팹을 사용한다.")]
        [SerializeField] private AwakeningVfx awakenEntryVfx;
        private bool ownsAwakenEntryVfx;

        [Header("턴 순서 (스타레일식 행동 순서 바)")]
        [Tooltip("좌측 행동 순서 바. 비우면 전투 시작 시 전투 캔버스에 자동 생성.")]
        [SerializeField] private TurnOrderTimeline turnOrderTimeline; // 행동 순서 바
        [Tooltip("ON이면 적 행동(공격 대기열 처리·방해행동 연출) 중에는 카드 사용/D 드로우를 막는다. 각성 중에는 잠그지 않음.")]
        [SerializeField] private bool lockInputDuringEnemyTurn = true; // 적 턴 입력 잠금 여부
        [Tooltip("적 턴 진입 후 실제 공격 시작까지 대기(초) — 행동 순서 바에서 행동자가 맨 위로 올라오는 걸 보여줄 시간.")]
        [SerializeField] private float enemyTurnStartDelay = 0.35f;    // 적 공격 전 대기
        [Tooltip("방해행동 발동 시 입력을 잠그는 시간(초).")]
        [SerializeField] private float disruptionLockSeconds = 0.6f;  // 방해행동 연출 잠금 시간

        [Header("디버그")]
        [Tooltip("켜 두면 각성이 한 번 발동된 뒤 시간이 줄지 않고 유지된다. 전투가 끝나면 같이 끝난다.")]
        [SerializeField] private bool holdAwakenForever;             // 각성 무한 유지
        [Tooltip("각성 진입 시 손패가 부적으로 바뀌는 연출 동안 각성 시간을 멈춘다(점진적 각성 발동 연출이 생기기 전 임시 여유 시간).")]
        [SerializeField] private bool pauseAwakenDuringTalismanBurn = true; // 부적 전환 중 각성 시간 정지

        [Header("적 행동 카메라 포커스")]
        [Tooltip("적이 공격/방해행동을 하는 동안 스테이지 카메라를 그 적 쪽으로 당기고 줌인해 누가 행동하는지 강조한다.")]
        [SerializeField] private EnemyActionFocusCamera.Settings enemyActionFocus = EnemyActionFocusCamera.Settings.Default;
        [Tooltip("적 행동 포커스(카메라 줌인) 동안 손패·덱·묘지·몬스터 체력 바를 잠시 숨김.")]
        [SerializeField] private EnemyActionHudFader.Settings enemyActionHudHide = EnemyActionHudFader.Settings.Default;
        private readonly List<GameObject> _hudHideTargets = new List<GameObject>(); // 숨김 대상 재사용 버퍼
        [SerializeField] private bool logVerbose = true;             // 상세 로그 출력 여부

        [Header("적 AI (방해행동 선택)")]
        [Tooltip("ON이면 FCM-RBFN 학습 모델로 방해행동을 선택. OFF면 랜덤.")]
        [SerializeField] private bool useAiDisruption = true;        // AI 방해행동 선택 사용 여부
        [Tooltip("탐험 비율 — 데이터 수집 시 행동 다양성 확보용(0=항상 최적, 0.1~0.2=수집용). 학습 데이터가 충분하면 0으로.")]
        [Range(0f, 1f)]
        [SerializeField] private float aiExplorationEpsilon = 0.15f; // AI 탐험 비율(epsilon)
        [Tooltip("ON이면 각 방해행동 결정을 CSV로 기록(persistentDataPath/ai_logs) -> 오프라인 재학습용.")]
        [SerializeField] private bool logAiDecisions = false;        // 방해행동 결정 CSV 기록 여부
        [Tooltip("ON이면 게임 중 실제 결과로 적 AI Q-가중치를 미세조정(세션 단위 누적, 오프라인 베이스에서 시작).")]
        [SerializeField] private bool onlineLearning = false;        // 온라인 학습 사용 여부
        [Tooltip("온라인 학습 TD 할인율 γ. 0=즉시 효과보상만(기존 동작·롤백), 0.6 권장. '스텝'은 시간이 아니라 방해행동 결정 횟수 — 행동 간 연계 가치를 학습. onlineLearning ON일 때만 적용.")]
        [Range(0f, 0.95f)]
        [SerializeField] private float aiRewardGamma = 0.6f;        // 온라인 학습 TD 할인율 γ

        private readonly CardDeckSystem _deck = new CardDeckSystem();     // 덱/패/더미 관리 시스템
        private readonly CardEffectResolver _resolver = new CardEffectResolver(); // 카드 효과 해석기
        private readonly CardEffectContext _ctx = new CardEffectContext();        // 카드 효과 실행 컨텍스트
        private readonly ComboEffectResolver _comboResolver = new ComboEffectResolver(); // 콤보 효과 해석기
        private readonly ComboResolveContext _comboCtx = new ComboResolveContext();      // 콤보 효과 실행 컨텍스트

        private Player _player;                  // 플레이어 참조
        private EnemyController _enemy;          // 현재 효과 대상 적
        private EnemyStat _enemyStat;            // 현재 효과 대상 적 스탯
        private readonly List<EnemyController> _boundEnemies = new List<EnemyController>();
        private readonly List<EnemyController> _aliveBuf = new List<EnemyController>();
        private readonly Dictionary<EnemyController, int> _frostAtCardStart = new Dictionary<EnemyController, int>();
        private readonly List<EnemyController> _frostHoldEnemies = new List<EnemyController>();
        private readonly List<int> _frostHoldAdded = new List<int>();
        int _frostFxSfxFrame = -1; // 같은 프레임의 빙결 연출은 효과음을 한 번만
        // 적 행동(공격/방해행동) 대기열 — 한 번에 하나씩, 이전 행동과 카드 연출이 완전히 끝난 뒤 진행
        private struct EnemyAction
        {
            public EnemyController enemy;
            public bool disruption; // true=방해행동, false=공격
        }
        private readonly Queue<EnemyAction> _attackQueue = new Queue<EnemyAction>();
        private bool _attackResolving;
        // 적 행동 도중 플레이어가 잃은 체력 — 체력 손실 유물(피 묻은 가시 등)은 행동이 완전히 끝난 뒤 발동
        private readonly List<(int amount, bool first)> _deferredHpLost = new List<(int amount, bool first)>();
        private EnemyController _actingEnemy;      // 지금 행동 중인 적(행동 순서 바 '현재' 칸)
        private bool _actingIsDisruption;          // 현재 행동이 방해행동인지(아니면 공격)
        private float _enemyActionLockUntil;       // 방해행동 연출 잠금 종료 시각
        private Coroutine _attackStartRoutine;     // 적 공격 시작 대기 코루틴

        // 적 턴 여부 — 공격 대기열 처리 중이거나 방해행동 연출 중
        public bool IsEnemyTurn =>
            _inBattle && (_attackResolving || _attackQueue.Count > 0 || Time.time < _enemyActionLockUntil);
        public EnemyController ActingEnemy => IsEnemyTurn ? _actingEnemy : null; // 현재 행동 중인 적
        public bool ActingIsDisruption => _actingIsDisruption;                   // 현재 행동 종류
        public IEnumerable<EnemyController> PendingAttackers                    // 공격 대기 중인 적(순서대로)
        {
            get
            {
                foreach (var a in _attackQueue)
                    if (!a.disruption) yield return a.enemy;
            }
        }
        bool InputLockedByEnemyTurn => lockInputDuringEnemyTurn && !_awakenActive && IsEnemyTurn;

        private bool _inBattle;                  // 전투 진행 중 여부
        private bool _cardPresenting;            // 일반 카드 사용 중앙 연출 진행 중 여부(재진입 방지)

        private int _elementInputCount;          // 각성 발동용 누적 속성 입력 수
        private bool _awakenActive;              // 각성 활성 여부
        private bool _awakenPending;             // 각성 발동 보류 상태(선택 완료 후 발동)
        private float _awakenTimeRemaining;      // 각성 남은 시간(초)
        private float _awakenMaxTime;            // 각성 동적 최대 시간(게이지 fill 계산용)
        private List<CardInstance> _awakenStoredNeutralCards = new List<CardInstance>(); // 각성 중 격리한 무속성 카드
        private GameObject _awakenScreenEffectInstance; // 각성 동안 유지되는 화면 효과 핸들

        private List<CardInstance> _awakenSnapshotHand;    // 각성 직전 패 스냅샷(복원용)
        private List<CardInstance> _awakenSnapshotDraw;    // 각성 직전 뽑을 더미 스냅샷(복원용)
        private List<CardInstance> _awakenSnapshotDiscard; // 각성 직전 버린 더미 스냅샷(복원용)

        private readonly List<CardElement> _comboInput = new List<CardElement>(); // 콤보 슬롯(슬라이딩 윈도우 3장)
        private int[] _comboCooldown = System.Array.Empty<int>();     // 콤보별 남은 재사용 대기(카드 사용 횟수)
        private readonly List<ComboSkillDef> _queuedComboSkills = new List<ComboSkillDef>(); // 각성 종료 시 발동할 콤보 큐
        private readonly List<CardElement> _awakenInputHistory = new List<CardElement>(); // 각성 중 입력 속성 전체 기록
        private readonly List<Vector2> _awakenMarkerPositions = new List<Vector2>(); // 콤보 큐와 1:1 표식 위치
        private readonly List<GameObject> _awakenMarkerHandles = new List<GameObject>(); // 콤보 큐와 1:1 표식 핸들
        private readonly List<GameObject> _awakenDetonatingParticles = new List<GameObject>();
        private Coroutine _awakenExplosionRoutine; // 진행 중인 표식 순차 폭발 코루틴

        private int _usedFireCardCount;       // 이번 전투 불 카드 사용 수
        private int _awakenFragmentExhausted; // 각성 진입 시 격리된 파편 수
        private int _awakenComboCountThisEnd; // 이번 각성 종료 시 발동되는 콤보 총수
        private readonly Dictionary<int, int> _comboSelfUseCount = new Dictionary<int, int>(); // refComboId별 이번 각성 발동 횟수

        public const int CURSE_DURATION_USES = 2;   // 저주 지속 카드 사용 횟수
        public const int CURSE_PLAYER_DAMAGE = 6;   // 저주 카드 사용 시 플레이어 피해량
        public const int ABSORB_AMOUNT = 2;         // 흡수 방해행동 각성 게이지 감소량(모든 몬스터 공통)
        public const int ATTACK_AWAKEN_DRAIN_DEFAULT = 5; // 적 공격 시 각성 감소량(EnemyData 없을 때)
        public const float RECOVER_RATIO = 0.1f;    // 회복 방해행동 회복 비율(최대 HP 대비)
        public const int DISCARD_DISRUPTION_COUNT = 1; // 버리기 방해행동 무작위 버림 장수
        public const int DISRUPTION_DISCARD_RANDOM_ABILITY_COUNT = 2; // 특이사항(가라카사오바케) 버리기 방해 시 버림 장수
        class ActiveCurse
        {
            public CardElement element;
            public int remainingUses;
            public string sourceName; // 이 저주를 건 몬스터. 저주로 죽으면 게임오버 문장에 쓴다.
            public bool held;         // 적(심해거신·백족승)이 붙잡고 있는 저주 — 사용 횟수로 풀리지 않고, 적이 다른 속성으로 옮길 때 풀림
            public bool Active => held || remainingUses > 0;
        }
        private readonly List<ActiveCurse> _curses = new List<ActiveCurse>(); // 중첩 가능한 속성 저주
        private int _recoverCooldown;               // 방해행동(회복) 쿨다운
        private int _lastDisruptionAction = -1;     // 직전에 발동한 방해행동(연속 발동 방지용, -1=없음)

        [Tooltip("지연형 방해가 발현되지 않은 채 흐른 최대 시간(초). 초과하면 미발현 보상으로 마감.")]
        [SerializeField] private float disruptionTrackTimeout = 25f; // 지연형 추적 타임아웃(초)
        private DisruptionTrack _track;       // 진행 중 지연형 추적(동시 1개)
        const float UNFIRED_REWARD = -0.3f;   // 발현 못 한 지연형 보상(약한 음수)

        // 진행 중인 지연형 방해행동 추적 상태
        class DisruptionTrack
        {
            public Battle.AI.AiDecision decision; // 추적 대상 AI 결정
            public int action;                // 0 저주 / 1 탈진 / 3 강화
            public float elapsed;             // 추적 경과 시간
            public int curseHits;             // 저주: 실제 자해 발생 횟수
            public CardInstance exhaustCard;  // 탈진: 삽입한 카드 인스턴스
            public int playerHpAtFire;        // 강화: 발동 시 플레이어 HP
        }

        private readonly List<TdTransition> _tdQueue = new List<TdTransition>(); // TD(0) 전이 큐

        // TD(0) 학습용 (s,a,r,s') 전이 한 건
        class TdTransition
        {
            public float[] mu, ctx;          // s (membership 배열 = 결정 식별자)
            public int action;               // a
            public float reward;             // r
            public bool hasReward;           // 보상 확정 여부
            public bool immediate;           // 디버그 라벨용(즉시형/지연형)
            public float[] nextMu, nextCtx;  // s' (다음 결정 상태). null이면 미확정
            public bool hasNext;             // s' 확정 여부
        }

        private int _skipNextGaugeCount;      // 바람14(313): 다음 카드 게이지 소모 스킵 횟수
        private CardInstance _lastResolvedCard; // 물12(211): 마지막으로 사용한 카드(재사용 대상)

        private int _cardsPlayedThisBattle;        // 유물(웃는 탈 10006): 이번 전투 일반 카드 사용 누적 수
        public int CardsPlayedThisBattle => _cardsPlayedThisBattle; // 유물 카운터 표시용
        private bool _hpLostThisBattle;            // 유물(혈묵주 10003): 이번 전투 체력 손실 발생 여부
        private bool _relicForceAwakenPending;     // 유물(황룡옥적 10010): 시작 드로우 후 강제 각성 예약
        private bool _relicFirstCardRecastPending; // 유물(복사경 10015): 첫 사용 카드 효과 1회 재발동 예약

        public bool IsAwakenActive => _awakenActive;        // 각성 활성 여부 노출
        public static bool IsAwakeningEntryPlaying =>
            Instance != null && Instance.awakenEntryVfx != null && Instance.awakenEntryVfx.IsPlaying;
        public EnemyController CurrentEnemy =>
            _enemy != null && _enemy.IsAlive ? _enemy : EnemyCombatParty.FirstAlive();
        public CardDeckSystem Deck => _deck;                // 덱 시스템 노출
        public CardInstance LastResolvedCard => _lastResolvedCard; // 마지막 사용 카드 노출
        public CardElement? CursedElement // 현재 저주 속성 노출(여러 개면 첫 활성)
        {
            get
            {
                for (int i = 0; i < _curses.Count; i++)
                    if (_curses[i].Active) return _curses[i].element;
                return null;
            }
        }
        // 불126: 적 행동 시 화상이 줄어들지 않는지를 EnemyController가 조회
        public bool BurnPersistsOnEnemyTurn => _ctx != null && _ctx.burnPersistsOnEnemyTurn;
        // 각성 발동에 필요한 실제 속성 카드 수(보정 반영, 최소 1)
        public int EffectiveAwakenInput =>
            Mathf.Max(1, AWAKEN_ACTIVATION_INPUT + (_ctx != null ? _ctx.awakenGaugeMaxDelta : 0));

        private List<CardInstance> _customStartingDeck;      // 외부가 주입한 사용자 정의 시작 덱
        // 라운드 시작 전 사용자 정의 시작 덱 주입
        public void SetCustomStartingDeck(List<CardInstance> deck) => _customStartingDeck = deck;

        // 싱글톤 설정 + 덱 이벤트 구독
        void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(gameObject); return; }
            _resolver.Bind(_ctx);

            _deck.OnCardDrawn += OnCardDrawnHandler;
            _deck.OnCardDiscarded += OnCardDiscardedHandler;
        }

        // 이벤트 구독 해제 및 싱글톤 정리
        void OnDestroy()
        {
            ClearAwakenComboParticles();
            if (ownsAwakenEntryVfx && awakenEntryVfx != null)
                Destroy(awakenEntryVfx.gameObject);

            _deck.OnCardDrawn -= OnCardDrawnHandler;
            _deck.OnCardDiscarded -= OnCardDiscardedHandler;
            if (_player != null)
            {
                _player.OnPlayerHit -= OnPlayerHitHandler;
                _player.OnBlockConsumedByAttack -= OnBlockConsumedHandler;
                _player.OnGuardGained -= OnGuardGainedHandler;
                _player.OnGuardBrokenByDamage -= OnGuardBrokenByDamageHandler;
                _player.OnGuardAbsorbedHit -= OnGuardAbsorbedHitHandler;
                _player.OnHpDecreased -= OnPlayerHpDecreasedHandler;
            }
            UnbindPartyEvents();
            if (Instance == this) Instance = null;
        }

        // 카드가 패로 들어올 때 게이지 리셋·트리거·탈진 추적 처리
        void OnCardDrawnHandler(CardInstance card)
        {
            if (card == null) return;
            card.gaugeSinceDrawn = 0;
            card.justDrawn = true;
            _ctx.currentCardJustDrawn = true;
            if (_inBattle && !_awakenActive)
            {
                _resolver.NotifyCardEnteredHand(card);
                _resolver.NotifyCardDrawnForPowers(); // 선회(325)
            }

            if (_track != null && _track.action == 1 && ReferenceEquals(card, _track.exhaustCard))
                ResolveTrack(0.7f);
        }

        // 패에서 카드가 버려질 때 트리거 처리(각성 중 제외)
        void OnCardDiscardedHandler(CardInstance card)
        {
            if (_inBattle && !_awakenActive) _resolver.NotifyCardDiscardedFromHand(card);
        }

        // 매 프레임 입력·각성·추적·UI 갱신
        void Update()
        {
            if (!_inBattle) return;

            if (_enemy == null || !_enemy.gameObject.activeInHierarchy || !_enemy.IsAlive)
            {
                BindEnemy(EnemyCombatParty.FirstAlive());
            }

            HandleInput();
            TickAwakenTimer();
            TickDisruptionTrack();
            UpdateAwakenText();
            SyncChainStatus();
        }

        private int _lastChainShown = -1;  // 마지막으로 UI에 표시한 연쇄 수
        // 연쇄 스택을 플레이어 상태 패널에 표시(변경 시에만 갱신)
        void SyncChainStatus()
        {
            if (_player == null) return;
            if (_ctx.chainCount == _lastChainShown) return;
            _lastChainShown = _ctx.chainCount;
            _player.SetStatus("chain", _ctx.chainCount);
        }

        // 전투 진입 — 참조/컨텍스트/덱/UI 초기화 후 시작 드로우
        public void StartBattle()
        {
            ClearAwakenComboParticles();
            if (_player != null)
            {
                _player.OnGuardGained -= OnGuardGainedHandler;
                _player.OnGuardBrokenByDamage -= OnGuardBrokenByDamageHandler;
                _player.OnGuardAbsorbedHit -= OnGuardAbsorbedHitHandler;
            }
            _player = Player.Resolve(true);
            BindParty();
            _ctx.player = _player;
            _ctx.deck = _deck;
            ResetContextFlags();
            if (RunDeckState.Instance != null)
            {
                _ctx.attackPowerBonus += RunDeckState.Instance.PermanentAttackPower;
                _ctx.doubleBasicCardEffects = RunDeckState.Instance.DoubleBasicCardEffects; // 태초의 서 10004
            }

            if (_player != null)
            {
                _player.OnPlayerHit -= OnPlayerHitHandler;
                _player.OnPlayerHit += OnPlayerHitHandler;
                _player.OnBlockConsumedByAttack -= OnBlockConsumedHandler;
                _player.OnBlockConsumedByAttack += OnBlockConsumedHandler;
                _player.OnGuardGained += OnGuardGainedHandler;
                _player.OnGuardBrokenByDamage += OnGuardBrokenByDamageHandler;
                _player.OnGuardAbsorbedHit += OnGuardAbsorbedHitHandler;
                _player.OnHpDecreased -= OnPlayerHpDecreasedHandler;
                _player.OnHpDecreased += OnPlayerHpDecreasedHandler;
            }

            var startingDeck = _customStartingDeck ?? CardDatabase.BuildDefaultPrototypeDeck();
            _deck.StartBattle(startingDeck);

            // 유물(독주 10013): 손패 한도 강제 오버라이드 (deck.StartBattle이 한도를 리셋한 뒤 적용)
            int handLimitOverride = RelicManager.Instance != null ? RelicManager.Instance.GetHandLimitOverride() : -1;
            if (handLimitOverride > 0) _deck.SetHandLimitOverride(handLimitOverride);

            if (useComboDatabase)
            {
                ownedComboSkills = BuildOwnedComboDefs();
                Log($"콤보 DB 로드 — 보유 콤보 {ownedComboSkills.Count}개" +
                    (ownedComboIds != null && ownedComboIds.Count > 0 ? $" (지정 {ownedComboIds.Count}개)" : " (없음)"));
            }

            if (handHud == null) handHud = ResolveOrCreateHandHud();
            if (handHud != null)
            {
                handHud.Bind(_deck);
                handHud.UseCardCallback = OnUseCardRequested;
                handHud.SelectionClosedCallback = TryActivatePendingAwaken;
                handHud.SetAwakenMode(false);
            }
            else
            {
                Debug.LogWarning("[NewBattleController] handHud가 미연결입니다.");
            }

            if (effectOverlay == null) effectOverlay = ResolveOrCreateEffectOverlay();
            if (effectOverlay != null) effectOverlay.ClearAll();
            if (damageOverlay == null) damageOverlay = ResolveOrCreateDamageOverlay();

            EnsureEventSystem();

            if (turnOrderTimeline == null) turnOrderTimeline = ResolveOrCreateTurnOrderTimeline();
            if (tabPanel == null) tabPanel = ResolveTabPanel();
            tabPanel?.Hide();

            _inBattle = true;
            BattleLog.Clear();
            _cardPresenting = false;
            ResetEnemyTurnState();
            ClearSelectionQueue();
            _elementInputCount = 0;
            _awakenActive = false;
            if (_deck != null) _deck.BlockNonComboDraws = false;
            _awakenPending = false;
            _awakenTimeRemaining = 0f;
            _awakenMaxTime = 0f;
            if (awakenTimerGauge != null) awakenTimerGauge.Hide();
            _usedFireCardCount = 0;
            _recoverCooldown = 0;
            _lastDisruptionAction = -1; // 방해행동 연속 방지 기록 초기화
            // 저주는 전투 범위 상태 — 이전 전투가 저주가 남은 채 끝나도 다음 전투로 넘어가지 않도록 초기화
            _curses.Clear();
            _track = null;
            _tdQueue.Clear();
            _cardsPlayedThisBattle = 0;
            _hpLostThisBattle = false;
            _relicForceAwakenPending = false;
            _relicFirstCardRecastPending = false;

            if (handHud != null)
            {
                var hpRect = ResolveActiveCombatHpBarRect();
                if (hpRect != null) handHud.SetAwakenGaugeAnchor(hpRect);
            }
            if (handHud != null)
                handHud.SetAwakenGaugeVisible(true);

            BattleLog.SetDrawLogging(false);
            try
            {
                _deck.Draw(START_DRAW);
            }
            finally
            {
                BattleLog.SetDrawLogging(true);
            }
            UpdateAwakenText();
            Log($"전투 시작 — {START_DRAW}장 드로우");

            // 전투 시작 유물 발동 — 오프닝 핸드 드로우 직후 처리한다.
            // (깨진 불상 10007의 파편이 첫 패에 섞여 들어가지 않도록 드로우 이후로 미룸)
            ApplyBattleStartRelics();

            // 유물(황룡옥적 10010): 시작 드로우 후 즉시 각성 진입
            if (_relicForceAwakenPending)
            {
                _relicForceAwakenPending = false;
                _awakenPending = false;
                if (!_awakenActive) { ActivateAwaken(); Log("[유물] 황룡옥적 — 전투 시작 시 각성 진입"); }
            }
        }

        // 전투 진입 시 발동하는 유물 효과 처리 — 보유 유물의 OnBattleStart 디스패치
        void ApplyBattleStartRelics()
        {
            RelicManager.Instance?.NotifyBattleStart(this);
        }

        // 전투 종료 — 추적/큐 마감·덱 종료·UI/이펙트 정리
        public void EndBattle()
        {
            bool wasAwakened = _awakenActive;
            _inBattle = false;
            if (_player != null)
            {
                _player.OnGuardGained -= OnGuardGainedHandler;
                _player.OnGuardBrokenByDamage -= OnGuardBrokenByDamageHandler;
                _player.OnGuardAbsorbedHit -= OnGuardAbsorbedHitHandler;
            }
            BattleLog.Clear();
            _cardPresenting = false;
            _awakenActive = false;
            if (_deck != null) _deck.BlockNonComboDraws = false;
            _awakenPending = false;
            // 저주가 남은 채 전투가 끝나면 보상창 카드가 저주(보라색) 모습으로 나왔다 — 종료 시 해제
            _curses.Clear();
            ResetEnemyTurnState();
            ClearSelectionQueue();
            UnbindPartyEvents();
            ResolveTrackUnfired();
            TdFlushTerminal();
            _deck.EndBattle();
            if (handHud != null)
            {
                handHud.CancelCardUsePresentations();
                // 선택창(선택/픽커/더미보기)이 열린 채 적을 처치해 전투가 끝나면
                // 모드가 남아 다음 전투까지 영향을 주므로 여기서 강제 종료한다.
                handHud.ExitSelectionMode();
                handHud.ExitPickerMode();
                handHud.CloseViewer();
                if (wasAwakened) handHud.SetAwakenMode(false);
                handHud.UseCardCallback = null;
                handHud.SelectionClosedCallback = null;
                handHud.SetAwakenGaugeVisible(false);
            }
            if (awakenTimerGauge != null) awakenTimerGauge.Hide();
            ClearAwakenComboParticles();
            if (effectOverlay != null) effectOverlay.ClearAll();
            StopAwakenScreenEffect();
            if (tabPanel != null) tabPanel.Hide();
            Log("전투 종료");
        }

        // 게임 종료(사망·클리어) 시 화면에 남는 전투 연출(카드 사용 연출·카드 이펙트·각성 화면/표식)을 즉시 정리.
        // 사망 화면은 timeScale=0이지만 이 연출들은 unscaled time으로 계속 돌아 결과 화면 위에 남았다.
        public void ClearPresentationFx()
        {
            if (handHud != null) handHud.CancelCardUsePresentations();
            ClearAwakenComboParticles();
            if (effectOverlay != null) effectOverlay.ClearAll();
            StopAwakenScreenEffect();
        }

        // 전투 시작 시 카드 효과 컨텍스트 플래그/카운터 전체 초기화
        void ResetContextFlags()
        {
            _ctx.bonusDamagePerCardActive = false;
            _ctx.bonusDamagePerCard = 0;
            _ctx.attackHitCountBonusActive = false;
            _ctx.attackHitCountBonus = 0;
            _ctx.burnOnHitActive = false;
            _ctx.burnOnHitAmount = 0;
            _ctx.burnOnExileActive = false;
            _ctx.burnOnExileAmount = 0;
            _ctx.fragmentOnEarthUseActive = false;
            _ctx.fragmentOnFragmentUseActive = false;
            _ctx.blockOnCardUseActive = false;
            _ctx.blockOnCardUseAmount = 0;
            _ctx.burnOnByCardActive = false;
            _ctx.burnOnByCardAmount = 0;
            _ctx.fragmentOnBlockConsumeActive = false;
            _ctx.healToDrawActive = false;
            _ctx.discardToDrawActive = false;
            _ctx.waterUseHealActive = false;
            _ctx.healOnCurseCleanseActive = false;
            _ctx.healOnCurseCleanseAmount = 0;
            _ctx.healOnPlayerHitActive = false;
            _ctx.chainGainOnHitActive = false;
            _ctx.chainCount = 0;
            _ctx.chainBonusDamage = 0;
            _ctx.attackPowerBonus = 0;
            _ctx.damageMultiplierActive = 0;
            _ctx.damageMultiplierThresholdHp = 25;

            _ctx.lastHpLost = 0;
            _ctx.lastConsumedBurn = 0;
            _ctx.exhaustedCardCount = 0;
            _ctx.consumedChainCount = 0;
            _ctx.lastHitEnemyCount = 0;
            _ctx.lastDiscardedCount = 0;
            _ctx.usedAttackCardCount = 0;
            _ctx.usedFragmentCardCount = 0;
            _ctx.previousCardType = null;
            _ctx.firstCardAfterEnemyAttack = false;
            _ctx.nextCardIsFree = false;
            _ctx.currentCardJustDrawn = false;
            _ctx.usedCardNameCounts.Clear();
            _ctx.handLimitBonus = 0;
            _ctx.firstCardAfterAttackFreeActive = false;

            _ctx.chainGainOnCardUseActive = false;
            _ctx.chainGainOnCardUseAmount = 0;
            _ctx.damageOnFragmentUseActive = false;
            _ctx.damageOnFragmentUseAmount = 0;
            _ctx.frostOnWaterUseActive = false;
            _ctx.frostOnWaterUseAmount = 0;
            _ctx.frostOnWaterUseEveryN = 0;
            _ctx.frostOnWaterUseRandomTarget = false;
            _ctx.waterUseCounter = 0;
            _ctx.attackPowerOnHpLossActive = false;
            _ctx.attackPowerOnHpLossAmount = 0;
            _ctx.frostOnFrostByCardActive = false;
            _ctx.frostOnFrostByCardAmount = 0;
            _ctx.damageOnFrostByCardActive = false;
            _ctx.damageOnFrostByCardAmount = 0;
            _ctx.burnOnHpLossActive = false;
            _ctx.damageOnDrawActive = false;
            _ctx.damageOnDrawAmount = 0;
            _ctx.blockOnEnemyAttackActive = false;
            _ctx.blockOnEnemyAttackAmount = 0;
            _ctx.recastExhaustCardActive = false;
            _ctx.burnSkillHitsAllActive = false;
            _ctx.burnPersistsOnEnemyTurn = false;
            _ctx.awakenGaugeMaxDelta = 0;
            _ctx.FrostTriggerReentrancy = false;
            _ctx.doubleBasicCardEffects = false;

            _skipNextGaugeCount = 0;
            _lastResolvedCard = null;
        }

        // 물18(217): 플레이어 피격 시 회복 1
        void OnPlayerHitHandler()
        {
            if (_ctx.healOnPlayerHitActive && _ctx.player != null)
                _ctx.player.Heal(1);
        }

        // 플레이어가 실제 체력을 잃었을 때 — 유물(혈묵주 10003, 피 묻은 가시 10019) 디스패치
        void OnPlayerHpDecreasedHandler(int amount)
        {
            if (amount <= 0) return;
            bool first = !_hpLostThisBattle;
            _hpLostThisBattle = true;
            // 적 행동 처리 중이면 공격 모션이 피격 모션으로 끊기지 않도록 행동이 끝난 뒤로 미룬다
            if (_attackResolving && _attackStartRoutine != null)
            {
                _deferredHpLost.Add((amount, first));
                return;
            }
            RelicManager.Instance?.NotifyPlayerHpLost(this, amount, first);
            if (_inBattle) _resolver.NotifyPlayerHpLostForPowers(amount); // 수라의 길(122)
        }

        // 미뤄 둔 체력 손실 유물 발동을 순서대로 처리
        void FlushDeferredHpLost()
        {
            if (_deferredHpLost.Count == 0) return;
            var pending = _deferredHpLost.ToArray();
            _deferredHpLost.Clear();
            if (!_inBattle) return;
            for (int i = 0; i < pending.Length; i++)
            {
                RelicManager.Instance?.NotifyPlayerHpLost(this, pending[i].amount, pending[i].first);
                _resolver.NotifyPlayerHpLostForPowers(pending[i].amount); // 수라의 길(122)
            }
        }

        void OnGuardGainedHandler()
        {
            if (_inBattle && isActiveAndEnabled && effectOverlay != null)
                effectOverlay.PlayGuardGain();
        }

        void OnGuardBrokenByDamageHandler()
        {
            if (_inBattle && isActiveAndEnabled && effectOverlay != null)
                effectOverlay.PlayGuardBreak();
        }

        void OnGuardAbsorbedHitHandler()
        {
            if (_inBattle && isActiveAndEnabled && effectOverlay != null)
                effectOverlay.PlayGuardBlock();
        }

        // 땅18(417): 방어도 소모 시 무작위 파편 1장 버린 더미에
        void OnBlockConsumedHandler()
        {
            if (_ctx.fragmentOnBlockConsumeActive && _deck != null)
                _deck.AddToDiscard(CardDatabase.CreateFragmentInstance());
        }

        // 손패 선택/픽커 요청 직렬화
        // CardHandHUD의 선택 모드는 콜백 슬롯이 1개라, 카드 효과 선택(소멸/버리기/복사/픽커)과
        // 적 방해 '버리기' 선택이 겹치면 나중 요청이 앞 요청의 콜백을 덮어써 효과가 유실되거나
        // 버리기 코루틴이 영원히 대기했다. 모든 선택 요청을 이 대기열로 받아 한 번에 하나씩 연다.
        private class SelectionRequest
        {
            public bool picker;                                  // true=픽커(카드 목록), false=손패 선택
            public string prompt;                                // 안내 문구
            public System.Action<CardInstance> callback;         // 선택 결과 콜백
            public System.Func<CardInstance, bool> filter;       // 손패 선택 필터(null=아무 카드)
            public System.Func<IList<CardInstance>> pickerCards; // 픽커 후보(진입 시점에 생성)
            public Battle.UI.PileViewerKind? pileKind;           // 덱/묘지 후보면 더미 패널로 표시(null=가로 배치)
            public int count = 1;                                // 손패에서 고를 장수(빈 칸 수)
            public System.Action<IList<CardInstance>> multiCallback; // 여러 장 선택 결과 콜백(있으면 callback 대신 사용)
            public System.Action onFinished;                     // 선택·취소·건너뜀 어느 쪽이든 이 요청이 끝난 뒤 1회 호출(전투 종료로 버려지면 호출 안 함)
        }
        private readonly Queue<SelectionRequest> _selectionQueue = new Queue<SelectionRequest>();
        private bool _selectionActive;          // 대기열이 연 선택창이 아직 열려 있음
        private Coroutine _selectionRoutine;    // 대기열 처리 코루틴

        // 선택창이 열려 있거나 열릴 예정인지 — 이 동안 카드 사용/D 드로우/적 행동/각성 발동을 미룬다
        public bool HasPendingSelection =>
            _selectionActive || _selectionQueue.Count > 0
            || (handHud != null && (handHud.IsSelectionMode || handHud.IsPickerMode));

        // 지금 D 드로우를 시도할 수 있는 상태인지 (버튼 활성/비활성 표시용)
        public bool CanDDraw =>
            _inBattle && !_cardPresenting && !InputLockedByEnemyTurn &&
            !HasPendingSelection && !(handHud != null && handHud.IsViewerMode) &&
            HasDDrawRoom;

        // 손패 여유 — 비급서(10011)는 패를 버리고 뽑으므로 항상 가능, 아니면 현재 손패 한도(확장 포함) 미만일 때만
        bool HasDDrawRoom
        {
            get
            {
                if (_deck == null) return false;
                if (RelicManager.Instance != null && RelicManager.Instance.HasDDrawOverride()) return true;
                return !_deck.IsHandFull;
            }
        }

        // D 드로우 시 적 게이지 증가량 (버튼 코스트 표시용)
        public int DDrawGaugeCost =>
            RelicManager.Instance != null ? RelicManager.Instance.GetDDrawGaugeCost(1) : 1;

        // [테스트] StageTestPanel 전용
        private int? _debugForcedDisruption; // 다음 방해행동 강제값(0 저주·1 탈진·2 흡수·3 강화·4 회복·5 버리기)

        // 지정한 방해행동을 적 행동 대기열로 발동(실제 흐름과 같은 순서·잠금으로 처리)
        public void DebugTriggerDisruption(int pick)
        {
            if (!_inBattle) return;
            EnemyController source = _enemy != null && _enemy.IsAlive ? _enemy : EnemyCombatParty.FirstAlive();
            if (source == null) return;
            _debugForcedDisruption = pick;
            EnqueueEnemyDisruption(source);
        }

        // 각성을 즉시 발동(선택창이 열려 있으면 끝난 뒤 발동)
        public void DebugForceAwaken()
        {
            if (!_inBattle || _awakenActive) return;
            _awakenPending = true;
            TryActivatePendingAwaken();
        }

        // [테스트] 각성 게이지를 최대-1로 채움 — 다음 속성 카드 1장으로 각성이 발동된다. 반환: 성공 여부
        public bool DebugFillAwakenGaugeAlmost()
        {
            if (!_inBattle || _awakenActive) return false;
            _elementInputCount = Mathf.Max(0, EffectiveAwakenInput - 1);
            UpdateAwakenText();
            Log($"[테스트] 각성 게이지 {_elementInputCount}/{EffectiveAwakenInput}");
            return true;
        }

        // 상세 로그 출력(logVerbose ON일 때만)
        void Log(string msg)
        {
            if (logVerbose) Debug.Log($"[NewBattle] {msg}");
        }

        private readonly HashSet<CardElement> _pendingCurseNudges = new HashSet<CardElement>(); // 떠오르기 대기 중인 저주 속성
        private Coroutine _curseNudgeCo; // 떠오르기 대기 코루틴

        // 저주 속성 카드 떠오르기를 예약 — 적 행동 중엔 손패가 숨겨져 있으므로 카메라가 돌아오고 UI가 다시 보인 뒤 실행
        void RequestCurseNudge(CardElement element)
        {
            _pendingCurseNudges.Add(element);
            if (_curseNudgeCo == null && isActiveAndEnabled)
                _curseNudgeCo = StartCoroutine(FlushCurseNudgesWhenHudVisible());
        }

        // 전투 진행 중 여부(적 특이사항 전투시작 트리거가 덱 준비 후 발동하도록)
        public bool InBattle => _inBattle;

    }
}
