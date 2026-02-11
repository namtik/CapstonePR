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
    
    // 자동 리셋 타이머
    private float autoResetTimer = 0f;
    private bool isWaitingForReset = false;
    private const float AUTO_RESET_DELAY = 2f; // 2초 후 자동 리셋
    
    // 스킬 발동 후 지연 리셋
    private float delayedResetTimer = 0f;
    private bool isWaitingForDelayedReset = false;
    private const float SKILL_DISPLAY_DELAY = 0.5f; // 스킬 발동 후 0.5초 유지
    
    // 스킬 발동 알림 UI
    private Text skillActivationText;
    
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
        public float cooldown; // 쿨타임 (초)
        public float remainingCooldown; // 남은 쿨타임
    }
    
    // 스킬 아이콘 UI 구조체
    private class SkillIcon
    {
        public GameObject iconObject;
        public Image iconImage;
        public Image cooldownOverlay;
        public Text cooldownText;
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
        // 스킬 쿨타임 업데이트
        UpdateSkillCooldowns();
        
        // 자동 리셋 타이머 업데이트
        if (isWaitingForReset)
        {
            autoResetTimer -= Time.deltaTime;
            if (autoResetTimer <= 0f)
            {
                ResetCombo();
                isWaitingForReset = false;
            }
        }
        
        // 스킬 발동 후 지연 리셋 타이머
        if (isWaitingForDelayedReset)
        {
            delayedResetTimer -= Time.deltaTime;
            if (delayedResetTimer <= 0f)
            {
                ResetCombo();
                isWaitingForDelayedReset = false;
                if (skillActivationText != null)
                {
                    skillActivationText.text = "";
                }
            }
        }
    }
    
    // 스킬 초기화
    void InitializeSkills()
    {
        skills.Add("QQ", new SkillData { name = "스킬1", combo = "QQ", damageMultiplier = 2.0f, cooldown = 5f, remainingCooldown = 0f });
        skills.Add("QEW", new SkillData { name = "스킬2", combo = "QEW", damageMultiplier = 2.5f, cooldown = 5f, remainingCooldown = 0f });
        skills.Add("WEW", new SkillData { name = "스킬3", combo = "WEW", damageMultiplier = 2.5f, cooldown = 5f, remainingCooldown = 0f });
        skills.Add("QQW", new SkillData { name = "스킬4", combo = "QQW", damageMultiplier = 2.5f, cooldown = 5f, remainingCooldown = 0f });
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
        
        // 쿨타임 오버레이 (반투명 검은색)
        GameObject overlayObj = new GameObject("CooldownOverlay");
        overlayObj.transform.SetParent(iconObj.transform, false);
        RectTransform overlayRect = overlayObj.AddComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.sizeDelta = Vector2.zero;
        Image overlayImage = overlayObj.AddComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, 0.7f);
        overlayImage.fillMethod = Image.FillMethod.Vertical;
        overlayImage.fillOrigin = (int)Image.OriginVertical.Top;
        overlayImage.type = Image.Type.Filled;
        overlayImage.fillAmount = 0f;
        
        // 쿨타임 텍스트
        GameObject textObj = new GameObject("CooldownText");
        textObj.transform.SetParent(iconObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.sizeDelta = Vector2.zero;
        Text cooldownText = textObj.AddComponent<Text>();
        cooldownText.font = Font.CreateDynamicFontFromOSFont("Arial", 20);
        cooldownText.fontSize = 20;
        cooldownText.alignment = TextAnchor.MiddleCenter;
        cooldownText.color = Color.white;
        cooldownText.fontStyle = FontStyle.Bold;
        cooldownText.text = "";
        
        // Outline 추가
        Outline outline = textObj.AddComponent<Outline>();
        outline.effectColor = Color.black;
        outline.effectDistance = new Vector2(2, -2);
        
        // 툴팁 생성
        GameObject tooltip = CreateTooltip(iconObj.transform, skillData);
        
        // 마우스 이벤트 추가
        AddMouseEvents(iconObj, tooltip);
        
        // SkillIcon 정보 저장
        SkillIcon skillIcon = new SkillIcon
        {
            iconObject = iconObj,
            iconImage = iconImage,
            cooldownOverlay = overlayImage,
            cooldownText = cooldownText,
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
        // 기존 자동 리셋 대기 취소
        isWaitingForReset = false;
        autoResetTimer = 0f;
        
        // 지연 리셋 대기 중이었다면 취소하고 즉시 리셋
        if (isWaitingForDelayedReset)
        {
            isWaitingForDelayedReset = false;
            delayedResetTimer = 0f;
            comboInput.Clear();
            if (skillActivationText != null)
            {
                skillActivationText.text = "";
            }
        }
        
        // 콤보 입력에 추가
        comboInput.Add(cardType);
        
        // 콤보 슬롯 UI 업데이트
        UpdateComboSlotUI();
        
        // 3개 초과 시 즉시 리셋
        if (comboInput.Count > 3)
        {
            ResetCombo();
            return;
        }
        
        // 스킬 매칭 확인 (순차적으로 짧은 것부터)
        bool skillActivated = CheckAndActivateSkills();
        
        // 3개가 꽉 찼는데 스킬이 발동되지 않았으면 2초 후 자동 리셋
        if (comboInput.Count == 3 && !skillActivated)
        {
            autoResetTimer = AUTO_RESET_DELAY;
            isWaitingForReset = true;
            Debug.Log("스킬 미발동: 2초 후 자동 리셋");
        }
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
    bool CheckAndActivateSkills()
    {
        string currentCombo = string.Join("", comboInput);
        List<string> matchedSkills = new List<string>();
        
        // 현재 콤보와 정확히 일치하는 스킬만 찾기
        foreach (var skill in skills)
        {
            if (currentCombo == skill.Value.combo)
            {
                matchedSkills.Add(skill.Key);
            }
        }
        
        // 짧은 순서대로 정렬 (같은 길이면 먼저 등록된 순서)
        matchedSkills.Sort((a, b) => skills[a].combo.Length.CompareTo(skills[b].combo.Length));
        
        bool anySkillActivated = false;
        
        // 순차적으로 스킬 발동
        foreach (string skillKey in matchedSkills)
        {
            if (ActivateSkill(skillKey))
            {
                anySkillActivated = true;
            }
        }
        
        return anySkillActivated;
    }
    
    // 스킬 발동
    bool ActivateSkill(string skillKey)
    {
        if (!skills.ContainsKey(skillKey)) return false;
        
        SkillData skill = skills[skillKey];
        
        // 쿨타임 확인 (이미 쿨타임 중이면 재발동 방지)
        if (skill.remainingCooldown > 0f)
        {
            Debug.Log($"{skill.name} 쿨타임 중: {skill.remainingCooldown:F1}초 남음 (재발동 방지)");
            return false;
        }
        
        // 데미지 계산 및 적용
        if (enemy == null)
        {
            enemy = Object.FindFirstObjectByType<Enemy>();
            Debug.LogError("Enemy가 할당되지 않았습니다!");
            return false;
        }
        if (player == null)
        {
            player = Object.FindFirstObjectByType<Player>();
            Debug.LogError("player가 할당되지 않았습니다!");
            return false;
        }

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

        // 쿨타임 시작
        skill.remainingCooldown = skill.cooldown;
        
        // 스킬 발동 알림 표시
        if (skillActivationText != null)
        {
            skillActivationText.text = $"{skill.name} 발동!";
        }
        
        // 스킬이 3글자(최대)면 0.5초 후 리셋, 2글자 이하는 콤보 유지
        if (skill.combo.Length >= 3)
        {
            delayedResetTimer = SKILL_DISPLAY_DELAY;
            isWaitingForDelayedReset = true;
        }
        else
        {
            // 2글자 스킬도 텍스트는 0.5초 후 제거 (콤보는 유지)
            StartCoroutine(ClearSkillTextAfterDelay(SKILL_DISPLAY_DELAY));
        }
        
        return true;
    }
    
    // 콤보 리셋
    void ResetCombo()
    {
        comboInput.Clear();
        UpdateComboSlotUI();
        isWaitingForReset = false;
        autoResetTimer = 0f;
        isWaitingForDelayedReset = false;
        delayedResetTimer = 0f;
        
        // 스킬 발동 텍스트 초기화 (지연 리셋이 아닐 때만)
        if (skillActivationText != null && !isWaitingForDelayedReset)
        {
            skillActivationText.text = "";
        }
        
        Debug.Log("콤보 슬롯 리셋");
    }
    
    // 2글자 스킬 발동 후 텍스트만 제거 (콤보는 유지)
    System.Collections.IEnumerator ClearSkillTextAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (skillActivationText != null)
        {
            skillActivationText.text = "";
        }
    }
    
    // 스킬 쿨타임 업데이트
    void UpdateSkillCooldowns()
    {
        foreach (var skill in skills)
        {
            if (skill.Value.remainingCooldown > 0f)
            {
                skill.Value.remainingCooldown -= Time.deltaTime;
                
                if (skill.Value.remainingCooldown < 0f)
                {
                    skill.Value.remainingCooldown = 0f;
                }
            }
            
            // UI 업데이트
            UpdateSkillIconUI(skill.Key, skill.Value);
        }
    }
    
    // 스킬 아이콘 UI 업데이트
    void UpdateSkillIconUI(string skillKey, SkillData skill)
    {
        if (!skillIcons.ContainsKey(skillKey)) return;
        
        SkillIcon icon = skillIcons[skillKey];
        
        if (skill.remainingCooldown > 0f)
        {
            // 쿨타임 중
            float fillAmount = skill.remainingCooldown / skill.cooldown;
            icon.cooldownOverlay.fillAmount = fillAmount;
            icon.cooldownText.text = skill.remainingCooldown.ToString("F1");
        }
        else
        {
            // 사용 가능
            icon.cooldownOverlay.fillAmount = 0f;
            icon.cooldownText.text = "";
        }
    }
}
