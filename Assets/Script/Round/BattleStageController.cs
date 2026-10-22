using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 3D 전투 스테이지 씬의 루트에 부착. 몬스터 스폰 앵커 / 카메라 앵커 / 스테이지 카메라를 노출하고
// 자신을 '현재 스테이지'로 등록한다. RoundManager는 additive 로드된 스테이지의 앵커/카메라를
// 인스펙터 크로스씬 참조 없이 Current로 런타임 조회한다.
public class BattleStageController : MonoBehaviour
{
    // 현재 활성 스테이지(가장 최근에 활성화/로드된 것). 3D 스테이지가 없으면 null → RoundManager는 2D 폴백.
    public static BattleStageController Current { get; private set; }

    [Header("앵커/카메라")]
    [Tooltip("1마리일 때 몬스터를 스폰할 위치(스테이지의 EnemyAnchor). 여러 마리일 때도 이 위치가 가운데 기준.")]
    [SerializeField] private Transform enemyAnchor;   // 몬스터 스폰 위치
    [Tooltip("카메라 배치 기준(참고용).")]
    [SerializeField] private Transform cameraAnchor;  // 카메라 앵커(참고)
    [Tooltip("이 스테이지를 렌더하는 원근 카메라. 비우면 자식에서 탐색, 그래도 없으면 Camera.main.")]
    [SerializeField] private Camera stageCamera;      // 스테이지(3D) 카메라
    [Tooltip("전투 외 구간에서 숨길 대상(환경+카메라 루트). 비우면 이 씬의 다른 루트 오브젝트 전체(조명·볼륨·카메라 포함)를 토글. 이 컨트롤러 오브젝트 자신은 지정하지 말 것(Current 유실).")]
    [SerializeField] private GameObject stageRoot;    // 표시/숨김 대상 루트

    [Header("슬롯 앵커(속성 스테이지)")]
    [Tooltip("1마리 전투 위치. 비우면 enemyAnchor 사용.")]
    [SerializeField] private Transform soloAnchor;
    [Tooltip("2마리 전투 위치(왼쪽, 오른쪽 순). 비우면 자동 오프셋 계산.")]
    [SerializeField] private Transform[] duoAnchors = new Transform[0];
    [Tooltip("3마리 전투 위치(왼쪽, 가운데, 오른쪽 순). 비우면 자동 오프셋 계산.")]
    [SerializeField] private Transform[] trioAnchors = new Transform[0];
    [Tooltip("슬롯 앵커에 배치할 때 곱할 크기(앵커 배치는 거리감이 이미 반영돼 있으므로 기본 1).")]
    [SerializeField, Range(0.2f, 1f)] private float slotAnchorScale = 1f;

    [Header("카메라 정리")]
    [Tooltip("스테이지 카메라가 UI 레이어를 그리지 않게 한다(MainScene UI가 3D 화면에 중복 렌더되는 것 방지).")]
    [SerializeField] private bool excludeUiLayer = true;
    [Tooltip("스테이지 씬의 AudioListener를 끈다(MainScene 리스너와 중복 경고 방지).")]
    [SerializeField] private bool disableAudioListeners = true;

    [Header("여러 마리 배치")]
    [Tooltip("가운데 적 기준, 양옆 적을 카메라 좌우로 벌리는 거리(월드 미터). 2~4 정도가 보통이고, 값이 클수록 더 벌어진다.")]
    [SerializeField] private float sideWorldSpacing = 2.4f;
    [Tooltip("양옆 적을 카메라 기준으로 뒤로 밀어 원근감 내는 거리(월드 미터).")]
    [SerializeField] private float behindWorldDistance = 1.8f;
    [Tooltip("여러 마리일 때 가운데 적 크기. 1마리 전투는 그대로 둔다.")]
    [SerializeField, Range(0.5f, 1f)] private float centerScale = 0.85f;
    [Tooltip("양옆 적 크기. 0.6 = 가운데(1.0)보다 40% 작음.")]
    [SerializeField, Range(0.2f, 1f)] private float sideScale = 0.6f;
    [Tooltip("양옆 적을 가운데보다 얼마나 올릴지(월드 미터). 발이 지면에 묻히면 이 값을 키운다.")]
    [SerializeField] private float sideHeightOffset = 0.85f;

