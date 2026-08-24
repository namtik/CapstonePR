using UnityEngine;

// 3D 전투 스테이지 씬의 루트에 부착. 몬스터 스폰 앵커 / 카메라 앵커 / 스테이지 카메라를 노출하고
// 자신을 '현재 스테이지'로 등록한다. RoundManager는 additive 로드된 스테이지의 앵커/카메라를
// 인스펙터 크로스씬 참조 없이 Current로 런타임 조회한다.
public class BattleStageController : MonoBehaviour
{
    // 현재 활성 스테이지(가장 최근에 활성화/로드된 것). 3D 스테이지가 없으면 null → RoundManager는 2D 폴백.
    public static BattleStageController Current { get; private set; }

    [Header("앵커/카메라")]
    [Tooltip("몬스터를 스폰할 위치(스테이지의 EnemyAnchor).")]
    [SerializeField] private Transform enemyAnchor;   // 몬스터 스폰 위치
    [Tooltip("카메라 배치 기준(참고용).")]
    [SerializeField] private Transform cameraAnchor;  // 카메라 앵커(참고)
    [Tooltip("이 스테이지를 렌더하는 원근 카메라. 비우면 자식에서 탐색, 그래도 없으면 Camera.main.")]
    [SerializeField] private Camera stageCamera;      // 스테이지(3D) 카메라
    [Tooltip("전투 외 구간에서 숨길 대상(환경+카메라 루트). 비우면 카메라 enabled만 토글. 이 컨트롤러 오브젝트 자신은 지정하지 말 것(Current 유실).")]
    [SerializeField] private GameObject stageRoot;    // 표시/숨김 대상 루트

    public Transform EnemyAnchor => enemyAnchor;
    public Transform CameraAnchor => cameraAnchor;
    public Camera StageCamera => stageCamera != null ? stageCamera : Camera.main;

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
