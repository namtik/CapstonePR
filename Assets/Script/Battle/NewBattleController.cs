using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Battle.Card;
using Battle.Deck;
using Battle.UI;

namespace Battle
{
    /// <summary>
    /// 새 전투 시스템 진입점.
    /// - 5장 손패, 드래그 사용
    /// - 카드별 게이지 → EnemyStat.ConsumeGaugeStep로 누적
    /// - D 드로우 / F 피버 / 속성 카드 입력 카운트
    /// 기존 ElementSlotSystem/ComboSystem과 공존하지 않는다(테스트 씬 전용).
    /// </summary>
    public class NewBattleController : MonoBehaviour
    {
        public const int FEVER_ACTIVATION_INPUT = 15;
        /// <summary>피버 발동 시 기본 지속 시간(초).</summary>
        public const float FEVER_DURATION_SECONDS = 10f;
        /// <summary>콤보 1회 성공 시 추가되는 시간(초).</summary>
        public const float COMBO_BONUS_SECONDS = 0.5f;
        public const int START_DRAW = 5;

        public static NewBattleController Instance { get; private set; }

        [Header("UI")]
        [SerializeField] private CardHandHUD handHud;
        [Tooltip("카드 사용 시 EffectName(예: Fire_ATK)에 해당하는 스프라이트 시트 애니메이션을 재생. " +
                 "비우면 자동으로 캔버스에 생성. 시트가 없으면 조용히 스킵.")]
        [SerializeField] private CardEffectOverlay effectOverlay;
        [Tooltip("플레이어 피격 시 화면 빨간 플래시. 비우면 자동 생성.")]
        [SerializeField] private PlayerDamageOverlay damageOverlay;

        [Header("보유 콤보 스킬 (피버 전용 / Inspector 편집)")]
        [SerializeField] private List<ComboSkillDef> ownedComboSkills = new List<ComboSkillDef>();
        public IReadOnlyList<ComboSkillDef> OwnedComboSkills => ownedComboSkills;

        [Header("피버 콤보 이펙트")]
        [Tooltip("피버 종료 시 Damage 콤보가 적에게 들어갈 때 재생할 이펙트 이름. " +
                 "Resources/CardEffects/{이름}.png 시트를 찾아 적 위치에서 재생. " +
                 "비우거나 시트가 없으면 조용히 스킵.")]
        [SerializeField] private string feverDamageEffectName = "Fever_ATK";
        [Tooltip("Damage 콤보가 N개 동시에 발동될 때 이펙트가 한 곳에 겹쳐 보이지 않도록 좌우/상하로 분산.\n" +
                 "X = 콤보 간 가로 간격, Y = zigzag 세로 진폭. 0으로 두면 분산 없이 한 곳.")]
        [SerializeField] private Vector2 feverDamageEffectSpread = new Vector2(220f, 80f);
        [Tooltip("피버 종료 시 데미지 콤보 1건당 더해질 셰이크 강도 배율. 콤보가 많을수록 더 세게 흔들림.\n" +
                 "0이면 셰이크 비활성.")]
        [SerializeField] private float feverShakeIntensityPerHit = 0.6f;
        [Tooltip("피버 셰이크 강도 상한 — 콤보가 너무 많아도 이 값을 넘지 않음.")]
        [SerializeField] private float feverShakeIntensityMax = 2.0f;

        [Header("디버그")]
        [SerializeField] private bool logVerbose = true;

        private readonly CardDeckSystem _deck = new CardDeckSystem();
        private readonly CardEffectResolver _resolver = new CardEffectResolver();
        private readonly CardEffectContext _ctx = new CardEffectContext();

        private Player _player;
        private EnemyController _enemy;
        private EnemyStat _enemyStat;

        private bool _inBattle;

        // 피버 게이지
        private int _elementInputCount;   // 피버 활성화용 누적
        private bool _feverArmed;         // 15회 도달 후 F 대기 중
        private bool _feverActive;
        private float _feverTimeRemaining; // 활성화 시 남은 시간(초). 0 이하가 되면 종료
        private float _feverMaxTime;       // 콤보 보너스로 늘어나는 동적 최대치 (게이지 fill 계산용)
        private List<CardInstance> _feverStoredNeutralCards = new List<CardInstance>();

