using UnityEngine;
using UnityEngine.UI;

public class HeartUI : MonoBehaviour
{
    [Header("하트 스프라이트")]
    public Sprite fullHeartSprite;
    public Sprite emptyHeartSprite;

    [Header("하트 오브젝트 (PlayerHp 하위의 Heart들)")]
    public Image[] hearts;

    /// <summary>
    /// 현재 체력에 따라 하트 UI를 업데이트합니다.
    /// currentHp: 남은 하트 수 (예: 2이면 하트 2개 채움)
    /// maxHp: 전체 하트 수 (예: 3이면 하트 3개)
    /// </summary>
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
