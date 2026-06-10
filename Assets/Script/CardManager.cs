using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class CardSystem : MonoBehaviour
{
    public GameObject cardPrefab; // 카드 프리팹
    public Transform cardParent; // 카드가 생성될 부모 오브젝트
    public Sprite[] cardSprites; // 카드 타입별 스프라이트
    private string[] cardTypes = { "Q", "W", "E", "R" }; // 카드 타입 목록

    private List<string> deck = new List<string>(); // 덱
    private List<GameObject> hand = new List<GameObject>(); // 손패
    private List<string> graveyard = new List<string>(); // 묘지

    private Player player; // 플레이어 참조
    private EnemyController enemyController; // 적 참조
    private float drawTimer = 0f; // 드로우 타이머
    private ComboSystem comboSystem; // 콤보 시스템 참조

    public TMP_Text deckText; // 덱 장수 표시 텍스트
    public TMP_Text graveyardText; // 묘지 장수 표시 텍스트


    public int baseDraw=10; // 손패 최대 장수
    public float drawTime=1f; // 드로우 간격(초)

    // 슬롯 시스템이 있으면 레거시 카드 시스템 비활성화
    void Awake()
    {
        if (HasElementSlotSystem())
            ForceDisableForElementSystem();
    }

    // 슬롯 시스템이 있으면 레거시 카드 시스템 비활성화
    void OnEnable()
    {
        if (HasElementSlotSystem())
            ForceDisableForElementSystem();
    }

    // 초기 참조 확보 및 덱 세팅
    void Start()
    {
        if (HasElementSlotSystem())
        {
            ForceDisableForElementSystem();
            return;
        }

        player = Player.Resolve(true);
        comboSystem = FindFirstObjectByType<ComboSystem>();
        RefreshEnemyRef();

        SetDeck();
        ShuffleDeck(deck);
    }

    // 슬롯 시스템 사용 시 레거시 UI 정리 후 비활성화
    public void ForceDisableForElementSystem()
    {
        ClearHandObjects();
        DisableLegacyHandUI();
        enabled = false;
    }

    // 슬롯 시스템 존재 여부 확인
    bool HasElementSlotSystem()
    {
        return ElementSlotSystem.Instance != null || FindFirstObjectByType<ElementSlotSystem>() != null;
    }

    // 손패/덱/묘지 오브젝트 및 데이터 정리
    void ClearHandObjects()
    {
        foreach (var card in hand)
        {
            if (card != null) Destroy(card);
        }

        if (cardParent != null)
        {
            for (int i = cardParent.childCount - 1; i >= 0; i--)
            {
                Destroy(cardParent.GetChild(i).gameObject);
            }
        }

        hand.Clear();
        deck.Clear();
        graveyard.Clear();
    }

    // 레거시 손패 관련 UI 숨김
    void DisableLegacyHandUI()
    {
        if (cardParent != null)
            cardParent.gameObject.SetActive(false);

        if (deckText != null)
            deckText.gameObject.SetActive(false);

        if (graveyardText != null)
            graveyardText.gameObject.SetActive(false);
    }


    // 새 스테이지 진입 시 덱/손패/묘지 초기화
    public void ResetDeck()
    {
        foreach (var card in hand)
        {
            if (card != null) Destroy(card);
        }
        hand.Clear();

        deck.Clear();
        graveyard.Clear();
        drawTimer = 0f;

        SetDeck();
        ShuffleDeck(deck);
        DrawCards(baseDraw);
    }

    // 입력/드로우 타이머/적 참조 갱신
    void Update()
    {
        HandleInput();
        UpdateDrawTimer();
        RefreshEnemyRef();
    }
    // 적 참조 재확보
    void RefreshEnemyRef()
    {
        if (enemyController == null || !enemyController.gameObject.activeInHierarchy)
        {
            enemyController = FindFirstObjectByType<EnemyController>();
        }
    }

    // 덱/묘지 장수 UI 갱신
    void UpdateCountUI()
    {
        if (deckText != null)
            deckText.text = $"{deck.Count}";

        if (graveyardText != null)
            graveyardText.text = $"{graveyard.Count}";
    }

    // 타입별 카드를 덱에 채움
    void SetDeck()
    {
        foreach (string type in cardTypes)
        {
            for (int i = 0; i < 5; i++)
            {
                deck.Add(type);
            }
        }
        ReshuffleGraveyard();
    }

    // 묘지의 카드를 덱으로 다시 섞음
    void ReshuffleGraveyard()
    {
        if (graveyard.Count == 0) return;
        deck.AddRange(graveyard);
        graveyard.Clear();
        ShuffleDeck(deck);
        Debug.Log("묘지의 카드를 덱으로 다시 섞음");
    }

    // 덱을 무작위로 섞음
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

    // 카드 키 입력 처리
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

            // 입력 키와 타입이 일치하는 첫 카드를 사용
            if (cardScript.cardType == inputKey)
            {
                UseCard(i);
                break;
            }
        }
    }

    // 카드 사용 — 피해/콤보 전달 후 묘지로 이동
    void UseCard(int index)
    {
        GameObject cardObj = hand[index];
        string type = cardObj.GetComponent<Card>().cardType;

        if (enemyController != null && player != null)
        {
            enemyController.TakeDamage(player.attackDamage, type);
            Debug.Log($"{type} 카드 사용! 적에게 {player.attackDamage} 데미지.");
            player.PlayAttackEffect();
        }

        if (comboSystem != null)
        {
            comboSystem.OnCardUsed(type);
        }

        graveyard.Add(type);
        hand.RemoveAt(index);
        Destroy(cardObj);
        UpdateCountUI();
    }

    // 손패가 가득 차지 않으면 일정 간격으로 드로우
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
            drawTimer = 0f;
        }
    }

    // 지정한 수만큼 카드를 손패에 드로우
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
