using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Battle.UI
{
    /// <summary>
    /// 플레이어가 HP를 잃을 때 화면 전체에 빨간 플래시를 띄우는 오버레이.
    /// Player.OnHpDecreased(int amount)를 구독해서 자동 발화.
    /// 풀스크린 Image를 자동 생성하므로 인스펙터 연결 없이도 동작.
    /// </summary>
    public class PlayerDamageOverlay : MonoBehaviour
    {
        [Header("화면 빨간 플래시")]
        [SerializeField] private bool enableScreenFlash = true;
        [Tooltip("화면 전체를 덮을 빨간 Image. 비우면 자동 생성.")]
        [SerializeField] private Image flashImage;
        [Tooltip("플래시 색 — 알파는 시작 강도. fade에 따라 0으로 감소.")]
        [SerializeField] private Color flashColor = new Color(1f, 0f, 0f, 0.45f);
        [Tooltip("플래시 지속 시간(초).")]
        [SerializeField] private float flashDuration = 0.4f;
        [Tooltip("ON이면 데미지가 클수록 플래시가 더 진해짐.")]
        [SerializeField] private bool scaleWithDamage = true;
        [Tooltip("scaleWithDamage 켜졌을 때 — 이 값 이상 데미지면 풀 강도.")]
        [SerializeField] private int referenceDamageForFullIntensity = 20;
        [Tooltip("최소 알파(데미지가 작아도 이 이하로는 안 떨어짐).")]
        [Range(0f, 1f)]
        [SerializeField] private float minAlpha = 0.15f;

        [Header("화면 흔들기 (Screen Space - Camera 호환)")]
        [SerializeField] private bool enableCameraShake = true;
        [Tooltip("흔들 대상 Transform들 — 비우면 메인 캔버스의 모든 자식을 자동 탐색.\n" +
                 "Screen Space - Camera 캔버스의 root는 매 프레임 카메라에 맞춰 재배치되므로,\n" +
                 "캔버스 안의 자식 UI들을 흔드는 게 게임 뷰에서 실제로 보임.")]
        [SerializeField] private List<Transform> shakeTargets = new List<Transform>();
        [Tooltip("최대 흔들기 강도(픽셀). 데미지에 따라 스케일됨.")]
        [SerializeField] private float shakeIntensity = 25f;
        [Tooltip("흔들기 지속 시간(초).")]
        [SerializeField] private float shakeDuration = 0.3f;

        [Header("HP 바 깜빡임")]
        [SerializeField] private bool enableHpBarFlash = true;
        [Tooltip("HP 바의 Fill Image. 비우면 Player.hpBar에서 자동 탐색.")]
        [SerializeField] private Image hpBarFillImage;
        [Tooltip("깜빡일 색.")]
        [SerializeField] private Color hpBarFlashColor = Color.white;
        [Tooltip("깜빡임 지속 시간.")]
        [SerializeField] private float hpBarFlashDuration = 0.35f;

        private Player _player;
        private Coroutine _activeFlash;
        private Coroutine _activeShake;
        private Coroutine _activeHpFlash;
        private readonly List<Vector3> _shakeOrigins = new List<Vector3>();
        private Color _hpBarOriginColor;
        private bool _hpBarOriginCaptured;

        void Start()
        {
            EnsureFlashImage();
            SetAlpha(0f);

            // Player 자동 구독
            _player = Player.Resolve(true);
            if (_player != null)
            {
                _player.OnHpDecreased -= OnPlayerHpDecreased;
                _player.OnHpDecreased += OnPlayerHpDecreased;
            }

            // 화면 흔들기 대상 자동 탐색 — 캔버스 자식들을 모두 흔든다.
            // (캔버스 root 자체는 Screen Space - Camera에서 Unity가 매 프레임 재배치하므로 효과 없음)
            if (shakeTargets == null || shakeTargets.Count == 0)
            {
                shakeTargets = new List<Transform>();
                var canvas = ResolveTargetCanvas();
                if (canvas != null)
                {
                    foreach (Transform child in canvas.transform)
                    {
                        // 본인이 만든 풀스크린 플래시는 제외 (흔들 필요 X)
                        if (flashImage != null && child == flashImage.transform) continue;
                        shakeTargets.Add(child);
                    }
                }
            }
            // 원위치 캡처
            _shakeOrigins.Clear();
            for (int i = 0; i < shakeTargets.Count; i++)
            {
                _shakeOrigins.Add(shakeTargets[i] != null ? shakeTargets[i].localPosition : Vector3.zero);
            }

            // HP 바 Fill 자동 탐색
            if (hpBarFillImage == null && _player != null && _player.hpBar != null)
            {
                var fillRect = _player.hpBar.fillRect;
                if (fillRect != null) hpBarFillImage = fillRect.GetComponent<Image>();
            }
            if (hpBarFillImage != null)
            {
                _hpBarOriginColor = hpBarFillImage.color;
                _hpBarOriginCaptured = true;
            }
        }

        void OnDestroy()
        {
            if (_player != null) _player.OnHpDecreased -= OnPlayerHpDecreased;
            // 코루틴 중단 후 원상복구 — 흔들기 / HP 바 색이 stale로 남지 않게
            RestoreShakeOrigins();
            if (_hpBarOriginCaptured && hpBarFillImage != null) hpBarFillImage.color = _hpBarOriginColor;
        }

        Canvas ResolveTargetCanvas()
        {
            // CombatStage 우선 (NewBattleController도 같은 패턴)
            var combatStage = GameObject.Find("CombatStage");
            if (combatStage != null)
            {
                var c = combatStage.GetComponentInChildren<Canvas>(true);
                if (c != null) return c;
            }
            // 자기 부모 캔버스
            var parent = GetComponentInParent<Canvas>();
            if (parent != null) return parent;
            return FindFirstObjectByType<Canvas>();
        }

        void RestoreShakeOrigins()
        {
            for (int i = 0; i < shakeTargets.Count && i < _shakeOrigins.Count; i++)
            {
                if (shakeTargets[i] != null) shakeTargets[i].localPosition = _shakeOrigins[i];
            }
        }

        void OnPlayerHpDecreased(int amount)
        {
            if (amount <= 0) return;

            float intensity = 1f;
            if (scaleWithDamage)
            {
                float ratio = (float)amount / Mathf.Max(1, referenceDamageForFullIntensity);
                intensity = Mathf.Clamp(ratio, minAlpha / Mathf.Max(0.001f, flashColor.a), 1f);
            }

            // 1) 화면 빨간 플래시
            if (enableScreenFlash)
            {
                if (_activeFlash != null) StopCoroutine(_activeFlash);
                _activeFlash = StartCoroutine(FlashRoutine(intensity));
            }

            // 2) 화면 흔들기
            TriggerShake(intensity);

            // 3) HP 바 깜빡임
            if (enableHpBarFlash && hpBarFillImage != null)
            {
                if (_activeHpFlash != null) StopCoroutine(_activeHpFlash);
                _activeHpFlash = StartCoroutine(HpBarFlashRoutine());
            }
        }

        /// <summary>
        /// 외부에서 임의 강도(0~∞)로 화면 셰이크 요청. 진행 중인 셰이크가 있으면 중단 후 새로 시작.
        /// 피버 종료의 콤보 데미지 같은 "플레이어 피격이 아닌" 시각 임팩트에 사용.
        /// </summary>
        public void TriggerShake(float intensityMul)
        {
            if (!enableCameraShake) return;
            if (shakeTargets == null || shakeTargets.Count == 0) return;
            if (intensityMul <= 0f) return;

            if (_activeShake != null)
            {
                StopCoroutine(_activeShake);
                RestoreShakeOrigins(); // 새 셰이크 전에 원위치 복구
            }
            _activeShake = StartCoroutine(ShakeRoutine(intensityMul));
        }

        IEnumerator ShakeRoutine(float intensityMul)
        {
            if (shakeTargets == null || shakeTargets.Count == 0) yield break;
            float timer = 0f;
            float strength = shakeIntensity * Mathf.Clamp01(intensityMul);

            while (timer < shakeDuration)
            {
                timer += Time.deltaTime;
                float decay = 1f - (timer / shakeDuration); // 시간이 흐를수록 감쇠
                // 같은 프레임 안에서 모든 target에 동일 오프셋 적용 (함께 흔들리도록)
                float x = Random.Range(-1f, 1f) * strength * decay;
                float y = Random.Range(-1f, 1f) * strength * decay;
                var offset = new Vector3(x, y, 0f);
                for (int i = 0; i < shakeTargets.Count && i < _shakeOrigins.Count; i++)
                {
                    if (shakeTargets[i] != null)
                        shakeTargets[i].localPosition = _shakeOrigins[i] + offset;
                }
                yield return null;
            }
            RestoreShakeOrigins();
            _activeShake = null;
        }

        IEnumerator HpBarFlashRoutine()
        {
            if (hpBarFillImage == null) yield break;
            float timer = 0f;
            Color start = hpBarFlashColor;
            Color end = _hpBarOriginCaptured ? _hpBarOriginColor : hpBarFillImage.color;

            while (timer < hpBarFlashDuration)
            {
                timer += Time.deltaTime;
                float t = timer / hpBarFlashDuration;
                if (hpBarFillImage != null)
                    hpBarFillImage.color = Color.Lerp(start, end, t);
                yield return null;
            }
            if (hpBarFillImage != null) hpBarFillImage.color = end;
            _activeHpFlash = null;
        }

        IEnumerator FlashRoutine(float intensityMul)
        {
            if (flashImage == null) yield break;

            float startAlpha = flashColor.a * intensityMul;
            float timer = 0f;

            while (timer < flashDuration)
            {
                timer += Time.deltaTime;
                float t = timer / flashDuration;
                // 빠르게 풀강도 → 부드럽게 페이드
                float a = startAlpha * (1f - t);
                SetAlpha(a);
                yield return null;
            }
            SetAlpha(0f);
            _activeFlash = null;
        }

        void SetAlpha(float a)
        {
            if (flashImage == null) return;
            var c = flashColor;
            c.a = Mathf.Clamp01(a);
            flashImage.color = c;
        }

        void EnsureFlashImage()
        {
            if (flashImage != null) return;

            // 캔버스 자기 부모로 풀스크린 Image 생성
            var canvas = GetComponentInParent<Canvas>();
            Transform parent = canvas != null ? canvas.transform : transform;

            var go = new GameObject("PlayerDamageFlash", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.SetAsLastSibling();

            flashImage = go.GetComponent<Image>();
            flashImage.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
            flashImage.raycastTarget = false; // 클릭 차단 X
        }
    }
}
