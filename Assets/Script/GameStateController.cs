using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
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

    [Header("Managers")]
    public MapManager mapManager;
    public BattleManger battleManager;
    public Roundmanager roundManager;  // 라운드 관리자 추가

    [Header("Game Over")]
    public GameObject gameOverPanel;
    [SerializeField] private TMP_FontAsset gameOverFont;

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

    // ── 플레이어 사망 처리 ──────────────────────────────────────

    /// <summary>Player의 사망 이벤트를 구독합니다.</summary>
    public void SubscribePlayerDeath()
    {
        Player player = FindFirstObjectByType<Player>();
        if (player != null)
        {
            player.OnPlayerDied -= OnPlayerDied;
            player.OnPlayerDied += OnPlayerDied;
        }
    }

    void OnPlayerDied()
    {
        Debug.Log("=== 플레이어 사망 — Game Over ===");

        ElementSlotSystem.Instance?.EndBattle();

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
            SetupGameOverPanel();
        }
        else
        {
            // gameOverPanel이 없으면 코드로 생성
            CreateGameOverPanel();
        }
    }

    void SetupGameOverPanel()
    {
        // 최상단에 표시
        Canvas canvas = gameOverPanel.GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameOverPanel.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 999;

        if (gameOverPanel.GetComponent<CanvasScaler>() == null)
        {
            var scaler = gameOverPanel.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
        }

        if (gameOverPanel.GetComponent<GraphicRaycaster>() == null)
            gameOverPanel.AddComponent<GraphicRaycaster>();

        // 버튼 바인딩 (이미 씬에 있는 경우)
        BindGameOverButtons(gameOverPanel);
    }

    void CreateGameOverPanel()
    {
        // 코드로 Game Over UI 생성
        GameObject panelObj = new GameObject("GameOverPanel");
        Canvas canvas = panelObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        var scaler = panelObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        panelObj.AddComponent<GraphicRaycaster>();

        // 어두운 배경
        GameObject bg = new GameObject("Background");
        bg.transform.SetParent(panelObj.transform, false);
        Image bgImage = bg.AddComponent<Image>();
        bgImage.color = new Color(0, 0, 0, 0.75f);
        RectTransform bgRect = bg.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;

        // 사망 문구
        GameObject textObj = new GameObject("GameOverText");
        textObj.transform.SetParent(panelObj.transform, false);
        TMP_Text tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = "사망했습니다";
        tmp.fontSize = 72;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.red;
        if (gameOverFont != null) tmp.font = gameOverFont;
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.6f);
        textRect.anchorMax = new Vector2(0.5f, 0.6f);
        textRect.sizeDelta = new Vector2(600, 100);
        textRect.anchoredPosition = Vector2.zero;

        // 다시하기 버튼
        CreateGameOverButton(panelObj.transform, "RestartButton", "다시 하기", new Vector2(0, -30), RestartGame);

        // 게임 종료 버튼
        CreateGameOverButton(panelObj.transform, "QuitButton", "게임 종료", new Vector2(0, -110), QuitGame);

        gameOverPanel = panelObj;
    }

    void CreateGameOverButton(Transform parent, string name, string label, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);

        Image btnImage = btnObj.AddComponent<Image>();
        btnImage.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = btnImage;
        btn.onClick.AddListener(action);

        RectTransform btnRect = btnObj.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0.5f);
        btnRect.anchorMax = new Vector2(0.5f, 0.5f);
        btnRect.sizeDelta = new Vector2(300, 60);
        btnRect.anchoredPosition = position;

        // 버튼 텍스트
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(btnObj.transform, false);
        TMP_Text tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 32;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        if (gameOverFont != null) tmp.font = gameOverFont;
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }

    void BindGameOverButtons(GameObject panel)
    {
        Button[] buttons = panel.GetComponentsInChildren<Button>(true);
        foreach (Button btn in buttons)
        {
            string n = btn.gameObject.name.ToLower();
            if (n.Contains("restart") || n.Contains("retry"))
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(RestartGame);
            }
            else if (n.Contains("quit") || n.Contains("exit"))
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(QuitGame);
            }
        }
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        DestroyPersistentSingletons();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitGame()
    {
        Debug.Log("게임 종료");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void DestroyPersistentSingletons()
    {
        if (MoneyManager.Instance != null)
            Destroy(MoneyManager.Instance.gameObject);

        if (ElementSlotSystem.Instance != null)
            Destroy(ElementSlotSystem.Instance.gameObject);

        if (ComboSystem.Instance != null)
            Destroy(ComboSystem.Instance.gameObject);

        if (GameManager.Instance != null)
            Destroy(GameManager.Instance.gameObject);

        // 런타임 Player 정리
        Player[] players = FindObjectsByType<Player>(FindObjectsSortMode.None);
        foreach (Player p in players)
            Destroy(p.gameObject);
    }

}