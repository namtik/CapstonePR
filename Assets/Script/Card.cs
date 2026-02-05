using UnityEngine;
using UnityEngine.UI;

public class Card : MonoBehaviour
{
    public string cardType; // Q, W, E, R 카드 타입
    public Image cardImage; // 카드 이미지

    public void SetType(string type, Sprite sprite)
    {
        this.cardType = type;
        this.cardImage.sprite = sprite;
        Debug.Log($"생성:{type}");
    }


}
