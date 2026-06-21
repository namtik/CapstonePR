using UnityEngine;

// 게임 상태를 관리하고 캔버스 활성화/비활성화로 화면을 전환하는 컨트롤러
public class GameStateController : MonoBehaviour
{
    // 전역 싱글턴 인스턴스
    public static GameStateController Instance { get; private set; }

    [Header("Canvas References")]
    public Canvas mapCanvas; // 맵 캔버스

    [Header("시작 화면")]
    public GameObject mainMenuStage;     // 메인 메뉴 화면 (가장 먼저 표시)

    [Header("Stage GameObjects")]
    public GameObject mapStage;          // 맵 스테이지
    public GameObject combatStage;       // 전투 스테이지
    public GameObject eliteStage;        // 정예 스테이지
    public GameObject bossStage;         // 보스 스테이지
    public GameObject shopStage;         // 상점 스테이지
    public GameObject restStage;         // 휴식 스테이지
    public GameObject eventStage;        // 이벤트 스테이지
    public GameObject relicStage;        // 유물 스테이지

    [Header("Managers")]
    public MapManager mapManager; // 맵 관리자
    public RoundManager roundManager;  // 라운드 관리자

    [Header("Game State")]
    public int lastVisitedNodeIndex = -1; // 마지막으로 방문한 노드 인덱스
    public System.Collections.Generic.List<int> clearedNodes = new System.Collections.Generic.List<int>(); // 클리어한 노드 인덱스 목록

    [Header("Run Lap")]
    [Tooltip("게임 클리어에 필요한 보스 처치 횟수(= 맵 바퀴 수).")]
    public const int RequiredBossDefeats = 2;
    public int bossDefeatCount = 0; // 이번 런에서 처치한 보스 수

    // 현재 진행 중인 바퀴 (1부터 시작)
    public int CurrentLap => bossDefeatCount + 1;

    // 아직 클리어에 필요한 보스 처치가 남았는지
    public bool HasMoreLapsRemaining => bossDefeatCount < RequiredBossDefeats;

    [Header("Stage Display")]
    [Tooltip("현재 바퀴 내 스테이지 번호 (1~11, 맵 컬럼+1).")]
    public int currentMapStageNumber = 1;

    public int CurrentDisplayLap => CurrentLap;
    public int CurrentDisplayStage => currentMapStageNumber;

    public event System.Action OnRunStageDisplayChanged;

    // 초기화: 싱글턴 중복 방지
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    // 시작 시 상태 초기화 후 메인 화면 또는 맵을 표시
    void Start()
    {
        InitializeGameState();

        // 메인 화면이 지정되어 있으면 맵 대신 메인 화면을 먼저 표시한다.
        if (mainMenuStage != null)
        {
            HideAllStages();
            mainMenuStage.SetActive(true);
        }
        else
        {
            ShowMap();
        }
    }

    // 메인 화면 [게임 플레이] 버튼: 메인 화면을 닫고 맵으로 진입
    public void StartGame()
    {
        if (mainMenuStage != null)
            mainMenuStage.SetActive(false);

        bossDefeatCount = 0;
        ResetMapStageForLapStart();

        if (mapManager != null)
            mapManager.RegenerateMap();

        ShowMap();
    }

    // 게임 상태 초기화 (레이캐스터 보장)
    void InitializeGameState()
    {
        EnsureGraphicRaycaster();
    }

    // 맵 캔버스에 GraphicRaycaster를 보장하고 패널 레이캐스트를 끈다
    void EnsureGraphicRaycaster()
    {
        if (mapCanvas != null)
        {
            var raycaster = mapCanvas.GetComponent<UnityEngine.UI.GraphicRaycaster>();
            if (raycaster == null)
            {
                mapCanvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }

            DisablePanelRaycast();
        }
    }

