using UnityEngine;
using UnityEngine.SceneManagement;  // ← 추가: 씬 재로드용
using TMPro;

// 게임 상태를 관리하고 캔버스 전환을 담당
//  씬 전환 대신 캔버스 활성화/비활성화로 상태 전환
public class GameStateController : MonoBehaviour
{
    public static GameStateController Instance { get; private set; }

    [Header("Canvas References")]
    public Canvas mapCanvas;
    
    [Header("Stage GameObjects")]
    public GameObject mapStage;          // MapStage GameObject
    public GameObject combatStage;       // CombatStage GameObject
    public GameObject eliteStage;        // EliteStage GameObject (있다면)
    public GameObject bossStage;         // BossStage GameObject (있다면)
    public GameObject shopStage;         // ShopStage GameObject (있다면)
    public GameObject restStage;         // RestStage GameObject (있다면)
    public GameObject eventStage;        // EventStage GameObject (이벤트 노드)

    [Header("Game Over UI")]
    public GameObject gameOverPanel;     // ← Unity Editor에서 Game Over 패널 드래그 할당
    public TMP_Text gameOverText;

    [Header("Managers")]
    public MapManager mapManager;
    public BattleManger battleManager;
    public Roundmanager roundManager;  // 라운드 관리자 추가

    [Header("Game State")]
    public int lastVisitedNodeIndex = -1;
    public System.Collections.Generic.List<int> clearedNodes = new System.Collections.Generic.List<int>();

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

    void Start()
    {
        // 게임 시작 시 초기화 및 맵 화면 표시
        InitializeGameState();
        ShowMap();

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        // 현재 존재하는 Player의 사망 이벤트를 구독
        SubscribePlayerDeath();
    }

    void InitializeGameState()
    {
        // GameManager와 동기화
        if (GameManager.Instance != null)
        {
            lastVisitedNodeIndex = GameManager.Instance.lastVisitedNodeIndex;
            clearedNodes = new System.Collections.Generic.List<int>(GameManager.Instance.clearedNodes);
        }
        
        // Panel raycastTarget 비활성화
        EnsureGraphicRaycaster();
    }
    
    void EnsureGraphicRaycaster()
    {
        if (mapCanvas != null)
        {
            var raycaster = mapCanvas.GetComponent<UnityEngine.UI.GraphicRaycaster>();
            if (raycaster == null)
            {
                mapCanvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }
            
            // Panel이나 background 이미지가 클릭을 막지 않도록 설정
            DisablePanelRaycast();
        }
    }
    
