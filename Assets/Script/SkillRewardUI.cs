using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using static SkillDataParser;

public class SkillRewardUI : MonoBehaviour
{
    public GameObject rewardPanel;     // 전체 패널
    public Transform cardContainer;    // 카드가 생성될 부모
    public GameObject cardPrefab;      // 선택지 카드 프리팹 (버튼 포함)
    public Roundmanager roundManager;          // 라운드 매니저 참조
    public MapNode currentNode;                      // 현재 노드 참조

    public int currentStage;
    [SerializeField] private bool returnToMapAfterSelection = true;

    public System.Action<SkillData> OnSkillSelected;

    public void SetReturnToMapAfterSelection(bool enabled)
    {
        returnToMapAfterSelection = enabled;
    }

    private bool isStarterSelection = false;

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
        // SkillRewardUIContainer가 비활성이면 활성화 (상위 계층은 건드리지 않음)
        EnsureContainerActive(true);

        rewardPanel.SetActive(true);
        Time.timeScale = 0f; // 게임 일시정지

        // 기존 카드 제거
        foreach (Transform t in cardContainer) Destroy(t.gameObject);

        HashSet<int> learnedSkillIds = new HashSet<int>();
        if (ComboSystem.Instance != null)
        {
            foreach (SkillData skill in ComboSystem.Instance.learnedSkills)
            {
                learnedSkillIds.Add(skill.id);
            }
        }

        // 랜덤 3개 가져오기
        List<SkillData> options = SkillDataParser.Instance.GetRandomSkills(3, learnedSkillIds);

        foreach (SkillData skill in options)
        {
            GameObject card = Instantiate(cardPrefab, cardContainer);

            SkillCardUI cardUI = card.GetComponent<SkillCardUI>();
            if (cardUI != null)
            {
                cardUI.Setup(skill, OnSelectSkill);
            }
        }
    }

    void OnSelectSkill(SkillData skill)
    {
        if (ComboSystem.Instance == null)
        {
            Debug.LogError("[SkillRewardUI] ComboSystem.Instance가 null입니다. 씬에 ComboSystem이 있는지 확인하세요.");
            rewardPanel.SetActive(false);
            Time.timeScale = 1f;
            return;
        }

        rewardPanel.SetActive(false);
        ComboSystem.Instance.LearnSkill(skill);
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
}