using UnityEngine;
using static SkillDataParser;

/// <summary>
/// 콤보스킬 상점에서 판매되는 각 스킬 항목
/// 스킬 데이터와 가격을 함께 관리합니다
/// </summary>
[System.Serializable]
public class ComboSkillShopItem
{
    public SkillData skill;              // 판매할 콤보스킬
    public int price;                   // 가격 (재화)
    
    // 가격 범위 설정
    public static int minPrice = 100;    // 최소 가격
    public static int maxPrice = 500;    // 최대 가격

    /// <summary>
    /// ComboSkillShopItem 생성자
    /// </summary>
    public ComboSkillShopItem(SkillData skillData)
    {
        skill = skillData;
        // 가격을 랜덤하게 설정 (minPrice ~ maxPrice 사이)
        price = Random.Range(minPrice, maxPrice + 1);
    }

    /// <summary>
    /// 스킬 정보를 로그로 출력
    /// </summary>
    public void PrintInfo()
    {
        Debug.Log($"[ComboSkillShop] {skill.name} - 가격: {price}원");
    }
}
