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
        public IReadOnlyList<RelicDef> RelicDefinitions => relicDefinitions;

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

        /// <summary>보유 유물을 모두 비운다. (런 종료/재시작 시 호출 — 다음 런은 유물 0개로 시작)</summary>
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

        public bool HasRelicId(string relicId)
        {
            if (string.IsNullOrWhiteSpace(relicId)) return false;
            return _owned.Exists(r => r.id == relicId);
        }

        /// <summary>
        /// 개발중(효과 미구현) 유물을 아이콘 기반으로 보유 목록에 추가한다.
        /// </summary>
        public bool TryAddSpriteOnlyRelic(Sprite sprite, out RelicDef granted)
        {
            granted = null;
            if (sprite == null) return false;

            string spriteName = string.IsNullOrWhiteSpace(sprite.name) ? "relic" : sprite.name.Trim();
            string relicId = $"sprite_{SanitizeId(spriteName)}";

            if (HasRelicId(relicId)) return false;

            var relic = new RelicDef
            {
                id = relicId,
                displayName = spriteName,
                description = "개발중인 유물",
                icon = sprite,
                effect = RelicEffectType.None,
            };

            _owned.Add(relic);
            RelicHUD.Instance?.Refresh(_owned);
            Debug.Log($"[유물] 획득(이미지 전용): {relic.displayName}");

            granted = relic;
            return true;
        }

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

        public bool HasEffect(RelicEffectType type)
        {
            foreach (var r in _owned)
                if (r.effect == type) return true;
            return false;
        }
    }
}
