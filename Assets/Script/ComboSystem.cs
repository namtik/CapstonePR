//using UnityEngine;
//using UnityEngine.UI;
//using System.Collections.Generic;
//using static SkillDataParser;
//using System.Linq;
//using System;
//using Object = UnityEngine.Object;
//using TMPro;

//// 콤보 스킬 시스템 관리 클래스
//public class ComboSystem : MonoBehaviour
//{
//    [Header("콤보 슬롯 설정")]
//    public Transform comboSlotParent; // 콤보 슬롯이 표시될 부모 오브젝트
//    public GameObject cardPrefab; // 카드 프리팹 (콤보 슬롯에 표시용)
//    public GameObject EleslotPrefab;
//    public Sprite[] cardSprites; // Q, W, E, R 카드 스프라이트

//    private string[] cardTypes = { "Q", "W", "E", "R" };

//    [Header("스킬 아이콘 설정")]
//    public Transform skillIconParent; // 스킬 아이콘이 표시될 부모 오브젝트
//    public SkillListItemUI skillListItemPrefab;

//    // 콤보 입력 저장
//    private List<string> comboInput = new List<string>(); // 현재 입력된 콤보 (최대 3개)
//    private List<GameObject> comboSlotCards = new List<GameObject>(); // 콤보 슬롯에 표시된 카드 오브젝트
//    private List<GameObject> emptySlots = new List<GameObject>(); // 빈 슬롯 오브젝트 (항상 표시)

//    [Header("스킬 발동 알림 UI")]
//    [SerializeField] private TMP_Text skillActivationText;
//    [SerializeField] private string skillActivationTextObjectName = "SkillActivationText";

//    [Header("다음 콤보 힌트 UI")]
//    [SerializeField] private bool enableNextComboHints = true;
//    [SerializeField] private string elementSlotRootName = "ElementSlotRoot";
//    [SerializeField] private string slotObjectPrefix = "Slot_";
//    [SerializeField] private string nextHintObjectPrefix = "NextSkillHint_";
//    [SerializeField] private TMP_FontAsset nextHintFont;
//    [SerializeField] private int nextHintFontSize = 22;
//    [SerializeField] private FontStyles nextHintFontStyle = FontStyles.Bold;
//    [SerializeField] private Color nextHintColor = new Color(1f, 0.9f, 0.4f, 1f);
//    [SerializeField] private Vector2 nextHintOffset = new Vector2(0f, 12f);
//    [SerializeField] private Vector2 nextHintSize = new Vector2(170f, 34f);
//    [SerializeField] private bool nextHintAutoSize = false;
//    private float skillTextTimer = 0f;
//    private bool isShowingSkillText = false;
//    private const float SKILL_TEXT_DISPLAY_TIME = 0.5f;

//    // 스킬 정의
//    public List<SkillData> learnedSkills = new List<SkillData>();
//    private Dictionary<string, SkillData> comboLookup = new Dictionary<string, SkillData>();
//    public int learnedSkillCount = 0;

//    private Player player;
//    private EnemyController enemyController;
//    // CardSystem 제거 — ElementSlotSystem으로 교체됨
//    private Roundmanager roundManager;  

//    private readonly Dictionary<string, TMP_Text> nextHintTexts = new Dictionary<string, TMP_Text>();
//    private static readonly string[] hintKeys = { "q", "w", "e", "r" };

//    public static ComboSystem Instance;

//    void Awake()
//    {
//        if (Instance == null)
//        {
//            Instance = this;
//            DontDestroyOnLoad(gameObject);
//        }
//        else
//        {
//            Destroy(gameObject);
//            return;
//        }
//    }
//    void Start()
//    {
//        player = FindFirstObjectByType<Player>();
//        // CM(CardSystem) → ElementSlotSystem으로 교체됨, 참조 불필요
//        RefreshEnemyRef();

//        // UI 생성
//        CreateComboSlots();
//        CreateSkillIcons();

//        ResolveSkillActivationText();

//        if (skillActivationText == null)
//            Debug.LogWarning("[ComboSystem] skillActivationText를 찾지 못했습니다. 인스펙터 연결 또는 이름 매칭을 확인하세요.");
//        else
//        {
//            HideSkillActivationText();
//        }

//        UpdateNextComboHints();
//    }

//    void Update()
//    {
//        // 스킬 텍스트 표시 타이머
//        if (isShowingSkillText)
//        {
//            skillTextTimer -= Time.deltaTime;
//            if (skillTextTimer <= 0f)
//            {
//                isShowingSkillText = false;
//                if (skillActivationText != null)
//                {
//                    HideSkillActivationText();
//                }
//            }
//        }

//        RefreshEnemyRef();

//        if (enableNextComboHints && !HasAllHintBindings())
//        {
//            UpdateNextComboHints();
//        }
//    }
//    void RefreshEnemyRef()
//    {
//        if (enemyController == null || !enemyController.gameObject.activeInHierarchy)
//            enemyController = Object.FindFirstObjectByType<EnemyController>();

//        if (skillActivationText == null)
//            ResolveSkillActivationText();
//    }

