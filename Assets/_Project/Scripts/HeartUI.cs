using UnityEngine;
using UnityEngine.UI;

// 플레이어 체력을 하트 아이콘으로 표시하는 UI
public class HeartUI : MonoBehaviour
{
    [Header("하트 스프라이트")]
    public Sprite fullHeartSprite; // 채워진 하트 이미지
    public Sprite emptyHeartSprite; // 빈 하트 이미지

    [Header("하트 오브젝트 (PlayerHp 하위의 Heart들)")]
    public Image[] hearts; // 하트 이미지 배열

    // 현재/최대 체력에 따라 하트 채움과 표시 개수를 갱신한다
    public void UpdateHearts(int currentHp, int maxHp)
    {
        for (int i = 0; i < hearts.Length; i++)
        {
            if (i < currentHp)
            {
                hearts[i].sprite = fullHeartSprite;
            }
            else
            {
                hearts[i].sprite = emptyHeartSprite;
            }

            hearts[i].gameObject.SetActive(i < maxHp);
        }
    }
}
