using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 속성별 3D 전투 스테이지 씬을 additive로 로드/표시/숨김 관리한다.
// 전투 진입 시 적 속성(EnemyElement)에 맞는 스테이지를 고르고, 매칭이 없으면 기본 스테이지를 쓴다.
// 스테이지 씬은 시작 시 미리 전부 로드해 두고(숨김 상주), 전투 때 해당 씬만 켜고 나머지는 끈다.
public class StageManager : MonoBehaviour
{
    public static StageManager Instance { get; private set; }

    [Serializable]
    public class ElementStage
    {
        [Tooltip("이 스테이지를 쓸 적 속성.")]
        public EnemyElement element;
        [Tooltip("해당 속성의 스테이지 씬 이름(Build Settings에 등록되어 있어야 함).")]
        public string sceneName;
    }

    [Header("속성별 스테이지 씬")]
    [Tooltip("적 속성 → 스테이지 씬 매핑. 전투 명단에서 첫 번째로 속성이 있는 적 기준.")]
    [SerializeField] private List<ElementStage> elementStages = new List<ElementStage>
    {
        new ElementStage { element = EnemyElement.Fire,  sceneName = "Fire_BurnedForest" },
        new ElementStage { element = EnemyElement.Earth, sceneName = "Land_ForestGate" },
        new ElementStage { element = EnemyElement.Water, sceneName = "Water_FishingVillagePier" },
        new ElementStage { element = EnemyElement.Wind,  sceneName = "Wind_BrokenSuspensionBridge" },
        new ElementStage { element = EnemyElement.Humanoid, sceneName = "Human_ClosedGateMarket" },
    };
    [Tooltip("매칭되는 속성 스테이지가 없을 때(무속성 등) 사용할 기본 스테이지 씬 이름.")]
    [SerializeField] private string defaultStageSceneName = "Land_ForestGate";
    [Tooltip("보스 전투 전용 스테이지 씬 이름. 비우면 보스도 속성 매핑을 따른다.")]
    [SerializeField] private string bossStageSceneName = "Boss_ReedField";
    [Tooltip("시작 시 모든 스테이지 씬을 숨김 상태로 미리 로드해 둘지 여부(전투 진입 시 끊김 방지).")]
    [SerializeField] private bool preloadAllOnStart = true;

    [Header("시간 흐름(스테이지 진행)")]
    [Tooltip("진행(맵 컬럼)에 따라 스테이지 시간대를 순환시킨다. 끄면 각 스테이지 씬의 StageLightingManager 설정을 그대로 쓴다.")]
    [SerializeField] private bool progressTimeOfDay = true;
    [Tooltip("시간대가 한 칸 넘어가는 데 필요한 진행 컬럼 수(맵 10컬럼 기준 2 → 한 바퀴에 5칸 진행, 보스는 다음 시간대).")]
    [SerializeField, Min(1)] private int columnsPerTimeStep = 2;
    [Tooltip("순환 순서. 진행 컬럼 / columnsPerTimeStep 을 이 목록 길이로 나눈 나머지 위치의 시간대를 쓴다.")]
    [SerializeField] private List<StageLightingManager.StageTime> timeCycle = new List<StageLightingManager.StageTime>
    {
        StageLightingManager.StageTime.Morning,
        StageLightingManager.StageTime.Afternoon,
        StageLightingManager.StageTime.LateAfternoon,
        StageLightingManager.StageTime.Sunset,
        StageLightingManager.StageTime.Evening,
        StageLightingManager.StageTime.Dawn,
    };

    private bool _hasStageTime;
    private StageLightingManager.StageTime _stageTime;

    [Header("카메라 전환")]
    [Tooltip("전투 밖에서 쓰는 기본(비전투) 카메라 — 보통 MainScene의 메인 카메라. 전투 중엔 자동으로 꺼서 스테이지 카메라(Base)와 충돌하지 않게 한다. URP는 Base 카메라 2개가 동시에 켜지면 서로 덮어써서 3D가 안 보인다.")]
    [SerializeField] private Camera nonCombatCamera;   // 비전투 기본 카메라(전투 중 off)

    [Header("디버그")]
    [Tooltip("스테이지 표시/숨김·로드 흐름을 콘솔에 출력.")]
    [SerializeField] private bool logVerbose = true;