//    bool HasAllHintBindings()
//    {
//        foreach (string key in hintKeys)
//        {
//            if (!nextHintTexts.TryGetValue(key, out TMP_Text hintText) || hintText == null)
//                return false;
//        }

//        return true;
//    }

//    void EnsureNextHintBindings()
//    {
//        if (!enableNextComboHints)
//            return;

//        GameObject slotRootObject = GameObject.Find(elementSlotRootName);
//        if (slotRootObject == null)
//            return;

//        Transform slotRoot = slotRootObject.transform;

//        foreach (string key in hintKeys)
//        {
//            string slotName = slotObjectPrefix + key.ToUpperInvariant();
//            Transform slotTransform = slotRoot.Find(slotName);
//            if (slotTransform == null)
//                continue;

//            string hintObjectName = nextHintObjectPrefix + key.ToUpperInvariant();
//            Transform hintTransform = slotTransform.Find(hintObjectName);
//            if (hintTransform == null)
//            {
//                GameObject hintObject = new GameObject(hintObjectName);
//                hintObject.transform.SetParent(slotTransform, false);
//                hintTransform = hintObject.transform;
//            }

//            RectTransform hintRect = hintTransform as RectTransform;
//            if (hintRect == null)
//                hintRect = hintTransform.gameObject.AddComponent<RectTransform>();

//            hintRect.anchorMin = new Vector2(0.5f, 1f);
//            hintRect.anchorMax = new Vector2(0.5f, 1f);
//            hintRect.pivot = new Vector2(0.5f, 0f);
//            hintRect.anchoredPosition = nextHintOffset;
//            hintRect.sizeDelta = nextHintSize;

//            TMP_Text hintText = hintTransform.GetComponent<TMP_Text>();
//            if (hintText == null)
//                hintText = hintTransform.gameObject.AddComponent<TextMeshProUGUI>();

//            hintText.alignment = TextAlignmentOptions.Center;
//            hintText.color = nextHintColor;
//            hintText.fontSize = nextHintFontSize;
//            hintText.fontStyle = nextHintFontStyle;
//            hintText.enableAutoSizing = nextHintAutoSize;
//            hintText.raycastTarget = false;
//            hintText.textWrappingMode = TextWrappingModes.NoWrap;
//            hintText.overflowMode = TextOverflowModes.Overflow;
//            if (nextHintFont != null)
//                hintText.font = nextHintFont;

//            nextHintTexts[key] = hintText;
//        }
//    }

//    void ClearNextComboHints()
//    {
//        foreach (string key in hintKeys)
//        {
//            if (!nextHintTexts.TryGetValue(key, out TMP_Text hintText) || hintText == null)
//                continue;

//            hintText.text = "";
//            hintText.gameObject.SetActive(false);
//        }
//    }

//    void UpdateNextComboHints()
//    {
//        if (!enableNextComboHints)
//            return;

//        EnsureNextHintBindings();
//        ClearNextComboHints();

//        if (comboInput.Count < 2)
//            return;

//        string prefix = comboInput[comboInput.Count - 2] + comboInput[comboInput.Count - 1];
//        Dictionary<string, SkillData> previewByNextKey = new Dictionary<string, SkillData>();

//        foreach (SkillData skill in learnedSkills)
//        {
//            if (skill == null)
//                continue;

//            string combo = NormalizeCombo(skill.combo);
//            if (combo.Length < 3)
//                continue;

//            if (!combo.StartsWith(prefix, StringComparison.Ordinal))
//                continue;

//            string nextKey = combo.Substring(2, 1);
//            if (!previewByNextKey.ContainsKey(nextKey))
//                previewByNextKey[nextKey] = skill;
//        }

//        foreach (KeyValuePair<string, SkillData> pair in previewByNextKey)
//        {
//            if (!nextHintTexts.TryGetValue(pair.Key, out TMP_Text hintText) || hintText == null)
//                continue;

//            hintText.text = pair.Value.name;
//            hintText.gameObject.SetActive(true);
//        }
//    }

//    void ResolveSkillActivationText()
//    {
//        if (skillActivationText != null)
//            return;

//        if (enemyController != null)
//        {
//            TMP_Text[] enemyTexts = enemyController.GetComponentsInChildren<TMP_Text>(true);
//            foreach (TMP_Text text in enemyTexts)
//            {
//                if (text != null && text.name == skillActivationTextObjectName)
//                {
//                    skillActivationText = text;
//                    HideSkillActivationText();
//                    return;
//                }
//            }
//        }

//        TMP_Text[] allTexts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
//        foreach (TMP_Text text in allTexts)
//        {
//            if (text != null && text.name == skillActivationTextObjectName)
//            {
//                skillActivationText = text;
//                HideSkillActivationText();
//                return;
//            }
//        }
//    }

//    void HideSkillActivationText()
//    {
//        if (skillActivationText == null)
//            return;

//        skillActivationText.text = "";
//        skillActivationText.gameObject.SetActive(false);
//    }

//    public void LearnSkill(SkillData newSkill)
//    {
//        learnedSkills.Add(newSkill);
//        Debug.Log($"스킬 습득: {newSkill.name} ({newSkill.combo})");
//        string comboKey = NormalizeCombo(newSkill.combo);
//        if (!string.IsNullOrEmpty(comboKey))
//            comboLookup[comboKey] = newSkill;
//        learnedSkillCount += 1;

