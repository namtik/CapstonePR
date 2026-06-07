using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Coffee.UIExtensions;
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

        [Header("파티클 프리팹 (Resources/CardEffects/{effectName} 프리팹 우선)")]
        [Tooltip("ON이면 effectName과 같은 이름의 프리팹이 있을 때 스프라이트 시트 대신 UIParticle로 재생.")]
        [SerializeField] private bool preferParticlePrefab = true;
        [Tooltip("UIParticle 기본 스케일 — 파티클이 월드 단위로 제작되므로 UI에서 키워야 보인다. 플레이로 튜닝.")]
        [SerializeField] private float particleScale = 100f;
        [Tooltip("파티클 효과별 스케일 오버라이드 (effectName 매핑).")]
        [SerializeField] private List<EffectScaleOverride> particleScaleOverrides = new List<EffectScaleOverride>();
        [Tooltip("파티클 자동 소멸 시 추가 여유시간(초).")]
        [SerializeField] private float particleLifetimePadding = 0.5f;

        [System.Serializable]
        public struct EffectScaleOverride
        {
            public string effectName;
            public float scale;
        }

        [Header("디버그")]
        [SerializeField] private bool logMissing = false;

        // 시트 캐시 — 반복 로드 회피
        private readonly Dictionary<string, Sprite[]> _cache = new Dictionary<string, Sprite[]>();
        // 파티클 프리팹 캐시 (없으면 null도 캐싱해 반복 로드 회피)
        private readonly Dictionary<string, GameObject> _prefabCache = new Dictionary<string, GameObject>();
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
            PlayByName(card.effectName);
        }

        /// <summary>
        /// 임의 effectName(예: "Fever_ATK")을 직접 재생. CardData 없이 콤보 스킬/전용 이펙트 등에 사용.
        /// 위치/크기 결정 규칙은 Play(CardData)와 동일 — 이름이 *_ATK / BURN* / CHAIN* 이면 적, 그 외엔 플레이어.
        /// </summary>
        public void PlayByName(string effectName)
        {
            PlayByNameAtOffset(effectName, Vector2.zero);
        }

        /// <summary>
        /// 같은 이펙트를 여러 번 분산해서 띄울 때 사용. 기본 위치(ResolvePositionFor 결과)에 offset을 더한 위치에서 재생.
        /// 콤보가 여러 건 발동될 때 위치를 어긋나게 해서 시각적으로 횟수를 구분할 수 있음.
        /// </summary>
        public void PlayByNameAtOffset(string effectName, Vector2 offsetFromDefault)
        {
            if (string.IsNullOrEmpty(effectName)) return;

            Vector2 pos = ResolvePositionFor(effectName) + offsetFromDefault;

            // 1순위 — 파티클 프리팹(Resources/CardEffects/{effectName}.prefab)
            if (preferParticlePrefab)
            {
                GameObject prefab = LoadPrefab(effectName);
                Debug.Log($"[CardEffect][진단] effectName='{effectName}' prefab={(prefab != null ? prefab.name : "NULL")}");
                if (prefab != null)
                {
                    SpawnParticle(prefab, pos, effectName);
                    return;
                }
            }

            // 2순위 — 스프라이트 시트(폴백)
            Sprite[] frames = LoadFrames(effectName);
            if (frames == null || frames.Length == 0)
            {
                if (logMissing && _warned.Add(effectName))
                    Debug.LogWarning($"[CardEffect] '{effectName}' 프리팹/시트 없음 — Resources/CardEffects/{effectName} 확인");
                return;
            }

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

        GameObject LoadPrefab(string effectName)
        {
            if (_prefabCache.TryGetValue(effectName, out var cached)) return cached;
            // 같은 이름의 .png(시트)와 .prefab이 공존해도 GameObject 타입만 로드됨.
            var prefab = Resources.Load<GameObject>($"CardEffects/{effectName}");
            _prefabCache[effectName] = prefab;
            return prefab;
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

        // ─────────────────────────────────────────────────────────────
        // 파티클 프리팹 재생 (UIParticle — Overlay 캔버스에서도 UI와 합성)
        // ─────────────────────────────────────────────────────────────

        void SpawnParticle(GameObject prefab, Vector2 anchoredPos, string effectName)
        {
            var go = new GameObject($"FX_{effectName}", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(Rect, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            rt.SetAsLastSibling();

            var uip = go.AddComponent<UIParticle>();
            uip.scale = ResolveParticleScaleFor(effectName);
            // 프리팹을 인스턴스화해 자식 ParticleSystem으로 등록(내부에서 RefreshParticles 수행).
            uip.SetParticleSystemPrefab(prefab);
            // UI(스크린) 공간에서 보이도록 모든 파티클을 Local 시뮬레이션으로 강제.
            // World 시뮬레이션이면 파티클이 캔버스 밖(월드 원점 근처)에서 터져 화면에 안 보인다.
            ForceLocalSimulation(uip);
            uip.Play();

            // 프리팹이 자동 파괴(stopAction) 설정이 없어도, 재생 길이만큼 뒤 안전하게 정리.
            float life = ComputeParticleLifetime(uip) + Mathf.Max(0f, particleLifetimePadding);
            Debug.Log($"[CardEffect][진단] FX go={go.name} 부모={Rect.name} 자식수={go.transform.childCount} particles수={(uip.particles != null ? uip.particles.Count : -1)} scale={uip.scale} pos={anchoredPos} life={life}");
            Destroy(go, life);
        }

        void ForceLocalSimulation(UIParticle uip)
        {
            var list = uip != null ? uip.particles : null;
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                var ps = list[i];
                if (ps == null) continue;
                var main = ps.main;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
            }
        }

        float ResolveParticleScaleFor(string effectName)
        {
            for (int i = 0; i < particleScaleOverrides.Count; i++)
            {
                if (string.Equals(particleScaleOverrides[i].effectName, effectName, System.StringComparison.OrdinalIgnoreCase))
                    return particleScaleOverrides[i].scale;
            }
            return particleScale;
        }

        // 자식 파티클들의 (duration + 최대 수명) 중 가장 긴 시간 — 1회 재생 후 파괴 시점.
        float ComputeParticleLifetime(UIParticle uip)
        {
            float max = 0f;
            var list = uip != null ? uip.particles : null;
            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var ps = list[i];
                    if (ps == null) continue;
                    var main = ps.main;
                    float d = main.duration + main.startLifetime.constantMax;
                    if (d > max) max = d;
                }
            }
            return max > 0f ? max : 3f;
        }
    }
}
