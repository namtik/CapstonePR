using System.Collections.Generic;
using UnityEngine;

// Enemy3DPrefabBuilder가 읽는 3D 몬스터 프리팹 생성 사양 목록(에디터 전용 에셋).
// 인스펙터 또는 Tools/3D Enemy/Prefab Builder 창에서 스펙을 추가·수정한다.
public class Enemy3DBuildSpecs : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        [Tooltip("체크 해제 시 '전체 생성'에서 제외")]
        public bool enabled = true;                  // 일괄 생성 포함 여부
        [Tooltip("프리팹 파일/루트 이름 (예: EnemyClayOni)")]
        public string prefabName;                    // 프리팹 파일/루트 이름
        [Tooltip("모델 자식 오브젝트 이름. 비우면 FBX 이름 사용")]
        public string modelName;                     // 모델 자식 오브젝트 이름
        public GameObject fbx;                       // 모델 FBX
        [Tooltip("모든 렌더러 슬롯에 적용할 머티리얼. 비우면 FBX 기본 머티리얼 유지")]
        public Material material;                    // 적용할 머티리얼
        [Tooltip("battlePrefab3D를 연결할 EnemyData. 비우면 연결 생략")]
        public EnemyData enemyData;                  // 연결할 EnemyData
        [Tooltip("EnemyData에 이미 다른 3D 프리팹이 연결돼 있어도 교체")]
        public bool replaceExistingLink;             // 기존 연결 교체 여부

        [Header("모델 트랜스폼")]
        public Vector3 modelPosition = Vector3.zero; // 모델 로컬 위치
        public Vector3 modelRotation = Vector3.zero; // 모델 로컬 회전(오일러)
        public Vector3 modelScale = Vector3.one;     // 모델 로컬 스케일
        [Tooltip("HUD(체력바 등) 앵커 로컬 위치")]
        public Vector3 hudAnchorPosition = Vector3.zero; // HudAnchor 로컬 위치

        public string ModelName => string.IsNullOrEmpty(modelName) && fbx != null ? fbx.name : modelName;
    }

    [Tooltip("컴포넌트 값을 복사할 템플릿 프리팹(무사)")]
    public GameObject template;                      // 템플릿 프리팹
    [Tooltip("프리팹 저장 폴더")]
    public string outputFolder = "Assets/Prefab/3D"; // 출력 폴더
    [Tooltip("모델에 적용할 레이어 (Enemy3D)")]
    public int enemyLayer = 7;                       // 모델 레이어

    public List<Entry> entries = new List<Entry>();  // 몬스터별 사양

    public string PrefabPath(Entry e) => $"{outputFolder.TrimEnd('/')}/{e.prefabName}.prefab";
}