//        // 스킬 아이콘 UI에 추가
//        CreateSkillIcon(newSkill, learnedSkills.Count - 1);

//        UpdateNextComboHints();

//    }

//    public void RefreshSkillUI()
//    {
//        // skillIconParent가 없거나 파괴됐으면 재생성
//        if (skillIconParent == null || !skillIconParent.gameObject.activeInHierarchy)
//        {
//            CreateSkillIcons();
//        }

//        foreach (Transform child in skillIconParent)
//            Destroy(child.gameObject);

//        // learnedSkills 데이터로 아이콘 재생성
//        for (int i = 0; i < learnedSkills.Count; i++)
//            CreateSkillIcon(learnedSkills[i], i);

//        Debug.Log($"[ComboSystem] 스킬 UI 재빌드: {learnedSkills.Count}개");
//    }
//    public void RefreshComboSlotUI()
//    {
//        if (comboSlotParent == null)
//        {
//            emptySlots.Clear();
//            comboSlotCards.Clear();
//            CreateComboSlots();
//            return;
//        }

//        if (!comboSlotParent.gameObject.activeInHierarchy)
//        {
//            comboSlotParent.gameObject.SetActive(true);

//            emptySlots.Clear();
//            comboSlotCards.Clear();
//            CreateEmptySlots();
//            UpdateComboSlotUI();
//        }
//    }

//    public int LearnedSkillCount()
//    {
//        return learnedSkills.Count;
//    }

//    public HashSet<int> GetLearnedSkillIds()
//    {
//        var ids = new HashSet<int>();
//        foreach (var skill in learnedSkills)
//            ids.Add(skill.id);
//        return ids;
//    }


//    // 콤보 슬롯 UI 생성 (화면 상단 중앙)
//    void CreateComboSlots()
//    {
//        if (comboSlotParent == null)
//        {
//            Canvas canvas = FindFirstObjectByType<Canvas>();
//            if (canvas == null) return;

//            Transform existingSlotParent = canvas.transform.Find("ComboSlotParent");
//            if (existingSlotParent != null)
//            {
//                comboSlotParent = existingSlotParent;
//            }
//            else
//            {
//                GameObject slotParentObj = new GameObject("ComboSlotParent");
//                slotParentObj.transform.SetParent(canvas.transform, false);

//                RectTransform rect = slotParentObj.AddComponent<RectTransform>();
//                rect.anchorMin = new Vector2(0.5f, 1f);
//                rect.anchorMax = new Vector2(0.5f, 1f);
//                rect.pivot = new Vector2(0.5f, 1f);
//                rect.anchoredPosition = new Vector2(0f, -50f);
//                rect.sizeDelta = new Vector2(200f, 120f);

//                // HorizontalLayoutGroup 추가
//                HorizontalLayoutGroup layout = slotParentObj.AddComponent<HorizontalLayoutGroup>();
//                layout.spacing = 30f;
//                layout.childAlignment = TextAnchor.MiddleCenter;
//                layout.childControlWidth = false;
//                layout.childControlHeight = false;

//                comboSlotParent = slotParentObj.transform;
//            }
//        }

//        // 빈 슬롯 3개 생성 (항상 표시)
//        CreateEmptySlots();
//    }

//    // 빈 콤보 슬롯 3개 생성
//    void CreateEmptySlots()
//    {
//        if (comboSlotParent == null) return;

//        foreach (Transform child in comboSlotParent)
//        {
//            if (child != null && child.name.StartsWith("EmptySlot_"))
//                Destroy(child.gameObject);
//        }
//        emptySlots.Clear();

//        for (int i = 0; i < 3; i++)
//        {
//            GameObject emptySlot = new GameObject($"EmptySlot_{i}");
//            emptySlot.transform.SetParent(comboSlotParent, false);

//            RectTransform rect = emptySlot.AddComponent<RectTransform>();
//            rect.sizeDelta = new Vector2(80f, 80f);

//            // 빈 슬롯 배경 이미지 (회색 테두리)
//            Image slotImage = emptySlot.AddComponent<Image>();
//            slotImage.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);

//            emptySlots.Add(emptySlot);
//        }
//    }

//    // 스킬 아이콘 UI 생성 (화면 하단 중앙, 카드 위)
//    void CreateSkillIcons()
//    {
//        if (skillIconParent == null)
//        {
//            Canvas canvas = FindFirstObjectByType<Canvas>();
//            if (canvas == null) return;

//            GameObject iconParentObj = new GameObject("SkillIconParent");
//            iconParentObj.layer = LayerMask.NameToLayer("UI");
//            iconParentObj.transform.SetParent(canvas.transform, false);

//            RectTransform rect = iconParentObj.AddComponent<RectTransform>();
//            rect.anchorMin = new Vector2(0.5f, 0f);
//            rect.anchorMax = new Vector2(0.5f, 0f);
//            rect.pivot = new Vector2(0.5f, 0f);
//            rect.anchoredPosition = new Vector2(0f, 180f);
//            rect.sizeDelta = new Vector2(500f, 100f);
//            rect.localScale = Vector3.one;

