using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using Battle.Card;
using Battle.Deck;
using Battle.Relic;
using Battle.UI;

namespace Battle
{
    // 새 전투 시스템 진입점 (5장 손패·드래그 사용·각성·콤보·적 AI 방해)
    public class NewBattleController : MonoBehaviour
    {
        public const int AWAKEN_ACTIVATION_INPUT = 10;                  // 각성 발동에 필요한 속성 카드 사용 횟수
        public const float AWAKEN_DURATION_SECONDS = 4f;               // 각성 기본 지속 시간(초)
        public const float AWAKEN_DURATION_PER_OWNED_COMBO_SECONDS = 1f; // 보유 콤보 1개당 추가 지속 시간(초)
        public const float COMBO_BONUS_SECONDS = 0.1f;                 // 콤보 1회 성공 시 추가 시간(초)
        public const int COMBO_REUSE_INPUT = 5;                        // 콤보 재사용까지 필요한 속성 입력 횟수
        public const int START_DRAW = 5;                              // 전투 시작 시 드로우 수

        public static NewBattleController Instance { get; private set; } // 싱글톤 인스턴스

        [Header("UI")]
        [SerializeField] private CardHandHUD handHud;                 // 손패 HUD
        [Tooltip("카드 사용 시 EffectName(예: Fire_ATK)에 해당하는 스프라이트 시트 애니메이션을 재생. " +
                 "비우면 자동으로 캔버스에 생성. 시트가 없으면 조용히 스킵.")]
        [SerializeField] private CardEffectOverlay effectOverlay;     // 카드 효과 이펙트 오버레이
        [Tooltip("플레이어 피격 시 화면 빨간 플래시. 비우면 자동 생성.")]
        [SerializeField] private PlayerDamageOverlay damageOverlay;   // 플레이어 피격 화면 오버레이
        [Tooltip("각성 동안 화면 상단에 남은 시간을 표시할 전용 게이지. 씬에 직접 만들어 연결(코드로 생성하지 않음). 비우면 표시 생략.")]
        [SerializeField] private AwakenTimerGauge awakenTimerGauge;   // 상단 각성 타이머 게이지

        [Header("보유 콤보 스킬 (각성 전용 / Inspector 편집)")]
        [SerializeField] private List<ComboSkillDef> ownedComboSkills = new List<ComboSkillDef>(); // 보유 콤보 스킬 목록
        public IReadOnlyList<ComboSkillDef> OwnedComboSkills => ownedComboSkills; // 보유 콤보 읽기 전용 노출

        [Header("콤보 스킬 DB")]
        [Tooltip("ON이면 Resources/ComboDB의 데이터 드리븐 콤보를 사용 (Inspector의 ownedComboSkills를 덮어씀). " +
                 "OFF면 위 Inspector 수동 리스트 사용.")]
        [SerializeField] private bool useComboDatabase = true;        // 콤보 DB 사용 여부
        [Tooltip("보유할 콤보 RefComboID(1000~1019). 비우면 전체 보유. 예: 1000,1004,1008")]
        [SerializeField] private List<int> ownedComboRefIds = new List<int>(); // 보유할 콤보 RefComboID 목록
        [Tooltip("[테스트] 콤보 보상 UI 전까지 수동 획득용 — 이 RefComboID(1000~1019)를 컨텍스트 메뉴 '콤보 1개 추가'로 보유에 더함.")]
        [SerializeField] private int debugAddComboRefId = 1000;       // 디버그 수동 콤보 추가용 RefComboID

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

        [Header("각성 화면 효과 (Screen wind 등)")]
        [Tooltip("각성 동안 화면 전체에 유지될 프리팹(예: Hovl 'Screen wind'). 각성 발동 시 루프 재생되고 종료 시 정지된다. " +
                 "UIParticle로 전투 UI 위에 렌더되며, 프리팹의 카메라 부착 스크립트(HS_ScreenEffect)는 자동 비활성된다. " +
                 "비우면 화면 효과 없음.")]
        [SerializeField] private GameObject awakenScreenEffectPrefab; // 각성 화면 효과 프리팹
        [Tooltip("각성 화면 효과 UIParticle 스케일 — 화면을 덮도록 플레이로 튜닝(클수록 큼). 0 이하면 오버레이 기본 스케일 사용.")]
        [SerializeField] private float awakenScreenEffectScale = 100f; // 각성 화면 효과 스케일

        [Header("디버그")]
        [SerializeField] private bool logVerbose = true;             // 상세 로그 출력 여부

        [Header("적 AI (방해행동 선택)")]
        [Tooltip("ON이면 FCM-RBFN 학습 모델로 방해행동을 선택. OFF면 랜덤.")]
        [SerializeField] private bool useAiDisruption = true;        // AI 방해행동 선택 사용 여부
        [Tooltip("탐험 비율 — 데이터 수집 시 행동 다양성 확보용(0=항상 최적, 0.1~0.2=수집용). 학습 데이터가 충분하면 0으로.")]
        [Range(0f, 1f)]
        [SerializeField] private float aiExplorationEpsilon = 0.15f; // AI 탐험 비율(epsilon)
        [Tooltip("ON이면 각 방해행동 결정을 CSV로 기록(persistentDataPath/ai_logs) → 오프라인 재학습용.")]
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
        private EnemyController _enemy;          // 적 컨트롤러 참조
        private EnemyStat _enemyStat;            // 적 스탯 참조

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
        private int[] _comboCooldown = System.Array.Empty<int>(); // 콤보별 재사용 쿨다운(남은 입력 횟수)
        private readonly List<ComboSkillDef> _queuedComboSkills = new List<ComboSkillDef>(); // 각성 종료 시 발동할 콤보 큐
        private readonly List<CardElement> _awakenInputHistory = new List<CardElement>(); // 각성 중 입력 속성 전체 기록

        private int _usedFireCardCount;       // 이번 전투 불 카드 사용 수
        private int _awakenFragmentExhausted; // 각성 진입 시 격리된 파편 수
        private int _awakenComboCountThisEnd; // 이번 각성 종료 시 발동되는 콤보 총수
        private readonly Dictionary<int, int> _comboSelfUseCount = new Dictionary<int, int>(); // refComboId별 이번 각성 발동 횟수

        public const int CURSE_DURATION_USES = 2;   // 저주 지속 카드 사용 횟수
        public const int CURSE_PLAYER_DAMAGE = 6;   // 저주 카드 사용 시 플레이어 피해량
        private CardElement? _cursedElement;        // 현재 저주된 속성
        private int _curseRemainingUses;            // 저주 남은 지속 횟수
        private int _recoverCooldown;               // 방해행동(회복) 쿨다운

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

        public bool IsAwakenActive => _awakenActive;        // 각성 활성 여부 노출
        public CardDeckSystem Deck => _deck;                // 덱 시스템 노출
        public CardInstance LastResolvedCard => _lastResolvedCard; // 마지막 사용 카드 노출
        public CardElement? CursedElement => _cursedElement; // 현재 저주 속성 노출
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
            _deck.OnCardDrawn -= OnCardDrawnHandler;
            _deck.OnCardDiscarded -= OnCardDiscardedHandler;
            if (_player != null)
            {
                _player.OnPlayerHit -= OnPlayerHitHandler;
                _player.OnBlockConsumedByAttack -= OnBlockConsumedHandler;
            }
            if (_enemyStat != null) _enemyStat.OnGaugeFull -= OnEnemyAttackFiredHandler;
            if (Instance == this) Instance = null;
        }

