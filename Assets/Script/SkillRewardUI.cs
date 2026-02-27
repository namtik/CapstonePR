using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using static SkillDataParser;

public class SkillRewardUI : MonoBehaviour
{
    public GameObject rewardPanel;     // 전체 패널
    public Transform cardContainer;    // 카드가 생성될 부모
    public GameObject cardPrefab;      // 선택지 카드 프리팹 (버튼 포함)


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
        // 콤보 시스템에 스킬 등록
        ComboSystem.Instance.LearnSkill(skill);

        rewardPanel.SetActive(false); // 창 닫기
        Time.timeScale = 1f; // 게임 재개
    }
}