using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Coffee.UIExtensions;
using Battle.Card;
using Sashindo.VFX;

namespace Battle.UI
{
    // 카드 사용 시 효과 애니메이션을 화면에 재생(시트/파티클 자동 로드)
    public class CardEffectOverlay : MonoBehaviour
    {
        [Header("효과 표시 위치 (캔버스 anchored 좌표)")]
        [Tooltip("적 대상 효과(*_ATK, Burn_EFF, Chain_EFF) 위치 — 화면 상단 쪽.")]
        [SerializeField] private Vector2 enemyAnchoredPos = new Vector2(0f, 300f); // 적 대상 효과 위치
        [Tooltip("플레이어 대상 효과(*_DEF, *_POW, Heal_EFF) 위치 — 화면 하단 쪽.")]
        [SerializeField] private Vector2 playerAnchoredPos = new Vector2(0f, 100f); // 플레이어 대상 효과 위치
        [Tooltip("Earth_ATK 전용 위치 — 땅 공격은 좀 더 아래쪽에서 표시.")]
        [SerializeField] private Vector2 earthAtkAnchoredPos = new Vector2(0f, 100f); // Earth_ATK 전용 위치
        [Tooltip("ON이면 적 대상 효과(*_ATK, Burn, Chain)를 카드의 대상 몬스터 화면 위치에서 재생. 위치 값들은 enemyAnchoredPos 대비 오프셋으로 적용된다.")]
        [SerializeField] private bool followTargetEnemy = true; // 대상 몬스터 위치 추적
        [Tooltip("대상 몬스터 조준 중심에서 추가로 옮길 오프셋(px).")]
        [SerializeField] private Vector2 targetEnemyOffset = Vector2.zero; // 대상 위치 추가 오프셋
        [Tooltip("효과 이미지 기본 크기.")]
        [SerializeField] private Vector2 effectSize = new Vector2(1024f, 1024f); // 효과 기본 크기
        [Tooltip("Heal_EFF 전용 — 화면 하단 풀폭 띠 모드. 가로는 자동 stretch, Y만 사용(높이).")]
        [SerializeField] private Vector2 healEffSize = new Vector2(0f, 800f); // Heal_EFF 전용 크기
        [Tooltip("Heal_EFF 전용 — 화면 바닥에서 위쪽 Y 오프셋.")]
        [SerializeField] private float healEffBottomY = 0f; // Heal_EFF 바닥 Y 오프셋
        [Tooltip("재생 FPS — 시트 프레임 수에 맞춰 조정. 보통 12~24.")]
        [SerializeField] private float defaultFps = 24f; // 기본 재생 FPS
        [Tooltip("재생 속도 배율 — 스프라이트 FPS와 파티클 시뮬레이션 속도에 함께 곱해진다. 1=원본, 0.8=더 천천히(오래 보임). 순식간에 사라지면 낮춰라.")]
        [SerializeField, Range(0.25f, 5f)] private float playbackSpeed = 1.0f; // 재생 속도 배율
        [Tooltip("이펙트를 손패 등 다른 UI보다 앞에 그리기 위한 Sort Order. 자체 Canvas+Override Sorting을 자동 설정한다. 손패보다 뒤면 값을 키우세요.")]
        [SerializeField] private int foregroundSortingOrder = 1000; // 전경 정렬 순서

        // 적 대상 효과 기준 위치(각성 표식을 몬스터 주위에 배치할 때 사용)
        public Vector2 EnemyAnchoredPos => enemyAnchoredPos;

        [Header("각성 콤보 표식 (콤보 성공 시 몬스터 주위에 표시)")]
        [Tooltip("각성 중 콤보 성공마다 몬스터 주위에 띄울 표식 스프라이트. 비우면 내장 원형 UI 스프라이트를 사용.")]
        [SerializeField] private Sprite awakenMarkerSprite; // 표식 스프라이트(없으면 기본 원형)
        [Tooltip("표식 한 변 크기(px).")]
        [SerializeField] private float awakenMarkerSize = 120f; // 표식 크기
        [Tooltip("표식 색(은은한 발광을 위해 알파를 약간 낮추는 것을 권장).")]
        [SerializeField] private Color awakenMarkerColor = new Color(1f, 0.55f, 0.18f, 0.95f); // 표식 색

