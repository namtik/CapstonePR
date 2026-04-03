using UnityEngine;

public class RewardHubUIController : MonoBehaviour
{
    [Header("UI Roots")]
    [SerializeField] private GameObject rewardHubRoot;
    [SerializeField] private GameObject skillRewardRoot;
    [SerializeField] private GameObject cardUpgradeRoot;

    [Header("References")]
    [SerializeField] private Roundmanager roundManager;
    [SerializeField] private SkillRewardUI skillRewardUI;

    [Header("Auto Find Names (Fallback)")]
    [SerializeField] private string rewardHubRootName = "RewardHubUI";
    [SerializeField] private string skillRewardRootName = "SkillRewardUIContainer";
    [SerializeField] private string cardUpgradeRootName = "CardUpgradeUI";

    private bool rewardFlowActive;

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

    void OnDestroy()
    {
        if (skillRewardUI != null)
            skillRewardUI.OnSkillSelected -= HandleSkillSelected;
    }

    public void OpenHub()
    {
        ResolveReferences();

        if (rewardHubRoot == null)
        {
            Debug.LogError("[RewardHubUIController] rewardHubRoot is not assigned.");
            return;
        }

        rewardFlowActive = true;
        Time.timeScale = 0f;
        Debug.Log($"[RewardHubUIController] OpenHub -> root={rewardHubRoot.name}, activeInHierarchy={rewardHubRoot.activeInHierarchy}");
        SetOnly(rewardHubRoot);
    }

    public void OpenSkillReward()
    {
        if (!rewardFlowActive)
            OpenHub();

        ResolveReferences();

        SetOnly(skillRewardRoot);
        if (skillRewardUI != null)
            skillRewardUI.ShowRewardOptions();
        else
            Debug.LogError("[RewardHubUIController] skillRewardUI reference is missing.");
    }

    public void OpenCardUpgrade()
    {
        if (!rewardFlowActive)
            OpenHub();

        ResolveReferences();
        SetOnly(cardUpgradeRoot);
    }

    public void BackToHub()
    {
        if (!rewardFlowActive)
            return;

        SetOnly(rewardHubRoot);
    }

    public void TryLeaveToMap()
    {
        rewardFlowActive = false;
        Time.timeScale = 1f;
        SetOnly(null);

        if (roundManager != null)
            roundManager.ReturnToMap();
        else
            Debug.LogWarning("[RewardHubUIController] Roundmanager reference is missing.");
    }

    void HandleSkillSelected(SkillDataParser.SkillData _)
    {
        if (rewardFlowActive)
            BackToHub();
    }

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
            roundManager = FindFirstObjectByType<Roundmanager>(FindObjectsInactive.Include);

        if (skillRewardUI != null)
            skillRewardUI.SetReturnToMapAfterSelection(false);

        if (rewardHubRoot == null)
            Debug.LogError("[RewardHubUIController] ResolveReferences failed: RewardHub root not found.");
    }

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
