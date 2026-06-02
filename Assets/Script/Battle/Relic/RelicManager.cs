using System.Collections.Generic;
using UnityEngine;
using Battle.UI;

namespace Battle.Relic
{
    /// <summary>
    /// 유물 보유 목록 관리 및 효과 조회.
    /// relicDefinitions 리스트에서 아이콘/설명을 Inspector에서 직접 편집 가능.
    /// BattleTestController의 testRelics 필드로 테스트 시작 시 자동 지급.
    /// </summary>
    public class RelicManager : MonoBehaviour
    {
        public static RelicManager Instance { get; private set; }

        /// <summary>비급서: 콤보 성공 시 기본 1s에 더해지는 추가 보너스 초.</summary>
        public const float COMBO_BONUS_SECONDS_EXTRA = 0.5f;

        [Header("사용 가능한 유물 정의 (아이콘 여기서 설정)")]
        [SerializeField] private List<RelicDef> relicDefinitions = new List<RelicDef>
        {
            new RelicDef
            {
                id          = "imugi_yeouiju",
                displayName = "이무기의 여의주",
                description = "각성 상태에서 성공한 콤보 횟수만큼\n각성 종료 후 각성 게이지를 회복한다.",
                effect      = RelicEffectType.AwakenGaugeRecoverPerCombo,
            },
            new RelicDef
            {
                id          = "secret_manual",
                displayName = "비급서",
                description = "각성 상태에서 콤보 발동 성공 시\n회복되는 시간이 0.5초 추가된다.",
                effect      = RelicEffectType.ComboBonusSecondsBoost,
            },
        };

        private readonly List<RelicDef> _owned = new List<RelicDef>();
        public IReadOnlyList<RelicDef> OwnedRelics => _owned;

        void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(gameObject); return; }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>effectType에 해당하는 유물을 definitions에서 찾아 지급.</summary>
        public void GiveRelicByEffect(RelicEffectType effectType)
        {
            var def = relicDefinitions.Find(r => r.effect == effectType);
            if (def == null)
            {
                Debug.LogWarning($"[유물] 정의를 찾을 수 없음: {effectType}");
                return;
            }
            AddRelic(def);
        }

        public void AddRelic(RelicDef relic)
        {
            if (relic == null) return;
            if (_owned.Exists(r => r.id == relic.id)) return;
            _owned.Add(relic);
            RelicHUD.Instance?.Refresh(_owned);
            Debug.Log($"[유물] 획득: {relic.displayName}");
        }

        public bool HasEffect(RelicEffectType type)
        {
            foreach (var r in _owned)
                if (r.effect == type) return true;
            return false;
        }
    }
}
