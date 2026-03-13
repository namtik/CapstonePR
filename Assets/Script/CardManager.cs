using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class CardSystem : MonoBehaviour
{
    public GameObject cardPrefab; // ī�� ������  
    public Transform cardParent; // ī�尡 ������ ������Ʈ  
    public Sprite[] cardSprites;
    private string[] cardTypes = { "Q", "W", "E", "R" };

    private List<string> deck = new List<string>();
    private List<GameObject> hand = new List<GameObject>();
    private List<string> graveyard = new List<string>();

    private Player player;
    private EnemyController enemyController;
    private float drawTimer = 0f;
    private ComboSystem comboSystem; // �޺� �ý��� ����

    public TMP_Text deckText;    
    public TMP_Text graveyardText;
  

    public int baseDraw=10;
    public float drawTime=1f;


    void Start()
    {
        player = FindFirstObjectByType<Player>();
        comboSystem = FindFirstObjectByType<ComboSystem>(); // �޺� �ý��� ã��
        RefreshEnemyRef();

        SetDeck();
        ShuffleDeck(deck);
        DrawCards(baseDraw);

    }

    /// <summary>
    /// 새 스테이지 진입 시 덱/손패/묘지 초기화
    /// </summary>
    public void ResetDeck()
    {
        // 손패 오브젝트 제거
        foreach (var card in hand)
        {
            if (card != null) Destroy(card);
        }
        hand.Clear();

        // 덱, 묘지 초기화
        deck.Clear();
        graveyard.Clear();
        drawTimer = 0f;

        // 덱 재구성
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
            for (int i = 0; i < 5; i++) // �� ī�� Ÿ�Դ� 5�徿  
            {
                deck.Add(type);
            }
        }
        ReshuffleGraveyard();
    }

    void ReshuffleGraveyard() // ������ ī�带 ������ �ٽ� ����
    {
        if (graveyard.Count == 0) return;
        deck.AddRange(graveyard);
        graveyard.Clear();
        ShuffleDeck(deck);
        Debug.Log("������ ī�带 ������ �ٽ� ����");
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

    void HandleInput() // ī�� Ű �Է� ó��
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

            // �Է��� Ű�� ī���� Ÿ���� ��ġ�ϴ� ù ��° ī�带 ã��  
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

        // ������ ��� (�÷��̾� ���ݷ��� 100%)
        if (enemyController != null && player != null)
        {
            player.PlayAttackEffect(); // �÷��̾� ���� ȿ�� ���
            enemyController.TakeDamage(player.attackDamage, type);
            Debug.Log($"{type} ī�� ���! ������ {player.attackDamage} ������.");
        }

        // �޺� �ý��ۿ� ī�� �Է� ����
        if (comboSystem != null)
        {
            comboSystem.OnCardUsed(type);
        }

        // ������ ������ �� �ı�  
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
            drawTimer = 0f; // �̹� ���� �� ������ Ÿ�̸� ����  
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
