using UnityEngine;
using UnityEngine.UI;

public class Player : MonoBehaviour
{
    [Header("능력치")]
    public float maxHp = 100f;
    public float currentHp;
    public float attackDamage = 10f;

    [Header("UI")]
    public Slider hpBar;
    public Slider cooldownBar; // 쿨타임 진행바
    public Text cooldownText; // 쿨타임 표시 텍스트
    public Text resultText; // 성공/실패 결과 텍스트

    [Header("방어/회피 설정")]
    public float defenseWindow = 0.5f; // 방어/회피 입력 유효 시간 (초)
    public float defenseActionCooldown = 3f; // 방어/회피 쿨타임 (초)
    
    private bool isDefending = false;
    private bool isDodging = false;
    private float inputTimer = 0f;
    private float cooldownTimer = 0f;
    private bool isOnCooldown = false;
    private float resultDisplayTimer = 0f;

    void Awake()
    {
        currentHp = maxHp;
        UpdateUI();
    }

    void Start()
    {
        // UI 텍스트 자동 생성 (Start에서 실행)
        CreateUITexts();
        UpdateCooldownUI();
        if (resultText != null) resultText.text = "";
    }

    void CreateUITexts()
    {
        // 이미 생성되었으면 스킵
        if (cooldownText != null && resultText != null && cooldownBar != null) return;

        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // 쿨타임 진행바 생성
        if (cooldownBar == null)
        {
            GameObject cooldownBarObj = new GameObject("CooldownBar");
            cooldownBarObj.transform.SetParent(canvas.transform, false);
            
            RectTransform barRect = cooldownBarObj.AddComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 1f);
            barRect.anchorMax = new Vector2(0f, 1f);
            barRect.pivot = new Vector2(0f, 1f);
            barRect.anchoredPosition = new Vector2(20f, -80f);
            barRect.sizeDelta = new Vector2(200f, 20f);
            
            cooldownBar = cooldownBarObj.AddComponent<Slider>();
            cooldownBar.minValue = 0f;
            cooldownBar.maxValue = 1f;
            cooldownBar.value = 1f;
            
            // Background
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(cooldownBarObj.transform, false);
            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            Image bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            
            // Fill Area
            GameObject fillAreaObj = new GameObject("Fill Area");
            fillAreaObj.transform.SetParent(cooldownBarObj.transform, false);
            RectTransform fillAreaRect = fillAreaObj.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.sizeDelta = Vector2.zero;
            
            // Fill
            GameObject fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(fillAreaObj.transform, false);
            RectTransform fillRect = fillObj.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;
            Image fillImage = fillObj.AddComponent<Image>();
            fillImage.color = new Color(0.3f, 1f, 0.3f, 1f); // 초록색
            
            cooldownBar.fillRect = fillRect;
            cooldownBar.targetGraphic = fillImage;
        }

        // 쿨타임 텍스트 생성
        if (cooldownText == null)
        {
            GameObject cooldownObj = new GameObject("CooldownText");
            cooldownObj.transform.SetParent(canvas.transform, false);
            
            RectTransform rectTransform = cooldownObj.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = new Vector2(20f, -110f);
            rectTransform.sizeDelta = new Vector2(300f, 40f);
            
            cooldownText = cooldownObj.AddComponent<Text>();
            cooldownText.font = Font.CreateDynamicFontFromOSFont("Arial", 24);
            cooldownText.fontSize = 24;
            cooldownText.alignment = TextAnchor.MiddleLeft;
            cooldownText.color = Color.white;
            
            // Outline 추가 (가독성)
            Outline outline = cooldownObj.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2, -2);
        }

        // 결과 텍스트 생성
        if (resultText == null)
        {
            GameObject resultObj = new GameObject("ResultText");
            resultObj.transform.SetParent(canvas.transform, false);
            
            RectTransform rectTransform = resultObj.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0f, 0.5f);
            rectTransform.anchorMax = new Vector2(0f, 0.5f);
            rectTransform.pivot = new Vector2(0f, 0.5f);
            rectTransform.anchoredPosition = new Vector2(50f, 100f);
            rectTransform.sizeDelta = new Vector2(300f, 60f);
            
            resultText = resultObj.AddComponent<Text>();
            resultText.font = Font.CreateDynamicFontFromOSFont("Arial", 36);
            resultText.fontSize = 36;
            resultText.alignment = TextAnchor.MiddleLeft;
            resultText.fontStyle = FontStyle.Bold;
            
            // Outline 추가 (가독성)
            Outline outline = resultObj.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(3, -3);
        }
    }

    void Update()
    {
        HandleDefenseInput();
        
        // 방어/회피 상태 타이머 감소
        if (inputTimer > 0)
        {
            inputTimer -= Time.deltaTime;
        }
        else
        {
            isDefending = false;
            isDodging = false;
        }

        // 쿨타임 처리
        if (isOnCooldown)
        {
            cooldownTimer -= Time.deltaTime;
            UpdateCooldownUI();
            
            if (cooldownTimer <= 0f)
            {
                isOnCooldown = false;
                cooldownTimer = 0f;
                UpdateCooldownUI();
            }
        }

        // 결과 텍스트 표시 타이머
        if (resultDisplayTimer > 0f)
        {
            resultDisplayTimer -= Time.deltaTime;
            if (resultDisplayTimer <= 0f && resultText != null)
            {
                resultText.text = "";
            }
        }
    }

    void HandleDefenseInput()
    {
        if (isOnCooldown) return;

        // 왼쪽 방향키 (<): 회피
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            isDodging = true;
            isDefending = false;
            inputTimer = defenseWindow;
            StartCooldown();
        }
        // 오른쪽 방향키 (>): 방어
        else if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            isDefending = true;
            isDodging = false;
            inputTimer = defenseWindow;
            StartCooldown();
        }
    }

    public void OnProjectileHit(float damage)
    {
        if (isDodging)
        {
            ShowResult("회피 성공!", Color.green);
            isDodging = false;
            inputTimer = 0f;
            return;
        }
        
        if (isDefending)
        {
            float reducedDamage = damage * 0.5f;
            ShowResult("방어 성공!", Color.cyan);
            TakeDamage(reducedDamage);
            isDefending = false;
            inputTimer = 0f;
            return;
        }

        ShowResult("피격!", Color.red);
        TakeDamage(damage);
    }

    public void TakeDamage(float damage)
    {
        currentHp -= damage;
        UpdateUI();
        if (currentHp <= 0) Debug.Log("플레이어 사망");
    }

    void UpdateUI()
    {
        if (hpBar != null) hpBar.value = currentHp / maxHp;
    }

    void UpdateCooldownUI()
    {
        if (cooldownText == null) return;

        if (isOnCooldown)
        {
            cooldownText.text = $"쿨타임: {cooldownTimer:F1}초";
            cooldownText.color = Color.yellow;
            
            // 쿨타임 진행바 업데이트
            if (cooldownBar != null)
            {
                cooldownBar.value = 1f - (cooldownTimer / defenseActionCooldown);
            }
        }
        else
        {
            cooldownText.text = "방어/회피 준비";
            cooldownText.color = Color.white;
            
            // 쿨타임 진행바 가득 채우기
            if (cooldownBar != null)
            {
                cooldownBar.value = 1f;
            }
        }
    }

    void StartCooldown()
    {
        isOnCooldown = true;
        cooldownTimer = defenseActionCooldown;
        UpdateCooldownUI();
    }

    void ShowResult(string message, Color color)
    {
        if (resultText != null)
        {
            resultText.text = message;
            resultText.color = color;
            resultDisplayTimer = 2f; // 2초 동안 표시
        }
    }
}