    // 패널/배경 이미지가 클릭을 막지 않도록 raycastTarget을 끈다
    void DisablePanelRaycast()
    {
        if (mapCanvas == null) return;

        var allImages = mapCanvas.GetComponentsInChildren<UnityEngine.UI.Image>(true);
        foreach (var img in allImages)
        {
            if (img.gameObject.name.ToLower().Contains("panel") ||
                img.gameObject.name.ToLower().Contains("background"))
            {
                img.raycastTarget = false;
            }
        }
    }

    // 맵 화면으로 전환하고 HP UI 동기화 및 맵 새로고침
    public void ShowMap()
    {
        HideAllStages();

        if (mapStage != null)
        {
            mapStage.SetActive(true);
        }
        else if (mapCanvas != null)
        {
            // 하위 호환: mapStage가 없으면 mapCanvas 사용
            mapCanvas.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogError("mapStage와 mapCanvas 둘 다 null입니다!");
        }

        if (roundManager != null)
        {
            roundManager.EnsurePlayerUiSync();
        }

        if (mapManager != null)
        {
            StartCoroutine(RefreshMapDelayed());
        }
        else
        {
            Debug.LogError("mapManager가 null입니다!");
        }
    }

    // 한 프레임 대기 후 맵을 새로고침하고 HP UI를 다시 동기화하는 코루틴
    System.Collections.IEnumerator RefreshMapDelayed()
    {
        yield return null;
        mapManager.RefreshMap();

        if (roundManager != null)
        {
            roundManager.EnsurePlayerUiSync();
        }
    }

    // 노드 타입에 따라 적절한 스테이지를 표시 (MapManager에서 호출)
    public void ShowCanvasForNodeType(NodeType nodeType, bool isBossNode)
    {
        HideAllStages();

        GameObject targetStage = null;

        // 전투 계열(Combat/Elite/Boss)은 모두 combatStage 사용
        if (isBossNode || nodeType == NodeType.Combat || nodeType == NodeType.Elite || nodeType == NodeType.Boss)
        {
            targetStage = combatStage;
        }
        else
        {
            switch (nodeType)
            {
                case NodeType.Shop:
                    targetStage = shopStage;
                    break;
                case NodeType.Rest:
                    targetStage = restStage;
                    break;
                case NodeType.Event:
                    targetStage = eventStage;
                    break;
                case NodeType.Relic:
                    targetStage = relicStage;
                    break;
            }
        }

        if (targetStage != null)
        {
            targetStage.SetActive(true);
        }
        else
        {
            Debug.LogWarning($"노드 타입 {nodeType}에 해당하는 스테이지가 없습니다!");
        }
    }

    // 모든 스테이지를 비활성화한다
    void HideAllStages()
    {
        if (mapStage != null)
        {
            mapStage.SetActive(false);
        }
        if (combatStage != null)
        {
            combatStage.SetActive(false);
        }
        if (eliteStage != null) eliteStage.SetActive(false);
        if (bossStage != null) bossStage.SetActive(false);
        if (shopStage != null) shopStage.SetActive(false);
        if (restStage != null) restStage.SetActive(false);
        if (eventStage != null) eventStage.SetActive(false);
        if (relicStage != null) relicStage.SetActive(false);
    }

    // (구버전) 전투 화면으로 전환
    [System.Obsolete("Use ShowCanvasForNodeType instead")]
    public void ShowBattle()
    {
        ShowCanvasForNodeType(NodeType.Combat, false);
    }

    // 노드를 클리어 처리해 목록에 추가한다
    public void MarkNodeCleared(int index)
    {
        if (index < 0) return;
        if (!clearedNodes.Contains(index))
        {
            clearedNodes.Add(index);
        }
    }

    // 노드가 클리어되었는지 확인한다
    public bool IsNodeCleared(int index)
    {
        return index >= 0 && clearedNodes.Contains(index);
    }

    // 전투 클리어 후 현재 노드를 클리어 처리한다
    public void OnRoundClear()
    {
        if (lastVisitedNodeIndex >= 0)
        {
            MarkNodeCleared(lastVisitedNodeIndex);
        }
    }

