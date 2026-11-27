using TMPro;
using UnityEngine;

// 에디터에서만 경로(AssetDatabase)로 불러오던 에셋을 빌드에서도 쓰도록 묶어 둔 참조 모음.
// 에셋 위치: Assets/_Project/Resources/BuildAssetRefs.asset (Resources.Load로 로드)
[CreateAssetMenu(fileName = "BuildAssetRefs", menuName = "Build/Asset Refs")]
public class BuildAssetRefs : ScriptableObject
{
    public GameObject comboBookPrefab;          // 전투 로그 팝업 콤보북 프리팹
    public Font chosunFont;                     // 조선체 원본 폰트(전투 로그 숫자용 동적 SDF 생성)
    public TMP_FontAsset nyjUnheoFont;          // 전투 로그 기본 TMP 폰트
    public Sprite enemyPowerUpIcon;             // 적 강화 아이콘
    public StatusIconDatabase statusIconDatabase; // 상태이상 아이콘 DB
    public Sprite freezeIcon;                   // 전투 로그 빙결 아이콘(Cartoon Coffee Snowflake_001)

    static BuildAssetRefs _instance;
    static bool _loaded;

    public static BuildAssetRefs Instance
    {
        get
        {
            if (!_loaded)
            {
                _loaded = true;
                _instance = Resources.Load<BuildAssetRefs>("BuildAssetRefs");
                if (_instance == null) Debug.LogWarning("[BuildAssetRefs] Resources/BuildAssetRefs.asset 없음");
            }
            return _instance;
        }
    }
}
