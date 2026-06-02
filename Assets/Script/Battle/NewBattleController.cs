using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Battle.Card;
using Battle.Deck;
using Battle.UI;

namespace Battle
{
    /// <summary>
    /// 새 전투 시스템 진입점.
    /// - 5장 손패, 드래그 사용
    /// - 카드별 게이지 → EnemyStat.ConsumeGaugeStep로 누적
    /// - D 드로우 / 각성(속성 카드 10장 사용 시 즉시 발동) / 속성 카드 입력 카운트
    /// 기존 ElementSlotSystem/ComboSystem과 공존하지 않는다(테스트 씬 전용).
    /// </summary>
    public class NewBattleController : MonoBehaviour
    {
        /// <summary>각성 발동에 필요한 속성 카드 사용 횟수 — 도달 시 즉시 발동.</summary>
        public const int AWAKEN_ACTIVATION_INPUT = 10;
        /// <summary>각성 발동 시 기본 지속 시간(초).</summary>
        public const float AWAKEN_DURATION_SECONDS = 10f;
        /// <summary>콤보 1회 성공 시 추가되는 시간(초).</summary>
        public const float COMBO_BONUS_SECONDS = 1f;
        /// <summary>콤보 스킬 발동 후 재사용까지 필요한 속성 카드 입력 횟수(발동 이후 카드부터 카운트).</summary>
        public const int COMBO_REUSE_INPUT = 5;
        public const int START_DRAW = 5;

        public static NewBattleController Instance { get; private set; }

        [Header("UI")]
        [SerializeField] private CardHandHUD handHud;
        [Tooltip("카드 사용 시 EffectName(예: Fire_ATK)에 해당하는 스프라이트 시트 애니메이션을 재생. " +
                 "비우면 자동으로 캔버스에 생성. 시트가 없으면 조용히 스킵.")]
        [SerializeField] private CardEffectOverlay effectOverlay;
        [Tooltip("플레이어 피격 시 화면 빨간 플래시. 비우면 자동 생성.")]
        [SerializeField] private PlayerDamageOverlay damageOverlay;

        [Header("보유 콤보 스킬 (각성 전용 / Inspector 편집)")]
        [SerializeField] private List<ComboSkillDef> ownedComboSkills = new List<ComboSkillDef>();
        public IReadOnlyList<ComboSkillDef> OwnedComboSkills => ownedComboSkills;

        [Header("각성 콤보 이펙트")]
        [Tooltip("각성 종료 시 Damage 콤보가 적에게 들어갈 때 재생할 이펙트 이름. " +
                 "Resources/CardEffects/{이름}.png 시트를 찾아 적 위치에서 재생. " +
                 "비우거나 시트가 없으면 조용히 스킵.")]
        [FormerlySerializedAs("feverDamageEffectName")]
        [SerializeField] private string awakenDamageEffectName = "Fever_ATK";
        [Tooltip("Damage 콤보가 N개 동시에 발동될 때 이펙트가 한 곳에 겹쳐 보이지 않도록 좌우/상하로 분산.\n" +
                 "X = 콤보 간 가로 간격, Y = zigzag 세로 진폭. 0으로 두면 분산 없이 한 곳.")]
        [FormerlySerializedAs("feverDamageEffectSpread")]
        [SerializeField] private Vector2 awakenDamageEffectSpread = new Vector2(220f, 80f);
        [Tooltip("각성 종료 시 데미지 콤보 1건당 더해질 셰이크 강도 배율. 콤보가 많을수록 더 세게 흔들림.\n" +
                 "0이면 셰이크 비활성.")]
        [FormerlySerializedAs("feverShakeIntensityPerHit")]
        [SerializeField] private float awakenShakeIntensityPerHit = 0.6f;
        [Tooltip("각성 셰이크 강도 상한 — 콤보가 너무 많아도 이 값을 넘지 않음.")]
        [FormerlySerializedAs("feverShakeIntensityMax")]
        [SerializeField] private float awakenShakeIntensityMax = 2.0f;

        [Header("디버그")]
        [SerializeField] private bool logVerbose = true;

        private readonly CardDeckSystem _deck = new CardDeckSystem();
        private readonly CardEffectResolver _resolver = new CardEffectResolver();
        private readonly CardEffectContext _ctx = new CardEffectContext();

        private Player _player;
        private EnemyController _enemy;
        private EnemyStat _enemyStat;

        private bool _inBattle;