    // 보스 처치를 기록한다
    public void RegisterBossDefeat()
    {
        bossDefeatCount++;
        Debug.Log($"[GameState] 보스 처치 {bossDefeatCount}/{RequiredBossDefeats}");
    }

    public void SetCurrentMapStageFromColumn(int mapColumn)
    {
        currentMapStageNumber = Mathf.Max(1, mapColumn + 1);
        NotifyRunStageDisplayChanged();
    }

    public void ResetMapStageForLapStart()
    {
        currentMapStageNumber = 1;
        NotifyRunStageDisplayChanged();
    }

    public void NotifyRunStageDisplayChanged()
    {
        OnRunStageDisplayChanged?.Invoke();
    }

    // 다음 바퀴 맵 진행을 위해 노드 클리어/위치를 초기화하고 새 맵을 생성한다 (덱·유물·골드·HP는 유지)
    public void BeginNextLap()
    {
        clearedNodes.Clear();
        lastVisitedNodeIndex = -1;
        ResetMapStageForLapStart();

        if (mapManager != null)
            mapManager.RegenerateMap();

        Debug.Log($"[GameState] {CurrentLap}바퀴째 맵 진행 시작 (난이도 오프셋 컬럼 +{GetDifficultyColumnOffset()})");
    }

    // 현재 바퀴 기준 누적 난이도 컬럼 오프셋 (1바퀴=0, 2바퀴=11, …)
    public int GetDifficultyColumnOffset()
    {
        if (roundManager != null && roundManager.DifficultyConfig != null)
            return roundManager.DifficultyConfig.GetEffectiveColumn(0, bossDefeatCount);

        // DifficultyConfig 미연결 시 기본값(컬럼 10 + 보스 1 = 11)
        return bossDefeatCount * 11;
    }

    // 런을 처음부터 재시작하고 맵으로 돌아간다
    public void RestartRunToMap()
    {
        Time.timeScale = 1f;

        // 사망 등으로 중단된 전투의 잔여 적/상태 정리 (살아있는 적이 다음 전투로 이월되는 것 방지)
        if (roundManager != null)
            roundManager.AbortActiveCombat();

        lastVisitedNodeIndex = -1;
        clearedNodes.Clear();
        bossDefeatCount = 0;
        ResetMapStageForLapStart();

        // 신규 전투 시스템: 런 덱을 기본값으로 리셋
        Battle.RunDeckState.Instance?.ResetRun();

        // 유물 보유 현황도 초기화 (다음 런은 유물 0개로 시작)
        Battle.Relic.RelicManager.Instance?.ClearOwnedRelics();

        // 골드도 초기화 (다음 런은 0골드로 시작)
        MoneyManager.Instance?.ResetMoney();

        if (mapManager != null)
            mapManager.RegenerateMap();

        ShowMap();
    }

    // 진행 중이던 런/전투를 정리하고 메인 메뉴 화면으로 돌아간다
    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;

        // 진행 중이던 전투의 잔여 적/상태 정리
        if (roundManager != null)
            roundManager.AbortActiveCombat();

        // 런 상태 초기화 (메인으로 나가면 현재 런을 포기)
        lastVisitedNodeIndex = -1;
        clearedNodes.Clear();
        bossDefeatCount = 0;
        ResetMapStageForLapStart();
        Battle.RunDeckState.Instance?.ResetRun();

        // 유물 보유 현황도 초기화 (메인으로 나가면 현재 런을 포기 → 다음 런은 유물 0개로 시작)
        Battle.Relic.RelicManager.Instance?.ClearOwnedRelics();

        // 골드도 초기화 (다음 런은 0골드로 시작)
        MoneyManager.Instance?.ResetMoney();

        HideAllStages();

        if (mainMenuStage != null)
            mainMenuStage.SetActive(true);
        else
            Debug.LogWarning("[GameState] mainMenuStage가 지정되지 않아 메인 화면으로 돌아갈 수 없습니다.");
    }

}