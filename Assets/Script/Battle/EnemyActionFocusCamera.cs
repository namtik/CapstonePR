using System;
using System.Collections.Generic;
using UnityEngine;

// 적이 행동(공격/방해)하는 동안 스테이지 카메라가 그 적 쪽으로 고개를 돌리듯 회전하고 살짝 줌인해 '누가 행동하는지'를 강조한다.
// - HitStopDirector와 같은 가산 방식: 지난 프레임에 더한 오프셋만 빼고 새로 더하므로 히트스톱 카메라 펀치와 겹쳐도 충돌하지 않는다.
// - 행동 시작 시 Focus, 행동 종료 시 Release. 다음 행동이 바로 이어지면 원위치로 돌아가지 않고 다음 적으로 부드럽게 옮겨 간다.
// - 씬에 없으면 첫 호출 때 자동 생성된다.
public class EnemyActionFocusCamera : MonoBehaviour
{
    [Serializable]
    public struct Settings
    {
        [Tooltip("적 행동 포커스 연출 사용 여부.")]
        public bool enabled;
        [Tooltip("적이 여러 마리일 때만 사용(한 마리면 누가 행동하는지 명확하므로 생략).")]
        public bool onlyWhenMultiple;
        [Tooltip("줌인 정도 — 원근 카메라는 FOV 감소량(도), 직교 카메라는 사이즈 감소 비율(값*0.01).")]
        public float zoomAmount;
        [Tooltip("행동하는 적 쪽으로 카메라를 전진시키는 거리(월드 미터). 0이면 제자리에서 고개만 돌림.")]
        public float pushTowardFocus;
        [Tooltip("행동하는 적 쪽으로 고개를 돌리는 비율(0=안 돌림, 1=적을 정중앙에). 0.3~0.5 권장.")]
        [Range(0f, 1f)] public float turnAmount;
        [Tooltip("고개를 돌리는 최대 각도(도). 화면 끝의 적이라도 이 이상은 돌리지 않는다.")]
        public float maxTurnAngle;
        [Tooltip("위아래(끄덕임) 회전 비율. 좌우 회전 대비 배율(0=좌우만).")]
        [Range(0f, 1f)] public float pitchScale;
        [Tooltip("돌리는 방향으로 살짝 기울이는 각도(도). 0이면 기울임 없음.")]
        public float tiltDegrees;
        [Tooltip("포커스로 들어가는 시간(초).")]
        public float easeInDuration;
        [Tooltip("원래 시점으로 돌아오는 시간(초).")]
        public float easeOutDuration;

        public static Settings Default => new Settings
        {
            enabled = true,
            onlyWhenMultiple = false,
            zoomAmount = 4f,
            pushTowardFocus = 0.2f,
            turnAmount = 0.4f,
            maxTurnAngle = 12f,
            pitchScale = 0.5f,
            tiltDegrees = 1.5f,
            easeInDuration = 0.35f,
            easeOutDuration = 0.45f,
        };
    }

    static EnemyActionFocusCamera _instance;

    Camera _cam;                   // 연출 대상 카메라
    Settings _s;                   // 현재 설정
    Transform _target;             // 행동 중인 적
    readonly List<Renderer> _renderers = new List<Renderer>(); // 초점 계산용 렌더러
    Vector3 _focusWorld;           // 현재(보간된) 초점
    bool _hasFocus;                // 초점 위치가 한 번이라도 잡혔는지
    float _k;                      // 연출 강도 0~1
    bool _active;                  // true=포커스 중(강도↑), false=복귀 중(강도↓)

    Vector3 _appliedPos;           // 지난 프레임에 더한 위치 오프셋
    float _appliedZoom;            // 지난 프레임에 더한 FOV/사이즈 오프셋
    Quaternion _appliedRot = Quaternion.identity; // 지난 프레임에 더한 회전(카메라 로컬 기준)

    // 행동하는 적에게 포커스. cam이 null이면 현재 스테이지 카메라.
    public static void Focus(Transform target, Settings s, Camera cam = null)
    {
        if (!s.enabled || target == null) return;
        if (s.onlyWhenMultiple && !EnemyCombatParty.IsMulti) return;
        if (cam == null)
            cam = BattleStageController.Current != null ? BattleStageController.Current.StageCamera : null;
        if (cam == null) return; // 3D 스테이지가 없으면(2D 폴백) 생략

        if (_instance == null)
        {
            var go = new GameObject("[EnemyActionFocusCamera]");
            _instance = go.AddComponent<EnemyActionFocusCamera>();
        }
        _instance.Begin(target, s, cam);
    }

    // 포커스 해제. immediate면 오프셋을 바로 원복(전투 종료·스테이지 숨김 등).
    public static void Release(bool immediate = false)
    {
        if (_instance == null) return;
        _instance._active = false;
        if (immediate)
        {
            _instance.RemoveApplied();
            _instance._k = 0f;
            _instance._target = null;
            _instance._hasFocus = false;
        }
    }

