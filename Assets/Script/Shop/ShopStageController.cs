using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using static SkillDataParser;

/// <summary>
/// ShopStage 전체를 관리하는 컨트롤러
/// - 상점 초기화
/// - 콤보스킬 상점 진입/퇴출
/// - 맵으로 돌아가기
/// </summary>
public class ShopStageController : MonoBehaviour
{
    [Header("ShopStage 버튼 참조")]
    [SerializeField] private Button storeButton;        // 콤보스킬 상점 진입 버튼
    [SerializeField] private Button removeButton;       // 카드 제거 기능 (아직 미구현)
    [SerializeField] private Button outButton;          // 상점 나가기 버튼

    [Header("콤보스킬 상점 UI 참조")]
    [SerializeField] private GameObject comboSkillShopPanel;  // 콤보스킬 상점 UI 패널
    [SerializeField] private Transform skillItemsParent;      // 스킬 아이템들이 들어갈 부모 (4개 슬롯)

    [Header("가격 범위 설정")]
    [SerializeField] private int minPrice = 100;
    [SerializeField] private int maxPrice = 500;

    [Header("스킬 상점 아이템 프리팹")]
    [SerializeField] private GameObject skillShopItemPrefab;  // 각 스킬을 표시할 프리팹

    private List<ComboSkillShopItem> currentShopItems = new List<ComboSkillShopItem>();
    private ComboSystem comboSystem;

    void Start()
    {
        // 필요한 컴포넌트 찾기
        comboSystem = FindFirstObjectByType<ComboSystem>();

        // 버튼 이벤트 연결
        if (storeButton != null)
            storeButton.onClick.AddListener(OnStoreButtonClicked);

        if (removeButton != null)
            removeButton.onClick.AddListener(OnRemoveButtonClicked);

        if (outButton != null)
            outButton.onClick.AddListener(OnOutButtonClicked);

        // 콤보스킬 상점 패널을 기본적으로 비활성화
        if (comboSkillShopPanel != null)
            comboSkillShopPanel.SetActive(false);

        Debug.Log("[ShopStageController] 초기화 완료");
    }

    /// <summary>
    /// [콤보스킬 상점 진입] 버튼 클릭 핸들러
    /// </summary>
    void OnStoreButtonClicked()
    {
        Debug.Log("[ShopStageController] 콤보스킬 상점 진입");
        OpenComboSkillShop();
    }

    /// <summary>
    /// [카드 제거] 버튼 클릭 핸들러 (아직 미구현)
    /// </summary>
    void OnRemoveButtonClicked()
    {
        Debug.Log("[ShopStageController] 카드 제거 기능 (아직 미구현)");
        // TODO: 카드 제거 로직 구현
    }

    /// <summary>
    /// [상점 나가기] 버튼 클릭 핸들러
    /// </summary>
    void OnOutButtonClicked()
    {
        Debug.Log("[ShopStageController] 상점에서 나가기 버튼 클릭");
        
        // 콤보스킬 상점이 열려있으면 먼저 닫기
        if (comboSkillShopPanel != null && comboSkillShopPanel.activeInHierarchy)
        {
            Debug.Log("[ShopStageController] 콤보스킬 상점 닫기");
            CloseComboSkillShop();
            return; // 상점만 닫고 ShopStage에 머물기
        }
        
        // 상점이 닫혀있으면 맵으로 복귀
        ReturnToMapFromShop();
    }