        // 각성 게이지
        private int _elementInputCount;   // 각성 발동용 누적 (10 도달 시 즉시 발동)
        private bool _awakenActive;
        private bool _awakenPending;        // 발동 조건 충족(보류) — 선택 카드의 선택 완료 후 발동
        private float _awakenTimeRemaining; // 활성화 시 남은 시간(초). 0 이하가 되면 종료
        private float _awakenMaxTime;       // 콤보 보너스로 늘어나는 동적 최대치 (게이지 fill 계산용)
        private List<CardInstance> _awakenStoredNeutralCards = new List<CardInstance>();

        // 각성 직전 패/뽑을 더미/버린 더미 스냅샷 — 각성 종료 시 복원용
        private List<CardInstance> _awakenSnapshotHand;
        private List<CardInstance> _awakenSnapshotDraw;
        private List<CardInstance> _awakenSnapshotDiscard;

        // 콤보 슬롯 (각성 동안 입력된 속성, sliding window 3장)
        private readonly List<CardElement> _comboInput = new List<CardElement>();
        // 콤보 스킬 인덱스별 재사용 쿨다운(남은 속성 입력 횟수). 0이면 재사용 가능, >0이면 대기 중.
        // ownedComboSkills와 같은 인덱스로 정렬. ActivateAwaken에서 보유 콤보 수만큼 할당.
        private int[] _comboCooldown = System.Array.Empty<int>();
        // 각성 종료 시 일괄 발동할 콤보 스킬 큐 (매칭 순서대로 적재)
        private readonly List<ComboSkillDef> _queuedComboSkills = new List<ComboSkillDef>();
        // 각성 동안 입력된 속성 전체 기록(왼쪽 UI 표시용)
        private readonly List<CardElement> _awakenInputHistory = new List<CardElement>();

        // 방해 행동 — 속성 저주 (PDF: 한 속성 모든 카드, 플레이어 카드 사용 2회 동안 지속)
        public const int CURSE_DURATION_USES = 2;
        public const int CURSE_PLAYER_DAMAGE = 5;
        private CardElement? _cursedElement;
        private int _curseRemainingUses;

        // 바람14(313): 다음 카드 게이지 소모 스킵 횟수
        private int _skipNextGaugeCount;
        // 물12(211): 마지막으로 사용한 카드 (재사용 대상)
        private CardInstance _lastResolvedCard;

        public bool IsAwakenActive => _awakenActive;
        public CardDeckSystem Deck => _deck;
        public CardInstance LastResolvedCard => _lastResolvedCard;
        public CardElement? CursedElement => _cursedElement;
        /// <summary>불126: 적 행동 시 화상이 줄어들지 않는지 — EnemyController가 조회.</summary>
        public bool BurnPersistsOnEnemyTurn => _ctx != null && _ctx.burnPersistsOnEnemyTurn;
        /// <summary>각성 발동에 필요한 실제 속성 카드 수 (바람326 등 보정 반영, 최소 1).</summary>
        public int EffectiveAwakenInput =>
            Mathf.Max(1, AWAKEN_ACTIVATION_INPUT + (_ctx != null ? _ctx.awakenGaugeMaxDelta : 0));

        /// <summary>BattleTestController가 라운드 시작 전에 주입하는 사용자 정의 시작 덱.</summary>
        private List<CardInstance> _customStartingDeck;
        public void SetCustomStartingDeck(List<CardInstance> deck) => _customStartingDeck = deck;

        // ─────────────────────────────────────────────────────────────
        // 생명주기
        // ─────────────────────────────────────────────────────────────

        void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(gameObject); return; }
            _resolver.Bind(_ctx);

