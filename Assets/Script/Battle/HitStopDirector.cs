using System;
using UnityEngine;

// 타격 순간 카메라 연출(펀치 줌 + 초점 쏠림 + 흔들기)을 담당한다. 라이브러리 오브 루이나식 '멈췄다 터지는' 타격감용.
// - 모든 시간은 unscaled(일시정지 메뉴 timeScale과 무관, 전역 timeScale은 건드리지 않음).
// - 카메라에 '이번 프레임에 더한 오프셋'만 빼고 다시 더하는 가산 방식이라, 다른 스크립트가 카메라를 움직여도 충돌하지 않는다.
// - 씬에 없으면 첫 호출 때 자동 생성된다.
public class HitStopDirector : MonoBehaviour
{
    // 한 번의 타격 연출 설정값(적 뷰 인스펙터에서 조정)
    [Serializable]
    public struct CameraPunch
    {
        [Tooltip("카메라 연출 사용 여부.")]
        public bool enabled;
        [Tooltip("멈춤(줌인 유지) 시간(초). 보통 적 애니 정지 시간과 같게.")]
        public float holdDuration;
        [Tooltip("멈춤 후 원래대로 돌아오는 시간(초).")]
        public float recoverDuration;
        [Tooltip("줌인 정도 — 원근 카메라는 FOV 감소량(도), 직교 카메라는 사이즈 감소 비율(값*0.01).")]
        public float zoomAmount;
        [Tooltip("타격 지점 쪽으로 카메라를 끌어당기는 거리(월드 미터).")]
        public float pushTowardFocus;
        [Tooltip("흔들기 세기(월드 미터).")]
        public float shakeAmplitude;
        [Tooltip("흔들기 진동수(Hz). 높을수록 잘게 떨림.")]
        public float shakeFrequency;

        public static CameraPunch Default => new CameraPunch
        {
            enabled = true,
            holdDuration = 0.22f,
            recoverDuration = 0.28f,
            zoomAmount = 4f,
            pushTowardFocus = 0.25f,
            shakeAmplitude = 0.08f,
            shakeFrequency = 28f,
        };
    }

    static HitStopDirector _instance;

    Camera _cam;                 // 현재 연출 대상 카메라
    CameraPunch _p;              // 현재 연출 설정
    float _intensity = 1f;       // 세기 배율
    Vector3 _focusWorld;         // 타격 지점(월드)
    float _elapsed = -1f;        // 연출 경과(음수=비활성)
    float _seed;                 // 흔들기 노이즈 시드

    Vector3 _appliedPos;         // 지난 프레임에 더한 위치 오프셋
    float _appliedZoom;          // 지난 프레임에 더한 FOV/사이즈 오프셋

    // 타격 연출 실행. cam이 null이면 현재 스테이지 카메라, 그것도 없으면 Camera.main.
    public static void Punch(Vector3 focusWorld, CameraPunch p, float intensity = 1f, Camera cam = null)
    {
        if (!p.enabled) return;
        if (cam == null)
            cam = BattleStageController.Current != null ? BattleStageController.Current.StageCamera : Camera.main;
        if (cam == null) return;

        if (_instance == null)
        {
            var go = new GameObject("[HitStopDirector]");
            _instance = go.AddComponent<HitStopDirector>();
        }
        _instance.Begin(cam, focusWorld, p, Mathf.Max(0f, intensity));
    }

    void Begin(Camera cam, Vector3 focusWorld, CameraPunch p, float intensity)
    {
        // 다른 카메라로 바뀌면 이전 카메라 오프셋부터 원복
        if (_cam != null && _cam != cam) RemoveApplied();
        _cam = cam;
        _p = p;
        _intensity = intensity;
        _focusWorld = focusWorld;
        _elapsed = 0f;
        _seed = UnityEngine.Random.value * 100f;
    }

    void LateUpdate()
    {
        if (_elapsed < 0f) return;
        if (_cam == null) { _elapsed = -1f; _appliedPos = Vector3.zero; _appliedZoom = 0f; return; }

        RemoveApplied();

        _elapsed += Time.unscaledDeltaTime;
        float hold = Mathf.Max(0f, _p.holdDuration);
        float recover = Mathf.Max(0.0001f, _p.recoverDuration);
        float total = hold + recover;
        if (_elapsed >= total) { _elapsed = -1f; return; }

        // 0~hold: 즉시 최대(스냅) 유지, 이후 ease-out으로 복귀
        float k = _elapsed <= hold ? 1f : 1f - EaseOutCubic((_elapsed - hold) / recover);
        k *= _intensity;

        Transform t = _cam.transform;

        // 초점 쏠림
        Vector3 toFocus = _focusWorld - t.position;
        Vector3 push = toFocus.sqrMagnitude > 0.0001f ? toFocus.normalized * (_p.pushTowardFocus * k) : Vector3.zero;

        // 흔들기(카메라 평면 기준, 펄린 노이즈로 부드럽게 떨림) — 멈춤 구간에 가장 강하고 복귀하며 감쇠
        float time = _elapsed * Mathf.Max(0f, _p.shakeFrequency);
        float sx = (Mathf.PerlinNoise(_seed, time) - 0.5f) * 2f;
        float sy = (Mathf.PerlinNoise(_seed + 37.1f, time) - 0.5f) * 2f;
        Vector3 shake = (t.right * sx + t.up * sy) * (_p.shakeAmplitude * k);

        _appliedPos = push + shake;
        t.position += _appliedPos;

        if (_cam.orthographic)
        {
            _appliedZoom = -_cam.orthographicSize * (_p.zoomAmount * 0.01f) * k;
            _cam.orthographicSize += _appliedZoom;
        }
        else
        {
            float before = _cam.fieldOfView;
            _cam.fieldOfView = Mathf.Clamp(before - _p.zoomAmount * k, 1f, 179f);
            _appliedZoom = _cam.fieldOfView - before; // 클램프된 실제 변화량만 기록(원복 대칭 보장)
        }
    }

    // 지난 프레임에 더했던 오프셋을 제거해 카메라를 원 상태로 돌린다
    void RemoveApplied()
    {
        if (_cam == null) { _appliedPos = Vector3.zero; _appliedZoom = 0f; return; }
        _cam.transform.position -= _appliedPos;
        if (_cam.orthographic) _cam.orthographicSize -= _appliedZoom;
        else _cam.fieldOfView -= _appliedZoom;
        _appliedPos = Vector3.zero;
        _appliedZoom = 0f;
    }

    static float EaseOutCubic(float x)
    {
        x = Mathf.Clamp01(x);
        float inv = 1f - x;
        return 1f - inv * inv * inv;
    }

    // 비활성/파괴 시 카메라 원복(씬 전환 중 연출이 남지 않게)
    void OnDisable() { RemoveApplied(); _elapsed = -1f; }
    void OnDestroy() { if (_instance == this) _instance = null; }
}
