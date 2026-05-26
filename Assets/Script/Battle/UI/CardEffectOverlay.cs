using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Battle.Card;

namespace Battle.UI
{
    /// <summary>
    /// 카드 사용 시 효과(스프라이트 시트 애니메이션)를 화면에 재생.
    /// 카드의 EffectName(예: Fire_ATK, Burn_EFF, Heal_EFF, Wind_POW)으로
    /// Resources/CardEffects/{EffectName}.png(스프라이트 시트, 다중 슬라이스)을 자동 로드.
    /// 시트가 없으면 조용히 스킵 — 이펙트 미완성 상태도 안전.
    /// </summary>
    public class CardEffectOverlay : MonoBehaviour
    {
        [Header("효과 표시 위치 (캔버스 anchored 좌표)")]
        [Tooltip("적 대상 효과(*_ATK, Burn_EFF, Chain_EFF) 위치 — 화면 상단 쪽.")]
        [SerializeField] private Vector2 enemyAnchoredPos = new Vector2(0f, 300f);
        [Tooltip("플레이어 대상 효과(*_DEF, *_POW, Heal_EFF) 위치 — 화면 하단 쪽.")]
        [SerializeField] private Vector2 playerAnchoredPos = new Vector2(0f, 100f);
        [Tooltip("Earth_ATK 전용 위치 — 땅 공격은 좀 더 아래쪽에서 표시.")]
        [SerializeField] private Vector2 earthAtkAnchoredPos = new Vector2(0f, 100f);
        [Tooltip("효과 이미지 크기.")]
        [SerializeField] private Vector2 effectSize = new Vector2(1024f, 1024f);
        [Tooltip("재생 FPS — 시트 프레임 수에 맞춰 조정. 보통 12~24.")]
        [SerializeField] private float defaultFps = 24f;

        [Header("개별 효과 위치 오버라이드 (Inspector에서 effectName 매핑)")]
        [SerializeField] private List<EffectPositionOverride> positionOverrides = new List<EffectPositionOverride>();

        [System.Serializable]
        public struct EffectPositionOverride
        {
            public string effectName;
            public Vector2 anchoredPos;
        }

        [Header("디버그")]
        [SerializeField] private bool logMissing = false;

        // 시트 캐시 — 반복 로드 회피
        private readonly Dictionary<string, Sprite[]> _cache = new Dictionary<string, Sprite[]>();
        // 누락 경고 1회만 출력
        private readonly HashSet<string> _warned = new HashSet<string>();

        private RectTransform _selfRect;
        public RectTransform Rect => _selfRect != null ? _selfRect : _selfRect = (RectTransform)transform;

        /// <summary>
        /// 카드 사용 시 호출. effectName에 해당하는 시트가 있으면 적절한 위치에 1회 재생.
        /// </summary>
        public void Play(CardData card)
        {
            if (card == null) return;
            string effectName = card.effectName;
            if (string.IsNullOrEmpty(effectName)) return;

            Sprite[] frames = LoadFrames(effectName);
            if (frames == null || frames.Length == 0)
            {
                if (logMissing && _warned.Add(effectName))
                    Debug.LogWarning($"[CardEffect] '{effectName}' 시트 없음 — Resources/CardEffects/{effectName}.png 확인");
                return;
            }

            Vector2 pos = ResolvePositionFor(effectName);
            SpawnEffect(frames, pos);
        }

        Sprite[] LoadFrames(string effectName)
        {
            if (_cache.TryGetValue(effectName, out var cached)) return cached;
            // 슬라이스된 시트의 모든 sprite 로드(이름순). 단일 sprite도 길이 1로 반환됨.
            var arr = Resources.LoadAll<Sprite>($"CardEffects/{effectName}");
            _cache[effectName] = arr;
            return arr;
        }

        Vector2 ResolvePositionFor(string effectName)
        {
            // 1순위 — Inspector 오버라이드 리스트
            for (int i = 0; i < positionOverrides.Count; i++)
            {
                if (string.Equals(positionOverrides[i].effectName, effectName, System.StringComparison.OrdinalIgnoreCase))
                    return positionOverrides[i].anchoredPos;
            }

            // 2순위 — Earth_ATK 전용 위치 (땅 공격은 좀 더 아래)
            if (string.Equals(effectName, "Earth_ATK", System.StringComparison.OrdinalIgnoreCase))
                return earthAtkAnchoredPos;

            // 3순위 — 카테고리별 기본 위치
            string up = effectName.ToUpperInvariant();
            if (up.EndsWith("_ATK") || up.StartsWith("BURN") || up.StartsWith("CHAIN"))
                return enemyAnchoredPos;
            return playerAnchoredPos;
        }

        void SpawnEffect(Sprite[] frames, Vector2 anchoredPos)
        {
            var go = new GameObject($"FX_{frames[0].name}", typeof(RectTransform), typeof(Image), typeof(CardEffectAnimator));
            var rt = (RectTransform)go.transform;
            rt.SetParent(Rect, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
            rt.sizeDelta = effectSize;
            rt.SetAsLastSibling();

            var anim = go.GetComponent<CardEffectAnimator>();
            anim.Play(frames, defaultFps);
        }
    }
}