        [Header("개별 효과 위치 오버라이드 (Inspector에서 effectName 매핑)")]
        [SerializeField] private List<EffectPositionOverride> positionOverrides = new List<EffectPositionOverride>(); // 효과별 위치 오버라이드

        [Header("개별 효과 크기 오버라이드 (Inspector에서 effectName 매핑)")]
        [SerializeField] private List<EffectSizeOverride> sizeOverrides = new List<EffectSizeOverride>(); // 효과별 크기 오버라이드

        // 효과 이름과 anchored 위치 매핑 구조체
        [System.Serializable]
        public struct EffectPositionOverride
        {
            public string effectName; // 대상 효과 이름
            public Vector2 anchoredPos; // 적용할 위치
        }

        // 효과 이름과 크기 매핑 구조체
        [System.Serializable]
        public struct EffectSizeOverride
        {
            public string effectName; // 대상 효과 이름
            public Vector2 size; // 적용할 크기
        }

        [Header("파티클 프리팹 (Resources/CardEffects/{effectName} 프리팹 우선)")]
        [Tooltip("ON이면 effectName과 같은 이름의 프리팹이 있을 때 스프라이트 시트 대신 UIParticle로 재생.")]
        [SerializeField] private bool preferParticlePrefab = true; // 파티클 프리팹 우선 사용 여부
        [Tooltip("UIParticle 기본 스케일 — 파티클이 월드 단위로 제작되므로 UI에서 키워야 보인다. 플레이로 튜닝.")]
        [SerializeField] private float particleScale = 100f; // UIParticle 기본 스케일
        [Tooltip("파티클 효과별 스케일 오버라이드 (effectName 매핑).")]
        [SerializeField] private List<EffectScaleOverride> particleScaleOverrides = new List<EffectScaleOverride>(); // 파티클별 스케일 오버라이드
        [Tooltip("파티클 자동 소멸 시 추가 여유시간(초).")]
        [SerializeField] private float particleLifetimePadding = 0.5f; // 파티클 소멸 여유시간
        [Tooltip("파티클 위치 오프셋 (effectName별) — 인스턴스를 래퍼 중심(0,0)으로 리셋한 뒤 이 값만큼 이동. 이펙트마다 위치 미세조정.")]
        [SerializeField] private List<EffectPositionOverride> particlePositionOffsets = new List<EffectPositionOverride>(); // 파티클별 위치 오프셋

        // 효과 이름과 파티클 스케일 매핑 구조체
        [System.Serializable]
        public struct EffectScaleOverride
        {
            public string effectName; // 대상 효과 이름
            public float scale; // 적용할 스케일
        }

        [Header("디버그")]
        [SerializeField] private bool logMissing = false; // 누락 로그 출력 여부

        private readonly Dictionary<string, Sprite[]> _cache = new Dictionary<string, Sprite[]>(); // 시트 캐시
        private readonly Dictionary<string, GameObject> _prefabCache = new Dictionary<string, GameObject>(); // 파티클 프리팹 캐시
        private readonly HashSet<string> _warned = new HashSet<string>(); // 누락 경고 1회 출력용
        private readonly List<GameObject> _activeFx = new List<GameObject>(); // 활성 FX 인스턴스 목록

        private GuardShieldEffect _guardShield;
        private GuardBreakEffect _guardBreak;
        private GuardBlockSettings _guardBlockSettings;
        private CanvasGroup _entryVisibility;
        private float _alphaBeforeEntry;
        private bool _hiddenDuringEntry;

        private RectTransform _selfRect; // 자신의 RectTransform 캐시
        private static int s_runtimeSortingOverride = int.MinValue; // 일시정지 중 전역 정렬 덮어쓰기
        // 자신의 RectTransform 반환(지연 캐싱)
        public RectTransform Rect => _selfRect != null ? _selfRect : _selfRect = (RectTransform)transform;