    public Transform EnemyAnchor => enemyAnchor;
    public Transform CameraAnchor => cameraAnchor;
    public Camera StageCamera => stageCamera != null ? stageCamera : Camera.main;

    // 슬롯 앵커가 있으면 그 앵커, 없으면 가운데 앵커(아래 로컬 오프셋으로 배치).
    public Transform ResolveEnemyAnchor(int slotIndex, int partySize)
    {
        Transform slot = SlotAnchor(slotIndex, partySize);
        return slot != null ? slot : enemyAnchor;
    }

    // 파티 크기별 지정 슬롯 앵커(없으면 null).
    Transform SlotAnchor(int slotIndex, int partySize)
    {
        if (partySize <= 1) return soloAnchor;
        Transform[] set = partySize == 2 ? duoAnchors : trioAnchors;
        if (set == null || slotIndex < 0 || slotIndex >= set.Length) return null;
        return set[slotIndex];
    }

    // 몬스터가 스폰될 수 있는 모든 앵커(전투 중단 시 잔여 몬스터 정리용).
    public IEnumerable<Transform> AllEnemyAnchors()
    {
        var seen = new HashSet<Transform>();
        if (enemyAnchor != null && seen.Add(enemyAnchor)) yield return enemyAnchor;
        if (soloAnchor != null && seen.Add(soloAnchor)) yield return soloAnchor;
        foreach (var set in new[] { duoAnchors, trioAnchors })
        {
            if (set == null) continue;
            foreach (var t in set)
                if (t != null && seen.Add(t)) yield return t;
        }
    }

    // -1 왼쪽, 0 가운데, +1 오른쪽.
    public static int PartySideSign(int slotIndex, int partySize)
    {
        if (partySize <= 1) return 0;
        if (partySize == 2) return slotIndex <= 0 ? -1 : 1;
        if (slotIndex <= 0) return -1;
        if (slotIndex == 1) return 0;
        return 1;
    }

    public float ResolvePartyScale(int slotIndex, int partySize)
    {
        if (partySize <= 1) return 1f;
        if (SlotAnchor(slotIndex, partySize) != null) return slotAnchorScale;
        return PartySideSign(slotIndex, partySize) == 0 ? centerScale : sideScale;
    }

