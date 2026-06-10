using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using static SkillDataParser;

public class SkillRewardUI : MonoBehaviour
{
    public GameObject rewardPanel;     // 전체 패널
    public Transform cardContainer;    // 카드가 생성될 부모
    public GameObject cardPrefab;      // 선택지 카드 프리팹 (버튼 포함)
    public RoundManager roundManager;          // 라운드 매니저 참조
    public MapNode currentNode;                      // 현재 노드 참조

    public int currentStage;
    [SerializeField] private bool returnToMapAfterSelection = true;
    [Header("Auto Resolve (Fallback)")]
    [SerializeField] private string rewardPanelName = "SkillRewardUI";
    [SerializeField] private string cardContainerName = "CardContainer";

    public System.Action<SkillData> OnSkillSelected;

    public void SetReturnToMapAfterSelection(bool enabled)
    {
        returnToMapAfterSelection = enabled;
    }

    private bool isStarterSelection = false;

    void Awake()
    {
        ResolveReferences();
    }

    public void ShowRewardOptions()
    {
        isStarterSelection = false;
        ShowSkillSelection();
    }

    /// <summary>게임 시작 시 초기 스킬 선택 UI 표시</summary>
    public void ShowStarterSkillSelection()
    {
        isStarterSelection = true;
        ShowSkillSelection();
    }

    void ShowSkillSelection()
    {
        ResolveReferences();

        if (rewardPanel == null)
        {
            Debug.LogError("[SkillRewardUI] rewardPanel을 찾지 못했습니다. 보상 선택을 중단합니다.");
            Time.timeScale = 1f;
            return;
        }

        if (cardContainer == null)
        {
            Debug.LogError("[SkillRewardUI] cardContainer를 찾지 못했습니다. 보상 선택을 중단합니다.");
            rewardPanel.SetActive(false);
            Time.timeScale = 1f;
            return;
        }

        if (cardPrefab == null)
        {
            Debug.LogError("[SkillRewardUI] cardPrefab이 비어 있습니다. 보상 선택을 중단합니다.");
            rewardPanel.SetActive(false);
            Time.timeScale = 1f;
            return;
        }

        // SkillRewardUIContainer가 비활성이면 활성화 (상위 계층은 건드리지 않음)
        EnsureContainerActive(true);
        ActivateParents(transform);
        ActivateParents(rewardPanel.transform);

        rewardPanel.SetActive(true);
        rewardPanel.transform.SetAsLastSibling();

        CanvasGroup panelGroup = rewardPanel.GetComponent<CanvasGroup>();
        if (panelGroup != null)
        {
            panelGroup.alpha = 1f;
            panelGroup.interactable = true;
            panelGroup.blocksRaycasts = true;
        }

        RectTransform panelRect = rewardPanel.GetComponent<RectTransform>();
        if (panelRect != null && panelRect.localScale == Vector3.zero)
            panelRect.localScale = Vector3.one;

        Time.timeScale = 0f; // 게임 일시정지

        // 기존 카드 제거
        foreach (Transform t in cardContainer) Destroy(t.gameObject);

        HashSet<int> learnedSkillIds = ComboSkillRepository.GetLearnedSkillIds();

        // 랜덤 3개 가져오기
        List<SkillData> options = SkillDataParser.Instance.GetRandomSkills(3, learnedSkillIds);

        foreach (SkillData skill in options)
        {
            GameObject card = Instantiate(cardPrefab, cardContainer);
            card.SetActive(true);

            RectTransform cardRect = card.GetComponent<RectTransform>();
            if (cardRect != null && cardRect.localScale == Vector3.zero)
                cardRect.localScale = Vector3.one;

            SkillCardUI cardUI = card.GetComponent<SkillCardUI>();
            if (cardUI != null)
            {
                cardUI.Setup(skill, OnSelectSkill);
            }
        }
    }

    void OnSelectSkill(SkillData skill)
    {
        if (skill == null)
        {
            Debug.LogWarning("[SkillRewardUI] 선택된 스킬 데이터가 null입니다.");
            rewardPanel.SetActive(false);
            Time.timeScale = 1f;
            return;
        }

        rewardPanel.SetActive(false);
        bool learned = ComboSkillRepository.LearnSkill(skill);
        if (!learned)
            Debug.Log($"[SkillRewardUI] 이미 보유 중인 스킬 선택 시도: {skill.name} ({skill.id})");

        OnSkillSelected?.Invoke(skill);

        // 초기 스킬 선택이면 맵 복귀 없이 종료
        if (isStarterSelection)
        {
            isStarterSelection = false;
            Time.timeScale = 1f;
            EnsureContainerActive(false);
            return;
        }

        if (returnToMapAfterSelection)
        {
            Time.timeScale = 1f;
            if (roundManager != null)
                roundManager.ReturnToMap();
            else
                Debug.LogWarning("[SkillRewardUI] roundManager reference is missing.");
        }
    }

    /// <summary>SkillRewardUI가 붙은 오브젝트만 활성/비활성 (상위 combatStage 등은 건드리지 않음)</summary>
    void EnsureContainerActive(bool active)
    {
        gameObject.SetActive(active);
    }

    void ResolveReferences()
    {
        if (rewardPanel == null)
        {
            rewardPanel = FindGameObjectByNameIncludingInactive(rewardPanelName);
            if (rewardPanel == null)
                rewardPanel = FindGameObjectByNameIncludingInactive("RewardPanel");
            if (rewardPanel == null)
                rewardPanel = gameObject;

            Debug.Log($"[SkillRewardUI] rewardPanel resolved: {rewardPanel.name}");
        }

        if (cardContainer == null && rewardPanel != null)
        {
            Transform found = rewardPanel.transform.Find(cardContainerName);
            if (found != null)
                cardContainer = found;
        }

        if (cardContainer == null && rewardPanel != null)
        {
            // 일부 씬은 rewardPanel 자체가 LayoutGroup 컨테이너 역할을 한다.
            if (rewardPanel.GetComponent<HorizontalLayoutGroup>() != null ||
                rewardPanel.GetComponent<VerticalLayoutGroup>() != null ||
                rewardPanel.GetComponent<GridLayoutGroup>() != null)
            {
                cardContainer = rewardPanel.transform;
            }
        }

        if (cardContainer == null && rewardPanel != null)
        {
            GameObject runtimeContainer = new GameObject("CardContainer", typeof(RectTransform));
            runtimeContainer.transform.SetParent(rewardPanel.transform, false);

            RectTransform rect = runtimeContainer.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(900f, 380f);

            HorizontalLayoutGroup layout = runtimeContainer.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 24f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            cardContainer = runtimeContainer.transform;
            Debug.LogWarning("[SkillRewardUI] cardContainer가 없어 런타임 컨테이너를 생성했습니다.");
        }

        if (cardContainer != null)
            Debug.Log($"[SkillRewardUI] cardContainer resolved: {cardContainer.name}");

        if (roundManager == null)
            roundManager = FindFirstObjectByType<RoundManager>(FindObjectsInactive.Include);
    }

    static GameObject FindGameObjectByNameIncludingInactive(string targetName)
    {
        if (string.IsNullOrEmpty(targetName))
            return null;

        Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Transform t in allTransforms)
        {
            if (t == null)
                continue;

            if (!t.gameObject.scene.IsValid())
                continue;

            if (t.name == targetName)
                return t.gameObject;
        }

        return null;
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
}