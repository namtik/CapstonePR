using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using static SkillDataParser;
using System.Linq;
using System;
using Object = UnityEngine.Object;

// �޺� ��ų �ý��� ���� Ŭ����
public class ComboSystem : MonoBehaviour
{
    [Header("�޺� ���� ����")]
    public Transform comboSlotParent; // �޺� ������ ǥ�õ� �θ� ������Ʈ
    public GameObject cardPrefab; // ī�� ������ (�޺� ���Կ� ǥ�ÿ�)
    public Sprite[] cardSprites; // Q, W, E, R ī�� ��������Ʈ

    private string[] cardTypes = { "Q", "W", "E", "R" };

    [Header("��ų ������ ����")]
    public Transform skillIconParent; // ��ų �������� ǥ�õ� �θ� ������Ʈ

    // �޺� �Է� ����
    private List<string> comboInput = new List<string>(); // ���� �Էµ� �޺� (�ִ� 3��)
    private List<GameObject> comboSlotCards = new List<GameObject>(); // �޺� ���Կ� ǥ�õ� ī�� ������Ʈ
    private List<GameObject> emptySlots = new List<GameObject>(); // �� ���� ������Ʈ (�׻� ǥ��)

    // ��ų �ߵ� �˸� UI
    private Text skillActivationText;
    private float skillTextTimer = 0f;
    private bool isShowingSkillText = false;
    private const float SKILL_TEXT_DISPLAY_TIME = 0.5f;

    // ��ų ����
    private List<SkillData> learnedSkills = new List<SkillData>();
    private Dictionary<string, SkillData> comboLookup = new Dictionary<string, SkillData>();
    public int learnedSkillCount = 0;

    private Player player;
    private EnemyController enemyController;
    private CardSystem CM;
    private Roundmanager roundManager;  

