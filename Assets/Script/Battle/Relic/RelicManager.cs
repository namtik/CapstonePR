using System.Collections.Generic;
using UnityEngine;
using Battle.UI;

namespace Battle.Relic
{
    // 유물 보유 목록 관리 및 효과 조회 매니저
    public class RelicManager : MonoBehaviour
    {
        public static RelicManager Instance { get; private set; } // 전역 싱글톤 인스턴스

        public const float COMBO_BONUS_SECONDS_EXTRA = 0.5f; // 비급서: 콤보 성공 시 추가 보너스 초
        public const int FROST_ON_BATTLE_START_AMOUNT = 7; // 한설의 결정: 전투 진입 시 적에게 부여할 빙결량

        [Header("사용 가능한 유물 정의 (아이콘 여기서 설정)")]
        [SerializeField] private List<RelicDef> relicDefinitions = new List<RelicDef> // 정의된 전체 유물 목록
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
            new RelicDef
            {
                id          = "frosted_flower",
                displayName = "서리화",
                description = "전투 라운드에 진입할 때\n적에게 빙결 7을 부여한다.",
                effect      = RelicEffectType.FrostEnemyOnBattleStart,
            },
        };

        private readonly List<RelicDef> _owned = new List<RelicDef>(); // 현재 보유 중인 유물
        public IReadOnlyList<RelicDef> OwnedRelics => _owned; // 보유 유물 읽기 전용 뷰
        public IReadOnlyList<RelicDef> RelicDefinitions => relicDefinitions; // 정의 목록 읽기 전용 뷰

        // 싱글톤 등록 (중복 시 자신 파괴)
        void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(gameObject); return; }
        }

        // 싱글톤 참조 해제
        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // 효과 종류로 정의를 찾아 해당 유물 지급
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

        // 유물을 보유 목록에 추가 (중복 id면 무시)
        public void AddRelic(RelicDef relic)
        {
            if (relic == null) return;
            if (_owned.Exists(r => r.id == relic.id)) return;
            _owned.Add(relic);
            RelicHUD.Instance?.Refresh(_owned);
            Debug.Log($"[유물] 획득: {relic.displayName}");
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
            return _owned.Exists(r => r.id == relicId);
        }

        // 효과 미구현(개발중) 유물을 아이콘 기반으로 보유 목록에 추가
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

        // 해당 효과를 가진 유물을 하나라도 보유 중인지 확인
        public bool HasEffect(RelicEffectType type)
        {
            foreach (var r in _owned)
                if (r.effect == type) return true;
            return false;
        }

        // 보유 중 해당 효과를 가진 첫 유물 정의를 반환(없으면 null) — 발동 팝업 표시용
        public RelicDef GetOwnedRelicByEffect(RelicEffectType type)
        {
            foreach (var r in _owned)
                if (r.effect == type) return r;
            return null;
        }
    }
}