//            HorizontalLayoutGroup layout = iconParentObj.AddComponent<HorizontalLayoutGroup>();
//            layout.spacing = 20f; // 아이콘 간격
//            layout.childAlignment = TextAnchor.MiddleCenter;
//            layout.childControlWidth = false;
//            layout.childControlHeight = false;

//            skillIconParent = iconParentObj.transform;
//        }

//    }

//    // 개별 스킬 아이콘 생성
//    void CreateSkillIcon(SkillData skillData, int index)
//    {
//        GameObject iconObj = new GameObject($"SkillIcon_{skillData.name}");
//        iconObj.layer = LayerMask.NameToLayer("UI");
//        iconObj.transform.SetParent(skillIconParent, false);

//        RectTransform rect = iconObj.AddComponent<RectTransform>();
//        rect.sizeDelta = new Vector2(80f, 80f); // 아이콘 크기

//        rect.localScale = Vector3.one;
//        rect.localPosition = Vector3.zero;

//        Image iconImage = iconObj.AddComponent<Image>();

//        // 아이콘 이미지 할당 (CSV에서 로드된 이미지 우선)
//        if (skillData.skillIcon != null)
//            iconImage.sprite = skillData.skillIcon;
//        else
//            iconImage.color = Color.green; // 없으면 초록색

//        // 툴팁 등 마우스 이벤트 추가
//        GameObject tooltip = CreateTooltip(iconObj.transform, skillData);
//        AddMouseEvents(iconObj, tooltip);
//    }

//    // 툴팁 생성
//    GameObject CreateTooltip(Transform parent, SkillData skillData)
//    {
//        GameObject tooltipObj = new GameObject("Tooltip");
//        tooltipObj.transform.SetParent(parent, false);

//        RectTransform tooltipRect = tooltipObj.AddComponent<RectTransform>();
//        tooltipRect.anchorMin = new Vector2(0.5f, 1f);
//        tooltipRect.anchorMax = new Vector2(0.5f, 1f);
//        tooltipRect.pivot = new Vector2(0.5f, 0f);
//        tooltipRect.anchoredPosition = new Vector2(0f, 15f);
//        tooltipRect.sizeDelta = new Vector2(200f, 150f);

//        Image bgImage = tooltipObj.AddComponent<Image>();
//        bgImage.color = new Color(0.9f, 0.7f, 0.3f, 1f);

//        Outline bgOutline = tooltipObj.AddComponent<Outline>();
//        bgOutline.effectColor = Color.black;
//        bgOutline.effectDistance = new Vector2(2, -2);

//        // 제목
//        GameObject titleObj = new GameObject("Title");
//        titleObj.transform.SetParent(tooltipObj.transform, false);
//        RectTransform titleRect = titleObj.AddComponent<RectTransform>();
//        titleRect.anchorMin = new Vector2(0f, 1f);
//        titleRect.anchorMax = new Vector2(1f, 1f);
//        titleRect.sizeDelta = new Vector2(-10f, 40f);
//        titleRect.anchoredPosition = new Vector2(0f, -5f);

//        Text titleText = titleObj.AddComponent<Text>();
//        titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
//        titleText.fontSize = 20;
//        titleText.alignment = TextAnchor.MiddleCenter;
//        titleText.color = Color.black;
//        titleText.fontStyle = FontStyle.Bold;
//        titleText.text = skillData.name;

//        // 설명
//        GameObject descObj = new GameObject("Description");
//        descObj.transform.SetParent(tooltipObj.transform, false);
//        RectTransform descRect = descObj.AddComponent<RectTransform>();
//        descRect.anchorMin = Vector2.zero;
//        descRect.anchorMax = Vector2.one;
//        descRect.sizeDelta = new Vector2(-10f, -50f);
//        descRect.anchoredPosition = new Vector2(0f, -10f);

//        Text descText = descObj.AddComponent<Text>();
//        descText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
//        descText.fontSize = 16;
//        descText.alignment = TextAnchor.MiddleCenter;
//        descText.color = Color.black;
//        descText.text = $"콤보: {skillData.combo}\n\n{skillData.description}";

//        tooltipObj.SetActive(false);
//        return tooltipObj;
//    }

//    void AddMouseEvents(GameObject iconObj, GameObject tooltip)
//    {
//        UnityEngine.EventSystems.EventTrigger trigger = iconObj.AddComponent<UnityEngine.EventSystems.EventTrigger>();

//        var enterEntry = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
//        enterEntry.callback.AddListener((data) => { tooltip.SetActive(true); });
//        trigger.triggers.Add(enterEntry);

//        var exitEntry = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit };
//        exitEntry.callback.AddListener((data) => { tooltip.SetActive(false); });
//        trigger.triggers.Add(exitEntry);
//    }

//    // 새 스테이지 진입 시 콤보 입력과 슬롯 UI 초기화
//    public void ResetComboInput()
//    {
//        comboInput.Clear();

//        foreach (var card in comboSlotCards)
//        {
//            if (card != null) Destroy(card);
//        }
//        comboSlotCards.Clear();

//        ClearNextComboHints();
//    }

