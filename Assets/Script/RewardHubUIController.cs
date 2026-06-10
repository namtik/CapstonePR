using UnityEngine;
using UnityEngine.UI;

// 전투 보상 허브(스킬/카드 업그레이드) 화면 전환을 관리한다
public class RewardHubUIController : MonoBehaviour
{
    [Header("UI Roots")]
    [SerializeField] private GameObject rewardHubRoot; // 허브 버튼 화면 루트
    [SerializeField] private GameObject skillRewardRoot; // 스킬 보상 화면 루트
    [SerializeField] private GameObject cardUpgradeRoot; // 카드 업그레이드 화면 루트

    [Header("References")]
    [SerializeField] private RoundManager roundManager; // 라운드 매니저 참조
    [SerializeField] private SkillRewardUI skillRewardUI; // 스킬 보상 UI 참조
    [SerializeField] private CardUpgradeUIController cardUpgradeUIController; // 카드 업그레이드 UI 참조
    [SerializeField] private Button comboSkillButton; // 콤보 스킬 보상 버튼

    [Header("Auto Find Names (Fallback)")]
    [SerializeField] private string rewardHubRootName = "RewardHubUI"; // 허브 루트 자동 탐색 이름
    [SerializeField] private string skillRewardRootName = "SkillRewardUIContainer"; // 스킬 보상 루트 자동 탐색 이름
    [SerializeField] private string cardUpgradeRootName = "CardUpgradeUI"; // 카드 업그레이드 루트 자동 탐색 이름

    private bool rewardFlowActive; // 보상 플로우 진행 중 여부
    private bool skillClaimedThisReward; // 이번 보상에서 스킬을 이미 받았는지

    // 참조를 확인하고 모든 화면을 비활성화한 뒤 이벤트를 연결한다
    void Awake()
    {
        ResolveReferences();

        if (rewardHubRoot != null)
            rewardHubRoot.SetActive(false);

        if (skillRewardRoot != null)
            skillRewardRoot.SetActive(false);

        if (cardUpgradeRoot != null)
            cardUpgradeRoot.SetActive(false);

        if (skillRewardUI != null)
        {
            skillRewardUI.SetReturnToMapAfterSelection(false);
            skillRewardUI.OnSkillSelected -= HandleSkillSelected;
            skillRewardUI.OnSkillSelected += HandleSkillSelected;
        }
    }

    // 스킬 선택 이벤트 구독을 해제한다
    void OnDestroy()
    {
        if (skillRewardUI != null)
            skillRewardUI.OnSkillSelected -= HandleSkillSelected;
    }

    // 보상 허브 화면을 열고 게임을 일시정지한다
    public void OpenHub()
    {
        ResolveReferences();

        if (rewardHubRoot == null)
        {
            Debug.LogError("[RewardHubUIController] rewardHubRoot is not assigned.");
            return;
        }

        rewardFlowActive = true;
        skillClaimedThisReward = false;
        Time.timeScale = 0f;
        if (cardUpgradeUIController != null)
            cardUpgradeUIController.SetUpgradeClaimed(false);
        if (comboSkillButton != null)
            comboSkillButton.interactable = true;

        Debug.Log($"[RewardHubUIController] OpenHub -> root={rewardHubRoot.name}, activeInHierarchy={rewardHubRoot.activeInHierarchy}");
        SetOnly(rewardHubRoot);
    }

    // 스킬 보상 선택 화면을 열고 선택지를 표시한다
    public void OpenSkillReward()
    {
        if (skillClaimedThisReward) return;
        if (!rewardFlowActive)
            OpenHub();

        ResolveReferences();

        SetOnly(skillRewardRoot);
        if (skillRewardUI != null)
            skillRewardUI.ShowRewardOptions();
        else
            Debug.LogError("[RewardHubUIController] skillRewardUI reference is missing.");
    }

    // 허브 없이 스킬 보상 카드만 단독으로 표시한다(신규 전투용)
    public void OpenSkillRewardStandalone()
    {
        ResolveReferences();

        rewardFlowActive = false;
        skillClaimedThisReward = false;
        Time.timeScale = 0f;

        SetOnly(skillRewardRoot);
        if (skillRewardUI != null)
        {
            skillRewardUI.SetReturnToMapAfterSelection(false);
            skillRewardUI.ShowRewardOptions();
        }
        else
        {
            Debug.LogError("[RewardHubUIController] OpenSkillRewardStandalone failed: skillRewardUI reference is missing.");
        }
    }