    // 로드되어 등록된 스테이지(씬 이름 → 컨트롤러)
    private readonly Dictionary<string, BattleStageController> _stages = new Dictionary<string, BattleStageController>();
    private readonly HashSet<string> _loadingScenes = new HashSet<string>();

    private string _shownSceneName;   // 현재 표시하려는 스테이지 씬
    private bool _visible;            // 전투 중(표시) 여부
    private int _requestId;           // ShowStage/HideStage 요청 번호(대기 중 콜백 취소용)

    // 스테이지 StageLightingManager가 RenderSettings(하늘/안개/환경광)를 덮어쓰므로, 비전투 화면용 원본을 보관했다가 복원한다.
    private struct RenderSnapshot
    {
        public Material skybox; public Light sun;
        public bool fog; public FogMode fogMode; public Color fogColor; public float fogStart, fogEnd, fogDensity;
        public UnityEngine.Rendering.AmbientMode ambientMode;
        public Color ambientSky, ambientEquator, ambientGround, ambientLight;
        public float reflectionIntensity;

        public static RenderSnapshot Capture() => new RenderSnapshot
        {
            skybox = RenderSettings.skybox, sun = RenderSettings.sun,
            fog = RenderSettings.fog, fogMode = RenderSettings.fogMode, fogColor = RenderSettings.fogColor,
            fogStart = RenderSettings.fogStartDistance, fogEnd = RenderSettings.fogEndDistance, fogDensity = RenderSettings.fogDensity,
            ambientMode = RenderSettings.ambientMode,
            ambientSky = RenderSettings.ambientSkyColor, ambientEquator = RenderSettings.ambientEquatorColor,
            ambientGround = RenderSettings.ambientGroundColor, ambientLight = RenderSettings.ambientLight,
            reflectionIntensity = RenderSettings.reflectionIntensity,
        };

        public void Restore()
        {
            RenderSettings.skybox = skybox; RenderSettings.sun = sun;
            RenderSettings.fog = fog; RenderSettings.fogMode = fogMode; RenderSettings.fogColor = fogColor;
            RenderSettings.fogStartDistance = fogStart; RenderSettings.fogEndDistance = fogEnd; RenderSettings.fogDensity = fogDensity;
            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientSkyColor = ambientSky; RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround; RenderSettings.ambientLight = ambientLight;
            RenderSettings.reflectionIntensity = reflectionIntensity;
        }
    }
    private RenderSnapshot _baseRenderSettings;

    void Awake()
    {
        Instance = this;
        _baseRenderSettings = RenderSnapshot.Capture(); // 스테이지 로드 전 MainScene 원본
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    void Start()
    {
        // 에디터에서 스테이지 씬을 미리 열어둔 채 플레이한 경우도 등록한다.
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene s = SceneManager.GetSceneAt(i);
            if (s.isLoaded && IsManagedScene(s.name)) RegisterScene(s);
        }

        if (preloadAllOnStart)
            foreach (string sceneName in AllSceneNames())
                StartCoroutine(EnsureLoaded(sceneName));
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        if (Instance == this) Instance = null;
    }

    // 전투 명단에서 스테이지 속성을 고른다(첫 번째로 속성이 있는 적, 없으면 None).
    public static EnemyElement ResolveElement(IList<EnemyData> roster)
    {
        if (roster == null) return EnemyElement.None;
        for (int i = 0; i < roster.Count; i++)
            if (roster[i] != null && roster[i].element != EnemyElement.None)
                return roster[i].element;
        return EnemyElement.None;
    }

    // 속성에 해당하는 씬 이름(매칭 없으면 기본 스테이지).
    public string ResolveSceneName(EnemyElement element, bool isBoss = false)
    {
        if (isBoss && !string.IsNullOrEmpty(bossStageSceneName)) return bossStageSceneName;
        foreach (var s in elementStages)
            if (s != null && s.element == element && !string.IsNullOrEmpty(s.sceneName))
                return s.sceneName;
        return defaultStageSceneName;
    }

    // 전투 진입 — 속성 스테이지를 표시한다. 스테이지가 준비되면(이미 로드돼 있으면 즉시) onReady를 호출한다.
    // 로드 실패 시에도 onReady는 호출되며, 이때 BattleStageController.Current는 null(→ RoundManager 2D 폴백).
    public void ShowStage(EnemyElement element, Action onReady = null)
    {
        ShowStage(element, -1, onReady);
    }

