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
    private EnemyController enemyController;
    private float drawTimer = 0f;
    private ComboSystem comboSystem; // 콤보 시스템 참조

    public TMP_Text deckText;    
    public TMP_Text graveyardText;
  

    public int baseDraw=10;
    public float drawTime=1f;
    

    void Start()
    {
        player = FindFirstObjectByType<Player>();
        comboSystem = FindFirstObjectByType<ComboSystem>(); // 콤보 시스템 찾기
        RefreshEnemyRef();

        SetDeck();
        ShuffleDeck(deck);
        DrawCards(baseDraw);

    }

    void Update()
    {
        HandleInput();
        UpdateDrawTimer();
        RefreshEnemyRef();
    }
    void RefreshEnemyRef()
    {
        if (enemyController == null || !enemyController.gameObject.activeInHierarchy)
        {
            enemyController = FindFirstObjectByType<EnemyController>();
        }
    }

    void UpdateCountUI()
    {
        if (deckText != null)
            deckText.text = $"{deck.Count}";

        if (graveyardText != null)
            graveyardText.text = $"{graveyard.Count}";
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

    void ReshuffleGraveyard() // 묘지의 카드를 덱으로 다시 섞음
    {
        if (graveyard.Count == 0) return;
        deck.AddRange(graveyard);
        graveyard.Clear();
        ShuffleDeck(deck);
        Debug.Log("묘지의 카드를 덱으로 다시 섞음");
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

    void HandleInput() // 카드 키 입력 처리
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

        // 데미지 계산 (플레이어 공격력의 100%)
        if (enemyController != null && player != null)
        {
            player.PlayAttackEffect(); // 플레이어 공격 효과 재생
            enemyController.TakeDamage(player.attackDamage, type);
            Debug.Log($"{type} 카드 사용! 적에게 {player.attackDamage} 데미지.");
        }

        // 콤보 시스템에 카드 입력 전달
        if (comboSystem != null)
        {
            comboSystem.OnCardUsed(type);
        }

        // 묘지로 보내기 및 파괴  
        graveyard.Add(type);
        hand.RemoveAt(index);
        Destroy(cardObj);
        UpdateCountUI();
    }

    void UpdateDrawTimer() 
    {
        if (hand.Count < baseDraw)
        {
            drawTimer += Time.deltaTime;
            if (drawTimer >= drawTime)
            {
                DrawCards(1);
                drawTimer = 0f;
            }
        }
        else
        {
            drawTimer = 0f; // 이미 가득 차 있으면 타이머 리셋  
        }
    }

    public void DrawCards(int count)
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
