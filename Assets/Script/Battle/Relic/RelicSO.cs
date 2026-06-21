using UnityEngine;
using Battle.Card;

namespace Battle.Relic
{
    // 유물 분류 (GDD): 전투형은 전투 내에서만 동작, 비전투형은 런타임(상점·체력 등)에 영향
    public enum RelicCategory
    {
        Combat,    // 전투형 — 적/플레이어에 직접 영향
        NonCombat, // 비전투형 — 전투 외 런타임 영향 (상점 할인, 최대 체력 등)
    }

    // 유물 한 종의 정의 + 효과 로직(훅)을 함께 담는 ScriptableObject 베이스.
    // 새 유물 = 이 클래스를 상속한 클래스 1개 + .asset 1개. 전투 코드 수정 불필요.
    public abstract class RelicSO : ScriptableObject
    {
        [Header("공통 정보")]
        public string id;                            // 고유 식별자 (예: "10000")
        public string displayName;                   // 표시 이름 (미정 시 빈 값 허용)
        [TextArea(2, 4)] public string description;  // 설명
        public Sprite icon;                          // 아이콘
        public RelicCategory category = RelicCategory.Combat; // 분류

        // 이름이 비어 있으면 id를 표시용으로 대체
        public string DisplayLabel => string.IsNullOrWhiteSpace(displayName) ? id : displayName;

        // ── 이벤트 훅 (해당 효과가 있는 유물만 override) ─────────────────────
        // 유물 획득 순간 — 런 레벨 1회성 효과 (최대 체력 증가, 전체 회복, 덱 수정 등)
        public virtual void OnAcquired(IRelicRunContext run) { }
        // 전투 진입 시 — 방어도/빙결 부여, 파편 삽입, 강제 각성 등
        public virtual void OnBattleStart(IRelicBattleContext ctx) { }
        // 일반 카드 사용 시 — cardsPlayedThisBattle = 이번 전투 누적 사용 수
        public virtual void OnCardPlayed(IRelicBattleContext ctx, CardInstance card, int cardsPlayedThisBattle) { }
        // 플레이어가 실제로 체력을 잃었을 때 — firstThisBattle = 이번 전투 첫 손실 여부
        public virtual void OnPlayerHpLost(IRelicBattleContext ctx, int amount, bool firstThisBattle) { }
        // 각성 중 콤보 매칭 성공 시
        public virtual void OnComboTriggered(IRelicBattleContext ctx) { }
        // 각성 종료 시 — successCombos = 이번 각성에서 성공한 콤보 수
        public virtual void OnAwakenEnded(IRelicBattleContext ctx, int successCombos) { }

        // ── 질의형 수정자 (보유 유물 전체가 합산되어 사용처에서 읽음) ─────────────
        public virtual int   ModifyMaxHp() => 0;                    // 최대 체력 가감
        public virtual float ModifyAwakenDurationSeconds() => 0f;   // 각성 지속시간 가감(초)
        public virtual int   ModifyGaugeCostPerCard() => 0;         // 카드 1장당 게이지 코스트 가감
        public virtual int   OverrideHandLimit() => -1;             // 손패 한도 강제(-1=영향 없음)
        public virtual float ShopPriceMultiplier() => 1f;           // 상점 가격 배수(곱)
        public virtual int   GoldRewardBonus() => 0;                // 전투 보상 골드 가산

        // ── 특수 플래그 ────────────────────────────────────────────────
        public virtual bool  OverridesDDraw => false;               // D 드로우 동작 교체 (비급서 10011)
        public virtual bool  GrantsAllRelicsInStage => false;       // 유물 라운드서 후보 전부 획득 (유물 호리병 10018)
    }
}