    public static ComboSystem Instance;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }
    void Start()
    {
        player = FindFirstObjectByType<Player>();
        CM = FindFirstObjectByType<CardSystem>();
        RefreshEnemyRef();

        // UI ����
        CreateComboSlots();
        CreateSkillIcons();
    }

    void Update()
    {
        // ��ų �ؽ�Ʈ ǥ�� Ÿ�̸�
        if (isShowingSkillText)
        {
            skillTextTimer -= Time.deltaTime;
            if (skillTextTimer <= 0f)
            {
                isShowingSkillText = false;
                if (skillActivationText != null)
                {
                    skillActivationText.text = "";
                }
            }
        }

        RefreshEnemyRef();
    }
    void RefreshEnemyRef()
    {
        if (enemyController == null || !enemyController.gameObject.activeInHierarchy)
            enemyController = Object.FindFirstObjectByType<EnemyController>();
    }

    public void LearnSkill(SkillData newSkill)
    {
        learnedSkills.Add(newSkill);
        Debug.Log($"��ų ����: {newSkill.name} ({newSkill.combo})");
        comboLookup[newSkill.combo] = newSkill;
        learnedSkillCount += 1;

        // ��ų ������ UI�� �߰�
        CreateSkillIcon(newSkill, learnedSkills.Count - 1);

    }

    public void RefreshSkillUI()
    {
        // skillIconParent�� ���ų� �ı������� �����
        if (skillIconParent == null || !skillIconParent.gameObject.activeInHierarchy)
        {
            CreateSkillIcons();
        }

        foreach (Transform child in skillIconParent)
            Destroy(child.gameObject);

        // learnedSkills �����ͷ� ������ �����
        for (int i = 0; i < learnedSkills.Count; i++)
            CreateSkillIcon(learnedSkills[i], i);

        Debug.Log($"[ComboSystem] ��ų UI �����: {learnedSkills.Count}��");
    }
    public void RefreshComboSlotUI()
    {
        if (comboSlotParent == null || !comboSlotParent.gameObject.activeInHierarchy)
        {
            emptySlots.Clear();
            comboSlotCards.Clear();
            CreateComboSlots();
        }
    }

    public int LearnedSkillCount()
    {
        return learnedSkills.Count;
    }

    // �޺� ���� UI ���� (ȭ�� ��� �߾�)
    void CreateComboSlots()
    {
        if (comboSlotParent == null)
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            GameObject slotParentObj = new GameObject("ComboSlotParent");
            slotParentObj.transform.SetParent(canvas.transform, false);

            RectTransform rect = slotParentObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -50f);
            rect.sizeDelta = new Vector2(400f, 120f);

            // HorizontalLayoutGroup �߰�
            HorizontalLayoutGroup layout = slotParentObj.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 30f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            comboSlotParent = slotParentObj.transform;
        }

        // �� ���� 3�� ���� (�׻� ǥ��)
        CreateEmptySlots();
        // ��ų �ߵ� �˸� �ؽ�Ʈ ���� (�޺� ���� �Ʒ�)
        CreateSkillActivationText();
    }

    // �� �޺� ���� 3�� ����
    void CreateEmptySlots()
    {
        for (int i = 0; i < 3; i++)
        {
            GameObject emptySlot = new GameObject($"EmptySlot_{i}");
            emptySlot.transform.SetParent(comboSlotParent, false);

            RectTransform rect = emptySlot.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(60f, 80f);

            // �� ���� ��� �̹��� (ȸ�� �׵θ�)
            Image slotImage = emptySlot.AddComponent<Image>();
            slotImage.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);

            emptySlots.Add(emptySlot);
        }
    }

    // ��ų �ߵ� �˸� �ؽ�Ʈ ����
    void CreateSkillActivationText()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;

        GameObject textObj = new GameObject("SkillActivationText");
        textObj.transform.SetParent(canvas.transform, false);

        RectTransform rect = textObj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -200f);
        rect.sizeDelta = new Vector2(500f, 60f);

        skillActivationText = textObj.AddComponent<Text>();
        skillActivationText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        skillActivationText.fontSize = 32;
        skillActivationText.alignment = TextAnchor.MiddleCenter;
        skillActivationText.color = new Color(1f, 0.8f, 0f, 1f);
        skillActivationText.fontStyle = FontStyle.Bold;
        skillActivationText.text = "";

        // Outline �߰�
        Outline outline = textObj.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(3, -3);
    }

    // ��ų ������ UI ���� (ȭ�� �ϴ� �߾�, ī�� ��)
    void CreateSkillIcons()
    {
        if (skillIconParent == null)
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            GameObject iconParentObj = new GameObject("SkillIconParent");
            iconParentObj.layer = LayerMask.NameToLayer("UI");
            iconParentObj.transform.SetParent(canvas.transform, false);

            RectTransform rect = iconParentObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 180f);
            rect.sizeDelta = new Vector2(500f, 100f);
            rect.localScale = Vector3.one;

            HorizontalLayoutGroup layout = iconParentObj.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 20f; // ������ ����
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            skillIconParent = iconParentObj.transform;
        }

    }

    // ���� ��ų ������ ����
    void CreateSkillIcon(SkillData skillData, int index)
    {
        GameObject iconObj = new GameObject($"SkillIcon_{skillData.name}");
        iconObj.layer = LayerMask.NameToLayer("UI");
        iconObj.transform.SetParent(skillIconParent, false);

        RectTransform rect = iconObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(80f, 80f); // ������ ũ��

        rect.localScale = Vector3.one;
        rect.localPosition = Vector3.zero;

        Image iconImage = iconObj.AddComponent<Image>();

        // ������ �̹��� �Ҵ� (CSV���� �ε�� �̹��� �켱)
        if (skillData.skillIcon != null)
            iconImage.sprite = skillData.skillIcon;
        else
            iconImage.color = Color.green; // ������ �ʷϻ�

        // ���� �� ���콺 �̺�Ʈ �߰�
        GameObject tooltip = CreateTooltip(iconObj.transform, skillData);
        AddMouseEvents(iconObj, tooltip);
    }

    // ���� ����
    GameObject CreateTooltip(Transform parent, SkillData skillData)
    {
        GameObject tooltipObj = new GameObject("Tooltip");
        tooltipObj.transform.SetParent(parent, false);

        RectTransform tooltipRect = tooltipObj.AddComponent<RectTransform>();
        tooltipRect.anchorMin = new Vector2(0.5f, 1f);
        tooltipRect.anchorMax = new Vector2(0.5f, 1f);
        tooltipRect.pivot = new Vector2(0.5f, 0f);
        tooltipRect.anchoredPosition = new Vector2(0f, 15f);
        tooltipRect.sizeDelta = new Vector2(200f, 150f);

        Image bgImage = tooltipObj.AddComponent<Image>();
        bgImage.color = new Color(0.9f, 0.7f, 0.3f, 1f);

        Outline bgOutline = tooltipObj.AddComponent<Outline>();
        bgOutline.effectColor = Color.black;
        bgOutline.effectDistance = new Vector2(2, -2);

        // ����
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(tooltipObj.transform, false);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.sizeDelta = new Vector2(-10f, 40f);
        titleRect.anchoredPosition = new Vector2(0f, -5f);

        Text titleText = titleObj.AddComponent<Text>();
        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleText.fontSize = 20;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = Color.black;
        titleText.fontStyle = FontStyle.Bold;
        titleText.text = skillData.name;

        // ����
        GameObject descObj = new GameObject("Description");
        descObj.transform.SetParent(tooltipObj.transform, false);
        RectTransform descRect = descObj.AddComponent<RectTransform>();
        descRect.anchorMin = Vector2.zero;
        descRect.anchorMax = Vector2.one;
        descRect.sizeDelta = new Vector2(-10f, -50f);
        descRect.anchoredPosition = new Vector2(0f, -10f);

        Text descText = descObj.AddComponent<Text>();
        descText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        descText.fontSize = 16;
        descText.alignment = TextAnchor.MiddleCenter;
        descText.color = Color.black;
        descText.text = $"�޺�: {skillData.combo}\n\n{skillData.description}";

        tooltipObj.SetActive(false);
        return tooltipObj;
    }

    void AddMouseEvents(GameObject iconObj, GameObject tooltip)
    {
        UnityEngine.EventSystems.EventTrigger trigger = iconObj.AddComponent<UnityEngine.EventSystems.EventTrigger>();

        var enterEntry = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
        enterEntry.callback.AddListener((data) => { tooltip.SetActive(true); });
        trigger.triggers.Add(enterEntry);

        var exitEntry = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit };
        exitEntry.callback.AddListener((data) => { tooltip.SetActive(false); });
        trigger.triggers.Add(exitEntry);
    }

    /// <summary>
    /// 새 스테이지 진입 시 콤보 입력과 슬롯 UI 초기화
    /// </summary>
    public void ResetComboInput()
    {
        comboInput.Clear();

        foreach (var card in comboSlotCards)
        {
            if (card != null) Destroy(card);
        }
        comboSlotCards.Clear();
    }

    public void OnCardUsed(string cardType)
    {
        if (comboInput.Count >= 3)// �̹� 3���� �� �� �ִٸ�, ���� ������ ��(0��)�� ����
        {
            comboInput.RemoveAt(0);
        }
        

        // �� ī�� �߰�
        comboInput.Add(cardType);
        Debug.Log($"���� �޺�: {string.Join("-", comboInput)}"); // ������

        if (comboInput.Count == 3)
            CheckAndActivateSkills();

        UpdateComboSlotUI();
    }

    void UpdateComboSlotUI()
    {
        // ������ ǥ�õ� ī�� ������Ʈ�� ��� ����
        foreach (var card in comboSlotCards)
        {
            if (card != null) Destroy(card);
        }
        comboSlotCards.Clear();

        // ���� �޺� ����Ʈ(comboInput)�� �ִ� ��ŭ ī�� ����
        for (int i = 0; i < comboInput.Count; i++)
        {
            
            if (i >= emptySlots.Count) break;

            string type = comboInput[i];

            // i��° �� ������ �ڽ����� ī�� ����
            GameObject newCard = Instantiate(cardPrefab, emptySlots[i].transform);

            // ī�� ��ũ��Ʈ ����
            Card cardScript = newCard.GetComponent<Card>();
            int spriteIndex = System.Array.IndexOf(cardTypes, type);
            if (spriteIndex >= 0 && spriteIndex < cardSprites.Length)
            {
                cardScript.SetType(type, cardSprites[spriteIndex]);
            }

            // UI ��ġ �ʱ�ȭ (�θ��� EmptySlot�� ���߾ӿ� ������)
            RectTransform rect = newCard.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.sizeDelta = Vector2.zero;
                rect.anchoredPosition = Vector2.zero;
            }
            comboSlotCards.Add(newCard);
        }
    }

    void CheckAndActivateSkills()
    {
        string currentCombo = string.Join("", comboInput);
        Debug.Log($"���� �޺�: {currentCombo}"); // ������
        // ��� ��ų �� �޺� ���̰� �� ������� �����Ͽ� ��Ī (QQQ�� QQ���� ���� �˻���)
        if (comboLookup.TryGetValue(currentCombo, out SkillData skill))
        {
            Debug.Log($"[��ų�ߵ�] {skill.name}");
            ActivateSkill(skill);
        }
        else
        {
            Debug.Log($"[�̹ߵ�] '{currentCombo}' ��ġ�ϴ� ��ų ����");
        }
    }

    void ActivateSkill(SkillData skill)
    {
        if(skill.draw > 0) 
        {
            CM.DrawCards(skill.draw);
            Debug.Log($"{skill.draw}�� ��ο�!");
        }
            
        if (player != null)
        {
            float damage = player.attackDamage * skill.damage;
            enemyController.TakeDamage(damage);
            Debug.Log($"{skill.name} �ߵ�! ������: {damage}");
        }

        if (skillActivationText != null)
        {
            skillActivationText.text = $"{skill.name} �ߵ�!";
            skillTextTimer = SKILL_TEXT_DISPLAY_TIME;
            isShowingSkillText = true;
        }
    }
}