        // 콤보 슬롯 (피버 동안 입력된 속성, sliding window 3장)
        private readonly List<CardElement> _comboInput = new List<CardElement>();
        // 이번 피버 사이클에서 매칭된 콤보 스킬 인덱스(중복 매칭 방지 + UI 표시)
        private readonly HashSet<int> _activatedComboSkillIndices = new HashSet<int>();
        // 피버 종료 시 일괄 발동할 콤보 스킬 큐 (매칭 순서대로 적재)
        private readonly List<ComboSkillDef> _queuedComboSkills = new List<ComboSkillDef>();
        // 피버 동안 입력된 속성 전체 기록(왼쪽 UI 표시용)
        private readonly List<CardElement> _feverInputHistory = new List<CardElement>();

        // 방해 행동 — 속성 저주 (PDF: 한 속성 모든 카드, 플레이어 카드 사용 2회 동안 지속)
        public const int CURSE_DURATION_USES = 2;
        public const int CURSE_PLAYER_DAMAGE = 5;
        private CardElement? _cursedElement;
        private int _curseRemainingUses;

        // 바람14(313): 다음 카드 게이지 소모 스킵 횟수
        private int _skipNextGaugeCount;
        // 물12(211): 마지막으로 사용한 카드 (재사용 대상)
        private CardInstance _lastResolvedCard;

        public bool IsFeverArmed => _feverArmed;
        public bool IsFeverActive => _feverActive;
        public CardDeckSystem Deck => _deck;
        public CardInstance LastResolvedCard => _lastResolvedCard;
        public CardElement? CursedElement => _cursedElement;

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
        }

        void OnDestroy()
        {
            _deck.OnCardDrawn -= OnCardDrawnHandler;
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
            TickFeverTimer();
            UpdateFeverText();
        }

