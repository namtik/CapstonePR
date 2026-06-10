using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using static SkillDataParser;
using System.Linq;
using System;
using Object = UnityEngine.Object;
using TMPro;
using Random = UnityEngine.Random;

// 콤보 스킬 시스템 관리 클래스
public class ComboSystem : MonoBehaviour
{
    [Header("콤보 슬롯 설정")]
    public Transform comboSlotParent; // 콤보 슬롯 부모
    public GameObject cardPrefab; // 카드 프리팹
    public GameObject eleslotPrefab; // 속성 슬롯 프리팹
    public Sprite[] cardSprites; // 카드 스프라이트 배열

    private string[] cardTypes = { "Q", "W", "E", "R" }; // 카드 타입 목록

    [Header("스킬 목록 UI 설정")]
    public Transform skillListParent; // 스킬 목록 부모 패널
    public SkillListItemUI skillListItemPrefab; // 스킬 목록 항목 프리팹

    private List<string> comboInput = new List<string>(); // 현재 콤보 입력
    private List<GameObject> comboSlotCards = new List<GameObject>(); // 콤보 슬롯에 표시된 카드
    private List<GameObject> emptySlots = new List<GameObject>(); // 빈 슬롯 목록

    [Header("스킬 발동 알림 UI")]
    [SerializeField] private TMP_Text skillActivationText; // 스킬 발동 알림 텍스트
    [SerializeField] private string skillActivationTextObjectName = "SkillActivationText"; // 알림 텍스트 오브젝트 이름

    [Header("다음 콤보 힌트 UI")]
    [SerializeField] private bool enableNextComboHints = true; // 다음 콤보 힌트 사용 여부
    [SerializeField] private string elementSlotRootName = "ElementSlotRoot"; // 속성 슬롯 루트 이름
    [SerializeField] private string slotObjectPrefix = "Slot_"; // 슬롯 오브젝트 접두사
    [SerializeField] private string nextHintObjectPrefix = "NextSkillHint_"; // 힌트 오브젝트 접두사
    [SerializeField] private TMP_FontAsset nextHintFont; // 힌트 폰트
    [SerializeField] private int nextHintFontSize = 22; // 힌트 폰트 크기
    [SerializeField] private FontStyles nextHintFontStyle = FontStyles.Bold; // 힌트 폰트 스타일
    [SerializeField] private Color nextHintColor = new Color(1f, 0.9f, 0.4f, 1f); // 힌트 색상
    [SerializeField] private Vector2 nextHintOffset = new Vector2(0f, 12f); // 힌트 위치 오프셋
    [SerializeField] private Vector2 nextHintSize = new Vector2(170f, 34f); // 힌트 크기
    [SerializeField] private bool nextHintAutoSize = false; // 힌트 자동 크기 조절 여부
    private float skillTextTimer = 0f; // 스킬 텍스트 표시 타이머
    private bool isShowingSkillText = false; // 스킬 텍스트 표시 중 여부
    private const float SKILL_TEXT_DISPLAY_TIME = 0.5f; // 스킬 텍스트 표시 시간

    public List<SkillData> learnedSkills = new List<SkillData>(); // 습득한 스킬 목록
    private Dictionary<string, SkillData> comboLookup = new Dictionary<string, SkillData>(); // 콤보→스킬 조회
    public int learnedSkillCount = 0; // 습득 스킬 수

    private Player player; // 플레이어 참조
    private EnemyController enemyController; // 적 참조
    private RoundManager roundManager; // 라운드 매니저 참조

    private readonly Dictionary<string, TMP_Text> nextHintTexts = new Dictionary<string, TMP_Text>(); // 키별 힌트 텍스트
    private static readonly string[] hintKeys = { "q", "w", "e", "r" }; // 힌트 키 목록

    public static ComboSystem Instance; // 싱글턴 인스턴스

    // 싱글턴 초기화
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

    // 참조 확보 및 UI 초기 생성
    void Start()
    {
        player = Player.Resolve(true);
        RefreshEnemyRef();

        CreateComboSlots();
        CreateSkillList();

        ResolveSkillActivationText();

        if (skillActivationText == null)
            Debug.LogWarning("[ComboSystem] skillActivationText를 찾지 못했습니다.");
        else
            HideSkillActivationText();

        UpdateNextComboHints();
    }