    void Begin(Transform target, Settings s, Camera cam)
    {
        if (_cam != null && _cam != cam) { RemoveApplied(); _k = 0f; _hasFocus = false; }
        _cam = cam;
        _s = s;
        _target = target;
        _renderers.Clear();
        target.GetComponentsInChildren(false, _renderers);
        _active = true;
    }

    void LateUpdate()
    {
        if (_cam == null) { _appliedPos = Vector3.zero; _appliedZoom = 0f; _k = 0f; return; }
        RemoveApplied();

        // 적이 사라졌으면(처치 등) 복귀
        if (_active && (_target == null || !_target.gameObject.activeInHierarchy)) _active = false;

        float dur = _active ? _s.easeInDuration : _s.easeOutDuration;
        float step = dur > 0.0001f ? Time.deltaTime / dur : 1f;
        _k = Mathf.Clamp01(_k + (_active ? step : -step));
        if (_k <= 0f) { _target = null; _hasFocus = false; return; }

        // 초점: 적 렌더러 바운드 중심(대상이 바뀌면 부드럽게 옮겨 감)
        if (_target != null && TryGetFocus(out Vector3 p))
        {
            _focusWorld = _hasFocus ? Vector3.Lerp(_focusWorld, p, 1f - Mathf.Exp(-12f * Time.deltaTime)) : p;
            _hasFocus = true;
        }
        if (!_hasFocus) return;

        float e = EaseInOutCubic(_k);
        Transform t = _cam.transform;
        Vector3 toFocus = _focusWorld - t.position;

        // 고개 돌리기: 카메라 기준 적 방향의 좌우(yaw)·위아래(pitch) 각도 중 일부만큼 회전
        Vector3 local = Quaternion.Inverse(t.rotation) * toFocus;
        if (local.z > 0.01f)
        {
            float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg;
            float max = Mathf.Max(0f, _s.maxTurnAngle);
            yaw = Mathf.Clamp(yaw * _s.turnAmount, -max, max);
            pitch = Mathf.Clamp(pitch * _s.turnAmount * _s.pitchScale, -max, max);
            // 돌리는 쪽으로 살짝 기울임(오른쪽을 보면 시계 방향) — 머리를 돌리는 느낌
            float roll = max > 0f ? -_s.tiltDegrees * Mathf.Clamp(yaw / max, -1f, 1f) : 0f;
            _appliedRot = Quaternion.Euler(pitch * e, yaw * e, roll * e);
            t.rotation = t.rotation * _appliedRot;
        }

        // 시선 방향으로 살짝 전진
        Vector3 push = toFocus.sqrMagnitude > 0.0001f ? toFocus.normalized * _s.pushTowardFocus : Vector3.zero;
        _appliedPos = push * e;
        t.position += _appliedPos;

        if (_cam.orthographic)
        {
            _appliedZoom = -_cam.orthographicSize * (_s.zoomAmount * 0.01f) * e;
            _cam.orthographicSize += _appliedZoom;
        }
        else
        {
            float before = _cam.fieldOfView;
            _cam.fieldOfView = Mathf.Clamp(before - _s.zoomAmount * e, 1f, 179f);
            _appliedZoom = _cam.fieldOfView - before; // 클램프된 실제 변화량만 기록(원복 대칭)
        }
    }

    bool TryGetFocus(out Vector3 center)
    {
        center = default;
        bool any = false;
        Bounds b = default;
        for (int i = 0; i < _renderers.Count; i++)
        {
            var r = _renderers[i];
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (r is ParticleSystemRenderer) continue; // 파티클은 범위가 튀므로 제외
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        if (!any)
        {
            if (_target == null) return false;
            center = _target.position;
            return true;
        }
        center = b.center;
        return true;
    }

    // 지난 프레임에 더했던 오프셋을 제거해 카메라를 원 상태로 돌린다
    void RemoveApplied()
    {
        if (_cam == null) { _appliedPos = Vector3.zero; _appliedZoom = 0f; _appliedRot = Quaternion.identity; return; }
        _cam.transform.position -= _appliedPos;
        _cam.transform.rotation = _cam.transform.rotation * Quaternion.Inverse(_appliedRot);
        if (_cam.orthographic) _cam.orthographicSize -= _appliedZoom;
        else _cam.fieldOfView -= _appliedZoom;
        _appliedPos = Vector3.zero;
        _appliedZoom = 0f;
        _appliedRot = Quaternion.identity;
    }

    static float EaseInOutCubic(float x)
    {
        x = Mathf.Clamp01(x);
        return x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;
    }

    void OnDisable() { RemoveApplied(); _k = 0f; _active = false; }
    void OnDestroy() { if (_instance == this) _instance = null; }
}