        // 카드가 패로 들어올 때 게이지 리셋·트리거·탈진 추적 처리
        void OnCardDrawnHandler(CardInstance card)
        {
            if (card == null) return;
            card.gaugeSinceDrawn = 0;
            card.justDrawn = true;
            _ctx.currentCardJustDrawn = true;
            if (_inBattle && !_awakenActive) _resolver.NotifyCardEnteredHand(card);

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

            if (_enemy == null || !_enemy.gameObject.activeInHierarchy)
            {
                _enemy = FindFirstObjectByType<EnemyController>();
                _enemyStat = _enemy != null ? _enemy.GetComponent<EnemyStat>() : null;
                _ctx.enemy = _enemy;
                _ctx.enemyStat = _enemyStat;
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

        // 각성 지속 시간 카운트다운 및 종료 처리
        void TickAwakenTimer()
        {
            if (!_awakenActive) return;
            _awakenTimeRemaining -= Time.deltaTime;
            if (_awakenTimeRemaining <= 0f)
            {
                _awakenTimeRemaining = 0f;
                EndAwaken();
                return;
            }
            if (awakenTimerGauge != null) awakenTimerGauge.SetTime(_awakenTimeRemaining, _awakenMaxTime);
        }

        // 전투 진입 — 참조/컨텍스트/덱/UI 초기화 후 시작 드로우
        public void StartBattle()
        {
            _player = Player.Resolve(true);
            _enemy = FindFirstObjectByType<EnemyController>();
            _enemyStat = _enemy != null ? _enemy.GetComponent<EnemyStat>() : null;

            _ctx.player = _player;
            _ctx.enemy = _enemy;
            _ctx.enemyStat = _enemyStat;
            _ctx.deck = _deck;
            ResetContextFlags();
            if (RunDeckState.Instance != null)
                _ctx.attackPowerBonus += RunDeckState.Instance.PermanentAttackPower;

            if (_player != null)
            {
                _player.OnPlayerHit -= OnPlayerHitHandler;
                _player.OnPlayerHit += OnPlayerHitHandler;
                _player.OnBlockConsumedByAttack -= OnBlockConsumedHandler;
                _player.OnBlockConsumedByAttack += OnBlockConsumedHandler;
            }

            if (_enemyStat != null)
            {
                _enemyStat.OnGaugeFull -= OnEnemyAttackFiredHandler;
                _enemyStat.OnGaugeFull += OnEnemyAttackFiredHandler;
            }

            var startingDeck = _customStartingDeck ?? CardDatabase.BuildDefaultPrototypeDeck();
            _deck.StartBattle(startingDeck);

            if (useComboDatabase)
            {
                ownedComboSkills = ComboSkillDatabase.BuildOwnedCombos(ownedComboRefIds);
                Log($"콤보 DB 로드 — 보유 콤보 {ownedComboSkills.Count}개" +
                    (ownedComboRefIds != null && ownedComboRefIds.Count > 0 ? $" (지정 {ownedComboRefIds.Count}종)" : " (전체)"));
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

            _inBattle = true;
            _cardPresenting = false;
            _elementInputCount = 0;
            _awakenActive = false;
            _awakenPending = false;
            _awakenTimeRemaining = 0f;
            _awakenMaxTime = 0f;
            if (awakenTimerGauge != null) awakenTimerGauge.Hide();
            _usedFireCardCount = 0;
            _recoverCooldown = 0;
            _track = null;
            _tdQueue.Clear();

            if (handHud != null)
            {
                var hpRect = ResolveActiveCombatHpBarRect();
                if (hpRect != null) handHud.SetAwakenGaugeAnchor(hpRect);
            }
            if (handHud != null)
                handHud.SetAwakenGaugeVisible(true);

            _deck.Draw(START_DRAW);
            UpdateAwakenText();
            Log($"전투 시작 — {START_DRAW}장 드로우");
        }

        // 전투 종료 — 추적/큐 마감·덱 종료·UI/이펙트 정리
        public void EndBattle()
        {
            _inBattle = false;
            _cardPresenting = false;
            _awakenActive = false;
            _awakenPending = false;
            ResolveTrackUnfired();
            TdFlushTerminal();
            _deck.EndBattle();
            if (handHud != null)
            {
                handHud.CancelCardUsePresentations();
                handHud.UseCardCallback = null;
                handHud.SelectionClosedCallback = null;
                handHud.SetAwakenGaugeVisible(false);
            }
            if (awakenTimerGauge != null) awakenTimerGauge.Hide();
            if (effectOverlay != null) effectOverlay.ClearAll();
            _awakenScreenEffectInstance = null;
            Log("전투 종료");
        }

        // [테스트] 인스펙터 ownedComboRefIds를 즉시 적용(각성 중에는 보류)
        [ContextMenu("[테스트] 보유 콤보 갱신 (인스펙터 리스트 적용)")]
        public void RefreshOwnedCombosRuntime()
        {
            if (!useComboDatabase)
            {
                Debug.LogWarning("[NewBattle] useComboDatabase가 OFF — 콤보 DB로 갱신하려면 ON으로 두세요.");
                return;
            }
            if (_awakenActive)
            {
                Debug.LogWarning("[NewBattle] 각성 중에는 콤보 갱신 보류 — 다음 각성부터 반영됩니다.");
                return;
            }
            ownedComboSkills = ComboSkillDatabase.BuildOwnedCombos(ownedComboRefIds);
            _comboCooldown = new int[ownedComboSkills.Count];
            if (handHud != null) handHud.UpdateComboSkillList(ownedComboSkills, _comboCooldown);
            Debug.Log($"[NewBattle] 보유 콤보 갱신 — {ownedComboSkills.Count}개 " +
                      (ownedComboRefIds != null && ownedComboRefIds.Count > 0
                          ? $"(refIds: {string.Join(",", ownedComboRefIds)})" : "(전체)"));
        }

        // 콤보 1개(refComboId) 추가 후 즉시 적용
        public void AddOwnedCombo(int refComboId)
        {
            if (ownedComboRefIds == null) ownedComboRefIds = new List<int>();
            if (ownedComboRefIds.Contains(refComboId))
            {
                Debug.Log($"[NewBattle] 콤보 {refComboId} 이미 보유 중.");
                return;
            }
            ownedComboRefIds.Add(refComboId);
            useComboDatabase = true;
            RefreshOwnedCombosRuntime();
            Debug.Log($"[NewBattle] 콤보 추가: {refComboId}");
        }

        // [테스트] debugAddComboRefId 콤보 1개 추가
        [ContextMenu("[테스트] 콤보 1개 추가 (debugAddComboRefId)")]
        void DebugAddOneCombo() => AddOwnedCombo(debugAddComboRefId);

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
            _ctx.waterUseCounter = 0;
            _ctx.attackPowerOnHpLossActive = false;
            _ctx.attackPowerOnHpLossAmount = 0;
            _ctx.frostOnFrostByCardActive = false;
            _ctx.frostOnFrostByCardAmount = 0;
            _ctx.blockOnEnemyAttackActive = false;
            _ctx.blockOnEnemyAttackAmount = 0;
            _ctx.recastExhaustCardActive = false;
            _ctx.burnSkillHitsAllActive = false;
            _ctx.burnPersistsOnEnemyTurn = false;
            _ctx.awakenGaugeMaxDelta = 0;
            _ctx.FrostTriggerReentrancy = false;

            _skipNextGaugeCount = 0;
            _lastResolvedCard = null;
        }

        // 카드 사용 통계(공격/파편/불/이름별·런 프로파일) 갱신
        void UpdateUsageStats(CardInstance card)
        {
            if (card == null) return;
            if (card.Type == CardType.Attack) _ctx.usedAttackCardCount++;
            if (card.Element == CardElement.Fragment) _ctx.usedFragmentCardCount++;
            if (card.Element == CardElement.Fire) _usedFireCardCount++;

            string name = card.data.displayName ?? "";
            if (!string.IsNullOrEmpty(name))
            {
                _ctx.usedCardNameCounts.TryGetValue(name, out int cur);
                _ctx.usedCardNameCounts[name] = cur + 1;
            }

            RunDeckState.Instance?.RecordCardUse(Battle.AI.EnemyDisruptionAI.Classify(card.data), card.data.gauge);
        }

        // 물15(214) — 플레이어에게 적용된 모든 저주 해제
        public void ClearAllCurses()
        {
            bool hadCurse = _cursedElement.HasValue;
            _cursedElement = null;
            _curseRemainingUses = 0;
            handHud?.Refresh();
            if (hadCurse) Log("[저주] 해제됨");
        }

        // EnemyController가 공격 종료 시 호출 — 다음 카드에 적 공격 후 플래그 적용
        public void NotifyEnemyAttackFinished()
        {
            _ctx.firstCardAfterEnemyAttack = true;
        }

        // 물18(217): 플레이어 피격 시 회복 1
        void OnPlayerHitHandler()
        {
            if (_ctx.healOnPlayerHitActive && _ctx.player != null)
                _ctx.player.Heal(1);
        }

        // 땅18(417): 방어도 소모 시 무작위 파편 1장 버린 더미에
        void OnBlockConsumedHandler()
        {
            if (_ctx.fragmentOnBlockConsumeActive && _deck != null)
                _deck.AddToDiscard(CardDatabase.CreateFragmentInstance());
        }

        // 적 공격 발생 시 — 강화 발현 처리·각성 게이지 -5·땅424 방어도 획득
        void OnEnemyAttackFiredHandler()
        {
            _ctx.firstCardAfterEnemyAttack = true;

            if (_track != null && _track.action == 3) ResolveTrack(EnhanceReward(_track.playerHpAtFire));

            if (!_awakenActive)
            {
                _elementInputCount = Mathf.Max(0, _elementInputCount - 5);
                UpdateAwakenText();
            }

            if (_ctx.blockOnEnemyAttackActive && _ctx.blockOnEnemyAttackAmount > 0 && _player != null)
                _player.AddGuard(_ctx.blockOnEnemyAttackAmount);
        }

        // 직전 효과의 시각 갱신 후 다음 프레임에 선택 모드 진입
        IEnumerator DeferredSelection(string prompt,
            System.Action<CardInstance> callback,
            System.Func<CardInstance, bool> filter)
        {
            yield return null;
            if (handHud != null) handHud.EnterSelectionMode(prompt, callback, filter: filter);
        }

        // 다음 프레임에 카드 픽커 모드 진입
        IEnumerator DeferredPicker(string prompt, IList<CardInstance> cards,
            System.Action<CardInstance> callback)
        {
            yield return null;
            if (handHud != null) handHud.EnterCardPickerMode(prompt, cards, callback);
        }

        // 카드 사용 후 보류된 각성을 선택 모드 진입 대기 후 발동 시도
        IEnumerator DeferredPendingAwakenCheck()
        {
            yield return null;
            yield return null;
            TryActivatePendingAwaken();
        }

        // 필터에 매칭되는 카드가 패에 있는지 확인
        bool HandHasMatch(System.Func<CardInstance, bool> filter)
        {
            if (filter == null) return _deck.Hand.Count > 0;
            for (int i = 0; i < _deck.Hand.Count; i++)
                if (filter(_deck.Hand[i])) return true;
            return false;
        }

        // CardEffects의 cardFilter 문자열을 선택 모드용 delegate로 변환
        static System.Func<CardInstance, bool> BuildHandCardFilter(string filter)
        {
            if (string.IsNullOrEmpty(filter)) return null;
            switch (filter.Trim().ToUpperInvariant())
            {
                case "":
                case "ANY_CARD":
                    return null;
                case "NEUTRAL_CARD":
                    return c => c != null && c.Element == CardElement.Neutral;
                case "FRAGMENT_CARD":
                    return c => c != null && c.Element == CardElement.Fragment;
                case "ATTACK_CARD":
                    return c => c != null && c.Type == CardType.Attack;
                default:
                    return null;
            }
        }

        // 파편 인스턴스를 지정 존(HAND/DRAW/DISCARD)에 배치
        void PlaceFragmentInZone(CardInstance frag, string zone)
        {
            if (frag == null) return;
            switch ((zone ?? "").Trim().ToUpperInvariant())
            {
                case "HAND":
                    if (!_deck.AddToHand(frag)) _deck.AddToDiscard(frag);
                    break;
                case "DRAW_PILE":
                case "DRAW_PILE_SHUFFLE":
                    _deck.AddToDrawShuffled(frag);
                    break;
                case "DISCARD_PILE":
                case "DISCARD_PILE_SHUFFLE":
                default:
                    _deck.AddToDiscard(frag);
                    break;
            }
        }

        // 키 입력 처리 (D 드로우)
        void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.D))
            {
                if (_cardPresenting) return;
                if (handHud != null && (handHud.IsSelectionMode || handHud.IsPickerMode || handHud.IsViewerMode)) return;
                TryDDraw();
            }
        }

        // D 드로우 시도 — 1장 뽑고 적 게이지 +1
        void TryDDraw()
        {
            if (_deck.IsHandFull)
            {
                Log("D 드로우 불가 — 손패 가득 참");
                return;
            }
            int drawn = _deck.Draw(1);
            if (drawn > 0)
            {
                AccrueEnemyGauge(1);
                Log("D 드로우 — 적 게이지 +1");
            }
            else
            {
                Log($"D 드로우 실패 — 뽑을 더미({_deck.DrawCount})·버린 더미({_deck.DiscardCount}) 카드 없음 " +
                    $"(소멸 더미={_deck.ExilePile.Count})");
            }
        }

        // 카드 사용 요청 처리 — 각성 중 콤보 슬롯 등록 / 일반은 중앙 연출 후 효과 실행
        bool OnUseCardRequested(CardInstance card)
        {
            if (!_inBattle) return false;
            if (card == null) return false;

            if (_awakenActive)
            {
                _deck.MoveAfterUse(card, exile: false, poweredField: false);
                _deck.Draw(1);

                bool comboTriggered = false;
                if (card.data.comboSlot)
                {
                    AddToComboSlot(card.Element);
                    TickComboCooldowns();
                    comboTriggered = TryActivateCombo();
                }

                if (comboTriggered)
                {
                    float extraBonus = (RelicManager.Instance != null &&
                                        RelicManager.Instance.HasEffect(RelicEffectType.ComboBonusSecondsBoost))
                                       ? RelicManager.COMBO_BONUS_SECONDS_EXTRA : 0f;
                    float totalBonus = COMBO_BONUS_SECONDS + extraBonus;
                    float before = _awakenTimeRemaining;
                    _awakenTimeRemaining += totalBonus;
                    _awakenMaxTime += totalBonus;
                    Log($"[각성] ⏱ 콤보 매칭 → 시간 +{totalBonus:F1}s ({before:F2}s → {_awakenTimeRemaining:F2}s, max={_awakenMaxTime:F2}s)" +
                        (extraBonus > 0f ? " [비급서]" : ""));
                }

                Log($"[각성] {card.data.displayName} → 콤보입력=[{string.Join(",", _comboInput)}] 콤보발동={comboTriggered} 남은시간={_awakenTimeRemaining:F2}s");

                if (handHud != null)
                {
                    handHud.UpdateComboSlot(_comboInput);
                    handHud.UpdateComboSkillList(ownedComboSkills, _comboCooldown);
                    handHud.UpdateAwakenInputHistory(_awakenInputHistory);
                    handHud.PlayCardUsePresentation(card, null, interruptable: true);
                }

                return true;
            }

            if (_cardPresenting) return false;

            _deck.PullFromHand(card);
            _cardPresenting = true;

            if (handHud != null)
                handHud.PlayCardUsePresentation(card, () => { ResolveCardUse(card); _cardPresenting = false; });
            else
            {
                ResolveCardUse(card);
                _cardPresenting = false;
            }
            return true;
        }

        // 카드 사용 실제 효과 처리 — 중앙 연출 종료 시점 호출(카드는 이미 패에서 제거됨)
        void ResolveCardUse(CardInstance card)
        {
            if (card == null) return;
            if (!_inBattle) return;

            ApplyCurseOnCardUse(card);

            int frostBeforeCard = (_enemyStat != null && _enemyStat.statusEffects.TryGetValue("frost", out int _fb0)) ? _fb0 : 0;

            var result = _resolver.Resolve(card);

            card.selfUseCount++;

            // 바람314: 버린 더미 대신 패로 복귀 (소멸이 아닐 때만)
            if (result.returnToHandInsteadOfDiscard && !result.exile && !result.keepOnField)
            {
                if (!_deck.AddToHand(card)) _deck.AddToDiscard(card);
            }
            else
            {
                _deck.PlaceCardAfterUse(card, result.exile, result.keepOnField);

                // 바람318: 사용 후 버린 더미로 이동하는 카드 트리거
                if (!result.exile && !result.keepOnField)
                    _resolver.NotifyCardMovedToDiscard(card);
            }

            if (effectOverlay != null) effectOverlay.Play(card.data);

            if (result.exile) _resolver.NotifyExile(card);

            // 물12(211): 마지막 사용 카드 효과 재사용
            if (result.recastLastCard && _lastResolvedCard != null)
            {
                Log($"[재사용] {_lastResolvedCard.data.displayName} 효과 한 번 더");
                _resolver.Resolve(_lastResolvedCard);
            }

            // 패에서 카드 선택 → 소멸 (불4/103, 불13/112, 물10/209)
            if (result.requiresHandExileSelection && handHud != null && _deck.Hand.Count > 0)
            {
                var filter = BuildHandCardFilter(result.handSelectionCardFilter);
                if (HandHasMatch(filter))
                {
                    StartCoroutine(DeferredSelection("패에서 소멸할 카드를 선택하세요",
                        (selected) =>
                        {
                            if (selected == null) return;
                            _deck.ExileFromHand(selected);
                            _resolver.NotifyExile(selected);
                        }, filter));
                }
                else Log("[효과] 소멸 가능한 카드 없음 — 건너뜀");
            }

            // 패에서 카드 선택 → 버리기 (물8/207)
            if (result.requiresHandDiscardSelection && handHud != null && _deck.Hand.Count > 0)
            {
                var filter = BuildHandCardFilter(result.handSelectionCardFilter);
                if (HandHasMatch(filter))
                {
                    StartCoroutine(DeferredSelection("패에서 버릴 카드를 선택하세요",
                        (selected) =>
                        {
                            if (selected == null) return;
                            _deck.DiscardCardFromHand(selected);
                            if (_ctx.discardToDrawActive) _deck.Draw(1);
                        }, filter));
                }
                else Log("[효과] 버릴 카드 없음 — 건너뜀");
            }

            // 패에서 카드 선택 → 복사하여 패에 추가 (물14/213)
            if (result.requiresHandCopySelection && handHud != null && _deck.Hand.Count > 0)
            {
                var filter = BuildHandCardFilter(result.handSelectionCardFilter);
                if (HandHasMatch(filter))
                {
                    StartCoroutine(DeferredSelection("복사할 카드를 선택하세요",
                        (selected) =>
                        {
                            if (selected == null) return;
                            var copy = new CardInstance(selected.data, transient: true);
                            if (!_deck.AddToHand(copy)) _deck.AddToDiscard(copy);
                        }, filter));
                }
                else Log("[효과] 복사 가능한 카드 없음 — 건너뜀");
            }

            // 뽑을 더미에서 카드 선택 → 패로 (물7/206)
            if (result.requiresDrawPileMoveSelection && handHud != null)
            {
                StartCoroutine(DeferredPicker("뽑을 더미에서 카드를 선택하세요",
                    new List<CardInstance>(_deck.DrawPile),
                    (picked) => { if (picked != null) _deck.MoveFromDrawPileToHand(picked); }));
            }

            // 버린 더미에서 카드 선택 → 뽑을 더미 맨 위 (물11/210)
            if (result.requiresDiscardMoveSelection && handHud != null)
            {
                StartCoroutine(DeferredPicker("버린 더미에서 카드를 선택하세요",
                    new List<CardInstance>(_deck.DiscardPile),
                    (picked) => { if (picked != null) _deck.MoveFromDiscardToDrawPileTop(picked); }));
            }

            // 파편 풀에서 선택 (땅10/409, 땅15/414)
            if (result.requiresFragmentPoolSelection && handHud != null)
            {
                var pool = new List<CardInstance>();
                foreach (var fid in CardDatabase.FragmentIds)
                {
                    var preview = CardDatabase.CreateFragmentInstance(fid);
                    if (preview != null) pool.Add(preview);
                }
                string zone = result.fragmentPickerTargetZone;
                int copies = Mathf.Max(1, result.fragmentPickerCopyCount);
                StartCoroutine(DeferredPicker("파편 카드를 선택하세요",
                    pool,
                    (picked) =>
                    {
                        if (picked == null) return;
                        for (int i = 0; i < copies; i++)
                        {
                            var inst = CardDatabase.CreateFragmentInstance(picked.Id);
                            if (inst == null) continue;
                            PlaceFragmentInZone(inst, zone);
                        }
                    }));
            }

            // 게이지 누적
            int gaugeCost = card.Gauge;
            if (result.currentCardFreeThisUse) gaugeCost = 0;
            if (_ctx.firstCardAfterAttackFreeActive && _ctx.firstCardAfterEnemyAttack)
                gaugeCost = 0;

            // 빙결 순서: 이 카드가 부여한 빙결은 게이지 처리 동안만 사용 전 빙결량으로 되돌렸다가 복원
            int frostAddedByCard = 0;
            if (_enemyStat != null && _enemyStat.statusEffects.TryGetValue("frost", out int frostAfterCard))
            {
                frostAddedByCard = Mathf.Max(0, frostAfterCard - frostBeforeCard);
                if (frostAddedByCard > 0) _enemyStat.statusEffects["frost"] = frostBeforeCard;
            }

            AccrueEnemyGauge(gaugeCost);

            if (frostAddedByCard > 0 && _enemyStat != null)
            {
                int frostRemaining = _enemyStat.statusEffects.TryGetValue("frost", out int fr) ? fr : 0;
                _enemy?.SetStatus("frost", frostRemaining + frostAddedByCard);
            }

            if (result.skipNextGaugeCost) _skipNextGaugeCount++;

            UpdateUsageStats(card);

            if (card.data.comboSlot) AccumulateAwakenInput();

            // 각성 발동이 보류됐다면 — 선택이 없으면 곧, 선택 카드면 선택 완료 후 발동
            if (_awakenPending) StartCoroutine(DeferredPendingAwakenCheck());

            if (card.Id != 211) _lastResolvedCard = card;
            _ctx.previousCardType = card.Type;
            _ctx.firstCardAfterEnemyAttack = false;
            _ctx.currentCardJustDrawn = false;

            // "방금 뽑은" 표시는 한 번이라도 카드를 쓰면 만료
            for (int i = 0; i < _deck.Hand.Count; i++)
                _deck.Hand[i].justDrawn = false;
            if (card != null) card.justDrawn = false;

            Log($"{card.data.displayName} 사용 — 게이지+{gaugeCost}, 연쇄={_ctx.chainCount}");
        }

        // 적 게이지 누적 (각성 중·바람14 스킵 처리 포함)
        void AccrueEnemyGauge(int amount)
        {
            if (_awakenActive) return;

            if (_skipNextGaugeCount > 0)
            {
                _skipNextGaugeCount--;
                Log("[게이지] 다음 카드 게이지 소모 스킵 (바람14)");
                return;
            }

            if (_enemyStat == null) return;
            for (int i = 0; i < amount; i++)
            {
                _enemyStat.ConsumeGaugeStep();
                // 패의 모든 카드 gaugeSinceDrawn 누적 (땅6/405, 땅16/415 공식용)
                for (int h = 0; h < _deck.Hand.Count; h++)
                    _deck.Hand[h].gaugeSinceDrawn++;
            }
        }

        // 각성 발동용 속성 입력 누적 — 임계 도달 시 발동 보류
        void AccumulateAwakenInput()
        {
            if (_awakenActive) return;

            _elementInputCount++;
            if (_elementInputCount >= EffectiveAwakenInput)
                _awakenPending = true;
        }

        // 보류된 각성을 실제로 발동(선택 진행 중이면 대기)
        void TryActivatePendingAwaken()
        {
            if (!_awakenPending || _awakenActive || !_inBattle) return;
            if (handHud != null && (handHud.IsSelectionMode || handHud.IsPickerMode)) return;
            _awakenPending = false;
            ActivateAwaken();
        }

        // 각성 발동 — 지속시간 계산·스냅샷·무속성 격리·콤보 UI 활성화
        void ActivateAwaken()
        {
            _awakenActive = true;
            RunDeckState.Instance?.RecordAwakenActivation();
            _elementInputCount = 0;
            int ownedComboCount = ownedComboSkills != null ? ownedComboSkills.Count : 0;
            float awakenDuration = AWAKEN_DURATION_SECONDS + ownedComboCount * AWAKEN_DURATION_PER_OWNED_COMBO_SECONDS;
            _awakenTimeRemaining = awakenDuration;
            _awakenMaxTime = awakenDuration;

            // 각성 직전 패/뽑을 더미/버린 더미 스냅샷 — 각성 종료 시 그대로 복원
            _awakenSnapshotHand = new List<CardInstance>(_deck.Hand);
            _awakenSnapshotDraw = new List<CardInstance>(_deck.DrawPile);
            _awakenSnapshotDiscard = new List<CardInstance>(_deck.DiscardPile);

            // 무속성 카드 임시 격리 (각성 중 속성 카드만 순환)
            _awakenStoredNeutralCards = _deck.ExtractAllNeutralCards();

            // 격리된 카드 중 파편 수 기록
            _awakenFragmentExhausted = 0;
            foreach (var c in _awakenStoredNeutralCards)
                if (c != null && c.Element == CardElement.Fragment) _awakenFragmentExhausted++;
            _comboSelfUseCount.Clear();

            _deck.Draw(_deck.HandLimit);

            _comboInput.Clear();
            _comboCooldown = new int[ownedComboSkills != null ? ownedComboSkills.Count : 0];
            _queuedComboSkills.Clear();
            _awakenInputHistory.Clear();

            if (handHud != null)
            {
                handHud.SetAwakenMode(true);
                handHud.UpdateComboSlot(_comboInput);
                handHud.UpdateComboSkillList(ownedComboSkills, _comboCooldown);
                handHud.UpdateAwakenInputHistory(_awakenInputHistory);
            }

            if (awakenTimerGauge != null)
            {
                awakenTimerGauge.Show();
                awakenTimerGauge.SetTime(_awakenTimeRemaining, _awakenMaxTime);
            }

            if (effectOverlay != null && awakenScreenEffectPrefab != null)
                _awakenScreenEffectInstance = effectOverlay.PlayPrefabPersistent(awakenScreenEffectPrefab, awakenScreenEffectScale);

            Log($"[각성] 발동! 격리된 무속성={_awakenStoredNeutralCards.Count}, 지속 시간={awakenDuration:F1}s (기본 {AWAKEN_DURATION_SECONDS:F0}s + 보유콤보 {ownedComboCount}×{AWAKEN_DURATION_PER_OWNED_COMBO_SECONDS:F0}s, 콤보 매칭 시 +{COMBO_BONUS_SECONDS}s, 종료 시 일괄 발동)");
        }

        // 각성 종료 — 더미 복원·큐 콤보 일괄 발동·이펙트/UI 정리
        void EndAwaken()
        {
            _awakenActive = false;
            _awakenTimeRemaining = 0f;
            _awakenMaxTime = 0f;
            if (awakenTimerGauge != null) awakenTimerGauge.Hide();

            if (effectOverlay != null) effectOverlay.StopPersistent(_awakenScreenEffectInstance);
            _awakenScreenEffectInstance = null;

            // 각성 직전 상태로 패/뽑을 더미/버린 더미 복원 (격리 카드 포함)
            _deck.RestorePiles(_awakenSnapshotHand, _awakenSnapshotDraw, _awakenSnapshotDiscard);
            _awakenSnapshotHand = null;
            _awakenSnapshotDraw = null;
            _awakenSnapshotDiscard = null;
            _awakenStoredNeutralCards.Clear();

            if (_queuedComboSkills.Count > 0)
            {
                // Damage 콤보 수를 먼저 세어 이펙트 좌우 분산에 사용
                int damageCount = 0;
                for (int i = 0; i < _queuedComboSkills.Count; i++)
                    if (_queuedComboSkills[i] != null && _queuedComboSkills[i].effect == ComboEffectType.Damage)
                        damageCount++;

                Log($"[각성] 종료 — 콤보 {_queuedComboSkills.Count}건 일괄 발동 (Damage {damageCount}건)");

                // DB 콤보 반복 공식용: 이번 각성 발동 콤보 총수 + refComboId별 발동 횟수
                _awakenComboCountThisEnd = _queuedComboSkills.Count;
                _comboSelfUseCount.Clear();
                foreach (var s in _queuedComboSkills)
                {
                    if (s == null || !s.fromDatabase) continue;
                    _comboSelfUseCount.TryGetValue(s.refComboId, out int c);
                    _comboSelfUseCount[s.refComboId] = c + 1;
                }

                int damageIndex = 0;
                for (int i = 0; i < _queuedComboSkills.Count; i++)
                {
                    var skill = _queuedComboSkills[i];
                    if (skill == null) continue;
                    ActivateComboSkill(skill);

                    if (skill.effect == ComboEffectType.Damage)
                    {
                        if (effectOverlay != null && !string.IsNullOrEmpty(awakenDamageEffectName))
                        {
                            Vector2 offset = ComputeAwakenDamageOffset(damageIndex, damageCount);
                            effectOverlay.PlayByNameAtOffset(awakenDamageEffectName, offset);
                        }
                        damageIndex++;
                    }
                }

                // Damage 콤보가 1건 이상이면 임팩트 강조용 셰이크 (콤보 수 비례, 상한 있음)
                if (damageCount > 0 && damageOverlay != null && awakenShakeIntensityPerHit > 0f)
                {
                    float intensity = Mathf.Min(damageCount * awakenShakeIntensityPerHit, awakenShakeIntensityMax);
                    damageOverlay.TriggerShake(intensity);
                }

                // 이무기의 여의주: 성공 콤보 수만큼 각성 게이지 회복 (즉시 재발동 방지 max-1 상한)
                if (RelicManager.Instance != null &&
                    RelicManager.Instance.HasEffect(RelicEffectType.AwakenGaugeRecoverPerCombo))
                {
                    int recover = Mathf.Min(_queuedComboSkills.Count, EffectiveAwakenInput - 1);
                    _elementInputCount = recover;
                    Log($"[유물] 이무기의 여의주 — 각성 게이지 {_elementInputCount}/{EffectiveAwakenInput} 회복");
                }

                _queuedComboSkills.Clear();
            }
            else
            {
                Log("[각성] 종료 — 발동된 콤보 없음");
            }

            _comboInput.Clear();
            _comboCooldown = System.Array.Empty<int>();
            _awakenInputHistory.Clear();

            if (handHud != null)
            {
                handHud.SetAwakenMode(false);
                handHud.UpdateComboSlot(_comboInput);
                handHud.UpdateComboSkillList(ownedComboSkills, _comboCooldown);
                handHud.UpdateAwakenInputHistory(_awakenInputHistory);
            }
        }

        // 콤보 슬롯에 속성 추가(윈도우 3장 유지) + 히스토리 누적
        void AddToComboSlot(CardElement element)
        {
            _comboInput.Add(element);
            if (_comboInput.Count > 3) _comboInput.RemoveAt(0);
            _awakenInputHistory.Add(element);
        }

        // 쿨다운 중인 콤보들의 남은 횟수를 1씩 감소
        void TickComboCooldowns()
        {
            for (int i = 0; i < _comboCooldown.Length; i++)
                if (_comboCooldown[i] > 0) _comboCooldown[i]--;
        }

        // 콤보 매칭 — 매칭 시 큐에 적재·쿨다운 부여, 매칭 여부 반환(시간 보너스용)
        bool TryActivateCombo()
        {
            if (_comboInput.Count < 3) return false;
            if (ownedComboSkills == null) return false;

            for (int i = 0; i < ownedComboSkills.Count; i++)
            {
                if (i < _comboCooldown.Length && _comboCooldown[i] > 0) continue;
                var skill = ownedComboSkills[i];
                if (skill == null) continue;
                if (skill.Matches(_comboInput))
                {
                    _queuedComboSkills.Add(skill);
                    if (i < _comboCooldown.Length) _comboCooldown[i] = COMBO_REUSE_INPUT;
                    Log($"[콤보 큐] {skill.displayName} ({skill.ComboString()}) — 각성 종료 시 발동 (대기 {_queuedComboSkills.Count}건, 재사용까지 {COMBO_REUSE_INPUT}입력)");
                    return true;
                }
            }
            return false;
        }

        // 콤보 스킬 1건 발동 — DB 콤보는 다중 효과, 레거시는 단일 효과
        void ActivateComboSkill(ComboSkillDef skill)
        {
            if (skill.fromDatabase)
            {
                Log($"[콤보 스킬] {skill.displayName} ({skill.ComboString()}, ref={skill.refComboId}) 발동 — 효과 {(skill.dbEffects != null ? skill.dbEffects.Count : 0)}건");
                FillComboContext(skill);
                _comboResolver.Resolve(skill, _comboCtx);
                return;
            }

            Log($"[콤보 스킬] {skill.displayName} ({skill.ComboString()}) 발동 — {skill.effect} {skill.amount}");

            switch (skill.effect)
            {
                case ComboEffectType.Damage:
                    if (_enemy != null) _enemy.TakeDamage(skill.amount);
                    // 데미지 이펙트는 EndAwaken이 분산+셰이크와 함께 처리하므로 여기선 띄우지 않음
                    break;
                case ComboEffectType.Burn:
                    if (_enemy != null) _enemy.AddStatus("burn", skill.amount);
                    break;
                case ComboEffectType.Guard:
                    if (_player != null) _player.AddGuard(skill.amount);
                    break;
                case ComboEffectType.Heal:
                    if (_player != null) _player.Heal(skill.amount);
                    break;
                case ComboEffectType.Draw:
                    _deck.Draw(skill.amount);
                    break;
            }
        }

        // DB 콤보 효과 실행 직전 런타임 참조/카운터를 채움
        void FillComboContext(ComboSkillDef skill)
        {
            _comboCtx.enemy = _enemy;
            _comboCtx.enemyStat = _enemyStat;
            _comboCtx.player = _player;
            _comboCtx.deck = _deck;

            _comboCtx.usedFireCardCount = _usedFireCardCount;
            _comboCtx.usedFragmentCardCount = _ctx.usedFragmentCardCount;
            _comboCtx.playerChain = _ctx.chainCount;
            _comboCtx.awakenComboCount = _awakenComboCountThisEnd;
            _comboCtx.awakenFragmentExhausted = _awakenFragmentExhausted;
            _comboSelfUseCount.TryGetValue(skill.refComboId, out int selfUse);
            _comboCtx.selfRepeatCount = selfUse;

            // GAIN_STATUS CHAIN → 연쇄 누적 (SyncChainStatus가 UI 반영)
            _comboCtx.onGainChain = n => { _ctx.chainCount += n; };
        }

        // AI 미사용/폴백 — 가용 행동 중 무작위 1개 선택
        int RandomFallbackPick(bool recoverReady)
        {
            var pool = new List<int> { 0, 1, 2, 3 };
            if (recoverReady) pool.Add(4);
            if (_deck.HandCount >= 2) pool.Add(5);
            return pool[Random.Range(0, pool.Count)];
        }

        // 적 방해행동 발동 — AI(또는 랜덤)로 1개 선택 후 분기 실행, 보상 추적 시작
        public string TriggerDisruption()
        {
            bool recoverReady = _recoverCooldown <= 0;
            int pick;
            Battle.AI.AiDecision? rewardDecision = null;
            if (useAiDisruption && Battle.AI.EnemyAiModel.Loaded)
            {
                try
                {
                    // self-state(상태의존 마스크용): 이미 건 저주/강화, 현재 각성 게이지
                    bool curseActive = _cursedElement.HasValue;
                    bool enhanceActive = _enemy != null && _enemy.IsNextAttackBuffed;
                    var decision = Battle.AI.EnemyDisruptionAI.Decide(_deck, _enemyStat, _player, recoverReady, aiExplorationEpsilon, onlineLearning, curseActive, enhanceActive, _elementInputCount);
                    pick = decision.action;
                    if (logVerbose)
                        Log($"[적AI] μ=[{string.Join(",", System.Array.ConvertAll(decision.membership, v => v.ToString("F2")))}]" +
                            $" → 선택={Battle.AI.EnemyAiModel.ActionNames[pick]}{(decision.explored ? " (탐험)" : "")}");
                    if (logAiDecisions) Battle.AI.DisruptionLogger.Log(decision);
                    rewardDecision = decision;
                    if (aiRewardGamma > 0f) TdOnDecision(decision);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[적AI] 추론 실패 → 랜덤 폴백: {e.Message}");
                    pick = RandomFallbackPick(recoverReady);
                }
            }
            else
            {
                pick = RandomFallbackPick(recoverReady);
            }

            // 회복 쿨다운 진행 — 회복이 아닌 행동이 발동되면 1 감소
            if (pick != 4 && _recoverCooldown > 0) _recoverCooldown--;

            switch (pick)
            {
                case 0: // 저주 — 한 속성 카드 사용 시 플레이어 피해(2회 지속)
                {
                    CardElement[] elements = { CardElement.Fire, CardElement.Water, CardElement.Wind, CardElement.Earth };
                    CardElement target = elements[Random.Range(0, elements.Length)];
                    _cursedElement = target;
                    _curseRemainingUses = CURSE_DURATION_USES;
                    handHud?.Refresh();
                    if (rewardDecision.HasValue) BeginTrack(rewardDecision.Value, 0);
                    Log($"[방해] {target} 속성 저주 — 카드 사용 {CURSE_DURATION_USES}회 동안 지속");
                    return $"방해: {ElementName(target)} 저주!";
                }
                case 1: // 탈진 — 무속성 더미 카드(500)를 뽑을 더미에 섞어 넣음
                {
                    var dummy = CardDatabase.CreateNeutralFillerInstance();
                    if (dummy != null) _deck.AddToDrawShuffled(dummy);
                    handHud?.Refresh();
                    if (rewardDecision.HasValue) { BeginTrack(rewardDecision.Value, 1); if (_track != null) _track.exhaustCard = dummy; }
                    Log("[방해] 탈진 — 무속성 카드 1장 뽑을 더미에 삽입");
                    return "방해: 탈진 카드 삽입!";
                }
                case 2: // 흡수 — 각성 게이지(누적 속성 카드 수) -2  [즉시형]
                {
                    int before = _elementInputCount;
                    _elementInputCount = Mathf.Max(0, _elementInputCount - 2);
                    UpdateAwakenText();
                    if (rewardDecision.HasValue) ObserveDisruption(rewardDecision.Value, 2, AbsorbReward(before));
                    Log($"[방해] 흡수 — 각성 게이지 -2 ({before} → {_elementInputCount})");
                    return "방해: 각성 게이지 흡수!";
                }
                case 3: // 강화 — 적 다음 공격 1회 증가(+50%)
                {
                    _enemy?.BuffNextAttack();
                    if (rewardDecision.HasValue) { BeginTrack(rewardDecision.Value, 3); if (_track != null) _track.playerHpAtFire = _player != null ? _player.currentHp : 0; }
                    Log("[방해] 강화 — 적 다음 공격 피해 증가(1회)");
                    return "방해: 적 강화!";
                }
                case 4: // 회복 — 적 HP 30% 회복, 재사용까지 다른 방해행동 2회 필요  [즉시형]
                {
                    _recoverCooldown = 2;
                    float enemyHpBefore = _enemyStat != null ? _enemyStat.currentHp : 0f;
                    if (_enemyStat != null)
                    {
                        float heal = _enemyStat.maxHp * 0.3f;
                        _enemyStat.Heal(heal);
                        Log($"[방해] 회복 — 적 HP +{heal:F0} (최대 30%)");
                    }
                    if (rewardDecision.HasValue) ObserveDisruption(rewardDecision.Value, 4, RecoverReward(enemyHpBefore));
                    return "방해: 적 체력 회복!";
                }
                default: // 5 버리기 — 플레이어가 패에서 2장 직접 선택해 버림  [즉시형]
                {
                    int handBefore = _deck != null ? _deck.HandCount : 0;
                    int n = Mathf.Min(2, _deck.HandCount);
                    if (n > 0) StartCoroutine(EnemyForcedDiscardRoutine(n));
                    if (rewardDecision.HasValue) ObserveDisruption(rewardDecision.Value, 5, DiscardReward(handBefore));
                    Log($"[방해] 버리기 — 플레이어가 패 {n}장 선택해 버림");
                    return "방해: 카드 버리기!";
                }
            }
        }

        // 방해(버리기): 플레이어가 패에서 count장을 직접 선택해 버림(선택 모드 연쇄)
        IEnumerator EnemyForcedDiscardRoutine(int count)
        {
            int done = 0;
            while (done < count && _deck.HandCount > 0 && handHud != null)
            {
                yield return null;
                if (_deck.HandCount == 0) break;
                bool picked = false;
                int idx = done + 1;
                handHud.EnterSelectionMode($"버릴 카드를 선택하세요 ({idx}/{count})", card =>
                {
                    if (card != null) _deck.DiscardCardFromHand(card);
                    picked = true;
                }, filter: null);
                while (!picked) yield return null;
                done++;
            }
        }

        // 지연형(저주/탈진/강화) 추적 시작 (직전 미발현 추적은 먼저 마감)
        void BeginTrack(Battle.AI.AiDecision decision, int action)
        {
            ResolveTrackUnfired();
            _track = new DisruptionTrack { decision = decision, action = action };
        }

        // 즉시형(흡수/회복/버리기): 발동 시점 가치로 보상 확정 후 학습/게시
        void ObserveDisruption(Battle.AI.AiDecision decision, int action, float reward)
        {
            reward = Mathf.Clamp(reward, -1f, 1f);
            if (aiRewardGamma > 0f) { TdSetReward(decision, reward, immediate: true); return; }
            if (onlineLearning) Battle.AI.OnlineQLearner.Observe(decision.membership, decision.context, action, reward);
            float drift = Battle.AI.OnlineQLearner.WeightDriftFromBase();
            Battle.AI.AiDebug.PublishReward(action, reward, true, drift);
            if (logVerbose)
                Log($"[적AI{(onlineLearning ? "학습" : "관찰")}] {Battle.AI.EnemyAiModel.ActionNames[action]} r={reward:+0.00;-0.00} (즉시) drift={drift:F3}");
        }

        // 지연형 발현 → 보상 확정 후 학습/게시
        void ResolveTrack(float reward)
        {
            if (_track == null) return;
            var t = _track; _track = null;
            reward = Mathf.Clamp(reward, -1f, 1f);
            if (aiRewardGamma > 0f) { TdSetReward(t.decision, reward, immediate: false); return; }
            if (onlineLearning) Battle.AI.OnlineQLearner.Observe(t.decision.membership, t.decision.context, t.action, reward);
            float drift = Battle.AI.OnlineQLearner.WeightDriftFromBase();
            Battle.AI.AiDebug.PublishReward(t.action, reward, false, drift);
            if (logVerbose)
                Log($"[적AI{(onlineLearning ? "학습" : "관찰")}] {Battle.AI.EnemyAiModel.ActionNames[t.action]} r={reward:+0.00;-0.00} (발현) drift={drift:F3}");
        }

        // 새 방해결정: 직전 전이들의 s'를 채우고 완성분 학습 후 새 전이 적재
        void TdOnDecision(Battle.AI.AiDecision decision)
        {
            for (int i = 0; i < _tdQueue.Count; i++)
                if (!_tdQueue[i].hasNext)
                {
                    _tdQueue[i].nextMu = decision.membership;
                    _tdQueue[i].nextCtx = decision.context;
                    _tdQueue[i].hasNext = true;
                }
            TdFlush();
            _tdQueue.Add(new TdTransition { mu = decision.membership, ctx = decision.context, action = decision.action });
        }

        // 같은 결정의 전이에 보상을 채움(완성되면 학습)
        void TdSetReward(Battle.AI.AiDecision decision, float reward, bool immediate)
        {
            for (int i = 0; i < _tdQueue.Count; i++)
                if (!_tdQueue[i].hasReward && ReferenceEquals(_tdQueue[i].mu, decision.membership))
                {
                    _tdQueue[i].reward = reward;
                    _tdQueue[i].hasReward = true;
                    _tdQueue[i].immediate = immediate;
                    break;
                }
            TdFlush();
        }

        // 보상과 s'가 모두 확정된 전이를 TD 학습 후 큐에서 제거
        void TdFlush()
        {
            for (int i = _tdQueue.Count - 1; i >= 0; i--)
            {
                var t = _tdQueue[i];
                if (!(t.hasReward && t.hasNext)) continue;
                if (onlineLearning) Battle.AI.OnlineQLearner.ObserveTD(t.mu, t.ctx, t.action, t.reward, t.nextMu, t.nextCtx, aiRewardGamma);
                float drift = Battle.AI.OnlineQLearner.WeightDriftFromBase();
                Battle.AI.AiDebug.PublishReward(t.action, t.reward, t.immediate, drift);
                if (logVerbose)
                    Log($"[적AI{(onlineLearning ? "학습" : "관찰")}] {Battle.AI.EnemyAiModel.ActionNames[t.action]} r={t.reward:+0.00;-0.00} " +
                        $"(TD γ={aiRewardGamma:F2}{(t.immediate ? ", 즉시" : ", 발현")}) drift={drift:F3}");
                _tdQueue.RemoveAt(i);
            }
        }

        // 전투 종료/리셋: 남은 전이를 터미널(s'=null)로 학습, 보상 미확정분은 버림
        void TdFlushTerminal()
        {
            for (int i = 0; i < _tdQueue.Count; i++)
            {
                var t = _tdQueue[i];
                if (!t.hasReward) continue;
                if (onlineLearning) Battle.AI.OnlineQLearner.ObserveTD(t.mu, t.ctx, t.action, t.reward, null, null, aiRewardGamma);
                float drift = Battle.AI.OnlineQLearner.WeightDriftFromBase();
                Battle.AI.AiDebug.PublishReward(t.action, t.reward, t.immediate, drift);
                if (logVerbose)
                    Log($"[적AI학습] {Battle.AI.EnemyAiModel.ActionNames[t.action]} r={t.reward:+0.00;-0.00} (TD 터미널) drift={drift:F3}");
            }
            _tdQueue.Clear();
        }

        // 발현 못 하고 끝난 지연형 추적 마감(저주는 자해/회피분, 그 외 미발현 음수)
        void ResolveTrackUnfired()
        {
            if (_track == null) return;
            if (_track.action == 0) ResolveTrack(CurseReward(_track.curseHits));
            else ResolveTrack(UNFIRED_REWARD);
        }

        // 지연형 추적 경과 시간 누적, 타임아웃 시 미발현 마감
        void TickDisruptionTrack()
        {
            if (_track == null) return;
            _track.elapsed += Time.deltaTime;
            if (_track.elapsed >= disruptionTrackTimeout) ResolveTrackUnfired();
        }

        // 흡수 보상: 발동 직전 각성 근접도(임박할수록 가치↑)
        float AbsorbReward(int awakenBefore)
        {
            float prox = EffectiveAwakenInput > 0 ? Mathf.Clamp01(awakenBefore / (float)EffectiveAwakenInput) : 0f;
            return Mathf.Clamp(prox * 2f - 0.6f, -1f, 1f);
        }
        // 회복 보상: 발동 직전 적 빈사도(빈사일수록 가치↑, 만피는 음수)
        float RecoverReward(float enemyHpBefore)
        {
            float ratio = (_enemyStat != null && _enemyStat.maxHp > 0f) ? Mathf.Clamp01(enemyHpBefore / _enemyStat.maxHp) : 1f;
            return Mathf.Clamp((1f - ratio) * 2f - 1f, -1f, 1f);
        }
        // 버리기 보상: 발동 직전 손패 압박(가득 찰수록 가치↑, 한도 5 기준)
        float DiscardReward(int handBefore)
        {
            float pressure = Mathf.Clamp01(handBefore / 5f);
            return Mathf.Clamp(pressure * 2f - 0.8f, -1f, 1f);
        }
        // 저주 보상: 자해 유도 시 강한+, 회피(제약 성공)는 약한+ (HP 못 깎아도 실패 아님)
        float CurseReward(int hits)
        {
            if (hits <= 0) return 0.2f;
            return Mathf.Clamp(0.3f + hits * 0.45f, -1f, 1f);
        }
        // 강화 보상: 발현 자체 +0.4, 그 사이 받은 HP 피해만큼 가산
        float EnhanceReward(int playerHpAtFire)
        {
            float lost = (_player != null && _player.maxHp > 0)
                ? Mathf.Clamp01((playerHpAtFire - _player.currentHp) / (float)_player.maxHp) : 0f;
            return Mathf.Clamp(0.4f + lost * 3f, -1f, 1f);
        }

        // 외부(NewCardView)가 카드 속성이 저주되었는지 확인용
        public bool IsElementCursed(CardElement element)
        {
            return _cursedElement.HasValue && _cursedElement.Value == element && _curseRemainingUses > 0;
        }

        // 저주된 속성 카드 사용 시 플레이어 피해 적용(2회 동안 지속)
        void ApplyCurseOnCardUse(CardInstance card)
        {
            if (!_cursedElement.HasValue || _curseRemainingUses <= 0) return;

            if (card.Element == _cursedElement.Value && _player != null)
            {
                _player.TakeDamage(CURSE_PLAYER_DAMAGE);
                Log($"[저주] {_cursedElement.Value} 카드 사용 — 플레이어 {CURSE_PLAYER_DAMAGE} 피해");
                if (_track != null && _track.action == 0) _track.curseHits++;
            }

            _curseRemainingUses--;
            if (_curseRemainingUses <= 0)
            {
                Log($"[저주] {_cursedElement.Value} 저주 종료");
                _cursedElement = null;
                if (_track != null && _track.action == 0) ResolveTrack(CurseReward(_track.curseHits));
            }
        }

        // 속성을 한글 이름으로 변환
        static string ElementName(CardElement e) => e switch
        {
            CardElement.Fire => "불", CardElement.Water => "물",
            CardElement.Wind => "바람", CardElement.Earth => "땅", _ => "?"
        };

        // 각성 게이지 텍스트/게이지 갱신 및 HP바 앵커 재시도
        void UpdateAwakenText()
        {
            if (handHud == null) return;

            var activeCombatHpRect = ResolveActiveCombatHpBarRect();
            if (activeCombatHpRect != null)
                handHud.SetAwakenGaugeAnchor(activeCombatHpRect);

            int awakenMax = EffectiveAwakenInput;
            handHud.SetAwakenText($"{_elementInputCount}/{awakenMax}");
            handHud.UpdateAwakenGauge(_elementInputCount, awakenMax, false, 0f, 0f);
        }

        // 상세 로그 출력(logVerbose ON일 때만)
        void Log(string msg)
        {
            if (logVerbose) Debug.Log($"[NewBattle] {msg}");
        }

        // 각성 종료 시 데미지 콤보 N건을 좌우/상하로 분산할 오프셋 계산
        Vector2 ComputeAwakenDamageOffset(int index, int total)
        {
            if (total <= 1) return Vector2.zero;
            float lane = index - (total - 1) * 0.5f;
            float x = lane * awakenDamageEffectSpread.x;
            float y = ((index % 2 == 0) ? -1f : 1f) * awakenDamageEffectSpread.y;
            return new Vector2(x, y);
        }

        // 손패 HUD를 찾거나 전투 캔버스 하위에 생성
        CardHandHUD ResolveOrCreateHandHud()
        {
            var existing = FindFirstObjectByType<CardHandHUD>(FindObjectsInactive.Include);
            if (existing != null)
            {
                if (!existing.gameObject.activeSelf) existing.gameObject.SetActive(true);
                return existing;
            }

            Canvas combatCanvas = ResolveCombatCanvas();
            var hudGo = new GameObject("CardHandHUD", typeof(RectTransform));
            if (combatCanvas != null)
            {
                hudGo.transform.SetParent(combatCanvas.transform, false);
                var rect = (RectTransform)hudGo.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }
            return hudGo.AddComponent<CardHandHUD>();
        }

        // 전투 캔버스 탐색(CombatStage 하위 우선, 없으면 첫 캔버스)
        Canvas ResolveCombatCanvas()
        {
            GameObject combatStage = GameObject.Find("CombatStage");
            if (combatStage != null)
            {
                var c = combatStage.GetComponentInChildren<Canvas>(true);
                if (c != null) return c;
            }
            return FindFirstObjectByType<Canvas>();
        }

        // 활성 전투 HP바(PlayerHpBar)의 RectTransform 탐색(맵 HP바 오인식 방지)
        RectTransform ResolveActiveCombatHpBarRect()
        {
            GameObject combatStage = GameObject.Find("CombatStage");
            if (combatStage != null)
            {
                var stageSliders = combatStage.GetComponentsInChildren<Slider>(true);
                foreach (var slider in stageSliders)
                {
                    if (slider == null) continue;
                    if (slider.gameObject.name != "PlayerHpBar") continue;
                    if (!slider.gameObject.activeInHierarchy) continue;

                    var rect = slider.GetComponent<RectTransform>();
                    if (rect != null) return rect;
                }
            }

            Canvas combatCanvas = ResolveCombatCanvas();
            if (combatCanvas != null)
            {
                var canvasSliders = combatCanvas.GetComponentsInChildren<Slider>(true);
                foreach (var slider in canvasSliders)
                {
                    if (slider == null) continue;
                    if (slider.gameObject.name != "PlayerHpBar") continue;
                    if (!slider.gameObject.activeInHierarchy) continue;

                    var rect = slider.GetComponent<RectTransform>();
                    if (rect != null) return rect;
                }
            }

            if (_player != null && _player.hpBar != null && _player.hpBar.gameObject.activeInHierarchy)
                return _player.hpBar.GetComponent<RectTransform>();

            return null;
        }

        // EventSystem이 없으면 생성해 보장
        void EnsureEventSystem()
        {
            var es = FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include);
            if (es != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent<UnityEngine.EventSystems.EventSystem>();
            go.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        // 카드 효과 오버레이를 찾거나 전투 캔버스 하위에 생성
        CardEffectOverlay ResolveOrCreateEffectOverlay()
        {
            var existing = FindFirstObjectByType<CardEffectOverlay>(FindObjectsInactive.Include);
            if (existing != null) return existing;

            Canvas combatCanvas = ResolveCombatCanvas();
            var go = new GameObject("CardEffectOverlay", typeof(RectTransform));
            if (combatCanvas != null)
            {
                go.transform.SetParent(combatCanvas.transform, false);
                var rect = (RectTransform)go.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.SetAsLastSibling();
            }
            return go.AddComponent<CardEffectOverlay>();
        }

        // 플레이어 피격 오버레이를 찾거나 전투 캔버스 하위에 생성
        PlayerDamageOverlay ResolveOrCreateDamageOverlay()
        {
            var existing = FindFirstObjectByType<PlayerDamageOverlay>(FindObjectsInactive.Include);
            if (existing != null) return existing;

            Canvas combatCanvas = ResolveCombatCanvas();
            var go = new GameObject("PlayerDamageOverlay", typeof(RectTransform));
            if (combatCanvas != null)
            {
                go.transform.SetParent(combatCanvas.transform, false);
                var rect = (RectTransform)go.transform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.SetAsLastSibling();
            }
            return go.AddComponent<PlayerDamageOverlay>();
        }
    }
}
