using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Battle.UI
{
    // 플레이어 피격 시 빨간 플래시/흔들기/HP바 깜빡임을 띄우는 오버레이
    public class PlayerDamageOverlay : MonoBehaviour
    {
        [Header("화면 빨간 플래시")]
        [SerializeField] private bool enableScreenFlash = true; // 화면 플래시 사용 여부
        [Tooltip("화면 전체를 덮을 빨간 Image. 비우면 자동 생성.")]
        [SerializeField] private Image flashImage; // 플래시 이미지
        [Tooltip("플래시 색 — 알파는 시작 강도. fade에 따라 0으로 감소.")]
        [SerializeField] private Color flashColor = new Color(1f, 0f, 0f, 0.45f); // 플래시 색
        [Tooltip("플래시 지속 시간(초).")]
        [SerializeField] private float flashDuration = 0.4f; // 플래시 지속 시간
        [Tooltip("ON이면 데미지가 클수록 플래시가 더 진해짐.")]
        [SerializeField] private bool scaleWithDamage = true; // 데미지 비례 강도 여부
        [Tooltip("scaleWithDamage 켜졌을 때 — 이 값 이상 데미지면 풀 강도.")]
        [SerializeField] private int referenceDamageForFullIntensity = 20; // 풀 강도 기준 데미지
        [Tooltip("최소 알파(데미지가 작아도 이 이하로는 안 떨어짐).")]
        [Range(0f, 1f)]
        [SerializeField] private float minAlpha = 0.15f; // 최소 알파

        [Header("화면 흔들기 (Screen Space - Camera 호환)")]
        [SerializeField] private bool enableCameraShake = true; // 화면 흔들기 사용 여부
        [Tooltip("흔들 대상 Transform들 — 비우면 메인 캔버스의 모든 자식을 자동 탐색.\n" +
                 "Screen Space - Camera 캔버스의 root는 매 프레임 카메라에 맞춰 재배치되므로,\n" +
                 "캔버스 안의 자식 UI들을 흔드는 게 게임 뷰에서 실제로 보임.")]
        [SerializeField] private List<Transform> shakeTargets = new List<Transform>(); // 흔들 대상 목록
        [Tooltip("최대 흔들기 강도(픽셀). 데미지에 따라 스케일됨.")]
        [SerializeField] private float shakeIntensity = 25f; // 최대 흔들기 강도
        [Tooltip("흔들기 지속 시간(초).")]
        [SerializeField] private float shakeDuration = 0.3f; // 흔들기 지속 시간

        [Header("HP 바 깜빡임")]
        [SerializeField] private bool enableHpBarFlash = true; // HP바 깜빡임 사용 여부
        [Tooltip("HP 바의 Fill Image. 비우면 Player.hpBar에서 자동 탐색.")]
        [SerializeField] private Image hpBarFillImage; // HP바 채움 이미지
        [Tooltip("깜빡일 색.")]
        [SerializeField] private Color hpBarFlashColor = Color.white; // 깜빡임 색
        [Tooltip("깜빡임 지속 시간.")]
        [SerializeField] private float hpBarFlashDuration = 0.35f; // 깜빡임 지속 시간

        private Player _player; // 구독한 플레이어
        private Coroutine _activeFlash; // 진행 중 플래시 코루틴
        private Coroutine _activeShake; // 진행 중 흔들기 코루틴
        private Coroutine _activeHpFlash; // 진행 중 HP바 깜빡임 코루틴
        private readonly List<Vector3> _shakeOrigins = new List<Vector3>(); // 흔들기 대상 원위치
        private Color _hpBarOriginColor; // HP바 원본 색
        private bool _hpBarOriginCaptured; // HP바 원본 색 캡처 여부

        // 플래시 이미지 생성·Player 구독·흔들기 대상/HP바 초기화
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

            // 흔들기 대상이 없으면 캔버스 자식들을 자동 수집
            if (shakeTargets == null || shakeTargets.Count == 0)
            {
                shakeTargets = new List<Transform>();
                var canvas = ResolveTargetCanvas();
                if (canvas != null)
                {
                    foreach (Transform child in canvas.transform)
                    {
                        // 자체 플래시 이미지는 제외
                        if (flashImage != null && child == flashImage.transform) continue;
                        shakeTargets.Add(child);
                    }
                }
            }
            // 흔들기 대상 원위치 캡처
            _shakeOrigins.Clear();
            for (int i = 0; i < shakeTargets.Count; i++)
            {
                _shakeOrigins.Add(shakeTargets[i] != null ? shakeTargets[i].localPosition : Vector3.zero);
            }

            // HP 바 Fill 이미지 자동 탐색
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

        // 구독 해제 및 흔들기/HP바 색 원상복구
        void OnDestroy()
        {
            if (_player != null) _player.OnHpDecreased -= OnPlayerHpDecreased;
            // stale 상태가 남지 않도록 원상복구
            RestoreShakeOrigins();
            if (_hpBarOriginCaptured && hpBarFillImage != null) hpBarFillImage.color = _hpBarOriginColor;
        }

        // 재활성화 시 진행 중 연출을 정리해 잔상을 제거
        void OnEnable()
        {
            // 중단된 코루틴이 남긴 잔상을 정리
            if (_activeFlash != null) { StopCoroutine(_activeFlash); _activeFlash = null; }
            if (_activeShake != null) { StopCoroutine(_activeShake); _activeShake = null; }
            if (_activeHpFlash != null) { StopCoroutine(_activeHpFlash); _activeHpFlash = null; }
            SetAlpha(0f);
            RestoreShakeOrigins();
            if (_hpBarOriginCaptured && hpBarFillImage != null) hpBarFillImage.color = _hpBarOriginColor;
        }

        // 전투 스테이지/부모/씬 순으로 대상 캔버스를 탐색
        Canvas ResolveTargetCanvas()
        {
            // CombatStage 우선
            var combatStage = GameObject.Find("CombatStage");
            if (combatStage != null)
            {
                var c = combatStage.GetComponentInChildren<Canvas>(true);
                if (c != null) return c;
            }
            // 부모 캔버스
            var parent = GetComponentInParent<Canvas>();
            if (parent != null) return parent;
            return FindFirstObjectByType<Canvas>();
        }

        // 흔들기 대상들을 원위치로 복구
        void RestoreShakeOrigins()
        {
            for (int i = 0; i < shakeTargets.Count && i < _shakeOrigins.Count; i++)
            {
                if (shakeTargets[i] != null) shakeTargets[i].localPosition = _shakeOrigins[i];
            }
        }

        // 피격 시 강도를 계산해 플래시/흔들기/HP바 깜빡임을 발동
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

        // 외부에서 임의 강도로 화면 흔들기를 요청
        public void TriggerShake(float intensityMul)
        {
            if (!enableCameraShake) return;
            if (shakeTargets == null || shakeTargets.Count == 0) return;
            if (intensityMul <= 0f) return;

            if (_activeShake != null)
            {
                StopCoroutine(_activeShake);
                RestoreShakeOrigins(); // 새 흔들기 전 원위치 복구
            }
            _activeShake = StartCoroutine(ShakeRoutine(intensityMul));
        }

        // 감쇠하며 대상들을 흔드는 코루틴
        IEnumerator ShakeRoutine(float intensityMul)
        {
            if (shakeTargets == null || shakeTargets.Count == 0) yield break;
            float timer = 0f;
            float strength = shakeIntensity * Mathf.Clamp01(intensityMul);

            while (timer < shakeDuration)
            {
                timer += Time.unscaledDeltaTime; // timeScale=0에서도 진행
                float decay = 1f - (timer / shakeDuration); // 시간이 흐를수록 감쇠
                // 모든 대상에 동일 오프셋 적용
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

        // HP바 색을 깜빡임 색에서 원본으로 보간하는 코루틴
        IEnumerator HpBarFlashRoutine()
        {
            if (hpBarFillImage == null) yield break;
            float timer = 0f;
            Color start = hpBarFlashColor;
            Color end = _hpBarOriginCaptured ? _hpBarOriginColor : hpBarFillImage.color;

            while (timer < hpBarFlashDuration)
            {
                timer += Time.unscaledDeltaTime; // timeScale=0에서도 진행
                float t = timer / hpBarFlashDuration;
                if (hpBarFillImage != null)
                    hpBarFillImage.color = Color.Lerp(start, end, t);
                yield return null;
            }
            if (hpBarFillImage != null) hpBarFillImage.color = end;
            _activeHpFlash = null;
        }

        // 플래시 알파를 시작 강도에서 0으로 페이드하는 코루틴
        IEnumerator FlashRoutine(float intensityMul)
        {
            if (flashImage == null) yield break;

            float startAlpha = flashColor.a * intensityMul;
            float timer = 0f;

            while (timer < flashDuration)
            {
                timer += Time.unscaledDeltaTime; // timeScale=0에서도 진행
                float t = timer / flashDuration;
                // 풀강도에서 점점 페이드
                float a = startAlpha * (1f - t);
                SetAlpha(a);
                yield return null;
            }
            SetAlpha(0f);
            _activeFlash = null;
        }

        // 플래시 이미지의 알파를 설정
        void SetAlpha(float a)
        {
            if (flashImage == null) return;
            var c = flashColor;
            c.a = Mathf.Clamp01(a);
            flashImage.color = c;
        }

        // 풀스크린 플래시 이미지가 없으면 캔버스에 생성
        void EnsureFlashImage()
        {
            if (flashImage != null) return;

            // 부모 캔버스에 풀스크린 이미지 생성
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
            flashImage.raycastTarget = false; // 클릭 차단 안 함
        }
    }
}
