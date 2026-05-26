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
        [Tooltip("효과 이미지 기본 크기.")]
        [SerializeField] private Vector2 effectSize = new Vector2(1024f, 1024f);
        [Tooltip("Heal_EFF 전용 — 화면 하단 풀폭 띠 모드. 가로는 자동 stretch, Y만 사용(높이).")]
        [SerializeField] private Vector2 healEffSize = new Vector2(0f, 800f);
        [Tooltip("Heal_EFF 전용 — 화면 바닥에서 위쪽 Y 오프셋.")]
        [SerializeField] private float healEffBottomY = 0f;
        [Tooltip("재생 FPS — 시트 프레임 수에 맞춰 조정. 보통 12~24.")]
        [SerializeField] private float defaultFps = 24f;

        [Header("개별 효과 위치 오버라이드 (Inspector에서 effectName 매핑)")]
        [SerializeField] private List<EffectPositionOverride> positionOverrides = new List<EffectPositionOverride>();

        [Header("개별 효과 크기 오버라이드 (Inspector에서 effectName 매핑)")]
        [SerializeField] private List<EffectSizeOverride> sizeOverrides = new List<EffectSizeOverride>();

        [System.Serializable]
        public struct EffectPositionOverride
        {
            public string effectName;
            public Vector2 anchoredPos;
        }

        [System.Serializable]
        public struct EffectSizeOverride
        {
            public string effectName;
            public Vector2 size;
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
            Vector2 size = ResolveSizeFor(effectName);
            SpawnEffect(frames, pos, size, effectName);
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

        Vector2 ResolveSizeFor(string effectName)
        {
            // 1순위 — Inspector 오버라이드 리스트
            for (int i = 0; i < sizeOverrides.Count; i++)
            {
                if (string.Equals(sizeOverrides[i].effectName, effectName, System.StringComparison.OrdinalIgnoreCase))
                    return sizeOverrides[i].size;
            }

            // 2순위 — Heal_EFF 전용 크기 (회복은 옆으로 늘림)
            if (string.Equals(effectName, "Heal_EFF", System.StringComparison.OrdinalIgnoreCase))
                return healEffSize;

            // 3순위 — 기본 크기
            return effectSize;
        }

        void SpawnEffect(Sprite[] frames, Vector2 anchoredPos, Vector2 size, string effectName)
        {
            var go = new GameObject($"FX_{frames[0].name}", typeof(RectTransform), typeof(Image), typeof(CardEffectAnimator));
            var rt = (RectTransform)go.transform;
            rt.SetParent(Rect, false);

            bool bottomStretch = string.Equals(effectName, "Heal_EFF", System.StringComparison.OrdinalIgnoreCase);

            if (bottomStretch)
            {
                // 화면 하단 풀폭 띠 — 회복 이펙트가 아래에서 전체적으로 올라오는 느낌
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(0f, healEffBottomY);
                // sizeDelta.x=0 이면 anchor stretch로 화면 폭에 자동 맞춤. y는 띠 높이.
                rt.sizeDelta = new Vector2(0f, size.y);
            }
            else
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = anchoredPos;
                rt.sizeDelta = size;
            }
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;
            rt.SetAsLastSibling();

            var anim = go.GetComponent<CardEffectAnimator>();
            // 풀폭 띠 모드에선 sprite를 RectTransform에 맞게 늘려야 함 (aspect 보존 X)
            anim.Play(frames, defaultFps, loop: false, preserveAspect: !bottomStretch);
        }
    }
}
