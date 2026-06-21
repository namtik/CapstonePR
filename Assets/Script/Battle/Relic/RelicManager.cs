using System.Collections.Generic;
using UnityEngine;
using Battle.UI;

namespace Battle.Relic
{
    // 유물 보유 목록 관리 + 효과 디스패치/질의 매니저
    public class RelicManager : MonoBehaviour
    {
        public static RelicManager Instance { get; private set; } // 전역 싱글톤 인스턴스

        [Header("유물 데이터베이스")]
        [Tooltip("등장 가능한 전체 유물 목록(RelicDatabase 에셋). 비우면 Resources/RelicDatabase를 자동 로드.")]
        [SerializeField] private RelicDatabase database; // 전체 유물 정의 DB

        private readonly List<RelicSO> _owned = new List<RelicSO>(); // 현재 보유 중인 유물
        public IReadOnlyList<RelicSO> OwnedRelics => _owned; // 보유 유물 읽기 전용 뷰

        // 전체 유물 정의 목록(보상/상점 후보 풀). DB 미설정 시 빈 목록.
        public IReadOnlyList<RelicSO> RelicDefinitions =>
            database != null ? database.Relics : System.Array.Empty<RelicSO>();

        // 싱글톤 등록 + DB 자동 로드
        void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(gameObject); return; }

