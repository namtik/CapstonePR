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
        public const int FEVER_END_INPUT = 5;
        public const int START_DRAW = 5;

        public static NewBattleController Instance { get; private set; }

        [Header("UI")]
        [SerializeField] private CardHandHUD handHud;

        [Header("보유 콤보 스킬 (피버 전용 / Inspector 편집)")]
        [SerializeField] private List<ComboSkillDef> ownedComboSkills = new List<ComboSkillDef>();
        public IReadOnlyList<ComboSkillDef> OwnedComboSkills => ownedComboSkills;

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
        private int _feverInputRemaining; // 활성화 시 5회 남으면 종료
        private List<CardInstance> _feverStoredNeutralCards = new List<CardInstance>();

        // 콤보 슬롯 (피버 동안 입력된 속성, sliding window 3장)
        private readonly List<CardElement> _comboInput = new List<CardElement>();
        // 이번 피버 사이클에서 발동된 콤보 스킬 인덱스(중복 발동 방지)
        private readonly HashSet<int> _activatedComboSkillIndices = new HashSet<int>();

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
            UpdateFeverText();
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
            EnsureEventSystem();

            _inBattle = true;
            _elementInputCount = 0;
            _feverArmed = false;
            _feverActive = false;
            _feverInputRemaining = 0;

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

                // 콤보 발동 시 속성 카드 입력 제한 차감 X (PDF 명세)
                if (!comboTriggered)
                    _feverInputRemaining--;

                Log($"[피버] {card.data.displayName} → 콤보입력=[{string.Join(",", _comboInput)}] 콤보발동={comboTriggered} 남은입력={_feverInputRemaining}");

                if (handHud != null)
                {
                    handHud.UpdateComboSlot(_comboInput);
                    handHud.UpdateComboSkillList(ownedComboSkills, _activatedComboSkillIndices);
                }

                // 모든 콤보 스킬 발동 또는 입력 한도 소진 → 강제 종료
                if (AllComboSkillsActivated() || _feverInputRemaining <= 0)
                    EndFever();
                return true;
            }

            // 일반: 효과 실행
            ApplyCurseOnCardUse(card);

            _deck.PullFromHand(card);

            var result = _resolver.Resolve(card);

            _deck.PlaceCardAfterUse(card, result.exile, result.keepOnField);

            if (result.exile) _resolver.NotifyExile(card);

            // 물12(211): 마지막 사용 카드 효과 재사용
            if (result.recastLastCard && _lastResolvedCard != null)
            {
                Log($"[재사용] {_lastResolvedCard.data.displayName} 효과 한 번 더");
                _resolver.Resolve(_lastResolvedCard);
            }

            // 패에서 카드 선택 → 소멸 (불4/103, 불13/112)
            if (result.requiresHandExileSelection && handHud != null && _deck.Hand.Count > 0)
            {
                handHud.EnterSelectionMode("패에서 소멸할 카드를 선택하세요",
                    (selected) =>
                    {
                        if (selected == null) return;
                        _deck.ExileFromHand(selected);
                        _resolver.NotifyExile(selected);
                    });
            }

            // 패에서 카드 선택 → 버리기 (물8/207)
            if (result.requiresHandDiscardSelection && handHud != null && _deck.Hand.Count > 0)
            {
                handHud.EnterSelectionMode("패에서 버릴 카드를 선택하세요",
                    (selected) =>
                    {
                        if (selected == null) return;
                        _deck.DiscardCardFromHand(selected);
                        if (_ctx.discardToDrawActive) _deck.Draw(1); // 물21(220)
                    });
            }

            // 패에서 카드 선택 → 복사하여 패에 추가 (물14/213)
            if (result.requiresHandCopySelection && handHud != null && _deck.Hand.Count > 0)
            {
                handHud.EnterSelectionMode("복사할 카드를 선택하세요",
                    (selected) =>
                    {
                        if (selected == null) return;
                        var copy = new CardInstance(selected.data, transient: true);
                        if (!_deck.AddToHand(copy)) _deck.AddToDiscard(copy);
                    });
            }

            // 뽑을 더미에서 카드 선택 → 패로 (물7/206)
            if (result.requiresDrawPileMoveSelection)
            {
                // 현재 UI 없음 — 자동: 무작위 1장 패로
                if (_deck.DrawPile.Count > 0)
                {
                    var pick = _deck.DrawPile[Random.Range(0, _deck.DrawPile.Count)];
                    _deck.MoveFromDrawPileToHand(pick);
                }
            }

            // 버린 더미에서 카드 선택 → 뽑을 더미 맨 위 (물11/210)
            if (result.requiresDiscardMoveSelection)
            {
                if (_deck.DiscardPile.Count > 0)
                {
                    var pick = _deck.DiscardPile[Random.Range(0, _deck.DiscardPile.Count)];
                    _deck.MoveFromDiscardToDrawPileTop(pick);
                }
            }

            // 파편 풀에서 선택 (땅10/409)
            if (result.requiresFragmentPoolSelection)
            {
                int rid = CardDatabase.FragmentIds[Random.Range(0, CardDatabase.FragmentIds.Length)];
                var frag = CardDatabase.CreateFragmentInstance(rid);
                if (frag != null && !_deck.AddToHand(frag))
                    _deck.AddToDiscard(frag);
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
            _feverInputRemaining = FEVER_END_INPUT;

            // 패/드로우/버린 더미의 무속성 카드 임시 격리
            _feverStoredNeutralCards = _deck.ExtractAllNeutralCards();

            // 콤보 입력/발동 기록 초기화
            _comboInput.Clear();
            _activatedComboSkillIndices.Clear();

            // 콤보 슬롯/스킬 UI 활성화 및 갱신
            if (handHud != null)
            {
                handHud.SetFeverMode(true);
                handHud.UpdateComboSlot(_comboInput);
                handHud.UpdateComboSkillList(ownedComboSkills, _activatedComboSkillIndices);
            }

            Log($"[피버] 발동! 격리된 무속성={_feverStoredNeutralCards.Count}, {FEVER_END_INPUT}회 입력 시 종료");
        }

        void EndFever()
        {
            _feverActive = false;

            _deck.ReturnNeutralCardsToDiscard(_feverStoredNeutralCards);
            _feverStoredNeutralCards.Clear();

            _deck.TrimHandOverflowToDiscard();

            // 콤보 슬롯/입력 초기화
            _comboInput.Clear();
            _activatedComboSkillIndices.Clear();

            // 콤보 슬롯/스킬 UI 비활성화
            if (handHud != null)
            {
                handHud.SetFeverMode(false);
                handHud.UpdateComboSlot(_comboInput);
                handHud.UpdateComboSkillList(ownedComboSkills, _activatedComboSkillIndices);
            }

            Log("[피버] 종료");
        }

        // ─────────────────────────────────────────────────────────────
        // 콤보 매칭
        // ─────────────────────────────────────────────────────────────

        void AddToComboSlot(CardElement element)
        {
            _comboInput.Add(element);
            if (_comboInput.Count > 3) _comboInput.RemoveAt(0);
        }

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
                    ActivateComboSkill(skill);
                    _activatedComboSkillIndices.Add(i);
                    _comboInput.Clear();
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
            string label;
            if (_feverActive) label = $"FEVER {_feverInputRemaining}";
            else if (_feverArmed) label = "FEVER READY (F)";
            else label = $"{_elementInputCount}/{FEVER_ACTIVATION_INPUT}";
            handHud.SetFeverText(label);
        }

        void Log(string msg)
        {
            if (logVerbose) Debug.Log($"[NewBattle] {msg}");
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
    }
}