    // progressColumn: 누적 진행 컬럼(난이도 컬럼 = 맵컬럼 + 바퀴*10). 0 이상이면 그에 맞는 시간대를 적용한다.
    // isBoss: 보스 전투면 bossStageSceneName을 우선 사용한다.
    public void ShowStage(EnemyElement element, int progressColumn, Action onReady = null, bool isBoss = false)
    {
        // [테스트] StageTestPanel이 스테이지 씬/시간대를 강제할 수 있다(평소에는 null → 기존 동작)
        string target = !string.IsNullOrEmpty(DebugSceneOverride) ? DebugSceneOverride : ResolveSceneName(element, isBoss);
        int id = ++_requestId;
        _visible = true;
        _shownSceneName = target;
        if (DebugTimeOverride.HasValue)
        {
            _stageTime = DebugTimeOverride.Value;
            _hasStageTime = true;
        }
        else
            _hasStageTime = progressTimeOfDay && progressColumn >= 0 && TryResolveTime(progressColumn, out _stageTime);
        if (logVerbose)
            Debug.Log($"[StageManager] ShowStage({element}{(isBoss ? ", boss" : "")} → '{target}', column={progressColumn}" +
                      (_hasStageTime ? $", time={_stageTime})" : ")"));

        if (string.IsNullOrEmpty(target))
        {
            Debug.LogWarning("[StageManager] 표시할 스테이지 씬 이름이 비어 있습니다.");
            ApplyVisibility();
            onReady?.Invoke();
            return;
        }

        if (_stages.ContainsKey(target))
        {
            ApplyVisibility();
            onReady?.Invoke();
            return;
        }

        StartCoroutine(ShowWhenLoaded(target, id, onReady));
    }

    // 전투 이탈 — 스테이지를 숨기고 비전투 카메라를 되살린다(언로드하지 않고 상주).
    public void HideStage()
    {
        ++_requestId; // 로딩 대기 중인 ShowStage 콜백 취소
        _visible = false;
        if (logVerbose) Debug.Log("[StageManager] HideStage()");
        ApplyVisibility();
    }

    IEnumerator ShowWhenLoaded(string sceneName, int id, Action onReady)
    {
        yield return EnsureLoaded(sceneName);
        if (id != _requestId) yield break; // 그 사이 다른 요청/HideStage가 들어옴

        ApplyVisibility();
        onReady?.Invoke();
    }

