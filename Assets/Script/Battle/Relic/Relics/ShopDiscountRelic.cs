using UnityEngine;

namespace Battle.Relic
{
    // 할인패 (10001): 보유 시, 상점 상품 가격이 할인된다.
    [CreateAssetMenu(menuName = "Relic/ShopDiscount", fileName = "ShopDiscountRelic")]
    public class ShopDiscountRelic : RelicSO
    {
        [Header("효과")]
        [Tooltip("상점 가격 배수(0.5 = 50% 할인)")]
        [Range(0f, 1f)] public float priceMultiplier = 0.5f; // 가격 배수

        public override float ShopPriceMultiplier() => priceMultiplier;
    }
}