//    public void OnCardUsed(string cardType)
//    {
//        string normalizedCardType = NormalizeCombo(cardType);
//        if (string.IsNullOrEmpty(normalizedCardType))
//            return;

//        if (comboInput.Count >= 3)// 이미 3개가 꽉 차 있다면, 가장 오래된 것(0번)을 제거
//        {
//            comboInput.RemoveAt(0);
//        }


//        // 새 카드 추가
//        comboInput.Add(normalizedCardType);
//        Debug.Log($"현재 콤보: {string.Join("-", comboInput)}"); // 디버깅용

//        if (comboInput.Count == 3)
//            CheckAndActivateSkills();

//        UpdateComboSlotUI();
//        UpdateNextComboHints();
//    }

//    void UpdateComboSlotUI()
//    {
//        // 기존에 표시된 카드 오브젝트들 모두 삭제
//        foreach (var card in comboSlotCards)
//        {
//            if (card != null) Destroy(card);
//        }
//        comboSlotCards.Clear();

//        // 현재 콤보 리스트(comboInput)에 있는 만큼 카드 생성
//        for (int i = 0; i < comboInput.Count; i++)
//        {
//            if (i >= emptySlots.Count) break;

//            string type = comboInput[i];
//            string displayType = type.ToUpperInvariant();

//            // i번째 빈 슬롯의 자식으로 카드 생성
//            GameObject newCard = Instantiate(cardPrefab, emptySlots[i].transform);

//            // 카드 스크립트 설정
//            Card cardScript = newCard.GetComponent<Card>();
//            int spriteIndex = System.Array.IndexOf(cardTypes, displayType);
//            if (spriteIndex >= 0 && spriteIndex < cardSprites.Length)
//            {
//                cardScript.SetType(displayType, cardSprites[spriteIndex]);
//            }

//            // UI 위치 초기화 (부모인 EmptySlot의 정중앙에 오도록)
//            RectTransform rect = newCard.GetComponent<RectTransform>();
//            if (rect != null)
//            {
//                rect.anchorMin = Vector2.zero;
//                rect.anchorMax = Vector2.one;
//                rect.sizeDelta = Vector2.zero;
//                rect.anchoredPosition = Vector2.zero;
//            }
//            comboSlotCards.Add(newCard);
//        }
//    }

//    void CheckAndActivateSkills()
//    {
//        string currentCombo = NormalizeCombo(string.Join("", comboInput));
//        Debug.Log($"현재 콤보: {currentCombo}"); // 디버깅용
//        // 배운 스킬 중 콤보 길이가 긴 순서대로 정렬하여 매칭 (QQQ가 QQ보다 먼저 검색됨)
//        if (comboLookup.TryGetValue(currentCombo, out SkillData skill))
//        {
//            Debug.Log($"[스킬발동] {skill.name}");
//            ActivateSkill(skill);
//        }
//        else
//        {
//            Debug.Log($"[미발동] '{currentCombo}' 일치하는 스킬 없음");
//        }
//    }

//    void ActivateSkill(SkillData skill)
//    {
//        // draw 기능은 ElementSlotSystem 슬롯 재충전으로 대체됨 (skill.draw 미사용)

//        if (enemyController != null && player != null)
//        {
//            float damage = player.attackDamage * skill.damage;
//            enemyController.TakeDamage(damage);
//            Debug.Log($"{skill.name} 발동! 데미지: {damage}");
//        }

//        if (EffectManager.Instance != null)
//        {
//            // 스킬 데이터에 있는 상태이상 종류와 수치를 넘겨줍니다.
//            // (caster는 플레이어, target은 적)
//            EffectManager.Instance.ApplySkillEffect(skill.statusType, skill.statusAmount, player, enemyController);
//        }
//        else
//        {
//            Debug.LogError("EffectManager.Instance가 존재하지 않습니다! 씬에 EffectManager가 있는지 확인하세요.");
//        }
//        if (skillActivationText == null)
//            ResolveSkillActivationText();

//        if (skillActivationText != null)
//        {
//            skillActivationText.gameObject.SetActive(true);

//            skillActivationText.text = $"{skill.name} 발동!";
//            skillTextTimer = SKILL_TEXT_DISPLAY_TIME;
//            isShowingSkillText = true;
//        }

//        if (enemyController != null)
//        {
//            EnemyStat enemyStat = enemyController.GetComponent<EnemyStat>();
//            if (enemyStat != null)
//                enemyStat.ReduceAttackCount(1);
//        }


//    }

//    string NormalizeCombo(string combo)
//    {
//        if (string.IsNullOrWhiteSpace(combo))
//            return string.Empty;

//        return combo.Trim().ToLowerInvariant();
//    }
//}

using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using static SkillDataParser;
using System.Linq;
using System;
using Object = UnityEngine.Object;
using TMPro;

// 콤보 스킬 시스템 관리 클래스
public class ComboSystem : MonoBehaviour
{
    [Header("콤보 슬롯 설정")]
    public Transform comboSlotParent;
    public GameObject cardPrefab;
    public GameObject EleslotPrefab;
    public Sprite[] cardSprites;

    private string[] cardTypes = { "Q", "W", "E", "R" };

