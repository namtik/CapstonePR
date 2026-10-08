using UnityEngine;

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
    [Tooltip("전투 외 구간에서 숨길 대상(환경+카메라 루트). 비우면 카메라 enabled만 토글. 이 컨트롤러 오브젝트 자신은 지정하지 말 것(Current 유실).")]
    [SerializeField] private GameObject stageRoot;    // 표시/숨김 대상 루트

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

    // 여러 마리는 모두 가운데 앵커 아래 로컬 오프셋으로 배치한다.
    public Transform ResolveEnemyAnchor(int slotIndex, int partySize) => enemyAnchor;

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
        return PartySideSign(slotIndex, partySize) == 0 ? centerScale : sideScale;
    }

    // 카메라 기준 좌우/뒤 월드 좌표. 가운데는 앵커 위치 그대로.
    public Vector3 ResolvePartyWorldPosition(int slotIndex, int partySize)
    {
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
    public void SetVisible(bool on)
    {
        if (stageRoot != null) stageRoot.SetActive(on);
        if (stageCamera != null) stageCamera.enabled = on;
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
        if (enemyAnchor == null) Debug.LogWarning($"[BattleStageController] {name}: enemyAnchor 미지정.");
    }
}