    // 카메라 기준 좌우/뒤 월드 좌표. 가운데는 앵커 위치 그대로.
    public Vector3 ResolvePartyWorldPosition(int slotIndex, int partySize)
    {
        Transform slot = SlotAnchor(slotIndex, partySize);
        if (slot != null) return slot.position;

        Vector3 origin = enemyAnchor != null ? enemyAnchor.position : Vector3.zero;
        int side = PartySideSign(slotIndex, partySize);
        if (side == 0) return origin;

        Camera cam = StageCamera;
        Vector3 right = Vector3.right;
        Vector3 away = Vector3.forward;
        if (cam != null)
        {
            right = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up);
            if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
            else right.Normalize();

            away = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up);
            if (away.sqrMagnitude < 0.0001f) away = Vector3.forward;
            else away.Normalize();
        }

        return origin
               + right * (side * Mathf.Max(0f, sideWorldSpacing))
               + away * Mathf.Max(0f, behindWorldDistance)
               + Vector3.up * sideHeightOffset;
    }

    // 3D 배치를 2D 캔버스 좌표로 투영한다. 가운데(EnemyPos)는 (0,0), 양옆은 화면상 벌어진 양.
    public Vector2 ResolvePartyCanvasOffset(RectTransform parent, int slotIndex, int partySize)
    {
        Camera worldCam = StageCamera;
        if (parent == null || worldCam == null) return Vector2.zero;

        Vector3 origin = enemyAnchor != null ? enemyAnchor.position : Vector3.zero;
        Vector3 world = ResolvePartyWorldPosition(slotIndex, partySize);

        Canvas canvas = parent.GetComponentInParent<Canvas>();
        Camera canvasCam = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            canvasCam = canvas.worldCamera;

        Vector2 originScreen = RectTransformUtility.WorldToScreenPoint(worldCam, origin);
        Vector2 worldScreen = RectTransformUtility.WorldToScreenPoint(worldCam, world);

        Vector2 originLocal;
        Vector2 worldLocal;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, originScreen, canvasCam, out originLocal);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, worldScreen, canvasCam, out worldLocal);
        return worldLocal - originLocal;
    }

    // 전투 진입/이탈 시 스테이지 표시를 토글한다(전투 밖에서 3D 카메라 렌더 낭비 방지).
    // stageRoot가 없으면 이 씬의 다른 루트 전체를 토글한다 — 여러 스테이지가 상주해도 숨긴 쪽의
    // 조명/Global Volume/StageLightingManager가 표시 중인 스테이지에 섞이지 않게 하기 위함.
    public void SetVisible(bool on)
    {
        if (stageRoot != null)
            stageRoot.SetActive(on);
        else
            foreach (var root in gameObject.scene.GetRootGameObjects())
                if (root != gameObject && root.activeSelf != on) root.SetActive(on);

        if (stageCamera != null) stageCamera.enabled = on;
    }

    // 이 씬의 StageLightingManager를 껐다 켜서 RenderSettings(하늘/안개/환경광)를 다시 적용한다.
    public void ReapplyLighting()
    {
        foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var lighting in root.GetComponentsInChildren<StageLightingManager>())
            {
                if (!lighting.enabled) continue;
                lighting.enabled = false;
                lighting.enabled = true;
            }
    }

    // StageManager가 표시할 스테이지를 현재로 지정/해제한다.
    public void MakeCurrent() { Current = this; }
    public static void ClearCurrent() { Current = null; }

    // 비어 있는 참조를 씬의 오브젝트 이름 규칙으로 채운다.
    // 속성 스테이지 규칙: StageCamera / Ancor_1&3(Left, Mid, Right) / Ancor_2(Left, Right) / (구) EnemyAnchor
    public void AutoWire()
    {
        Scene scene = gameObject.scene;

        if (stageCamera == null)
        {
            Transform camT = FindInScene(scene, "StageCamera");
            if (camT != null) stageCamera = camT.GetComponent<Camera>();
            if (stageCamera == null)
                foreach (var root in scene.GetRootGameObjects())
                {
                    stageCamera = root.GetComponentInChildren<Camera>(true);
                    if (stageCamera != null) break;
                }
        }

        Transform trio = FindInScene(scene, "Ancor_1&3");
        Transform duo = FindInScene(scene, "Ancor_2");
        if (trio != null && IsEmpty(trioAnchors))
            trioAnchors = new[] { trio.Find("Left"), trio.Find("Mid"), trio.Find("Right") };
        if (duo != null && IsEmpty(duoAnchors))
            duoAnchors = new[] { duo.Find("Left"), duo.Find("Right") };
        if (soloAnchor == null && trio != null) soloAnchor = trio.Find("Mid");

        if (enemyAnchor == null) enemyAnchor = FindInScene(scene, "EnemyAnchor");
        if (enemyAnchor == null) enemyAnchor = soloAnchor;

        if (stageCamera != null && excludeUiLayer)
        {
            int ui = LayerMask.NameToLayer("UI");
            if (ui >= 0) stageCamera.cullingMask &= ~(1 << ui);
        }

        if (disableAudioListeners)
            foreach (var root in scene.GetRootGameObjects())
                foreach (var listener in root.GetComponentsInChildren<AudioListener>(true))
                    listener.enabled = false;

        if (enemyAnchor == null)
            Debug.LogWarning($"[BattleStageController] '{scene.name}': 적 앵커를 찾지 못했습니다(Ancor_1&3/Mid 또는 EnemyAnchor).");
    }

    static bool IsEmpty(Transform[] set)
    {
        if (set == null) return true;
        foreach (var t in set) if (t != null) return false;
        return true;
    }

    static Transform FindInScene(Scene scene, string objectName)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name == objectName) return root.transform;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == objectName) return t;
        }
        return null;
    }

    void Awake()
    {
        if (stageCamera == null) stageCamera = GetComponentInChildren<Camera>(true);
        Current = this;
    }

    // additive 로드로 여러 스테이지가 잠깐 공존할 수 있으므로, 활성화되는 스테이지를 현재로 승격.
    void OnEnable() { Current = this; }

    void OnDestroy()
    {
        if (Current == this) Current = null;
    }

    // 필수 참조 누락 경고
    void OnValidate()
    {
        if (enemyAnchor == null && soloAnchor == null)
            Debug.LogWarning($"[BattleStageController] {name}: enemyAnchor/soloAnchor 미지정(플레이 시 이름으로 자동 연결 시도).");
    }
}