    void DisablePanelRaycast()
    {
        if (mapCanvas == null) return;
        
        // Canvas 하위의 모든 Image 중 Panel, Background 등의 raycastTarget 비활성화
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

    // 맵 화면으로 전환
    // 전투 캔버스 숨기고 맵 캔버스 표시
    public void ShowMap()
    {
        //  모든 스테이지 비활성화
        HideAllStages();

        //  맵 스테이지만 활성화
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

        // 맵 UI가 켜진 뒤 즉시 플레이어 HP UI 동기화
        if (roundManager != null)
        {
            roundManager.EnsurePlayerUiSync();
        }

        //  맵을 새로고침 (약간의 지연으로 MapManager 초기화 완료 대기)
        if (mapManager != null)
        {
            StartCoroutine(RefreshMapDelayed());
        }
        else
        {
            Debug.LogError("mapManager가 null입니다!");
        }
    }

    System.Collections.IEnumerator RefreshMapDelayed()
    {
        // 한 프레임 대기하여 MapManager.Start() 완료 보장
        yield return null;
        mapManager.RefreshMap();

        // 맵 갱신 직후 한 번 더 동기화해 첫 프레임 값 깜빡임 방지
        if (roundManager != null)
        {
            roundManager.EnsurePlayerUiSync();
        }
    }

    // 노드 타입에 따라 적절한 스테이지 표시
    // MapManager.OnNodeSelected()에서 호출됨
    public void ShowCanvasForNodeType(NodeType nodeType, bool isBossNode)
    {
        //  모든 스테이지 비활성화
        HideAllStages();

        //  노드 타입에 따라 스테이지 활성화
        GameObject targetStage = null;

        // 전투 계열(Combat/Elite/Boss)은 모두 combatStage 사용
        // CombatStageController가 배경 스프라이트를 타입별로 전환
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

    // 모든 스테이지 비활성화
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
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
    }

    // 전투 화면으로 전환
    [System.Obsolete("Use ShowCanvasForNodeType instead")]
    public void ShowBattle()
    {
        ShowCanvasForNodeType(NodeType.Combat, false);
    }

    // 노드 클리어 처리
    //GameManager 역할을 대신함
    public void MarkNodeCleared(int index)
    {
        if (index < 0) return;
        if (!clearedNodes.Contains(index))
        {
            clearedNodes.Add(index);
        }
    }

    // 노드가 클리어되었는지 확인
    public bool IsNodeCleared(int index)
    {
        return index >= 0 && clearedNodes.Contains(index);
    }

    // 전투 클리어 후 맵으로 복귀
    public void OnRoundClear()
    {
        // 현재 노드 클리어 처리
        if (lastVisitedNodeIndex >= 0)
        {
            MarkNodeCleared(lastVisitedNodeIndex);
        }
    }
    public void SubscribePlayerDeath()
    {
        Player player = FindFirstObjectByType<Player>();
        if (player != null)
        {
            player.OnPlayerDied -= OnPlayerDied; // 중복 구독 방지 (같은 함수 두 번 등록 X)
            player.OnPlayerDied += OnPlayerDied; // 사망 이벤트에 OnPlayerDied 연결
        }
    }
    void OnPlayerDied()
    {
        OnPlayerDeath();  // 이벤트 → public 메서드로 중계
    }

    // 플레이어 사망 시 호출 (이벤트 또는 Player에서 직접 호출)
    public void OnPlayerDeath()
    {
        Debug.Log("=== 게임 오버 ===");

        HideAllStages();                         // 맵, 전투 등 모든 스테이지 숨김
        ElementSlotSystem.Instance?.EndBattle();  // 전투 시스템 정리

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);       // Game Over 패널 표시

            // 다른 UI 위에 확실히 보이도록 Canvas 설정
            Canvas canvas = gameOverPanel.GetComponent<Canvas>();
            if (canvas == null)
                canvas = gameOverPanel.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 999;           // 최상단 렌더링

            // 클릭 이벤트가 작동하려면 GraphicRaycaster 필요
            if (gameOverPanel.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
                gameOverPanel.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }

        if (gameOverText != null)
            gameOverText.text = "사망하였습니다...";  // 사망 문구 설정
    }

    public void RestartGame()
    {
        Debug.Log("=== 게임 재시작 ===");
        Time.timeScale = 1f;  // 일시정지 해제 (게임오버 시 멈춰있을 수 있으므로)

        DestroyPersistentSingletons();  // DontDestroyOnLoad 객체들 파괴

        // 현재 씬을 처음부터 다시 로드 → 모든 것이 Awake/Start부터 재실행
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void DestroyPersistentSingletons()
    {
        // 각 싱글턴은 DontDestroyOnLoad로 씬 재로드 후에도 살아남음
        // → 수동으로 파괴해야 이전 런의 데이터(돈, 스킬, 슬롯)가 남지 않음

        if (MoneyManager.Instance != null)        // 돈 초기화
            Destroy(MoneyManager.Instance.gameObject);

        if (ElementSlotSystem.Instance != null)   // 슬롯/덱 초기화
            Destroy(ElementSlotSystem.Instance.gameObject);

        if (ComboSystem.Instance != null)         // 학습한 스킬 + 콤보 UI 초기화
            Destroy(ComboSystem.Instance.gameObject);

        if (GameManager.Instance != null)         // 클리어 노드 기록 초기화
            Destroy(GameManager.Instance.gameObject);

        // Roundmanager가 DontDestroyOnLoad로 생성한 Player 오브젝트도 정리
        Player[] players = FindObjectsByType<Player>(FindObjectsSortMode.None);
        foreach (Player p in players)
            Destroy(p.gameObject);
    }

    public void QuitGame()
    {
        Debug.Log("=== 게임 종료 ===");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;  // 에디터에서는 플레이 중지
#else
    Application.Quit();                               // 빌드에서는 앱 종료
#endif
    }
}