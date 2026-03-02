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

    public void ShowRewardOptions()
    {
        rewardPanel.SetActive(true);
        Time.timeScale = 0f; // 게임 일시정지

        // 기존 카드 제거
        foreach (Transform t in cardContainer) Destroy(t.gameObject);

        // 랜덤 3개 가져오기
        List<SkillData> options = SkillDataParser.Instance.GetRandomSkills(3);

        foreach (SkillData skill in options)
        {
            GameObject card = Instantiate(cardPrefab, cardContainer);

            SkillCardUI cardUI = card.GetComponent<SkillCardUI>();
            if (cardUI != null)
            {
                // 데이터와 클릭했을 때 할 행동(OnSelectSkill)을 전달
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
        Time.timeScale = 1f;
        ComboSystem.Instance.LearnSkill(skill);
        if (ComboSystem.Instance.learnedSkillCount == 1)
        {
            return;
        }
        else
        {
            roundManager.ReturnToMap();
        }
    }
}