using UnityEngine;
using static SkillDataParser;

// 콤보스킬 상점에서 판매되는 개별 스킬 아이템
// 스킬 데이터와 가격 정보를 함께 보관
[System.Serializable]
public class ComboSkillShopItem
{
    public SkillData skill;
    public int price;

    // 가격 범위 설정
    public static int minPrice = 100;
    public static int maxPrice = 500;

    // ComboSkillShopItem 생성자
    public ComboSkillShopItem(SkillData skillData)
    {
        skill = skillData;
        // 가격은 랜덤하게 설정 (minPrice ~ maxPrice 범위)
        price = Random.Range(minPrice, maxPrice + 1);
    }

    // 스킬 정보를 로그로 출력
    public void PrintInfo()
    {
        Debug.Log($"[ComboSkillShop] {skill.name} - 가격: {price}원");
    }
}
