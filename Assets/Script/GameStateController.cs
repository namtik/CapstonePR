using UnityEngine;

// ���� ���¸� �����ϰ� ĵ���� ��ȯ�� ���
//  �� ��ȯ ��� ĵ���� Ȱ��ȭ/��Ȱ��ȭ�� ���� ��ȯ
public class GameStateController : MonoBehaviour
{
    public static GameStateController Instance { get; private set; }

    [Header("Canvas References")]
    public Canvas mapCanvas;
    
    [Header("Stage GameObjects")]
    public GameObject mapStage;          // MapStage GameObject
    public GameObject combatStage;       // CombatStage GameObject
    public GameObject eliteStage;        // EliteStage GameObject (�ִٸ�)
    public GameObject bossStage;         // BossStage GameObject (�ִٸ�)
    public GameObject shopStage;         // ShopStage GameObject (�ִٸ�)
    public GameObject restStage;         // RestStage GameObject (�ִٸ�)
    public GameObject eventStage;        // EventStage GameObject (�̺�Ʈ ���)

    [Header("Managers")]
    public MapManager mapManager;
    public BattleManger battleManager;
    public Roundmanager roundManager;  // ���� ������ �߰�

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
        // ���� ���� �� �ʱ�ȭ �� �� ȭ�� ǥ��
        InitializeGameState();
        ShowMap();
    }

    void InitializeGameState()
    {
        // GameManager�� ����ȭ
        if (GameManager.Instance != null)
        {
            lastVisitedNodeIndex = GameManager.Instance.lastVisitedNodeIndex;
            clearedNodes = new System.Collections.Generic.List<int>(GameManager.Instance.clearedNodes);
        }
        
        // Panel raycastTarget ��Ȱ��ȭ
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
            
            // Panel�̳� background �̹����� Ŭ���� ���� �ʵ��� ����
            DisablePanelRaycast();
        }
    }
    
    void DisablePanelRaycast()
    {
        if (mapCanvas == null) return;
        
        // Canvas ������ ��� Image �� Panel, Background ���� raycastTarget ��Ȱ��ȭ
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

    // �� ȭ������ ��ȯ
    // ���� ĵ���� ����� �� ĵ���� ǥ��
    public void ShowMap()
    {
        //  ��� �������� ��Ȱ��ȭ
        HideAllStages();

        //  �� ���������� Ȱ��ȭ
        if (mapStage != null)
        {
            mapStage.SetActive(true);
        }
        else if (mapCanvas != null)
        {
            // ���� ȣȯ: mapStage�� ������ mapCanvas ���
            mapCanvas.gameObject.SetActive(true);
        }
        else
        {
            Debug.LogError("mapStage�� mapCanvas �� �� null�Դϴ�!");
        }

        //  ���� ���ΰ�ħ (�ణ�� �������� MapManager �ʱ�ȭ �Ϸ� ���)
        if (mapManager != null)
        {
            StartCoroutine(RefreshMapDelayed());
        }
        else
        {
            Debug.LogError("mapManager�� null�Դϴ�!");
        }
    }

    System.Collections.IEnumerator RefreshMapDelayed()
    {
        // �� ������ ����Ͽ� MapManager.Start() �Ϸ� ����
        yield return null;
        mapManager.RefreshMap();
    }

    // ��� Ÿ�Կ� ���� ������ �������� ǥ��
    // MapManager.OnNodeSelected()���� ȣ���
    public void ShowCanvasForNodeType(NodeType nodeType, bool isBossNode)
    {
        //  ��� �������� ��Ȱ��ȭ
        HideAllStages();

        //  ��� Ÿ�Կ� ���� �������� Ȱ��ȭ
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
            Debug.LogWarning($"��� Ÿ�� {nodeType}�� �ش��ϴ� ���������� �����ϴ�!");
        }
    }

    // ��� �������� ��Ȱ��ȭ
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

    // ���� ȭ������ ��ȯ
    [System.Obsolete("Use ShowCanvasForNodeType instead")]
    public void ShowBattle()
    {
        ShowCanvasForNodeType(NodeType.Combat, false);
    }

    // ��� Ŭ���� ó��
    //GameManager ������ �����
    public void MarkNodeCleared(int index)
    {
        if (index < 0) return;
        if (!clearedNodes.Contains(index))
        {
            clearedNodes.Add(index);
        }
    }

    // ��尡 Ŭ����Ǿ����� Ȯ��
    public bool IsNodeCleared(int index)
    {
        return index >= 0 && clearedNodes.Contains(index);
    }

    // ���� Ŭ���� �� ������ ����
    public void OnRoundClear()
    {
        // ���� ��� Ŭ���� ó��
        if (lastVisitedNodeIndex >= 0)
        {
            MarkNodeCleared(lastVisitedNodeIndex);
        }
    }

}