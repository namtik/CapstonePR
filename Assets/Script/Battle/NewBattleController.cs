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

        public bool IsFeverArmed => _feverArmed;
        public bool IsFeverActive => _feverActive;
        public CardDeckSystem Deck => _deck;

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
                if (card.Element != CardElement.Neutral)
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
            // 효과 처리 중 손패 조작 카드(예: 물3)가 자기 자신을 영향에 포함시키지 않도록
            // 패에서 먼저 분리한 뒤 효과 해결.
            _deck.PullFromHand(card);

            var result = _resolver.Resolve(card);

            _deck.PlaceCardAfterUse(card, result.exile, result.keepOnField);

            if (result.exile)
                _resolver.NotifyExile(card);

            // 적 행동 게이지 누적
            AccrueEnemyGauge(card.Gauge);

            // 속성 카드 입력 카운팅(피버 게이지)
            if (card.Element != CardElement.Neutral)
                AccumulateFeverInput();

            Log($"{card.data.displayName} 사용 — 게이지+{card.Gauge}");
            return true;
        }

        // ─────────────────────────────────────────────────────────────
        // 적 게이지
        // ─────────────────────────────────────────────────────────────

        void AccrueEnemyGauge(int amount)
        {
            if (_feverActive) return; // 피버 중에는 적 게이지 증가 X
            if (_enemyStat == null) return;

            for (int i = 0; i < amount; i++) _enemyStat.ConsumeGaugeStep();
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