        // 시작 시 전경 캔버스를 보장
        void Awake()
        {
            EnsureForegroundCanvas();
        }

        void LateUpdate()
        {
            bool hide = NewBattleController.IsAwakeningEntryPlaying;
            if (hide == _hiddenDuringEntry) return;

            if (hide)
            {
                if (_entryVisibility == null &&
                    !TryGetComponent(out _entryVisibility))
                    _entryVisibility = gameObject.AddComponent<CanvasGroup>();
                _alphaBeforeEntry = _entryVisibility.alpha;
                _entryVisibility.alpha = 0f;
                _hiddenDuringEntry = true;
                GuardBlockRendererFeature.Stop();
            }
            else
            {
                RestoreEntryVisibility();
            }
        }

        void OnDisable()
        {
            RestoreEntryVisibility();
        }

        void RestoreEntryVisibility()
        {
            if (!_hiddenDuringEntry) return;
            if (_entryVisibility != null) _entryVisibility.alpha = _alphaBeforeEntry;
            _hiddenDuringEntry = false;
        }

        // 일시정지 창보다 뒤에 두거나, 기본 전경 정렬로 되돌린다
        public static void SetPauseOverlaySorting(bool paused, int pausedOrder, int defaultOrder)
        {
            s_runtimeSortingOverride = paused ? pausedOrder : int.MinValue;
            int order = paused ? pausedOrder : defaultOrder;
            var overlays = FindObjectsByType<CardEffectOverlay>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < overlays.Length; i++)
            {
                if (overlays[i] != null)
                    overlays[i].ApplySortingOrder(order);
            }
        }

        // 자체 Canvas + Override Sorting으로 다른 UI보다 앞에 그려지도록 보장
        void EnsureForegroundCanvas()
        {
            ApplySortingOrder(ResolveSortingOrder());
        }

        int ResolveSortingOrder()
        {
            return s_runtimeSortingOverride != int.MinValue ? s_runtimeSortingOverride : foregroundSortingOrder;
        }

        void ApplySortingOrder(int order)
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = order;