            if (database == null)
                database = Resources.Load<RelicDatabase>("RelicDatabase");
        }

        // 싱글톤 참조 해제
        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private readonly RelicRunContext _runCtx = new RelicRunContext(); // 획득 시 런 효과 적용용

        // 유물을 보유 목록에 추가 (중복 id면 무시) — 획득 시 런 레벨 효과(OnAcquired) 발동
        public void AddRelic(RelicSO relic)
        {
            if (relic == null) return;
            if (_owned.Exists(r => r != null && r.id == relic.id)) return;
            _owned.Add(relic);
            RelicHUD.Instance?.Refresh(_owned);
            Debug.Log($"[유물] 획득: {relic.DisplayLabel}");
            relic.OnAcquired(_runCtx);
        }

        // 보유 유물을 모두 비움 (런 종료/재시작 시 호출)
        public void ClearOwnedRelics()
        {
            if (_owned.Count == 0)
            {
                RelicHUD.Instance?.Refresh(_owned);
                return;
            }

            _owned.Clear();
            RelicHUD.Instance?.Refresh(_owned);
            Debug.Log("[유물] 보유 목록 초기화");
        }

        // 해당 id의 유물을 보유 중인지 확인
        public bool HasRelicId(string relicId)
        {
            if (string.IsNullOrWhiteSpace(relicId)) return false;
            return _owned.Exists(r => r != null && r.id == relicId);
        }

        // 효과 미구현(개발중) 유물을 아이콘 기반으로 보유 목록에 추가
        public bool TryAddSpriteOnlyRelic(Sprite sprite, out RelicSO granted)
        {
            granted = null;
            if (sprite == null) return false;

            string spriteName = string.IsNullOrWhiteSpace(sprite.name) ? "relic" : sprite.name.Trim();
            string relicId = $"sprite_{SanitizeId(spriteName)}";

            if (HasRelicId(relicId)) return false;

            var relic = ScriptableObject.CreateInstance<NoEffectRelic>();
            relic.id = relicId;
            relic.displayName = spriteName;
            relic.description = "개발중인 유물";
            relic.icon = sprite;
            relic.category = RelicCategory.Combat;

            _owned.Add(relic);
            RelicHUD.Instance?.Refresh(_owned);
            Debug.Log($"[유물] 획득(이미지 전용): {relic.DisplayLabel}");

            granted = relic;
            return true;
        }

        // 문자열을 id로 쓸 수 있게 소문자/허용문자만 남기고 정규화
        static string SanitizeId(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "relic";

            char[] chars = raw.ToLowerInvariant().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                bool allowed = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '-';
                if (!allowed) chars[i] = '_';
            }

            return new string(chars);
        }

        // ── 이벤트 디스패치 (보유 유물 전체에 훅 전달) ──────────────────────
        // 전투 진입
        public void NotifyBattleStart(IRelicBattleContext ctx)
        {
            for (int i = 0; i < _owned.Count; i++) _owned[i]?.OnBattleStart(ctx);
        }
        // 일반 카드 사용
        public void NotifyCardPlayed(IRelicBattleContext ctx, Card.CardInstance card, int cardsPlayedThisBattle)
        {
            for (int i = 0; i < _owned.Count; i++) _owned[i]?.OnCardPlayed(ctx, card, cardsPlayedThisBattle);
        }
        // 플레이어 체력 손실
        public void NotifyPlayerHpLost(IRelicBattleContext ctx, int amount, bool firstThisBattle)
        {
            for (int i = 0; i < _owned.Count; i++) _owned[i]?.OnPlayerHpLost(ctx, amount, firstThisBattle);
        }
        // 콤보 매칭 성공
        public void NotifyComboTriggered(IRelicBattleContext ctx)
        {
            for (int i = 0; i < _owned.Count; i++) _owned[i]?.OnComboTriggered(ctx);
        }
        // 각성 종료
        public void NotifyAwakenEnded(IRelicBattleContext ctx, int successCombos)
        {
            for (int i = 0; i < _owned.Count; i++) _owned[i]?.OnAwakenEnded(ctx, successCombos);
        }

        // ── 질의형 수정자 합산 (사용처에서 읽음) ───────────────────────────
        public int GetMaxHpBonus()
        {
            int sum = 0;
            for (int i = 0; i < _owned.Count; i++) if (_owned[i] != null) sum += _owned[i].ModifyMaxHp();
            return sum;
        }
        public float GetAwakenDurationBonus()
        {
            float sum = 0f;
            for (int i = 0; i < _owned.Count; i++) if (_owned[i] != null) sum += _owned[i].ModifyAwakenDurationSeconds();
            return sum;
        }
        public int GetGaugeCostDelta()
        {
            int sum = 0;
            for (int i = 0; i < _owned.Count; i++) if (_owned[i] != null) sum += _owned[i].ModifyGaugeCostPerCard();
            return sum;
        }
        // 손패 한도 강제값 — 보유 유물 중 가장 작은(가장 제한적인) 오버라이드, 없으면 -1
        public int GetHandLimitOverride()
        {
            int result = -1;
            for (int i = 0; i < _owned.Count; i++)
            {
                if (_owned[i] == null) continue;
                int ov = _owned[i].OverrideHandLimit();
                if (ov < 0) continue;
                result = result < 0 ? ov : Mathf.Min(result, ov);
            }
            return result;
        }
        public float GetShopPriceMultiplier()
        {
            float mul = 1f;
            for (int i = 0; i < _owned.Count; i++) if (_owned[i] != null) mul *= _owned[i].ShopPriceMultiplier();
            return mul;
        }
        public int GetGoldRewardBonus()
        {
            int sum = 0;
            for (int i = 0; i < _owned.Count; i++) if (_owned[i] != null) sum += _owned[i].GoldRewardBonus();
            return sum;
        }
        // D 드로우 동작을 교체하는 유물 보유 여부 (비급서 10011)
        public bool HasDDrawOverride()
        {
            for (int i = 0; i < _owned.Count; i++) if (_owned[i] != null && _owned[i].OverridesDDraw) return true;
            return false;
        }
        // 유물 라운드서 후보를 전부 획득시키는 유물 보유 여부 (유물 호리병 10018)
        public bool HasGrantAllRelicsInStage()
        {
            for (int i = 0; i < _owned.Count; i++) if (_owned[i] != null && _owned[i].GrantsAllRelicsInStage) return true;
            return false;
        }
    }
}