    [Header("스킬 목록 UI 설정")]
    public Transform skillListParent; // 스킬 목록이 표시될 부모 패널 (세로 정렬)
    public SkillListItemUI skillListItemPrefab; // 아이콘+이름+커맨드가 있는 프리팹

    private List<string> comboInput = new List<string>();
    private List<GameObject> comboSlotCards = new List<GameObject>();
    private List<GameObject> emptySlots = new List<GameObject>();

    [Header("스킬 발동 알림 UI")]
    [SerializeField] private TMP_Text skillActivationText;
    [SerializeField] private string skillActivationTextObjectName = "SkillActivationText";

    [Header("다음 콤보 힌트 UI")]
    [SerializeField] private bool enableNextComboHints = true;
    [SerializeField] private string elementSlotRootName = "ElementSlotRoot";
    [SerializeField] private string slotObjectPrefix = "Slot_";
    [SerializeField] private string nextHintObjectPrefix = "NextSkillHint_";
    [SerializeField] private TMP_FontAsset nextHintFont;
    [SerializeField] private int nextHintFontSize = 22;
    [SerializeField] private FontStyles nextHintFontStyle = FontStyles.Bold;
    [SerializeField] private Color nextHintColor = new Color(1f, 0.9f, 0.4f, 1f);
    [SerializeField] private Vector2 nextHintOffset = new Vector2(0f, 12f);
    [SerializeField] private Vector2 nextHintSize = new Vector2(170f, 34f);
    [SerializeField] private bool nextHintAutoSize = false;
    private float skillTextTimer = 0f;
    private bool isShowingSkillText = false;
    private const float SKILL_TEXT_DISPLAY_TIME = 0.5f;

    public List<SkillData> learnedSkills = new List<SkillData>();
    private Dictionary<string, SkillData> comboLookup = new Dictionary<string, SkillData>();
    public int learnedSkillCount = 0;

    private Player player;
    private EnemyController enemyController;
    private Roundmanager roundManager;

    private readonly Dictionary<string, TMP_Text> nextHintTexts = new Dictionary<string, TMP_Text>();
    private static readonly string[] hintKeys = { "q", "w", "e", "r" };

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
        RefreshEnemyRef();

        // UI 생성
        CreateComboSlots();
        CreateSkillList();

        ResolveSkillActivationText();

        if (skillActivationText == null)
            Debug.LogWarning("[ComboSystem] skillActivationText를 찾지 못했습니다.");
        else
            HideSkillActivationText();

        // 스킬이 하나도 없으면 랜덤 초기 스킬 1개 지급
        if (learnedSkills.Count == 0)
            LearnStarterSkill();