            var canvases = GetComponentsInChildren<Canvas>(true);
            for (int c = 0; c < canvases.Length; c++)
            {
                if (canvases[c] == null) continue;
                canvases[c].overrideSorting = true;
                canvases[c].sortingOrder = order;
            }
        }

        GuardShieldEffect ResolveGuardShieldPrefab()
        {
            var prefab = Resources.Load<GameObject>("VFX/GuardShieldCombat");
            return prefab != null ? prefab.GetComponent<GuardShieldEffect>() : null;
        }

        public void PlayGuardGain()
        {
            if (!isActiveAndEnabled) return;
            GuardShieldEffect prefab = ResolveGuardShieldPrefab();
            if (prefab == null) return;

            if (_guardShield == null)
            {
                _guardShield = Instantiate(prefab, Rect, false);
                var rect = (RectTransform)_guardShield.transform;
                rect.localScale = Vector3.one;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            _guardShield.gameObject.SetActive(true);
            _guardShield.PlayFromPrefab(prefab);
        }

        public void PlayGuardBreak()
        {
            if (!isActiveAndEnabled) return;
            GuardBlockRendererFeature.Stop();

            if (_guardBreak == null)
            {
                var prefab = Resources.Load<GameObject>("VFX/GuardBreakCombat");
                var graphic = prefab != null ? prefab.GetComponentInChildren<GuardBreakEffect>() : null;
                if (graphic == null) return;

                // Clone the graphic into the battle canvas to inherit its full-screen size and pause sorting.
                _guardBreak = Instantiate(graphic, Rect, false);
            }

            if (_guardShield != null)
                _guardShield.gameObject.SetActive(false);

            _guardBreak.transform.SetAsLastSibling();
            _guardBreak.Play();
        }

        [ContextMenu("완전 방어 미리보기")]
        public void PlayGuardBlock()
        {
            if (!Application.isPlaying || !isActiveAndEnabled || NewBattleController.IsAwakeningEntryPlaying) return;

            if (_guardBlockSettings == null)
                _guardBlockSettings = Resources.Load<GuardBlockSettings>("VFX/GuardBlockSettings");

            var stage = BattleStageController.Current;
            var camera = stage != null ? stage.StageCamera : Camera.main;
            if (camera != null && _guardBlockSettings != null)
                GuardBlockRendererFeature.Play(camera, _guardBlockSettings);
        }

        // 카드의 effectName으로 효과를 1회 재생
        public void Play(CardData card)
        {
            if (card == null) return;
            PlayByName(card.effectName);
        }

        // 카드 효과를 재생 — 적 대상 효과면 target 몬스터의 화면 위치에서(없으면 기본 위치)
        public void Play(CardData card, EnemyController target)
        {
            if (card == null || string.IsNullOrEmpty(card.effectName)) return;
            string effectName = card.effectName;

            if (followTargetEnemy && target != null && IsEnemyTargetEffect(effectName)
                && target.TryGetAimScreen(out Vector2 screen) && TryScreenToAnchored(screen, out Vector2 at))
            {
                // 기존 위치 설정(Earth_ATK 전용·개별 오버라이드)은 기본 적 위치 대비 차이만큼 유지
                Vector2 delta = ResolvePositionFor(effectName) - enemyAnchoredPos;
                PlayAtCore(effectName, at + delta + targetEnemyOffset);
                return;
            }
            PlayByName(effectName);
        }

        internal static bool IsGuardGainEffect(string effectName)
        {
            return effectName != null && effectName.EndsWith("_DEF", System.StringComparison.OrdinalIgnoreCase);
        }

        // 이름 규칙상 적에게 재생되는 효과인지(공격·화상·연쇄)
        static bool IsEnemyTargetEffect(string effectName)
        {
            string up = effectName.ToUpperInvariant();
            return up.EndsWith("_ATK") || up.StartsWith("BURN") || up.StartsWith("CHAIN");
        }

        // 스크린 좌표 → 이 오버레이의 중앙 기준 anchored 좌표
        bool TryScreenToAnchored(Vector2 screen, out Vector2 anchored)
        {
            anchored = default;
            Canvas canvas = GetComponentInParent<Canvas>();
            Canvas root = canvas != null ? canvas.rootCanvas : null;
            Camera cam = (root == null || root.renderMode == RenderMode.ScreenSpaceOverlay) ? null : root.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect, screen, cam, out Vector2 local))
                return false;
            anchored = local - Rect.rect.center; // 자식은 부모 중앙 앵커 기준
            return true;
        }

        // 임의 effectName을 기본 위치에 직접 재생

        public void PlayByName(string effectName)
        {
            PlayByNameAtOffset(effectName, Vector2.zero);
        }

        // 기본 위치에 offset을 더한 위치에서 효과를 재생
        public void PlayByNameAtOffset(string effectName, Vector2 offsetFromDefault)
        {
            if (string.IsNullOrEmpty(effectName)) return;
            PlayAtCore(effectName, ResolvePositionFor(effectName) + offsetFromDefault);
        }

        // 절대 anchored 위치에 효과를 재생(각성 표식 폭발 등 위치 직접 지정용)
        public void PlayByNameAtPosition(string effectName, Vector2 anchoredPos)
        {
            PlayByNameAtPosition(effectName, anchoredPos, true);
        }

        // playSfx가 꺼지면 같은 프레임에 이미 난 효과음은 다시 내지 않고 그림만 재생한다
        public void PlayByNameAtPosition(string effectName, Vector2 anchoredPos, bool playSfx)
        {
            if (string.IsNullOrEmpty(effectName)) return;
            PlayAtCore(effectName, anchoredPos, playSfx);
        }

        // 화면 좌표를 이 오버레이의 anchored 좌표로 바꾼다
        public bool TryAnchoredFromScreen(Vector2 screen, out Vector2 anchored)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            Camera cam = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
            anchored = default;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect, screen, cam, out Vector2 local))
                return false;
            // 이펙트는 부모 중앙 앵커 기준으로 놓인다. local은 피벗 기준이라 중앙으로 맞춘다.
            Vector2 center = new Vector2(
                (0.5f - Rect.pivot.x) * Rect.rect.width,
                (0.5f - Rect.pivot.y) * Rect.rect.height);
            anchored = local - center;
            return true;
        }

        // 지정된 anchored 위치에서 프리팹/시트 효과를 재생(공통 코어)
        void PlayAtCore(string effectName, Vector2 pos, bool playSfx = true)
        {
            if (playSfx)
                SfxManager.Instance?.PlayEffect(effectName);

            if (NewBattleController.IsAwakeningEntryPlaying) return;

            // 방어도 획득 이벤트에서 공통 방패를 재생한다.
            if (IsGuardGainEffect(effectName) && ResolveGuardShieldPrefab() != null)
                return;

            // 1순위로 파티클 프리팹이 있으면 사용
            if (preferParticlePrefab)
            {
                GameObject prefab = LoadPrefab(effectName);
                if (prefab != null)
                {
                    SpawnParticle(prefab, pos, effectName);
                    return;
                }
            }

            // 프리팹이 없으면 스프라이트 시트로 폴백
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

        // 재생 중이거나 파괴 대기 중인 모든 FX를 즉시 제거
        public void ClearAll()
        {
            GuardBlockRendererFeature.Stop();
            if (_guardShield != null)
                _guardShield.gameObject.SetActive(false);
            if (_guardBreak != null)
                _guardBreak.Stop();
            for (int i = 0; i < _activeFx.Count; i++)
            {
                var go = _activeFx[i];
                if (go != null) Destroy(go);
            }
            _activeFx.Clear();
        }

        // 각성 콤보 표식을 anchoredPos에 생성하고 핸들 반환(콤보 성공 시 호출)
        // parentOverride를 주면 그 RectTransform 자식으로 생성(딤 바로 위 레이어 등). 풀스크린 부모여야 좌표가 일치.
        public GameObject SpawnAwakenMarker(Vector2 anchoredPos, RectTransform parentOverride = null)
        {
            var go = new GameObject("AwakenMarker", typeof(RectTransform), typeof(Image), typeof(AwakenMarkerView));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parentOverride != null ? parentOverride : Rect, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(awakenMarkerSize, awakenMarkerSize);
            rt.localRotation = Quaternion.identity;
            rt.SetAsLastSibling();
            _activeFx.Add(go);

            var img = go.GetComponent<Image>();
            img.sprite = awakenMarkerSprite != null ? awakenMarkerSprite : DefaultMarkerSprite();
            img.color = awakenMarkerColor;
            img.raycastTarget = false;
            img.preserveAspect = true;

            go.GetComponent<AwakenMarkerView>().Begin();
            return go;
        }

        // 각성 콤보 표식을 제거(폭발 이펙트는 호출 측에서 별도 재생)
        public void DestroyAwakenMarker(GameObject handle)
        {
            if (handle == null) return;
            _activeFx.Remove(handle);
            Destroy(handle);
        }

        private Sprite _defaultMarkerSprite; // 내장 원형 스프라이트 캐시
        // 표식 기본 비주얼용 내장 원형 스프라이트(Knob)
        Sprite DefaultMarkerSprite()
        {
            if (_defaultMarkerSprite != null) return _defaultMarkerSprite;
            _defaultMarkerSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
            return _defaultMarkerSprite;
        }

        // effectName에 해당하는 시트 프레임들을 로드(캐시)
        Sprite[] LoadFrames(string effectName)
        {
            if (_cache.TryGetValue(effectName, out var cached)) return cached;
            var arr = Resources.LoadAll<Sprite>($"CardEffects/{effectName}");
            _cache[effectName] = arr;
            return arr;
        }

        // effectName에 해당하는 파티클 프리팹을 로드(캐시)
        GameObject LoadPrefab(string effectName)
        {
            if (_prefabCache.TryGetValue(effectName, out var cached)) return cached;
            var prefab = Resources.Load<GameObject>($"CardEffects/{effectName}");
            _prefabCache[effectName] = prefab;
            return prefab;
        }

        // effectName에 맞는 표시 위치를 결정(오버라이드 > 전용 > 카테고리)
        Vector2 ResolvePositionFor(string effectName)
        {
            // 인스펙터 오버라이드 우선
            for (int i = 0; i < positionOverrides.Count; i++)
            {
                if (string.Equals(positionOverrides[i].effectName, effectName, System.StringComparison.OrdinalIgnoreCase))
                    return positionOverrides[i].anchoredPos;
            }

            // Earth_ATK 전용 위치
            if (string.Equals(effectName, "Earth_ATK", System.StringComparison.OrdinalIgnoreCase))
                return earthAtkAnchoredPos;

            // 이름 카테고리별 기본 위치(공격류는 적, 그 외 플레이어)
            string up = effectName.ToUpperInvariant();
            if (up.EndsWith("_ATK") || up.StartsWith("BURN") || up.StartsWith("CHAIN"))
                return enemyAnchoredPos;
            return playerAnchoredPos;
        }

        // effectName에 맞는 표시 크기를 결정(오버라이드 > 전용 > 기본)
        Vector2 ResolveSizeFor(string effectName)
        {
            // 인스펙터 오버라이드 우선
            for (int i = 0; i < sizeOverrides.Count; i++)
            {
                if (string.Equals(sizeOverrides[i].effectName, effectName, System.StringComparison.OrdinalIgnoreCase))
                    return sizeOverrides[i].size;
            }

            // Heal_EFF 전용 크기
            if (string.Equals(effectName, "Heal_EFF", System.StringComparison.OrdinalIgnoreCase))
                return healEffSize;

            // 기본 크기
            return effectSize;
        }

        // 시트 프레임으로 FX 오브젝트를 생성해 애니메이션 재생
        void SpawnEffect(Sprite[] frames, Vector2 anchoredPos, Vector2 size, string effectName)
        {
            var go = new GameObject($"FX_{frames[0].name}", typeof(RectTransform), typeof(Image), typeof(CardEffectAnimator));
            var rt = (RectTransform)go.transform;
            rt.SetParent(Rect, false);

            bool bottomStretch = string.Equals(effectName, "Heal_EFF", System.StringComparison.OrdinalIgnoreCase);

            if (bottomStretch)
            {
                // 화면 하단 풀폭 띠로 배치
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 0f);
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = new Vector2(0f, healEffBottomY);
                // x=0이면 화면 폭에 자동 stretch, y는 띠 높이
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
            _activeFx.Add(go);

            var anim = go.GetComponent<CardEffectAnimator>();
            anim.Play(frames, defaultFps * Mathf.Max(0.01f, playbackSpeed), loop: false, preserveAspect: !bottomStretch);
        }

        // 파티클 프리팹을 UIParticle 래퍼로 생성해 재생
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
            _activeFx.Add(go);

            var uip = go.AddComponent<UIParticle>();
            uip.scale = ResolveParticleScaleFor(effectName);

            // 프리팹을 래퍼 자식으로 붙이고 effectName별 오프셋만큼만 이동
            var inst = Instantiate(prefab);
            inst.transform.SetParent(go.transform, false);
            Vector2 off = ResolveParticleOffsetFor(effectName);
            inst.transform.localPosition = new Vector3(off.x, off.y, 0f);
            inst.transform.localRotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one;
            uip.RefreshParticles();

            // 스크린 공간에 보이도록 Local 시뮬레이션으로 강제
            ForceLocalSimulation(uip);
            uip.Play();

            // 재생 길이만큼 뒤 안전하게 파괴
            float life = ComputeParticleLifetime(uip) / Mathf.Max(0.01f, playbackSpeed) + Mathf.Max(0f, particleLifetimePadding);
            Destroy(go, life);
        }

        // 임의 파티클 프리팹을 화면 중앙에 1회 재생
        public void PlayPrefab(GameObject prefab, float scale = 0f, float duration = 0f)
        {
            if (prefab == null || NewBattleController.IsAwakeningEntryPlaying) return;

            GameObject go = SpawnPrefabFx(prefab, scale, loop: false, out var uip);
            float life = duration > 0f
                ? duration
                : ComputeParticleLifetime(uip) / Mathf.Max(0.01f, playbackSpeed) + Mathf.Max(0f, particleLifetimePadding);
            Destroy(go, life);
        }

        // 프리팹을 루프 재생해 멈출 때까지 유지(핸들 반환)
        public GameObject PlayPrefabPersistent(GameObject prefab, float scale = 0f)
        {
            if (prefab == null) return null;
            return SpawnPrefabFx(prefab, scale, loop: true, out _);
        }

        // 지속 재생 중인 프리팹을 즉시 정지하고 래퍼를 제거
        public void StopPersistent(GameObject handle)
        {
            if (handle == null) return;

            // 남은 입자를 즉시 비워 잔상까지 제거한 뒤 래퍼 파괴
            var systems = handle.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] == null) continue;
                systems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            _activeFx.Remove(handle);
            Destroy(handle);
        }

        // UIParticle 래퍼를 만들고 프리팹을 부착해 재생(공통 로직)
        GameObject SpawnPrefabFx(GameObject prefab, float scale, bool loop, out UIParticle uip)
        {
            var go = new GameObject($"FX_{prefab.name}", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(Rect, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            rt.SetAsLastSibling();
            _activeFx.Add(go);

            uip = go.AddComponent<UIParticle>();
            uip.scale = scale > 0f ? scale : particleScale;

            var inst = Instantiate(prefab);
            DisableCameraReparenting(inst);
            inst.transform.SetParent(go.transform, false);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one;

            if (loop)
            {
                // 비-루프 프리팹도 모든 파티클을 루프로 변경
                var systems = inst.GetComponentsInChildren<ParticleSystem>(true);
                for (int i = 0; i < systems.Length; i++)
                {
                    if (systems[i] == null) continue;
                    var main = systems[i].main;
                    main.loop = true;
                }
            }

            uip.RefreshParticles();
            ForceLocalSimulation(uip);
            uip.Play();
            return go;
        }

        // 인스턴스의 카메라 재부모화 스크립트(HS_ScreenEffect)를 비활성
        static void DisableCameraReparenting(GameObject inst)
        {
            var behaviours = inst.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                var b = behaviours[i];
                if (b == null) continue;
                // 카메라로 재부모화하는 풀스크린 스크립트를 비활성
                if (b.GetType().Name == "HS_ScreenEffect") b.enabled = false;
            }
        }

        // 모든 파티클을 Local 시뮬레이션으로 강제하고 속도 배율 적용
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
                main.simulationSpeed = main.simulationSpeed * Mathf.Max(0.01f, playbackSpeed);
            }
        }

        // effectName에 맞는 파티클 스케일을 결정(오버라이드 > 기본)
        float ResolveParticleScaleFor(string effectName)
        {
            for (int i = 0; i < particleScaleOverrides.Count; i++)
            {
                if (string.Equals(particleScaleOverrides[i].effectName, effectName, System.StringComparison.OrdinalIgnoreCase))
                    return particleScaleOverrides[i].scale;
            }
            return particleScale;
        }

        // effectName에 맞는 파티클 위치 오프셋을 결정(없으면 0)
        Vector2 ResolveParticleOffsetFor(string effectName)
        {
            for (int i = 0; i < particlePositionOffsets.Count; i++)
            {
                if (string.Equals(particlePositionOffsets[i].effectName, effectName, System.StringComparison.OrdinalIgnoreCase))
                    return particlePositionOffsets[i].anchoredPos;
            }
            return Vector2.zero;
        }

        // 자식 파티클의 (duration+최대 수명) 중 최댓값을 계산
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
