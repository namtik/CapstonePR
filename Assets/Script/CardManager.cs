using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class CardSystem : MonoBehaviour
{
    public GameObject cardPrefab; // 카드 프리팹  
    public Transform cardParent; // 카드가 생성될 오브젝트  
    public Sprite[] cardSprites;
    private string[] cardTypes = { "Q", "W", "E", "R" };

    private List<string> deck = new List<string>();
    private List<GameObject> hand = new List<GameObject>();
    private List<string> graveyard = new List<string>();

    private Player player;
    private Enemy enemy;
    private float drawTimer = 0f;

    public TMP_Text deckText;    
    public TMP_Text graveyardText;

    void Start()
    {
        player = Object.FindFirstObjectByType<Player>();
        enemy = Object.FindFirstObjectByType<Enemy>();

        SetDeck();
        ShuffleDeck(deck);
        DrawCards(5);

    }

    void Update()
    {
        HandleInput();
        UpdateDrawTimer();
    }

    void UpdateCountUI()
    {
        if (deckText != null)
            deckText.text = $"Deck: {deck.Count}";

        if (graveyardText != null)
            graveyardText.text = $"Grave: {graveyard.Count}";
    }

    void SetDeck()
    {
        foreach (string type in cardTypes)
        {
            for (int i = 0; i < 5; i++) // 각 카드 타입당 5장씩  
            {
                deck.Add(type);
            }
        }
        ReshuffleGraveyard();
    }

    void ReshuffleGraveyard()
    {
        if (graveyard.Count == 0) return;
        deck.AddRange(graveyard);
        graveyard.Clear();
        ShuffleDeck(deck);
        Debug.Log("묘지의 카드를 덱으로 다시 섞었습니다.");
    }

    void ShuffleDeck(List<string> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int rand = Random.Range(i, list.Count);
            string temp = list[i];
            list[i] = list[rand];
            list[rand] = temp;
        }
    }

    void HandleInput()
    {
        if (hand.Count == 0) return;

        string inputKey = "";
        if (Input.GetKeyDown(KeyCode.Q)) inputKey = "Q";
        else if (Input.GetKeyDown(KeyCode.W)) inputKey = "W";
        else if (Input.GetKeyDown(KeyCode.E)) inputKey = "E";
        else if (Input.GetKeyDown(KeyCode.R)) inputKey = "R";

        if (inputKey == "") return;

        for (int i = 0; i < hand.Count; i++)
        {
            Card cardScript = hand[i].GetComponent<Card>();

            // 입력한 키와 카드의 타입이 일치하는 첫 번째 카드를 찾음  
            if (cardScript.cardType == inputKey)
            {
                UseCard(i);
                break;
            }
        }
    }

    void UseCard(int index)
    {
        GameObject cardObj = hand[index];
        string type = cardObj.GetComponent<Card>().cardType;

        // 데미지 계산 (플레이어 공격력의 100%) 추후 수정
        if (enemy != null && player != null)
        {
            enemy.TakeDamage(player.attackDamage);
            Debug.Log($"{type} 카드 사용! 적에게 {player.attackDamage} 데미지.");
        }

        // 묘지로 보내기 및 파괴  
        graveyard.Add(type);
        hand.RemoveAt(index);
        Destroy(cardObj);
        UpdateCountUI();
    }

    void UpdateDrawTimer()
    {
        if (hand.Count < 5)
        {
            drawTimer += Time.deltaTime;
            if (drawTimer >= 5f)
            {
                DrawCards(5 - hand.Count);
                drawTimer = 0f;
            }
        }
        else
        {
            drawTimer = 0f; // 이미 가득 차 있으면 타이머 리셋  
        }
    }

    private void DrawCards(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (deck.Count == 0)
            {
                ReshuffleGraveyard();
                if (deck.Count == 0) break;
            }

            string type = deck[0];
            deck.RemoveAt(0);

            GameObject newCard = Instantiate(cardPrefab, cardParent);
            Card cardScript = newCard.GetComponent<Card>();

            int spriteIndex = System.Array.IndexOf(cardTypes, type);
            cardScript.SetType(type, cardSprites[spriteIndex]);

            hand.Add(newCard);
       
        }
        UpdateCountUI();
    }
}