        UpdateNextComboHints();
    }

    void LearnStarterSkill()
    {
        if (SkillDataParser.Instance == null || SkillDataParser.Instance.SkillRewardUI == null)
            return;

        SkillDataParser.Instance.SkillRewardUI.ShowStarterSkillSelection();
        Debug.Log("[ComboSystem] 초기 스킬 선택 UI 표시");
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
                    HideSkillActivationText();
                }
            }
        }

        RefreshEnemyRef();

        if (enableNextComboHints && !HasAllHintBindings())
        {
            UpdateNextComboHints();
        }
    }

    void RefreshEnemyRef()
    {
        if (enemyController == null || !enemyController.gameObject.activeInHierarchy)
            enemyController = Object.FindFirstObjectByType<EnemyController>();

        if (skillActivationText == null)
            ResolveSkillActivationText();
    }

    bool HasAllHintBindings()
    {
        foreach (string key in hintKeys)
        {
            if (!nextHintTexts.TryGetValue(key, out TMP_Text hintText) || hintText == null)
                return false;
        }
        return true;
    }

    void EnsureNextHintBindings()
    {
        if (!enableNextComboHints) return;
        GameObject slotRootObject = GameObject.Find(elementSlotRootName);
        if (slotRootObject == null) return;
        Transform slotRoot = slotRootObject.transform;

        foreach (string key in hintKeys)
        {
            string slotName = slotObjectPrefix + key.ToUpperInvariant();
            Transform slotTransform = slotRoot.Find(slotName);
            if (slotTransform == null) continue;

            string hintObjectName = nextHintObjectPrefix + key.ToUpperInvariant();
            Transform hintTransform = slotTransform.Find(hintObjectName);
            if (hintTransform == null)
            {
                GameObject hintObject = new GameObject(hintObjectName);
                hintObject.transform.SetParent(slotTransform, false);
                hintTransform = hintObject.transform;
            }

            RectTransform hintRect = hintTransform as RectTransform;
            if (hintRect == null) hintRect = hintTransform.gameObject.AddComponent<RectTransform>();

            hintRect.anchorMin = new Vector2(0.5f, 1f);
            hintRect.anchorMax = new Vector2(0.5f, 1f);
            hintRect.pivot = new Vector2(0.5f, 0f);
            hintRect.anchoredPosition = nextHintOffset;
            hintRect.sizeDelta = nextHintSize;

            TMP_Text hintText = hintTransform.GetComponent<TMP_Text>();
            if (hintText == null) hintText = hintTransform.gameObject.AddComponent<TextMeshProUGUI>();

            hintText.alignment = TextAlignmentOptions.Center;
            hintText.color = nextHintColor;
            hintText.fontSize = nextHintFontSize;
            hintText.fontStyle = nextHintFontStyle;
            hintText.enableAutoSizing = nextHintAutoSize;
            hintText.raycastTarget = false;
            hintText.textWrappingMode = TextWrappingModes.NoWrap;
            hintText.overflowMode = TextOverflowModes.Overflow;
            if (nextHintFont != null) hintText.font = nextHintFont;

            nextHintTexts[key] = hintText;
        }
    }

    void ClearNextComboHints()
    {
        foreach (string key in hintKeys)
        {
            if (!nextHintTexts.TryGetValue(key, out TMP_Text hintText) || hintText == null) continue;
            hintText.text = "";
            hintText.gameObject.SetActive(false);
        }
    }

    void UpdateNextComboHints()
    {
        if (!enableNextComboHints) return;
        EnsureNextHintBindings();
        ClearNextComboHints();

        if (comboInput.Count < 2) return;

        string prefix = comboInput[comboInput.Count - 2] + comboInput[comboInput.Count - 1];
        Dictionary<string, SkillData> previewByNextKey = new Dictionary<string, SkillData>();

        foreach (SkillData skill in learnedSkills)
        {
            if (skill == null) continue;
            string combo = NormalizeCombo(skill.combo);
            if (combo.Length < 3) continue;
            if (!combo.StartsWith(prefix, StringComparison.Ordinal)) continue;

            string nextKey = combo.Substring(2, 1);
            if (!previewByNextKey.ContainsKey(nextKey))
                previewByNextKey[nextKey] = skill;
        }

        foreach (KeyValuePair<string, SkillData> pair in previewByNextKey)
        {
            if (!nextHintTexts.TryGetValue(pair.Key, out TMP_Text hintText) || hintText == null) continue;
            hintText.text = pair.Value.name;
            hintText.gameObject.SetActive(true);
        }
    }

    void ResolveSkillActivationText()
    {
        if (skillActivationText != null) return;
        if (enemyController != null)
        {
            TMP_Text[] enemyTexts = enemyController.GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text text in enemyTexts)
            {
                if (text != null && text.name == skillActivationTextObjectName)
                {
                    skillActivationText = text;
                    HideSkillActivationText();
                    return;
                }
            }
        }
        TMP_Text[] allTexts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (TMP_Text text in allTexts)
        {
            if (text != null && text.name == skillActivationTextObjectName)
            {
                skillActivationText = text;
                HideSkillActivationText();
                return;
            }
        }
    }

    void HideSkillActivationText()
    {
        if (skillActivationText == null) return;
        skillActivationText.text = "";
        skillActivationText.gameObject.SetActive(false);
    }

    public void LearnSkill(SkillData newSkill)
    {
        learnedSkills.Add(newSkill);
        Debug.Log($"스킬 습득: {newSkill.name} ({newSkill.combo})");
        string comboKey = NormalizeCombo(newSkill.combo);
        if (!string.IsNullOrEmpty(comboKey))
            comboLookup[comboKey] = newSkill;
        learnedSkillCount += 1;

        CreateSkillListItem(newSkill);

        UpdateNextComboHints();
    }

    public void RefreshSkillUI()
    {
        if (skillListParent == null || !skillListParent.gameObject.activeInHierarchy)
        {
            CreateSkillList();
        }

        foreach (Transform child in skillListParent)
            Destroy(child.gameObject);

        for (int i = 0; i < learnedSkills.Count; i++)
            CreateSkillListItem(learnedSkills[i]);

        Debug.Log($"[ComboSystem] 스킬 UI 재빌드: {learnedSkills.Count}개");
    }

    public void RefreshComboSlotUI()
    {
        if (comboSlotParent == null)
        {
            emptySlots.Clear();
            comboSlotCards.Clear();
            CreateComboSlots();
            return;
        }

        if (!comboSlotParent.gameObject.activeInHierarchy)
        {
            comboSlotParent.gameObject.SetActive(true);
            emptySlots.Clear();
            comboSlotCards.Clear();
            CreateEmptySlots();
            UpdateComboSlotUI();
        }
    }

    public int LearnedSkillCount() => learnedSkills.Count;

    public HashSet<int> GetLearnedSkillIds()
    {
        var ids = new HashSet<int>();
        foreach (var skill in learnedSkills) ids.Add(skill.id);
        return ids;
    }

    void CreateComboSlots()
    {
        if (comboSlotParent == null)
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            Transform existingSlotParent = canvas.transform.Find("ComboSlotParent");
            if (existingSlotParent != null)
            {
                comboSlotParent = existingSlotParent;
            }
            else
            {
                GameObject slotParentObj = new GameObject("ComboSlotParent");
                slotParentObj.transform.SetParent(canvas.transform, false);

                RectTransform rect = slotParentObj.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -50f);
                rect.sizeDelta = new Vector2(200f, 120f);

                HorizontalLayoutGroup layout = slotParentObj.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 30f;
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.childControlWidth = false;
                layout.childControlHeight = false;

                comboSlotParent = slotParentObj.transform;
            }
        }
        CreateEmptySlots();
    }

    void CreateEmptySlots()
    {
        if (comboSlotParent == null) return;

        foreach (Transform child in comboSlotParent)
        {
            if (child != null && child.name.StartsWith("EmptySlot_"))
                Destroy(child.gameObject);
        }
        emptySlots.Clear();

        for (int i = 0; i < 3; i++)
        {
            GameObject emptySlot = new GameObject($"EmptySlot_{i}");
            emptySlot.transform.SetParent(comboSlotParent, false);

            RectTransform rect = emptySlot.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(80f, 80f);

            Image slotImage = emptySlot.AddComponent<Image>();
            slotImage.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);

            emptySlots.Add(emptySlot);
        }
    }

    void CreateSkillList()
    {
        if (skillListParent == null)
        {
            Canvas canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            GameObject listParentObj = new GameObject("SkillListParent");
            listParentObj.layer = LayerMask.NameToLayer("UI");
            listParentObj.transform.SetParent(canvas.transform, false);

            RectTransform rect = listParentObj.AddComponent<RectTransform>();
            // 화면 왼쪽(Left)에 배치
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f); // 기준점을 우측 상단으로
            rect.anchoredPosition = new Vector2(-150f, -150f); // 오른쪽 끝에서 왼쪽으로 30만큼 띄움
            rect.sizeDelta = new Vector2(250f, 500f);

            // 세로 정렬 레이아웃 적용
            VerticalLayoutGroup layout = listParentObj.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 15f; // 항목 간격
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            skillListParent = listParentObj.transform;
        }
    }

    void CreateSkillListItem(SkillData skillData)
    {
        if (skillListItemPrefab == null)
        {
            Debug.LogError("[ComboSystem] skillListItemPrefab이 연결되지 않았습니다! 인스펙터에서 프리팹을 할당해주세요.");
            return;
        }

        SkillListItemUI newItem = Instantiate(skillListItemPrefab, skillListParent);
        newItem.Setup(skillData);
    }

    public void ResetComboInput()
    {
        comboInput.Clear();
        foreach (var card in comboSlotCards)
        {
            if (card != null) Destroy(card);
        }
        comboSlotCards.Clear();
        ClearNextComboHints();
    }

    public void OnCardUsed(string cardType)
    {
        string normalizedCardType = NormalizeCombo(cardType);
        if (string.IsNullOrEmpty(normalizedCardType)) return;

        if (comboInput.Count >= 3) comboInput.RemoveAt(0);

        comboInput.Add(normalizedCardType);
        Debug.Log($"현재 콤보: {string.Join("-", comboInput)}");

        if (comboInput.Count == 3) CheckAndActivateSkills();

        UpdateComboSlotUI();
        UpdateNextComboHints();
    }

    void UpdateComboSlotUI()
    {
        foreach (var card in comboSlotCards)
        {
            if (card != null) Destroy(card);
        }
        comboSlotCards.Clear();

        for (int i = 0; i < comboInput.Count; i++)
        {
            if (i >= emptySlots.Count) break;

            string type = comboInput[i];
            string displayType = type.ToUpperInvariant();

            GameObject newCard = Instantiate(cardPrefab, emptySlots[i].transform);
            Card cardScript = newCard.GetComponent<Card>();
            int spriteIndex = System.Array.IndexOf(cardTypes, displayType);
            if (spriteIndex >= 0 && spriteIndex < cardSprites.Length)
            {
                cardScript.SetType(displayType, cardSprites[spriteIndex]);
            }

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
        string currentCombo = NormalizeCombo(string.Join("", comboInput));
        Debug.Log($"현재 콤보: {currentCombo}");
        if (comboLookup.TryGetValue(currentCombo, out SkillData skill))
        {
            Debug.Log($"[스킬발동] {skill.name}");
            ActivateSkill(skill);
        }
        else
        {
            Debug.Log($"[미발동] '{currentCombo}' 일치하는 스킬 없음");
        }
    }

    void ActivateSkill(SkillData skill)
    {
        if (enemyController != null && player != null)
        {
            float damage = player.attackDamage * skill.damage;
            enemyController.TakeDamage(damage);
            Debug.Log($"{skill.name} 발동! 데미지: {damage}");
        }

        if (EffectManager.Instance != null)
        {
            EffectManager.Instance.ApplySkillEffect(skill.statusType, skill.statusAmount, player, enemyController);
        }
        else
        {
            Debug.LogError("EffectManager.Instance가 존재하지 않습니다! 씬에 EffectManager가 있는지 확인하세요.");
        }

        if (skillActivationText == null) ResolveSkillActivationText();

        if (skillActivationText != null)
        {
            skillActivationText.gameObject.SetActive(true);
            skillActivationText.text = $"{skill.name} 발동!";
            skillTextTimer = SKILL_TEXT_DISPLAY_TIME;
            isShowingSkillText = true;
        }

        if (enemyController != null)
        {
            EnemyStat enemyStat = enemyController.GetComponent<EnemyStat>();
            if (enemyStat != null) enemyStat.ReduceAttackCount(1);
        }
    }

    string NormalizeCombo(string combo)
    {
        if (string.IsNullOrWhiteSpace(combo)) return string.Empty;
        return combo.Trim().ToLowerInvariant();
    }
}