    // 초기 스킬 선택 UI 표시
    public void LearnStarterSkill()
    {
        if (SkillDataParser.Instance == null)
            return;

        if (SkillDataParser.Instance.SkillRewardUI == null)
            SkillDataParser.Instance.SkillRewardUI = FindFirstObjectByType<SkillRewardUI>(FindObjectsInactive.Include);

        if (SkillDataParser.Instance.SkillRewardUI == null)
        {
            Debug.LogWarning("[ComboSystem] SkillRewardUI를 찾을 수 없어 초기 스킬 선택을 건너뜁니다.");
            return;
        }

        SkillDataParser.Instance.SkillRewardUI.ShowStarterSkillSelection();
        Debug.Log("[ComboSystem] 초기 스킬 선택 UI 표시");
    }

    // 스킬 텍스트 타이머 및 힌트 바인딩 갱신
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

    // 적 참조 및 알림 텍스트 재확보
    void RefreshEnemyRef()
    {
        if (enemyController == null || !enemyController.gameObject.activeInHierarchy)
            enemyController = Object.FindFirstObjectByType<EnemyController>();

        if (skillActivationText == null)
            ResolveSkillActivationText();
    }

    // 모든 힌트 바인딩이 준비됐는지 확인
    bool HasAllHintBindings()
    {
        foreach (string key in hintKeys)
        {
            if (!nextHintTexts.TryGetValue(key, out TMP_Text hintText) || hintText == null)
                return false;
        }
        return true;
    }

    // 슬롯별 다음 콤보 힌트 텍스트 생성/바인딩
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

    // 다음 콤보 힌트 텍스트 초기화
    void ClearNextComboHints()
    {
        foreach (string key in hintKeys)
        {
            if (!nextHintTexts.TryGetValue(key, out TMP_Text hintText) || hintText == null) continue;
            hintText.text = "";
            hintText.gameObject.SetActive(false);
        }
    }

    // 현재 입력 기준 다음 콤보 후보 힌트 표시
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

    // 스킬 발동 알림 텍스트 오브젝트 탐색
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

    // 스킬 발동 알림 텍스트 숨김
    void HideSkillActivationText()
    {
        if (skillActivationText == null) return;
        skillActivationText.text = "";
        skillActivationText.gameObject.SetActive(false);
    }

    // 스킬 습득 및 조회 등록, UI 갱신
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

    // 스킬 목록 UI 재빌드
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

    // 콤보 슬롯 UI 재구성
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

    // 습득 스킬 개수 반환
    public int LearnedSkillCount() => learnedSkills.Count;

    // 습득 스킬 ID 집합 반환
    public HashSet<int> GetLearnedSkillIds()
    {
        var ids = new HashSet<int>();
        foreach (var skill in learnedSkills) ids.Add(skill.id);
        return ids;
    }

    // 콤보 슬롯 부모 확보 및 빈 슬롯 생성
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

    // 빈 콤보 슬롯 확보(기존 자식 우선, 없으면 생성)
    void CreateEmptySlots()
    {
        if (comboSlotParent == null) return;

        emptySlots.Clear();

        if (comboSlotParent.childCount >= 3)
        {
            for (int i = 0; i < comboSlotParent.childCount; i++)
                emptySlots.Add(comboSlotParent.GetChild(i).gameObject);
            return;
        }

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

    // 스킬 목록 부모 패널 생성
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
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-150f, -150f);
            rect.sizeDelta = new Vector2(250f, 500f);

            VerticalLayoutGroup layout = listParentObj.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 15f;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            skillListParent = listParentObj.transform;
        }
    }

    // 스킬 목록 항목 1개 생성
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

    // 콤보 입력 및 슬롯 표시 초기화
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

    // 카드 입력을 콤보에 누적하고 스킬 발동 검사
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

    // 현재 콤보 입력에 맞춰 슬롯 카드 표시 갱신
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

    // 현재 콤보와 일치하는 스킬을 발동
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

    // 스킬 효과/피해/알림 적용 및 적 공격 횟수 감소
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

    // 콤보 문자열을 소문자 정규화
    string NormalizeCombo(string combo)
    {
        if (string.IsNullOrWhiteSpace(combo)) return string.Empty;
        return combo.Trim().ToLowerInvariant();
    }

    // 현재 콤보 입력 복사본 반환
    public List<string> GetComboInput()
    {
        return new List<string>(comboInput);
    }

    // 콤보 입력 순서를 무작위로 섞고 UI 갱신
    public void ShuffleComboInput()
    {
        if (comboInput.Count <= 1) return;

        for (int i = comboInput.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            string temp = comboInput[i];
            comboInput[i] = comboInput[j];
            comboInput[j] = temp;
        }
        UpdateComboSlotUI();
        UpdateNextComboHints();

        Debug.Log($"[ComboSystem] 콤보 셔플됨: {string.Join("-", comboInput)}");
    }

}

