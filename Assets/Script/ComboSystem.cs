using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using static SkillDataParser;
using System.Linq;
using System;
using Object = UnityEngine.Object;

// 콤보 스킬 시스템 관리 클래스
public class ComboSystem : MonoBehaviour
{
    [Header("콤보 슬롯 설정")]
    public Transform comboSlotParent; // 콤보 슬롯이 표시될 부모 오브젝트
    public GameObject cardPrefab; // 카드 프리팹 (콤보 슬롯에 표시용)
    public Sprite[] cardSprites; // Q, W, E, R 카드 스프라이트

    private string[] cardTypes = { "Q", "W", "E", "R" };

    [Header("스킬 아이콘 설정")]
    public Transform skillIconParent; // 스킬 아이콘이 표시될 부모 오브젝트

    // 콤보 입력 저장
    private List<string> comboInput = new List<string>(); // 현재 입력된 콤보 (최대 3개)
    private List<GameObject> comboSlotCards = new List<GameObject>(); // 콤보 슬롯에 표시된 카드 오브젝트
    private List<GameObject> emptySlots = new List<GameObject>(); // 빈 슬롯 오브젝트 (항상 표시)

    // 스킬 발동 알림 UI
    private Text skillActivationText;
    private float skillTextTimer = 0f;
    private bool isShowingSkillText = false;
    private const float SKILL_TEXT_DISPLAY_TIME = 0.5f;

    // 스킬 정의
    private List<SkillData> learnedSkills = new List<SkillData>();

    private Player player;
    private Enemy enemy;
    private CardSystem CM; // CardSystem을 CardManager로 쓰고 계시다면 이름 맞춰주세요

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
        player = Object.FindFirstObjectByType<Player>();
        enemy = Object.FindFirstObjectByType<Enemy>();
        CM = Object.FindFirstObjectByType<CardSystem>();