            // 카드가 패로 들어올 때 gaugeSinceDrawn 리셋 + USED_IMMEDIATELY_AFTER_DRAW 플래그
            _deck.OnCardDrawn += OnCardDrawnHandler;
            // 패에서 버려질 때 ON_SELF_DISCARDED 등 트리거
            _deck.OnCardDiscarded += OnCardDiscardedHandler;
        }

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

        void OnCardDrawnHandler(CardInstance card)
        {
            if (card == null) return;
            card.gaugeSinceDrawn = 0;
            card.justDrawn = true;
            _ctx.currentCardJustDrawn = true;
            // 바람319: 패에 들어올 때 트리거 (각성 중에는 효과가 드로우로 치환되므로 제외)
            if (_inBattle && !_awakenActive) _resolver.NotifyCardEnteredHand(card);
        }

        void OnCardDiscardedHandler(CardInstance card)
        {
            // 물205/206/219: 패에서 버려질 때 트리거 (각성 중 제외)
            if (_inBattle && !_awakenActive) _resolver.NotifyCardDiscardedFromHand(card);
        }

        void Update()
        {
            if (!_inBattle) return;

            // 적 참조 최신화
            if (_enemy == null || !_enemy.gameObject.activeInHierarchy)
            {
                _enemy = FindFirstObjectByType<EnemyController>();
                _enemyStat = _enemy != null ? _enemy.GetComponent<EnemyStat>() : null;
                _ctx.enemy = _enemy;
                _ctx.enemyStat = _enemyStat;
            }

            HandleInput();
            TickAwakenTimer();
            UpdateAwakenText();
            SyncChainStatus();
        }

        // 연쇄(chain) 스택을 플레이어 상태 패널에 표시 — Ctx.chainCount가 바뀔 때만 갱신.
        private int _lastChainShown = -1;
        void SyncChainStatus()
        {
            if (_player == null) return;
            if (_ctx.chainCount == _lastChainShown) return;
            _lastChainShown = _ctx.chainCount;
            _player.SetStatus("chain", _ctx.chainCount);
        }

        /// <summary>각성 지속 시간 카운트다운 (Update에서 매 프레임 호출).</summary>
        void TickAwakenTimer()
        {
            if (!_awakenActive) return;
            _awakenTimeRemaining -= Time.deltaTime;
            if (_awakenTimeRemaining <= 0f)
            {
                _awakenTimeRemaining = 0f;
                EndAwaken();
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 전투 진입/종료
        // ─────────────────────────────────────────────────────────────

        public void StartBattle()
        {
            _player = FindFirstObjectByType<Player>();
            _enemy = FindFirstObjectByType<EnemyController>();
            _enemyStat = _enemy != null ? _enemy.GetComponent<EnemyStat>() : null;

            _ctx.player = _player;
            _ctx.enemy = _enemy;
            _ctx.enemyStat = _enemyStat;
            _ctx.deck = _deck;
            ResetContextFlags();

            // 플레이어 피격/방어도 소모 이벤트 구독 (전투마다 갱신)
            if (_player != null)
            {
                _player.OnPlayerHit -= OnPlayerHitHandler;
                _player.OnPlayerHit += OnPlayerHitHandler;
                _player.OnBlockConsumedByAttack -= OnBlockConsumedHandler;
                _player.OnBlockConsumedByAttack += OnBlockConsumedHandler;
            }

            // 적 공격 직후 첫 카드 무료 트리거(바람20/319)용
            if (_enemyStat != null)
            {
                _enemyStat.OnGaugeFull -= OnEnemyAttackFiredHandler;
                _enemyStat.OnGaugeFull += OnEnemyAttackFiredHandler;
            }

            var startingDeck = _customStartingDeck ?? CardDatabase.BuildDefaultPrototypeDeck();
            _deck.StartBattle(startingDeck);

            if (handHud == null) handHud = ResolveOrCreateHandHud();
            if (handHud != null)
            {
                handHud.Bind(_deck);
                handHud.UseCardCallback = OnUseCardRequested;
                handHud.SelectionClosedCallback = TryActivatePendingAwaken; // 선택 완료 후 보류 각성 발동
                handHud.SetAwakenMode(false); // 전투 시작 시점에는 콤보 UI 숨김
            }
            else
            {
                Debug.LogWarning("[NewBattleController] handHud가 미연결입니다.");
            }

            // 카드 효과 이펙트 오버레이 — 비어 있으면 자동 생성
            if (effectOverlay == null) effectOverlay = ResolveOrCreateEffectOverlay();
            if (damageOverlay == null) damageOverlay = ResolveOrCreateDamageOverlay();

            EnsureEventSystem();

            _inBattle = true;
            _elementInputCount = 0;
            _awakenActive = false;
            _awakenPending = false;
            _awakenTimeRemaining = 0f;
            _awakenMaxTime = 0f;

            // 각성 게이지를 Player.hpBar 아래에 자동 배치
            if (handHud != null && _player != null && _player.hpBar != null)
                handHud.SetAwakenGaugeAnchor(_player.hpBar.GetComponent<RectTransform>());

            _deck.Draw(START_DRAW);
            UpdateAwakenText();
            Log($"전투 시작 — {START_DRAW}장 드로우");
        }

        public void EndBattle()
        {
            _inBattle = false;
            _awakenActive = false;
            _awakenPending = false;
            _deck.EndBattle();
            if (handHud != null)
            {
                handHud.UseCardCallback = null;
                handHud.SelectionClosedCallback = null;
            }
            Log("전투 종료");
        }

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

            // 신규 카드 효과 플래그 초기화
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

        void UpdateUsageStats(CardInstance card)
        {
            if (card == null) return;
            if (card.Type == CardType.Attack) _ctx.usedAttackCardCount++;
            if (card.Element == CardElement.Fragment) _ctx.usedFragmentCardCount++;

            string name = card.data.displayName ?? "";
            if (!string.IsNullOrEmpty(name))
            {
                _ctx.usedCardNameCounts.TryGetValue(name, out int cur);
                _ctx.usedCardNameCounts[name] = cur + 1;
            }
        }

        /// <summary>물15(214) — 플레이어에게 적용된 모든 저주 해제.</summary>
        public void ClearAllCurses()
        {
            bool hadCurse = _cursedElement.HasValue;
            _cursedElement = null;
            _curseRemainingUses = 0;
            handHud?.Refresh();
            if (hadCurse) Log("[저주] 해제됨");
        }

        /// <summary>EnemyController가 공격 종료 시 호출 — 다음 카드에 FIRST_CARD_AFTER_ENEMY_ATTACK 적용.</summary>
        public void NotifyEnemyAttackFinished()
        {
            _ctx.firstCardAfterEnemyAttack = true;
        }

        void OnPlayerHitHandler()
        {
            // 물18(217): 피격 시 회복 1
            if (_ctx.healOnPlayerHitActive && _ctx.player != null)
                _ctx.player.Heal(1);
        }

        void OnBlockConsumedHandler()
        {
            // 땅18(417): 방어도 소모 시 무작위 파편 1장 버린 더미에
            if (_ctx.fragmentOnBlockConsumeActive && _deck != null)
                _deck.AddToDiscard(CardDatabase.CreateFragmentInstance());
        }

        void OnEnemyAttackFiredHandler()
        {
            _ctx.firstCardAfterEnemyAttack = true;

            // 땅424: 적 공격 종료 후 방어도 획득
            if (_ctx.blockOnEnemyAttackActive && _ctx.blockOnEnemyAttackAmount > 0 && _player != null)
                _player.AddGuard(_ctx.blockOnEnemyAttackAmount);
        }

        /// <summary>
        /// 직전 효과(예: 207의 DRAW)의 시각 갱신이 먼저 반영되도록
        /// 다음 프레임에 선택 모드 진입. 동일 프레임 내 다중 OnPileChanged → 한꺼번에 렌더되는 문제 회피.
        /// </summary>
        IEnumerator DeferredSelection(string prompt,
            System.Action<CardInstance> callback,
            System.Func<CardInstance, bool> filter)
        {
            yield return null; // 1프레임 대기 — 드로우/배치 변화가 화면에 먼저 반영됨
            if (handHud != null) handHud.EnterSelectionMode(prompt, callback, filter: filter);
        }

        IEnumerator DeferredPicker(string prompt, IList<CardInstance> cards,
            System.Action<CardInstance> callback)
        {
            yield return null;
            if (handHud != null) handHud.EnterCardPickerMode(prompt, cards, callback);
        }

        /// <summary>카드 사용 후 보류된 각성을 (선택 모드 진입을 기다린 뒤) 발동 시도.</summary>
        IEnumerator DeferredPendingAwakenCheck()
        {
            yield return null; // DeferredSelection이 선택 모드를 켜는 프레임 이후까지 대기
            yield return null;
            TryActivatePendingAwaken();
        }

        /// <summary>필터에 매칭되는 카드가 패에 있는지 확인.</summary>
        bool HandHasMatch(System.Func<CardInstance, bool> filter)
        {
            if (filter == null) return _deck.Hand.Count > 0;
            for (int i = 0; i < _deck.Hand.Count; i++)
                if (filter(_deck.Hand[i])) return true;
            return false;
        }

        /// <summary>CardEffects의 cardFilter 문자열을 EnterSelectionMode용 delegate로.</summary>
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

        /// <summary>파편 인스턴스를 지정 존(HAND/DRAW_PILE_SHUFFLE/DISCARD_PILE_SHUFFLE)에 배치.</summary>
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

        // ─────────────────────────────────────────────────────────────
        // 입력
        // ─────────────────────────────────────────────────────────────

        void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.D)) TryDDraw();
            // 각성은 속성 카드 10장 사용 시 즉시 발동 — 별도 입력 키 없음.
        }

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

        // ─────────────────────────────────────────────────────────────
        // 카드 사용
        // ─────────────────────────────────────────────────────────────

        bool OnUseCardRequested(CardInstance card)
        {
            if (!_inBattle) return false;
            if (card == null) return false;

            // 각성 중: 모든 카드 효과를 '드로우 1'로 치환 + 콤보 슬롯 등록
            if (_awakenActive)
            {
                _deck.MoveAfterUse(card, exile: false, poweredField: false);
                _deck.Draw(1);

                bool comboTriggered = false;
                if (card.data.comboSlot)
                {
                    AddToComboSlot(card.Element);
                    // 이번 입력은 '발동 이후' 입력 — 직전까지 발동된 콤보들의 쿨다운을 1 감소시킨 뒤 매칭 시도.
                    TickComboCooldowns();
                    comboTriggered = TryActivateCombo();
                }

                // 콤보 매칭 시 지속 시간 +1초 — 매칭이 일어났을 때만 보너스
                // 게이지가 줄어들지 않도록 max도 함께 증가시킨다 (시각적으로 끝부분이 살짝 차오르는 효과)
                if (comboTriggered)
                {
                    float before = _awakenTimeRemaining;
                    _awakenTimeRemaining += COMBO_BONUS_SECONDS;
                    _awakenMaxTime += COMBO_BONUS_SECONDS;
                    Log($"[각성] ⏱ 콤보 매칭 → 시간 +{COMBO_BONUS_SECONDS:F1}s ({before:F2}s → {_awakenTimeRemaining:F2}s, max={_awakenMaxTime:F2}s)");
                }

                Log($"[각성] {card.data.displayName} → 콤보입력=[{string.Join(",", _comboInput)}] 콤보발동={comboTriggered} 남은시간={_awakenTimeRemaining:F2}s");

                if (handHud != null)
                {
                    handHud.UpdateComboSlot(_comboInput);
                    handHud.UpdateComboSkillList(ownedComboSkills, _comboCooldown);
                    handHud.UpdateAwakenInputHistory(_awakenInputHistory);
                }

                // 콤보는 쿨다운 후 재사용 가능하므로 '전부 소진' 종료 없음 — 시간 종료(TickAwakenTimer)로만 종료.
                return true;
            }

            // 일반: 효과 실행
            ApplyCurseOnCardUse(card);

            _deck.PullFromHand(card);

            // 빙결 순서 처리용: 카드 사용 '전' 적 빙결량 기록 (이 카드가 부여한 빙결은 이 카드의 게이지 상승을 막지 않음)
            int frostBeforeCard = (_enemyStat != null && _enemyStat.statusEffects.TryGetValue("frost", out int _fb0)) ? _fb0 : 0;

            var result = _resolver.Resolve(card);

            // 이 카드 사용 횟수 누적(바람314 N회 후 소멸 판정용 — Resolve가 직전 값을 읽고 판정함)
            card.selfUseCount++;

            // 바람314: 버린 더미 대신 패로 복귀 (소멸이 아닐 때만)
            if (result.returnToHandInsteadOfDiscard && !result.exile && !result.keepOnField)
            {
                if (!_deck.AddToHand(card)) _deck.AddToDiscard(card); // 패가 가득이면 버린 더미로 폴백
            }
            else
            {
                _deck.PlaceCardAfterUse(card, result.exile, result.keepOnField);

                // 바람318: 사용 후 버린 더미로 이동하는 카드의 ON_CARD_MOVED_TO_DISCARD 트리거
                if (!result.exile && !result.keepOnField)
                    _resolver.NotifyCardMovedToDiscard(card);
            }

            // 카드 사용 효과 애니메이션 (EffectName 기반 스프라이트 시트)
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
                            if (_ctx.discardToDrawActive) _deck.Draw(1); // 물21(220)
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
            if (result.currentCardFreeThisUse) gaugeCost = 0; // 바람11(310)
            if (_ctx.firstCardAfterAttackFreeActive && _ctx.firstCardAfterEnemyAttack)
                gaugeCost = 0; // 바람20(319)

            // 빙결 순서: 이 카드가 부여한 빙결은 '이 카드의 게이지 상승'을 막지 않도록,
            // 게이지 처리 동안만 카드 사용 전 빙결량으로 되돌렸다가 처리 후 복원.
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
                _enemy?.SetStatus("frost", frostRemaining + frostAddedByCard); // 카드가 부여한 빙결 복원 + UI 갱신
            }

            if (result.skipNextGaugeCost) _skipNextGaugeCount++;

            // 통계 갱신
            UpdateUsageStats(card);

            // 각성 입력 카운팅
            if (card.data.comboSlot) AccumulateAwakenInput();

            // 각성 발동이 보류됐다면 — 선택 카드가 아니면 곧 발동, 선택 카드면 선택 완료 후 발동.
            // (선택 모드는 DeferredSelection이 1프레임 뒤 진입하므로 2프레임 후 확인)
            if (_awakenPending) StartCoroutine(DeferredPendingAwakenCheck());

            // 마지막 카드 기록 (자기 복사 방지)
            if (card.Id != 211) _lastResolvedCard = card;
            _ctx.previousCardType = card.Type;
            _ctx.firstCardAfterEnemyAttack = false; // 카드 사용 후 리셋
            _ctx.currentCardJustDrawn = false;

            // "방금 뽑은" 표시는 한 번이라도 카드를 쓰면 만료 (310의 USED_IMMEDIATELY_AFTER_DRAW용)
            for (int i = 0; i < _deck.Hand.Count; i++)
                _deck.Hand[i].justDrawn = false;
            if (card != null) card.justDrawn = false;

            Log($"{card.data.displayName} 사용 — 게이지+{gaugeCost}, 연쇄={_ctx.chainCount}");
            return true;
        }

        // ─────────────────────────────────────────────────────────────
        // 적 게이지
        // ─────────────────────────────────────────────────────────────

        void AccrueEnemyGauge(int amount)
        {
            if (_awakenActive) return; // 각성 중에는 적 게이지 증가 X

            // 바람14(313): 다음 카드는 게이지 소모 스킵
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

        // ─────────────────────────────────────────────────────────────
        // 각성
        // ─────────────────────────────────────────────────────────────

        void AccumulateAwakenInput()
        {
            if (_awakenActive) return;

            _elementInputCount++;
            // 속성 카드 N장 사용 시 발동 (별도 입력 키 없음). N은 바람326 등으로 가변.
            // 단, 즉시 발동하지 않고 '보류'로 둔 뒤 — 선택 카드의 선택이 끝난 후 발동(TryActivatePendingAwaken).
            if (_elementInputCount >= EffectiveAwakenInput)
                _awakenPending = true;
        }

        /// <summary>
        /// 각성 발동 보류분을 실제로 발동. 단, 카드 선택/픽커가 진행 중이면 대기(선택 완료 후 다시 호출됨).
        /// 호출 시점: 카드 사용 종료 시(선택이 없을 때) + 선택/픽커 종료 콜백.
        /// </summary>
        void TryActivatePendingAwaken()
        {
            if (!_awakenPending || _awakenActive || !_inBattle) return;
            if (handHud != null && (handHud.IsSelectionMode || handHud.IsPickerMode)) return; // 선택 진행 중 — 대기
            _awakenPending = false;
            ActivateAwaken();
        }

        void ActivateAwaken()
        {
            _awakenActive = true;
            _elementInputCount = 0;
            _awakenTimeRemaining = AWAKEN_DURATION_SECONDS;
            _awakenMaxTime = AWAKEN_DURATION_SECONDS;

            // 각성 직전 패/뽑을 더미/버린 더미 스냅샷 — 각성 종료 시 그대로 복원.
            _awakenSnapshotHand = new List<CardInstance>(_deck.Hand);
            _awakenSnapshotDraw = new List<CardInstance>(_deck.DrawPile);
            _awakenSnapshotDiscard = new List<CardInstance>(_deck.DiscardPile);

            // 패/드로우/버린 더미의 무속성 카드 임시 격리 (각성 중 속성 카드만 순환)
            _awakenStoredNeutralCards = _deck.ExtractAllNeutralCards();

            // 패를 가득 차게 드로우 (기획서: 각성 발동 시 패 보충)
            _deck.Draw(_deck.HandLimit);

            // 콤보 입력/쿨다운/큐/히스토리 초기화
            _comboInput.Clear();
            _comboCooldown = new int[ownedComboSkills != null ? ownedComboSkills.Count : 0];
            _queuedComboSkills.Clear();
            _awakenInputHistory.Clear();

            // 콤보 슬롯/스킬 UI 활성화 및 갱신
            if (handHud != null)
            {
                handHud.SetAwakenMode(true);
                handHud.UpdateComboSlot(_comboInput);
                handHud.UpdateComboSkillList(ownedComboSkills, _comboCooldown);
                handHud.UpdateAwakenInputHistory(_awakenInputHistory);
            }

            Log($"[각성] 발동! 격리된 무속성={_awakenStoredNeutralCards.Count}, 지속 시간={AWAKEN_DURATION_SECONDS}s (콤보 매칭 시 +{COMBO_BONUS_SECONDS}s, 종료 시 일괄 발동)");
        }

        void EndAwaken()
        {
            _awakenActive = false;
            _awakenTimeRemaining = 0f;
            _awakenMaxTime = 0f;

            // 각성 직전 상태로 패/뽑을 더미/버린 더미 복원 (각성 중의 드로우·사용 churn 되돌림).
            // 격리했던 무속성/파편 카드도 스냅샷에 포함돼 있으므로 함께 복원됨.
            _deck.RestorePiles(_awakenSnapshotHand, _awakenSnapshotDraw, _awakenSnapshotDiscard);
            _awakenSnapshotHand = null;
            _awakenSnapshotDraw = null;
            _awakenSnapshotDiscard = null;
            _awakenStoredNeutralCards.Clear();

            // 큐에 쌓인 콤보 스킬 일괄 발동 (매칭 순서대로) — 복원 후 적용되므로 Draw 콤보 등은 복원된 덱에서 유효
            if (_queuedComboSkills.Count > 0)
            {
                // Damage 콤보를 먼저 세어두면, 이펙트 분산 시 i번째/총N개로 좌우 정렬 가능
                int damageCount = 0;
                for (int i = 0; i < _queuedComboSkills.Count; i++)
                    if (_queuedComboSkills[i] != null && _queuedComboSkills[i].effect == ComboEffectType.Damage)
                        damageCount++;

                Log($"[각성] 종료 — 콤보 {_queuedComboSkills.Count}건 일괄 발동 (Damage {damageCount}건)");

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

                // Damage 콤보가 1건 이상이면 데미지 임팩트 강조용 셰이크 (콤보 수에 비례, 상한 있음)
                if (damageCount > 0 && damageOverlay != null && awakenShakeIntensityPerHit > 0f)
                {
                    float intensity = Mathf.Min(damageCount * awakenShakeIntensityPerHit, awakenShakeIntensityMax);
                    damageOverlay.TriggerShake(intensity);
                }

                _queuedComboSkills.Clear();
            }
            else
            {
                Log("[각성] 종료 — 발동된 콤보 없음");
            }

            // 콤보 슬롯/입력/쿨다운/히스토리 초기화
            _comboInput.Clear();
            _comboCooldown = System.Array.Empty<int>();
            _awakenInputHistory.Clear();

            // 콤보 슬롯/스킬 UI 비활성화
            if (handHud != null)
            {
                handHud.SetAwakenMode(false);
                handHud.UpdateComboSlot(_comboInput);
                handHud.UpdateComboSkillList(ownedComboSkills, _comboCooldown);
                handHud.UpdateAwakenInputHistory(_awakenInputHistory);
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 콤보 매칭
        // ─────────────────────────────────────────────────────────────

        void AddToComboSlot(CardElement element)
        {
            _comboInput.Add(element);
            if (_comboInput.Count > 3) _comboInput.RemoveAt(0);
            // 전체 히스토리(왼쪽 표시용)에도 누적
            _awakenInputHistory.Add(element);
        }

        /// <summary>발동 이후 입력 1회 — 쿨다운 중인 콤보들의 남은 횟수를 1씩 감소(0 미만 방지).</summary>
        void TickComboCooldowns()
        {
            for (int i = 0; i < _comboCooldown.Length; i++)
                if (_comboCooldown[i] > 0) _comboCooldown[i]--;
        }

        /// <summary>
        /// 콤보 매칭. 매칭되면 큐에 적재(즉시 발동 X). 각성 종료 시 일괄 발동.
        /// 쿨다운(>0)인 콤보는 매칭 제외, 매칭 시 COMBO_REUSE_INPUT 만큼 쿨다운 부여.
        /// 반환값=true는 "이번에 매칭됐다" — 호출자가 시간 보너스(+1s) 부여용.
        /// </summary>
        bool TryActivateCombo()
        {
            if (_comboInput.Count < 3) return false;
            if (ownedComboSkills == null) return false;

            for (int i = 0; i < ownedComboSkills.Count; i++)
            {
                if (i < _comboCooldown.Length && _comboCooldown[i] > 0) continue; // 재사용 대기 중
                var skill = ownedComboSkills[i];
                if (skill == null) continue;
                if (skill.Matches(_comboInput))
                {
                    _queuedComboSkills.Add(skill);
                    if (i < _comboCooldown.Length) _comboCooldown[i] = COMBO_REUSE_INPUT; // 재사용까지 N입력 대기
                    // 슬롯은 클리어하지 않음 — 슬라이딩 윈도우 그대로 유지
                    Log($"[콤보 큐] {skill.displayName} ({skill.ComboString()}) — 각성 종료 시 발동 (대기 {_queuedComboSkills.Count}건, 재사용까지 {COMBO_REUSE_INPUT}입력)");
                    return true;
                }
            }
            return false;
        }

        void ActivateComboSkill(ComboSkillDef skill)
        {
            Log($"[콤보 스킬] {skill.displayName} ({skill.ComboString()}) 발동 — {skill.effect} {skill.amount}");

            switch (skill.effect)
            {
                case ComboEffectType.Damage:
                    if (_enemy != null) _enemy.TakeDamage(skill.amount);
                    // 데미지 이펙트는 EndAwaken이 위치 분산+셰이크와 함께 일괄 처리하므로 여기선 띄우지 않음.
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

        // ─────────────────────────────────────────────────────────────
        // 방해 행동 — EnemyController.HandleMidPattern이 호출
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// PDF [방해행동]: 적이 게이지 5에 도달하면 호출.
        /// 콤보 셔플은 사용 X. 무속성 삽입 또는 속성 저주를 무작위로 발동.
        /// </summary>
        public string TriggerDisruption()
        {
            bool isCurse = Random.value < 0.5f;

            if (isCurse)
            {
                CardElement[] elements = { CardElement.Fire, CardElement.Water, CardElement.Wind, CardElement.Earth };
                CardElement target = elements[Random.Range(0, elements.Length)];
                _cursedElement = target;
                _curseRemainingUses = CURSE_DURATION_USES;

                handHud?.Refresh();
                Log($"[방해] {target} 속성 저주 — 카드 사용 {CURSE_DURATION_USES}회 동안 지속");
                return $"방해: {ElementName(target)} 저주!";
            }
            else
            {
                // 무속성 카드(500) 1장을 뽑을 카드 더미에 섞어 넣음 — 사용해도 효과 없는 더미.
                var dummy = CardDatabase.CreateNeutralFillerInstance();
                if (dummy != null) _deck.AddToDrawShuffled(dummy);

                handHud?.Refresh();
                Log("[방해] 무속성 카드 1장 뽑을 더미에 삽입");
                return "방해: 무속성 카드 삽입!";
            }
        }

        /// <summary>외부(NewCardView)가 카드가 저주되었는지 확인용.</summary>
        public bool IsElementCursed(CardElement element)
        {
            return _cursedElement.HasValue && _cursedElement.Value == element && _curseRemainingUses > 0;
        }

        /// <summary>저주된 속성 카드 사용 시 플레이어 피해. 카드 사용 2회 동안 지속(어떤 카드든 카운트).</summary>
        void ApplyCurseOnCardUse(CardInstance card)
        {
            if (!_cursedElement.HasValue || _curseRemainingUses <= 0) return;

            if (card.Element == _cursedElement.Value && _player != null)
            {
                _player.TakeDamage(CURSE_PLAYER_DAMAGE);
                Log($"[저주] {_cursedElement.Value} 카드 사용 — 플레이어 {CURSE_PLAYER_DAMAGE} 피해");
            }

            _curseRemainingUses--;
            if (_curseRemainingUses <= 0)
            {
                Log($"[저주] {_cursedElement.Value} 저주 종료");
                _cursedElement = null;
            }
        }

        static string ElementName(CardElement e) => e switch
        {
            CardElement.Fire => "불", CardElement.Water => "물",
            CardElement.Wind => "바람", CardElement.Earth => "땅", _ => "?"
        };

        void UpdateAwakenText()
        {
            if (handHud == null) return;

            // Player.hpBar가 늦게 잡힐 수 있으므로 매 프레임 anchor 재시도 (이미 잡혔으면 CardHandHUD가 무시)
            if (_player != null && _player.hpBar != null)
            {
                var hpRect = _player.hpBar.GetComponent<RectTransform>();
                if (hpRect != null) handHud.SetAwakenGaugeAnchor(hpRect);
            }

            // 각성은 N장 도달 시 즉시 발동되므로 'READY 대기' 상태가 없다. N은 바람326 등으로 가변.
            int awakenMax = EffectiveAwakenInput;
            string label = _awakenActive
                ? $"각성 {_awakenTimeRemaining:F1}s"
                : $"{_elementInputCount}/{awakenMax}";
            handHud.SetAwakenText(label);
            handHud.UpdateAwakenGauge(
                _elementInputCount,
                awakenMax,
                _awakenActive,
                _awakenTimeRemaining,
                _awakenMaxTime
            );
        }

        void Log(string msg)
        {
            if (logVerbose) Debug.Log($"[NewBattle] {msg}");
        }

        /// <summary>
        /// 각성 종료 시 데미지 콤보 N건을 좌우/상하로 분산시키기 위한 오프셋 계산.
        /// index 0..total-1 → 중심에서 좌우로 펼침, Y는 짝/홀로 zigzag.
        /// total<=1 이면 분산 없음(원점).
        /// </summary>
        Vector2 ComputeAwakenDamageOffset(int index, int total)
        {
            if (total <= 1) return Vector2.zero;
            // -(total-1)/2 ~ +(total-1)/2 로 정규화 → 가운데 기준 좌우 균등 배치
            float lane = index - (total - 1) * 0.5f;
            float x = lane * awakenDamageEffectSpread.x;
            // Y: 짝수 인덱스 -dip, 홀수 +dip (작은 zigzag로 단조로움 회피)
            float y = ((index % 2 == 0) ? -1f : 1f) * awakenDamageEffectSpread.y;
            return new Vector2(x, y);
        }

        // ─────────────────────────────────────────────────────────────
        // 자동 셋업 헬퍼
        // ─────────────────────────────────────────────────────────────

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

        void EnsureEventSystem()
        {
            var es = FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include);
            if (es != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent<UnityEngine.EventSystems.EventSystem>();
            go.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

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