        /// <summary>피버 지속 시간 카운트다운 (Update에서 매 프레임 호출).</summary>
        void TickFeverTimer()
        {
            if (!_feverActive) return;
            _feverTimeRemaining -= Time.deltaTime;
            if (_feverTimeRemaining <= 0f)
            {
                _feverTimeRemaining = 0f;
                EndFever();
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
                handHud.SetFeverMode(false); // 전투 시작 시점에는 콤보 UI 숨김
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
            _feverArmed = false;
            _feverActive = false;
            _feverTimeRemaining = 0f;
            _feverMaxTime = 0f;

            // 피버 게이지를 Player.hpBar 아래에 자동 배치
            if (handHud != null && _player != null && _player.hpBar != null)
                handHud.SetFeverGaugeAnchor(_player.hpBar.GetComponent<RectTransform>());

            _deck.Draw(START_DRAW);
            UpdateFeverText();
            Log($"전투 시작 — {START_DRAW}장 드로우");
        }

        public void EndBattle()
        {
            _inBattle = false;
            _feverActive = false;
            _feverArmed = false;
            _deck.EndBattle();
            if (handHud != null) handHud.UseCardCallback = null;
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
            if (Input.GetKeyDown(KeyCode.F)) TryActivateFever();
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

        void TryActivateFever()
        {
            if (_feverActive)
            {
                Log("이미 피버 발동 중");
                return;
            }
            if (!_feverArmed)
            {
                Log($"피버 미충전 ({_elementInputCount}/{FEVER_ACTIVATION_INPUT})");
                return;
            }
            ActivateFever();
        }

        // ─────────────────────────────────────────────────────────────
        // 카드 사용
        // ─────────────────────────────────────────────────────────────

        bool OnUseCardRequested(CardInstance card)
        {
            if (!_inBattle) return false;
            if (card == null) return false;

            // 피버 중: 모든 카드 효과를 '드로우 1'로 치환 + 콤보 슬롯 등록
            if (_feverActive)
            {
                _deck.MoveAfterUse(card, exile: false, poweredField: false);
                _deck.Draw(1);

                bool comboTriggered = false;
                if (card.data.comboSlot)
                {
                    AddToComboSlot(card.Element);
                    comboTriggered = TryActivateCombo();
                }

                // 콤보 매칭 시 지속 시간 +0.5초 — 매칭이 일어났을 때만 보너스
                // 게이지가 줄어들지 않도록 max도 함께 증가시킨다 (시각적으로 끝부분이 살짝 차오르는 효과)
                if (comboTriggered)
                {
                    float before = _feverTimeRemaining;
                    _feverTimeRemaining += COMBO_BONUS_SECONDS;
                    _feverMaxTime += COMBO_BONUS_SECONDS;
                    Log($"[피버] ⏱ 콤보 매칭 → 시간 +{COMBO_BONUS_SECONDS:F1}s ({before:F2}s → {_feverTimeRemaining:F2}s, max={_feverMaxTime:F2}s)");
                }

                Log($"[피버] {card.data.displayName} → 콤보입력=[{string.Join(",", _comboInput)}] 콤보발동={comboTriggered} 남은시간={_feverTimeRemaining:F2}s");

                if (handHud != null)
                {
                    handHud.UpdateComboSlot(_comboInput);
                    handHud.UpdateComboSkillList(ownedComboSkills, _activatedComboSkillIndices);
                    handHud.UpdateFeverInputHistory(_feverInputHistory);
                }

                // 모든 콤보 스킬 발동 시 즉시 종료 (시간 종료는 Update의 TickFeverTimer가 담당)
                if (AllComboSkillsActivated())
                    EndFever();
                return true;
            }

            // 일반: 효과 실행
            ApplyCurseOnCardUse(card);

            _deck.PullFromHand(card);

            var result = _resolver.Resolve(card);

            _deck.PlaceCardAfterUse(card, result.exile, result.keepOnField);

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
            AccrueEnemyGauge(gaugeCost);
            if (result.skipNextGaugeCost) _skipNextGaugeCount++;

            // 통계 갱신
            UpdateUsageStats(card);

            // 피버 입력 카운팅
            if (card.data.comboSlot) AccumulateFeverInput();

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
            if (_feverActive) return; // 피버 중에는 적 게이지 증가 X

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
        // 피버
        // ─────────────────────────────────────────────────────────────

        void AccumulateFeverInput()
        {
            if (_feverArmed) return;

            _elementInputCount++;
            if (_elementInputCount >= FEVER_ACTIVATION_INPUT)
            {
                _feverArmed = true;
                Log("[피버] 충전 완료 — F로 발동 가능");
            }
        }

        void ActivateFever()
        {
            _feverActive = true;
            _feverArmed = false;
            _elementInputCount = 0;
            _feverTimeRemaining = FEVER_DURATION_SECONDS;
            _feverMaxTime = FEVER_DURATION_SECONDS;

            // 패/드로우/버린 더미의 무속성 카드 임시 격리
            _feverStoredNeutralCards = _deck.ExtractAllNeutralCards();

            // 콤보 입력/매칭/큐/히스토리 초기화
            _comboInput.Clear();
            _activatedComboSkillIndices.Clear();
            _queuedComboSkills.Clear();
            _feverInputHistory.Clear();

            // 콤보 슬롯/스킬 UI 활성화 및 갱신
            if (handHud != null)
            {
                handHud.SetFeverMode(true);
                handHud.UpdateComboSlot(_comboInput);
                handHud.UpdateComboSkillList(ownedComboSkills, _activatedComboSkillIndices);
                handHud.UpdateFeverInputHistory(_feverInputHistory);
            }

            Log($"[피버] 발동! 격리된 무속성={_feverStoredNeutralCards.Count}, 지속 시간={FEVER_DURATION_SECONDS}s (콤보 매칭 시 +{COMBO_BONUS_SECONDS}s, 종료 시 일괄 발동)");
        }

        void EndFever()
        {
            _feverActive = false;
            _feverTimeRemaining = 0f;
            _feverMaxTime = 0f;

            _deck.ReturnNeutralCardsToDiscard(_feverStoredNeutralCards);
            _feverStoredNeutralCards.Clear();

            _deck.TrimHandOverflowToDiscard();

            // 큐에 쌓인 콤보 스킬 일괄 발동 (매칭 순서대로)
            if (_queuedComboSkills.Count > 0)
            {
                // Damage 콤보를 먼저 세어두면, 이펙트 분산 시 i번째/총N개로 좌우 정렬 가능
                int damageCount = 0;
                for (int i = 0; i < _queuedComboSkills.Count; i++)
                    if (_queuedComboSkills[i] != null && _queuedComboSkills[i].effect == ComboEffectType.Damage)
                        damageCount++;

                Log($"[피버] 종료 — 콤보 {_queuedComboSkills.Count}건 일괄 발동 (Damage {damageCount}건)");

                int damageIndex = 0;
                for (int i = 0; i < _queuedComboSkills.Count; i++)
                {
                    var skill = _queuedComboSkills[i];
                    if (skill == null) continue;
                    ActivateComboSkill(skill);

                    if (skill.effect == ComboEffectType.Damage)
                    {
                        if (effectOverlay != null && !string.IsNullOrEmpty(feverDamageEffectName))
                        {
                            Vector2 offset = ComputeFeverDamageOffset(damageIndex, damageCount);
                            effectOverlay.PlayByNameAtOffset(feverDamageEffectName, offset);
                        }
                        damageIndex++;
                    }
                }

                // Damage 콤보가 1건 이상이면 데미지 임팩트 강조용 셰이크 (콤보 수에 비례, 상한 있음)
                if (damageCount > 0 && damageOverlay != null && feverShakeIntensityPerHit > 0f)
                {
                    float intensity = Mathf.Min(damageCount * feverShakeIntensityPerHit, feverShakeIntensityMax);
                    damageOverlay.TriggerShake(intensity);
                }

                _queuedComboSkills.Clear();
            }
            else
            {
                Log("[피버] 종료 — 발동된 콤보 없음");
            }

            // 콤보 슬롯/입력/히스토리 초기화
            _comboInput.Clear();
            _activatedComboSkillIndices.Clear();
            _feverInputHistory.Clear();

            // 콤보 슬롯/스킬 UI 비활성화
            if (handHud != null)
            {
                handHud.SetFeverMode(false);
                handHud.UpdateComboSlot(_comboInput);
                handHud.UpdateComboSkillList(ownedComboSkills, _activatedComboSkillIndices);
                handHud.UpdateFeverInputHistory(_feverInputHistory);
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
            _feverInputHistory.Add(element);
        }

        /// <summary>
        /// 콤보 매칭. 매칭되면 큐에 적재(즉시 발동 X). 피버 종료 시 일괄 발동.
        /// 반환값=true는 "이번에 매칭됐다" — 호출자가 시간 보너스(+0.5s) 부여용.
        /// </summary>
        bool TryActivateCombo()
        {
            if (_comboInput.Count < 3) return false;
            if (ownedComboSkills == null) return false;

            for (int i = 0; i < ownedComboSkills.Count; i++)
            {
                if (_activatedComboSkillIndices.Contains(i)) continue;
                var skill = ownedComboSkills[i];
                if (skill == null) continue;
                if (skill.Matches(_comboInput))
                {
                    _queuedComboSkills.Add(skill);
                    _activatedComboSkillIndices.Add(i);
                    // 슬롯은 클리어하지 않음 — 슬라이딩 윈도우 그대로 유지
                    Log($"[콤보 큐] {skill.displayName} ({skill.ComboString()}) — 피버 종료 시 발동 ({_queuedComboSkills.Count}건 대기)");
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
                    // 데미지 이펙트는 EndFever가 위치 분산+셰이크와 함께 일괄 처리하므로 여기선 띄우지 않음.
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

        bool AllComboSkillsActivated()
        {
            if (ownedComboSkills == null || ownedComboSkills.Count == 0) return false;
            return _activatedComboSkillIndices.Count >= ownedComboSkills.Count;
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

        void UpdateFeverText()
        {
            if (handHud == null) return;

            // Player.hpBar가 늦게 잡힐 수 있으므로 매 프레임 anchor 재시도 (이미 잡혔으면 CardHandHUD가 무시)
            if (_player != null && _player.hpBar != null)
            {
                var hpRect = _player.hpBar.GetComponent<RectTransform>();
                if (hpRect != null) handHud.SetFeverGaugeAnchor(hpRect);
            }

            string label;
            if (_feverActive) label = $"FEVER {_feverTimeRemaining:F1}s";
            else if (_feverArmed) label = "FEVER READY (F)";
            else label = $"{_elementInputCount}/{FEVER_ACTIVATION_INPUT}";
            handHud.SetFeverText(label);
            handHud.UpdateFeverGauge(
                _elementInputCount,
                FEVER_ACTIVATION_INPUT,
                _feverArmed,
                _feverActive,
                _feverTimeRemaining,
                _feverMaxTime
            );
        }

        void Log(string msg)
        {
            if (logVerbose) Debug.Log($"[NewBattle] {msg}");
        }

        /// <summary>
        /// 피버 종료 시 데미지 콤보 N건을 좌우/상하로 분산시키기 위한 오프셋 계산.
        /// index 0..total-1 → 중심에서 좌우로 펼침, Y는 짝/홀로 zigzag.
        /// total<=1 이면 분산 없음(원점).
        /// </summary>
        Vector2 ComputeFeverDamageOffset(int index, int total)
        {
            if (total <= 1) return Vector2.zero;
            // -(total-1)/2 ~ +(total-1)/2 로 정규화 → 가운데 기준 좌우 균등 배치
            float lane = index - (total - 1) * 0.5f;
            float x = lane * feverDamageEffectSpread.x;
            // Y: 짝수 인덱스 -dip, 홀수 +dip (작은 zigzag로 단조로움 회피)
            float y = ((index % 2 == 0) ? -1f : 1f) * feverDamageEffectSpread.y;
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
