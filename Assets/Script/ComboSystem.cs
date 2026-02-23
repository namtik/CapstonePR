using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;


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
    public Sprite[] skillSprites; // 스킬1~4의 아이콘 스프라이트 (Inspector에서 할당)
    
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
    private Dictionary<string, SkillData> skills = new Dictionary<string, SkillData>();
    private Dictionary<string, SkillIcon> skillIcons = new Dictionary<string, SkillIcon>();
    
    private Player player;
    private Enemy enemy;
    private CardSystem CM;

// 스킬 데이터 구조체
[System.Serializable]
    public class SkillData
    {
        public string name;
        public string combo; // 콤보 문자열 (예: "QQ", "QEW")
        public float damageMultiplier; // 데미지 배율 (200% = 2.0f)
    }
    
    // 스킬 아이콘 UI 구조체
    private class SkillIcon
    {
        public GameObject iconObject;
        public Image iconImage;
        public GameObject tooltip; // 툴팁 팝업
    }
    
    void Start()
    {
        player = Object.FindFirstObjectByType<Player>();
        enemy = Object.FindFirstObjectByType<Enemy>();
        
        // 스킬 등록
        InitializeSkills();
        
        // UI 생성
        CreateComboSlots();
        CreateSkillIcons();
    }
    
    void Update()
    {
        // 스킬 텍스트 표시 타이머
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
        
        if (enemy == null)
        {
            enemy = Object.FindFirstObjectByType<Enemy>();
            if (enemy == null)
                Debug.LogError("Enemy가 할당되지 않았습니다!");
        }
        if (player == null)
        {
            player = Object.FindFirstObjectByType<Player>();
            if (player == null)
                Debug.LogError("player가 할당되지 않았습니다!");
        }
    }
    
    // 스킬 초기화
    void InitializeSkills()
    {
        skills.Add("QQ", new SkillData { name = "스킬1", combo = "QQ", damageMultiplier = 2.0f });
        skills.Add("QEW", new SkillData { name = "스킬2", combo = "QEW", damageMultiplier = 2.5f });
        skills.Add("WEW", new SkillData { name = "스킬3", combo = "WEW", damageMultiplier = 2.5f });
        skills.Add("QQW", new SkillData { name = "스킬4", combo = "QQW", damageMultiplier = 2.5f });
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
            rect.anchoredPosition = new Vector2(0f, -20f);
            rect.sizeDelta = new Vector2(300f, 100f);
            
            // HorizontalLayoutGroup 추가 (카드를 가로로 정렬)
            HorizontalLayoutGroup layout = slotParentObj.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
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
        rect.anchoredPosition = new Vector2(0f, -170f);
        rect.sizeDelta = new Vector2(400f, 50f);
        
        skillActivationText = textObj.AddComponent<Text>();
        skillActivationText.font = Font.CreateDynamicFontFromOSFont("Arial", 28);
        skillActivationText.fontSize = 28;
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
            iconParentObj.transform.SetParent(canvas.transform, false);
            
            RectTransform rect = iconParentObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 150f); // 카드 위쪽
            rect.sizeDelta = new Vector2(400f, 80f);
            
            HorizontalLayoutGroup layout = iconParentObj.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 15f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            
            skillIconParent = iconParentObj.transform;
        }
        
        // 각 스킬 아이콘 생성
        int index = 0;
        foreach (var skill in skills)
        {
            CreateSkillIcon(skill.Key, skill.Value, index);
            index++;
        }
    }
    
    // 개별 스킬 아이콘 생성
    void CreateSkillIcon(string skillKey, SkillData skillData, int index)
    {
        GameObject iconObj = new GameObject($"SkillIcon_{skillData.name}");
        iconObj.transform.SetParent(skillIconParent, false);
        
        RectTransform rect = iconObj.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(70f, 70f);
        
        // 스킬 아이콘 이미지
        Image iconImage = iconObj.AddComponent<Image>();
        iconImage.color = Color.white;
        
        // 스프라이트 할당 (Inspector에서 할당된 경우)
        if (skillSprites != null && index < skillSprites.Length && skillSprites[index] != null)
        {
            iconImage.sprite = skillSprites[index];
        }
        else
        {
            // 임시: 스프라이트가 없으면 색상 박스로 표시
            Color[] colors = { Color.red, Color.blue, Color.green, Color.yellow };
            iconImage.color = colors[index % colors.Length];
        }
        
        // 툴팁 생성
        GameObject tooltip = CreateTooltip(iconObj.transform, skillData);
        
        // 마우스 이벤트 추가
        AddMouseEvents(iconObj, tooltip);
        
        // SkillIcon 정보 저장
        SkillIcon skillIcon = new SkillIcon
        {
            iconObject = iconObj,
            iconImage = iconImage,
            tooltip = tooltip
        };
        
        skillIcons[skillKey] = skillIcon;
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
        tooltipRect.anchoredPosition = new Vector2(0f, 10f);
        tooltipRect.sizeDelta = new Vector2(150f, 120f);
        
        // 배경
        Image bgImage = tooltipObj.AddComponent<Image>();
        bgImage.color = new Color(0.9f, 0.7f, 0.3f, 1f); // 노란색 배경
        
        // 테두리
        Outline bgOutline = tooltipObj.AddComponent<Outline>();
        bgOutline.effectColor = Color.black;
        bgOutline.effectDistance = new Vector2(2, -2);
        
        // 제목 텍스트 (스킬 이름)
        GameObject titleObj = new GameObject("Title");
        titleObj.transform.SetParent(tooltipObj.transform, false);
        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -5f);
        titleRect.sizeDelta = new Vector2(-10f, 30f);
        
        Text titleText = titleObj.AddComponent<Text>();
        titleText.font = Font.CreateDynamicFontFromOSFont("Arial", 18);
        titleText.fontSize = 18;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = Color.black;
        titleText.fontStyle = FontStyle.Bold;
        titleText.text = skillData.name;
        
        // 설명 텍스트 (이미지, 콤보 키)
        GameObject descObj = new GameObject("Description");
        descObj.transform.SetParent(tooltipObj.transform, false);
        RectTransform descRect = descObj.AddComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0f, 0f);
        descRect.anchorMax = new Vector2(1f, 1f);
        descRect.pivot = new Vector2(0.5f, 0.5f);
        descRect.anchoredPosition = new Vector2(0f, -10f);
        descRect.sizeDelta = new Vector2(-10f, -45f);
        
        Text descText = descObj.AddComponent<Text>();
        descText.font = Font.CreateDynamicFontFromOSFont("Arial", 14);
        descText.fontSize = 14;
        descText.alignment = TextAnchor.MiddleCenter;
        descText.color = Color.black;
        descText.text = $"이미지\n\n{skillData.combo}\n\n설명\n데미지: {skillData.damageMultiplier * 100}%";
        
        tooltipObj.SetActive(false);
        return tooltipObj;
    }
    
    // 마우스 이벤트 추가
    void AddMouseEvents(GameObject iconObj, GameObject tooltip)
    {
        UnityEngine.EventSystems.EventTrigger trigger = iconObj.AddComponent<UnityEngine.EventSystems.EventTrigger>();
        
        // 마우스 진입
        UnityEngine.EventSystems.EventTrigger.Entry enterEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
        enterEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter;
        enterEntry.callback.AddListener((data) => { tooltip.SetActive(true); });
        trigger.triggers.Add(enterEntry);
        
        // 마우스 나감
        UnityEngine.EventSystems.EventTrigger.Entry exitEntry = new UnityEngine.EventSystems.EventTrigger.Entry();
        exitEntry.eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit;
        exitEntry.callback.AddListener((data) => { tooltip.SetActive(false); });
        trigger.triggers.Add(exitEntry);
    }
    
    // 카드 입력 처리 (CardSystem에서 호출)
    public void OnCardUsed(string cardType)
    {
        // 슬라이딩 큐 방식: 3개가 꽉 차있으면 가장 왼쪽(오래된) 것을 제거
        if (comboInput.Count >= 3)
        {
            comboInput.RemoveAt(0); // 가장 오래된 카드(맨 왼쪽) 제거
        }
        
        // 콤보 입력에 새 카드 추가
        comboInput.Add(cardType);
        
        // 스킬 패턴 매칭 및 발동 확인
        CheckAndActivateSkills();
        
        // 3개가 꽉 찼으면 자동으로 왼쪽 1개 제거하여 슬라이딩
        // 예: [E][W][R] → [W][R][빈칸] (스킬 발동 여부와 무관)
        if (comboInput.Count >= 3)
        {
            comboInput.RemoveAt(0); // 가장 왼쪽 카드 제거
        }
        
        // 콤보 슬롯 UI 업데이트 (화면에 현재 콤보 상태 표시)
        UpdateComboSlotUI();
    }
    
    // 콤보 슬롯 UI 업데이트
    void UpdateComboSlotUI()
    {
        // 기존 카드 삭제
        foreach (var card in comboSlotCards)
        {
            Destroy(card);
        }
        comboSlotCards.Clear();
        
        // 현재 콤보 입력을 카드로 표시 (최대 3개)
        for (int i = 0; i < comboInput.Count && i < 3; i++)
        {
            string type = comboInput[i];
            
            // 카드 생성 (빈 슬롯의 자식으로 배치)
            GameObject newCard = Instantiate(cardPrefab, emptySlots[i].transform);
            Card cardScript = newCard.GetComponent<Card>();
            
            int spriteIndex = System.Array.IndexOf(cardTypes, type);
            if (spriteIndex >= 0 && spriteIndex < cardSprites.Length)
            {
                cardScript.SetType(type, cardSprites[spriteIndex]);
            }
            
            // 크기 및 위치 조정 (부모 슬롯을 꽉 채우도록)
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
    
    // 스킬 매칭 및 발동 확인
    void CheckAndActivateSkills()
    {
        // 현재 콤보 슬롯의 카드들을 하나의 문자열로 결합
        string currentCombo = string.Join("", comboInput);
        
        // 현재 콤보에 포함된 스킬 패턴 찾기 (긴 스킬부터 우선 확인)
        var sortedSkills = new List<KeyValuePair<string, SkillData>>(skills);
        sortedSkills.Sort((a, b) => b.Value.combo.Length.CompareTo(a.Value.combo.Length)); // 3글자 → 2글자 순
        
        foreach (var skill in sortedSkills)
        {
            // 현재 콤보 문자열에 스킬 패턴이 포함되어 있는지 확인

            if (currentCombo.Contains(skill.Value.combo))
            {
                ActivateSkill(skill.Key, skill.Value);
                break; // 첫 번째 매칭된 스킬만 발동 (우선순위가 가장 높은 스킬)
            }
        }
    }
    
    // 스킬 발동
    void ActivateSkill(string skillKey, SkillData skill)
    {
        // 데미지 계산 및 적용
        if (enemy != null && player != null)
        {
            float damage = player.attackDamage * skill.damageMultiplier;
            enemy.TakeDamage(damage);
            Debug.Log($"{skill.name} ({skill.combo}) 발동! 적에게 {damage} 데미지 ({skill.damageMultiplier * 100}%)");
        }
        else
        {
            // 연결 끊김 확인
            if (player == null) Debug.LogError("Player가 null입니다!");
            if (enemy == null) Debug.LogError("Enemy가 null입니다!");
        }
        
        // 스킬 발동 알림 표시 (화면 상단에 "{스킬명} 발동!" 텍스트)
        if (skillActivationText != null)
        {
            skillActivationText.text = $"{skill.name} 발동!";
            skillTextTimer = SKILL_TEXT_DISPLAY_TIME; // 0.5초간 표시
            isShowingSkillText = true;
        }
    }
}
