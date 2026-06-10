using UnityEngine;
using UnityEngine.UI;

public class Card : MonoBehaviour
{
    public string cardType; // 카드 타입(Q/W/E/R)
    public Image cardImage; // 카드 이미지

    // 카드 타입과 스프라이트를 설정
    public void SetType(string type, Sprite sprite)
    {
        this.cardType = type;
        this.cardImage.sprite = sprite;
        Debug.Log($"생성:{type}");
    }


}