    IEnumerator EnsureLoaded(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName) || _stages.ContainsKey(sceneName)) yield break;

        // 다른 코루틴이 이미 로드 중이면 끝날 때까지 대기
        if (_loadingScenes.Contains(sceneName))
        {
            while (_loadingScenes.Contains(sceneName)) yield return null;
            yield break;
        }

        Scene existing = SceneManager.GetSceneByName(sceneName);
        if (existing.IsValid() && existing.isLoaded)
        {
            RegisterScene(existing);
            yield break;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError($"[StageManager] 씬 '{sceneName}'을 로드할 수 없습니다 — Build Settings에 등록됐는지 확인.");
            yield break;
        }

        _loadingScenes.Add(sceneName);
        var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
        while (op != null && !op.isDone) yield return null;
        _loadingScenes.Remove(sceneName);
        if (logVerbose) Debug.Log($"[StageManager] 로드 완료: '{sceneName}'");
    }

    // 씬 로드 직후(같은 프레임, 렌더 전) 호출 — 등록하고 바로 표시 상태를 맞춰 깜빡임을 막는다.
    void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsManagedScene(scene.name)) RegisterScene(scene);
    }

    void RegisterScene(Scene scene)
    {
        if (_stages.ContainsKey(scene.name)) return;

        BattleStageController stage = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            stage = root.GetComponentInChildren<BattleStageController>(true);
            if (stage != null) break;
        }

        // 스테이지 씬에 컨트롤러가 없으면 자동으로 만든다(앵커/카메라는 이름으로 자동 연결).
        if (stage == null)
        {
            var go = new GameObject("_BattleStage (Auto)");
            SceneManager.MoveGameObjectToScene(go, scene);
            stage = go.AddComponent<BattleStageController>();
            if (logVerbose) Debug.Log($"[StageManager] '{scene.name}'에 BattleStageController가 없어 자동 생성했습니다.");
        }

        stage.AutoWire();
        _stages[scene.name] = stage;
        bool show = _visible && scene.name == _shownSceneName;
        if (show && _hasStageTime) stage.SetStageTime(_stageTime);
        stage.SetVisible(show);
        if (show) return;

        // 로드 중 켜졌던 이 씬의 StageLightingManager가 RenderSettings를 바꿨으므로 되돌린다.
        if (!_visible)
            _baseRenderSettings.Restore();
        else if (!string.IsNullOrEmpty(_shownSceneName) && _stages.TryGetValue(_shownSceneName, out var shown) && shown != null)
            shown.ReapplyLighting(); // 전투 중 다른 스테이지가 늦게 로드된 경우 — 표시 중 스테이지 조명 재적용
    }

    // 표시 대상 스테이지만 켜고 나머지는 끈다 + 비전투 카메라 반전 토글(Base 카메라 충돌 방지).
    void ApplyVisibility()
    {
        BattleStageController shown = null;
        if (_visible && !string.IsNullOrEmpty(_shownSceneName))
            _stages.TryGetValue(_shownSceneName, out shown);

        // 끄는 것 먼저(StageLightingManager OnDisable → 켜지는 쪽 OnEnable이 최종 조명을 적용)
        foreach (var kv in _stages)
            if (kv.Value != null && kv.Value != shown) kv.Value.SetVisible(false);

        if (shown != null)
        {
            // 켜기 전에 시간대를 넣어 둔다 → OnEnable의 Apply가 바로 이 시간대로 조명을 적용(깜빡임 없음).
            if (_hasStageTime) shown.SetStageTime(_stageTime);
            shown.SetVisible(true);
            shown.MakeCurrent();
        }
        else
        {
            _baseRenderSettings.Restore(); // 비전투/2D 화면은 MainScene 원래 하늘·안개로
            if (_visible)
            {
                BattleStageController.ClearCurrent(); // 표시할 스테이지가 없으면 2D 폴백
                Debug.LogWarning($"[StageManager] 스테이지 '{_shownSceneName}'가 로드되지 않아 2D로 진행합니다.");
            }
        }

        bool stageOn = shown != null;
        if (nonCombatCamera != null)
            nonCombatCamera.enabled = !stageOn; // 전투 표시 중엔 비전투 카메라 off, 이탈 시 on

        if (logVerbose)
            Debug.Log($"[StageManager] ApplyVisibility — visible={_visible}, scene='{_shownSceneName}', stage={stageOn}");
    }

    // 진행 컬럼 → 순환 시간대. 맵 10컬럼·2컬럼/칸 기준: 0-1 아침, 2-3 오후, 4-5 늦은오후, 6-7 노을, 8-9 저녁, 보스(10) 새벽 …
    public bool TryResolveTime(int progressColumn, out StageLightingManager.StageTime time)
    {
        time = StageLightingManager.StageTime.Afternoon;
        if (timeCycle == null || timeCycle.Count == 0) return false;
        int step = Mathf.Max(0, progressColumn) / Mathf.Max(1, columnsPerTimeStep);
        time = timeCycle[step % timeCycle.Count];
        return true;
    }

    bool IsManagedScene(string sceneName)
    {
        foreach (string s in AllSceneNames())
            if (s == sceneName) return true;
        return false;
    }

    // [테스트] 다음 ShowStage에서 쓸 스테이지 씬/시간대 강제값(null이면 적 속성·진행 컬럼으로 결정)
    public static string DebugSceneOverride;
    public static StageLightingManager.StageTime? DebugTimeOverride;

    // 관리 중인 스테이지 씬 이름 목록(테스트 패널 선택지용)
    public List<string> StageSceneNames => new List<string>(AllSceneNames());

    IEnumerable<string> AllSceneNames()
    {
        var seen = new HashSet<string>();
        foreach (var s in elementStages)
            if (s != null && !string.IsNullOrEmpty(s.sceneName) && seen.Add(s.sceneName))
                yield return s.sceneName;
        if (!string.IsNullOrEmpty(defaultStageSceneName) && seen.Add(defaultStageSceneName))
            yield return defaultStageSceneName;
        if (!string.IsNullOrEmpty(bossStageSceneName) && seen.Add(bossStageSceneName))
            yield return bossStageSceneName;
    }
}