        // UI 생성
        CreateComboSlots();
        CreateSkillIcons();
    }

    void Update()
    {
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
    }

    public void LearnSkill(SkillData newSkill)
    {
        learnedSkills.Add(newSkill);
        Debug.Log($"스킬 습득: {newSkill.name} ({newSkill.combo})");

        // 스킬 아이콘 UI에 추가
        CreateSkillIcon(newSkill, learnedSkills.Count - 1);
    }

    // 콤보 슬롯 UI 생성 (화면 상단 중앙)
    void CreateComboSlots()
    {
        if (comboSlotParent == null)
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            GameObject slotParentObj = new GameObject("ComboSlotParent");
            slotParentObj.transform.SetParent(canvas.transform, false);

            RectTransform rect = slotParentObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -50f);
            rect.sizeDelta = new Vector2(400f, 120f);

            // HorizontalLayoutGroup 추가
            HorizontalLayoutGroup layout = slotParentObj.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 30f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            comboSlotParent = slotParentObj.transform;
        }

        // 빈 슬롯 3개 생성 (항상 표시)
        CreateEmptySlots();
        // 스킬 발동 알림 텍스트 생성 (콤보 슬롯 아래)
        CreateSkillActivationText();
    }

    // 빈 콤보 슬롯 3개 생성
    void CreateEmptySlots()
    {
        for (int i = 0; i < 3; i++)
        {
            GameObject emptySlot = new GameObject($"EmptySlot_{i}");
            emptySlot.transform.SetParent(comboSlotParent, false);

            RectTransform rect = emptySlot.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(60f, 80f);

            // 빈 슬롯 배경 이미지 (회색 테두리)
            Image slotImage = emptySlot.AddComponent<Image>();
            slotImage.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);

            emptySlots.Add(emptySlot);
        }
    }

    // 스킬 발동 알림 텍스트 생성
    void CreateSkillActivationText()
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
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

        // Outline 추가
        Outline outline = textObj.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(3, -3);
    }

    // 스킬 아이콘 UI 생성 (화면 하단 중앙, 카드 위)
    void CreateSkillIcons()
    {
        if (skillIconParent == null)
        {
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
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
            layout.spacing = 20f; // 아이콘 간격
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            skillIconParent = iconParentObj.transform;
        }

    }

    // 개별 스킬 아이콘 생성
    void CreateSkillIcon(SkillData skillData, int index)
    {
        GameObject iconObj = new GameObject($"SkillIcon_{skillData.name}");
        iconObj.layer = LayerMask.NameToLayer("UI");
        iconObj.transform.SetParent(skillIconParent, false);

        RectTransform rect = iconObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(80f, 80f); // 아이콘 크기

        rect.localScale = Vector3.one;
        rect.localPosition = Vector3.zero;

        Image iconImage = iconObj.AddComponent<Image>();

        // 아이콘 이미지 할당 (CSV에서 로드된 이미지 우선)
        if (skillData.skillIcon != null)
            iconImage.sprite = skillData.skillIcon;
        else
            iconImage.color = Color.green; // 없으면 초록색

        // 툴팁 등 마우스 이벤트 추가
        GameObject tooltip = CreateTooltip(iconObj.transform, skillData);
        AddMouseEvents(iconObj, tooltip);
    }

    // 툴팁 생성
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

        // 제목
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

        // 설명
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
        descText.text = $"콤보: {skillData.combo}\n\n{skillData.description}";

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

    public void OnCardUsed(string cardType)
    {
        if (comboInput.Count >= 3)// 이미 3개가 꽉 차 있다면, 가장 오래된 것(0번)을 제거
        {
            comboInput.RemoveAt(0);
        }
        

        // 새 카드 추가
        comboInput.Add(cardType);
        Debug.Log($"현재 콤보: {string.Join("-", comboInput)}"); // 디버깅용
        
        CheckAndActivateSkills();// 스킬 사용 여부 체크

        //UI 갱신
        UpdateComboSlotUI();
    }

    void UpdateComboSlotUI()
    {
        // 기존에 표시된 카드 오브젝트들 모두 삭제
        foreach (var card in comboSlotCards)
        {
            if (card != null) Destroy(card);
        }
        comboSlotCards.Clear();

        // 현재 콤보 리스트(comboInput)에 있는 만큼 카드 생성
        for (int i = 0; i < comboInput.Count; i++)
        {
            
            if (i >= emptySlots.Count) break;

            string type = comboInput[i];

            // i번째 빈 슬롯의 자식으로 카드 생성
            GameObject newCard = Instantiate(cardPrefab, emptySlots[i].transform);

            // 카드 스크립트 설정
            Card cardScript = newCard.GetComponent<Card>();
            int spriteIndex = System.Array.IndexOf(cardTypes, type);
            if (spriteIndex >= 0 && spriteIndex < cardSprites.Length)
            {
                cardScript.SetType(type, cardSprites[spriteIndex]);
            }

            // UI 위치 초기화 (부모인 EmptySlot의 정중앙에 오도록)
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
        Debug.Log($"현재 콤보: {currentCombo}"); // 디버깅용
        // 배운 스킬 중 콤보 길이가 긴 순서대로 정렬하여 매칭 (QQQ가 QQ보다 먼저 검색됨)
        var sortedSkills = learnedSkills.OrderByDescending(s => s.combo.Length);
        Debug.Log(sortedSkills); // 디버깅용
        foreach (var skill in sortedSkills)
        {
            // 입력된 콤보의 스킬 콤보와 일치하는지 확인
            if (currentCombo==skill.combo)
            {
                ActivateSkill(skill);
                Debug.Log($"스킬 발동: {skill.name} (콤보: {skill.combo})"); // 디버깅용
                break; // 한 번에 하나의 스킬만 발동
            }
        }
    }

    void ActivateSkill(SkillData skill)
    {
        if (player == null) player = Object.FindFirstObjectByType<Player>();
        if (enemy == null) enemy = Object.FindFirstObjectByType<Enemy>();
        if (CM == null) CM = Object.FindFirstObjectByType<CardSystem>();
        
        if(skill.draw > 0 && CM != null) 
        {
            CM.DrawCards(skill.draw);
            Debug.Log($"{skill.draw}장 드로우!");
        }
            
        if (enemy != null && player != null)
        {
            float damage = player.attackDamage * skill.damage;
            enemy.TakeDamage(damage);
            Debug.Log($"{skill.name} 발동! 데미지: {damage}");
        }

        if (skillActivationText != null)
        {
            skillActivationText.text = $"{skill.name} 발동!";
            skillTextTimer = SKILL_TEXT_DISPLAY_TIME;
            isShowingSkillText = true;
        }
    }
}