    /// <summary>
    /// 콤보스킬 상점 오픈 - 4개의 랜덤 스킬 표시
    /// </summary>
    void OpenComboSkillShop()
    {
        if (comboSkillShopPanel == null)
        {
            Debug.LogError("[ShopStageController] comboSkillShopPanel이 할당되지 않았습니다!");
            return;
        }

        // 기존 아이템 제거
        currentShopItems.Clear();
        foreach (Transform child in skillItemsParent)
        {
            Destroy(child.gameObject);
        }

        // 플레이어가 이미 배운 스킬 ID 가져오기
        HashSet<int> learnedSkillIds = comboSystem.GetLearnedSkillIds();

        // 4개의 랜덤 스킬 선택
        List<SkillData> randomSkills = SkillDataParser.Instance.GetRandomSkills(4, learnedSkillIds);

        // 각 스킬에 대해 ComboSkillShopItem 생성
        foreach (SkillData skill in randomSkills)
        {
            ComboSkillShopItem item = new ComboSkillShopItem(skill);
            currentShopItems.Add(item);

            // 가격 범위 설정 적용
            ComboSkillShopItem.minPrice = minPrice;
            ComboSkillShopItem.maxPrice = maxPrice;

            item.PrintInfo();
        }

        // UI 갱신
        RefreshShopUI();

        // 상점 패널 활성화
        comboSkillShopPanel.SetActive(true);

        Debug.Log("[ShopStageController] 콤보스킬 상점 오픈 - 4개 스킬 표시");
    }

    /// <summary>
    /// 콤보스킬 상점 UI 갱신
    /// </summary>
    void RefreshShopUI()
    {
        for (int i = 0; i < currentShopItems.Count; i++)
        {
            ComboSkillShopItem item = currentShopItems[i];

            // 스킬 아이템 UI 생성 (프리팹 사용)
            GameObject itemUI = Instantiate(skillShopItemPrefab, skillItemsParent);

            // ComboSkillShopItemUI 컴포넌트에 데이터 전달
            ComboSkillShopItemUI itemUIScript = itemUI.GetComponent<ComboSkillShopItemUI>();
            if (itemUIScript != null)
            {
                itemUIScript.Initialize(item, i, this);
            }
        }
    }

    /// <summary>
    /// 스킬 구매 처리 (ComboSkillShopItemUI에서 호출)
    /// </summary>
    public bool TryBuySkill(int itemIndex)
    {
        if (itemIndex < 0 || itemIndex >= currentShopItems.Count)
        {
            Debug.LogError("[ShopStageController] 유효하지 않은 아이템 인덱스");
            return false;
        }

        ComboSkillShopItem item = currentShopItems[itemIndex];

        // 재화 확인 및 차감
        if (MoneyManager.Instance.SpendMoney(item.price))
        {
            // 스킬 배우기
            comboSystem.LearnSkill(item.skill);

            Debug.Log($"[ShopStageController] {item.skill.name} 구매 완료! (-{item.price}원)");
            return true;
        }
        else
        {
            Debug.Log($"[ShopStageController] 재화 부족! 필요: {item.price}원");
            return false;
        }
    }

    /// <summary>
    /// 콤보스킬 상점 닫기
    /// </summary>
    void CloseComboSkillShop()
    {
        if (comboSkillShopPanel != null)
            comboSkillShopPanel.SetActive(false);

        Debug.Log("[ShopStageController] 콤보스킬 상점 닫음");
    }

    /// <summary>
    /// 콤보스킬 상점 닫기 (외부에서 호출 가능)
    /// </summary>
    public void CloseShop()
    {
        CloseComboSkillShop();
    }

    /// <summary>
    /// ShopStage에서 맵으로 복귀
    /// </summary>
    void ReturnToMapFromShop()
    {
        Debug.Log("[ShopStageController] 맵으로 복귀 시작");
        
        // GameStateController를 통해 맵으로 복귀
        var stateController = GameStateController.Instance;
        if (stateController == null)
        {
            Debug.LogError("[ShopStageController] GameStateController.Instance를 찾을 수 없습니다!");
            return;
        }
        
        // 현재 노드 클리어 처리
        stateController.MarkNodeCleared(stateController.lastVisitedNodeIndex);
        
        // 맵으로 복귀
        stateController.ShowMap();
        Debug.Log("[ShopStageController] 맵으로 복귀 완료");
    }
}
