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

    //public void ShowRewardOptions()
    //{
    //    rewardPanel.SetActive(true);
    //    Time.timeScale = 0f; // 게임 일시정지

    //    // 기존 카드 제거
    //    foreach (Transform t in cardContainer) Destroy(t.gameObject);

    //    HashSet<int> learnedSkillIds = new HashSet<int>();
    //    foreach (SkillData skill in ComboSystem.Instance.learnedSkills)
    //    {
    //        learnedSkillIds.Add(skill.id);
    //    }

    //    // 랜덤 3개 가져오기
    //    List<SkillData> options = SkillDataParser.Instance.GetRandomSkills(3, learnedSkillIds);


    //    foreach (SkillData skill in options)
    //    {
    //        GameObject card = Instantiate(cardPrefab, cardContainer);

    //        SkillCardUI cardUI = card.GetComponent<SkillCardUI>();
    //        if (cardUI != null)
    //        {
    //            // 데이터와 클릭했을 때 할 행동(OnSelectSkill)을 전달
    //            cardUI.Setup(skill, OnSelectSkill);
    //        }
    //    }
    //}
    public void ShowRewardOptions()
    {
        isStarterSelection = false;    // 일반 보상 모드
        ShowSkillSelection();
    }

    /// <summary>게임 시작 시 초기 스킬 선택 UI 표시</summary>
    public void ShowStarterSkillSelection()
    {
        isStarterSelection = true;     // 초기 선택 모드 → 맵 복귀 안 함
        ShowSkillSelection();
    }

    void ShowSkillSelection()
    {
        rewardPanel.SetActive(true);
        Time.timeScale = 0f;  // 선택 중 게임 일시정지

        foreach (Transform t in cardContainer) Destroy(t.gameObject);  // 기존 카드 제거

        // 이미 배운 스킬 ID 수집 (중복 방지)
        HashSet<int> learnedSkillIds = new HashSet<int>();
        if (ComboSystem.Instance != null)
        {
            foreach (SkillData skill in ComboSystem.Instance.learnedSkills)
                learnedSkillIds.Add(skill.id);
        }

        // 랜덤 3개 스킬 선택지 생성
        List<SkillData> options = SkillDataParser.Instance.GetRandomSkills(3, learnedSkillIds);

        foreach (SkillData skill in options)
        {
            GameObject card = Instantiate(cardPrefab, cardContainer);
            SkillCardUI cardUI = card.GetComponent<SkillCardUI>();
            if (cardUI != null)
                cardUI.Setup(skill, OnSelectSkill);  // 클릭 시 OnSelectSkill 호출
        }
    }

    void OnSelectSkill(SkillData skill)
    {
        if (ComboSystem.Instance == null)
        {
            Debug.LogError("[SkillRewardUI] ComboSystem.Instance가 null입니다.");
            rewardPanel.SetActive(false);
            Time.timeScale = 1f;
            return;
        }

        rewardPanel.SetActive(false);
        ComboSystem.Instance.LearnSkill(skill);     // 스킬 학습
        OnSkillSelected?.Invoke(skill);              // ← UpdatingSys 측 외부 콜백

        // ── 초기 스킬 선택이면 맵 복귀 없이 종료 ──
        if (isStarterSelection)
        {
            isStarterSelection = false;
            Time.timeScale = 1f;  // 일시정지 해제
            return;               // ← 여기서 끝. ReturnToMap() 호출 안 함
        }

        // ── 일반 전투 보상이면 맵으로 복귀 ──
        if (returnToMapAfterSelection)               // ← UpdatingSys 측 플래그
        {
            Time.timeScale = 1f;
            if (roundManager != null)
                roundManager.ReturnToMap();
            else
                Debug.LogWarning("[SkillRewardUI] roundManager reference is missing.");
        }
    }
}