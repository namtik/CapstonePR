using UnityEngine;

[DisallowMultipleComponent]
public class ElementSlotSpriteOverride : MonoBehaviour
{
    [Tooltip("Slot icon sprite override for this specific slot (Q/W/E/R).")]
    public Sprite slotSprite; // 이 슬롯 전용 아이콘 스프라이트 오버라이드
}