    // 카드 업그레이드 선택 화면을 열고 선택지를 표시한다
    public void OpenCardUpgrade()
    {
        if (!rewardFlowActive)
            OpenHub();

        ResolveReferences();
        SetOnly(cardUpgradeRoot);
        if (cardUpgradeUIController != null)
            cardUpgradeUIController.ShowUpgradeOptions();
        else
            Debug.LogError("[RewardHubUIController] cardUpgradeUIController reference is missing.");
    }

    // 보상 허브 화면으로 되돌아간다
    public void BackToHub()
    {
        if (!rewardFlowActive)
            return;

        SetOnly(rewardHubRoot);
    }

    // 보상을 종료하고 시간을 복구한 뒤 맵으로 돌아간다
    public void TryLeaveToMap()
    {
        rewardFlowActive = false;
        Time.timeScale = 1f;
        SetOnly(null);

        if (roundManager != null)
            roundManager.ReturnToMap();
        else
            Debug.LogWarning("[RewardHubUIController] RoundManager reference is missing.");
    }

    // 스킬 선택 완료 시 버튼을 잠그고 허브로 복귀한다
    void HandleSkillSelected(SkillDataParser.SkillData _)
    {
        skillClaimedThisReward = true;
        if (comboSkillButton != null)
            comboSkillButton.interactable = false;
        if (rewardFlowActive)
            BackToHub();
    }

    // 지정한 루트만 활성화하고 나머지 보상 화면은 끈다
    void SetOnly(GameObject activeRoot)
    {
        if (rewardHubRoot != null)
            rewardHubRoot.SetActive(rewardHubRoot == activeRoot);

        if (skillRewardRoot != null)
            skillRewardRoot.SetActive(skillRewardRoot == activeRoot);

        if (cardUpgradeRoot != null)
            cardUpgradeRoot.SetActive(cardUpgradeRoot == activeRoot);

        if (activeRoot != null)
            ActivateParents(activeRoot.transform);
    }

    // 비어 있는 루트/컴포넌트 참조를 이름·타입으로 찾아 채운다
    void ResolveReferences()
    {
        if (rewardHubRoot == null)
        {
            GameObject foundHub = FindGameObjectByNameIncludingInactive(rewardHubRootName);
            if (foundHub != null)
                rewardHubRoot = foundHub;
            else if (name == rewardHubRootName)
                rewardHubRoot = gameObject;
            else
                Debug.LogWarning($"[RewardHubUIController] '{rewardHubRootName}' 오브젝트를 찾지 못해 현재 오브젝트({name})를 허브 루트로 사용합니다.");
        }

        if (skillRewardRoot == null)
            skillRewardRoot = FindGameObjectByNameIncludingInactive(skillRewardRootName);

        if (cardUpgradeRoot == null)
            cardUpgradeRoot = FindGameObjectByNameIncludingInactive(cardUpgradeRootName);

        if (skillRewardUI == null)
        {
            if (skillRewardRoot != null)
                skillRewardUI = skillRewardRoot.GetComponentInChildren<SkillRewardUI>(true);

            if (skillRewardUI == null)
                skillRewardUI = FindFirstObjectByType<SkillRewardUI>(FindObjectsInactive.Include);
        }

        if (roundManager == null)
            roundManager = FindFirstObjectByType<RoundManager>(FindObjectsInactive.Include);

        if (cardUpgradeUIController == null && cardUpgradeRoot != null)
            cardUpgradeUIController = cardUpgradeRoot.GetComponent<CardUpgradeUIController>();

        if (skillRewardUI != null)
            skillRewardUI.SetReturnToMapAfterSelection(false);

        if (rewardHubRoot == null)
            Debug.LogError("[RewardHubUIController] ResolveReferences failed: RewardHub root not found.");
    }

    // 자식부터 부모까지 비활성 오브젝트를 모두 활성화한다
    static void ActivateParents(Transform child)
    {
        Transform current = child;
        while (current != null)
        {
            if (!current.gameObject.activeSelf)
                current.gameObject.SetActive(true);

            current = current.parent;
        }
    }

    // 비활성 포함 이름으로 게임오브젝트를 검색한다
    static GameObject FindGameObjectByNameIncludingInactive(string targetName)
    {
        if (string.IsNullOrEmpty(targetName))
            return null;

        Transform[] allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
        foreach (Transform t in allTransforms)
        {
            if (t == null || t.hideFlags != HideFlags.None)
                continue;

            if (t.name == targetName)
                return t.gameObject;
        }

        return null;
    }
}
