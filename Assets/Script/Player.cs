using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System;

public class Player : MonoBehaviour, IBattleUnit
{
    [Header("능력치")]
    public int maxHp = 100;
    public int currentHp;
    public float attackDamage = 10f;

    [Header("UI")]
    public HeartUI heartUI;
    public TMP_Text hpText;
    public Slider hpBar;
    public Slider cooldownBar; // 쿨타임 슬라이더
    public Text cooldownText; // 쿨타임 표시 텍스트
    public Text resultText; // 방어/회피 결과 텍스트

    [Header("상태이상 및 방어도")]
    public float guard = 0f;
    public Dictionary<string, int> statusEffects = new Dictionary<string, int>();

    //[Header("방어/회피 설정")]
    //public float defenseWindow = 0.5f; // 방어/회피 입력 유효 시간 (초)
    //public float defenseActionCooldown = 3f; // 방어/회피 쿨타임 (초)

    //private bool isDefending = false;
    //private bool isDodging = false;
    //private float inputTimer = 0f;
    //private float cooldownTimer = 0f;
    //private bool isOnCooldown = false;
    //private float resultDisplayTimer = 0f;

    public ParticleSystem attackParticle;

    void Awake()
    {
        ResolveUiReferences();
        currentHp = maxHp;

        statusEffects["launcher"] = 0;
        statusEffects["fortify"] = 0;
        statusEffects["charge"] = 0;
        UpdateUI();
    }

    void Start()
    {
        ResolveUiReferences();
        HideUnusedCooldownUI();
        if (resultText != null) resultText.text = "";
        statusEffects["launcher"] = 0;
        statusEffects["fortify"] = 0;
        statusEffects["charge"] = 0;
    }

    void ResolveUiReferences()
    {
        if (heartUI == null)
            heartUI = FindFirstObjectByType<HeartUI>();

        DisableLegacyHpSliders();

        if (hpText == null)
        {
            foreach (TMP_Text text in GetAllSceneHpTexts())
            {
                if (text != null && text.gameObject.activeInHierarchy)
                {
                    hpText = text;
                    break;
                }
            }

            if (hpText == null)
            {
                foreach (TMP_Text text in GetAllSceneHpTexts())
                {
                    hpText = text;
                    break;
                }
            }
        }

        if (hpBar == null)
        {
            foreach (Slider slider in GetAllScenePlayerHpBars())
            {
                if (slider != null && slider.gameObject.activeInHierarchy)
                {
                    hpBar = slider;
                    break;
                }
            }

            if (hpBar == null)
            {
                foreach (Slider slider in GetAllScenePlayerHpBars())
                {
                    hpBar = slider;
                    break;
                }
            }
        }
    }

    bool IsSceneObject(GameObject go)
    {
        if (go == null) return false;
        return go.scene.IsValid() && go.scene.isLoaded;
    }

    IEnumerable<TMP_Text> GetAllSceneHpTexts()
    {
        TMP_Text[] texts = Resources.FindObjectsOfTypeAll<TMP_Text>();
        foreach (TMP_Text text in texts)
        {
            if (text == null) continue;
            if (text.gameObject.name != "HpText") continue;
            if (!IsSceneObject(text.gameObject)) continue;
            yield return text;
        }
    }

    IEnumerable<Slider> GetAllScenePlayerHpBars()
    {
        Slider[] sliders = Resources.FindObjectsOfTypeAll<Slider>();
        foreach (Slider slider in sliders)
        {
            if (slider == null) continue;
            if (slider.gameObject.name != "PlayerHpBar") continue;
            if (!IsSceneObject(slider.gameObject)) continue;
            yield return slider;
        }
    }

    void DisableLegacyHpSliders()
    {
        Slider[] sliders = FindObjectsByType<Slider>(FindObjectsSortMode.None);
        foreach (Slider slider in sliders)
        {
            if (slider != null && slider.gameObject.name == "HpSlider")
                slider.gameObject.SetActive(false);
        }
    }

    void HideUnusedCooldownUI()
    {
        if (cooldownBar != null)
            cooldownBar.gameObject.SetActive(false);

        if (cooldownText != null)
            cooldownText.gameObject.SetActive(false);
    }

    RectTransform GetTopHpContainer()
    {
        if (hpText != null && hpText.transform.parent is RectTransform parentRect)
            return parentRect;

        if (heartUI != null && heartUI.transform is RectTransform heartRect)
            return heartRect;

        RectTransform[] rects = FindObjectsByType<RectTransform>(FindObjectsSortMode.None);
        foreach (RectTransform rect in rects)
        {
            if (rect != null && rect.gameObject.name == "PlayerHP")
                return rect;
        }

        return null;
    }

    Slider FindHpBarInContainer(RectTransform container)
    {
        if (container == null) return null;

        for (int i = 0; i < container.childCount; i++)
        {
            Transform child = container.GetChild(i);
            if (child == null || child.name != "PlayerHpBar") continue;

            Slider slider = child.GetComponent<Slider>();
            if (slider != null)
                return slider;
        }

        return null;
    }

    void EnsureTopHpBarPlacement()
    {
        RectTransform container = GetTopHpContainer();
        if (container == null) return;

        if (hpBar == null || hpBar.gameObject.name != "PlayerHpBar" || hpBar.transform.parent != container)
            hpBar = FindHpBarInContainer(container);

        if (hpBar != null)
        {
            hpBar.interactable = false;
            hpBar.transition = Selectable.Transition.None;
        }
    }

    void CreateUITexts()
    {
        // Runtime auto-creation is disabled.
        // Assign resultText in inspector if this UI is needed.
    }

    void Update()
    {
        //HandleDefenseInput();

        //// 방어/회피 입력 타이머 감소
        //if (inputTimer > 0)
        //{
        //    inputTimer -= Time.deltaTime;
        //}
        //else
        //{
        //    isDefending = false;
        //    isDodging = false;
        //}

        //// 쿨타임 처리
        //if (isOnCooldown)
        //{
        //    cooldownTimer -= Time.deltaTime;
        //    UpdateCooldownUI();

        //    if (cooldownTimer <= 0f)
        //    {
        //        isOnCooldown = false;
        //        cooldownTimer = 0f;
        //        UpdateCooldownUI();
        //    }
        //}

        //// 결과 텍스트 표시 타이머
        //if (resultDisplayTimer > 0f)
        //{
        //    resultDisplayTimer -= Time.deltaTime;
        //    if (resultDisplayTimer <= 0f && resultText != null)
        //    {
        //        resultText.text = "";
        //    }
        //}
    }

    //void HandleDefenseInput()
    //{
    //    if (isOnCooldown) return;

    //    // 왼쪽 방향키 (<): 회피
    //    if (Input.GetKeyDown(KeyCode.LeftArrow))
    //    {
    //        isDodging = true;
    //        isDefending = false;
    //        inputTimer = defenseWindow;
    //        StartCooldown();
    //    }
    //    // 오른쪽 방향키 (>): 방어
    //    else if (Input.GetKeyDown(KeyCode.RightArrow))
    //    {
    //        isDefending = true;
    //        isDodging = false;
    //        inputTimer = defenseWindow;
    //        StartCooldown();
    //    }
    //}

    public void OnProjectileHit(float damage)
    {
        //if (isDodging)
        //{
        //    ShowResult("회피 성공!", Color.green);
        //    isDodging = false;
        //    inputTimer = 0f;
        //    damage = 0f; // 회피 성공
        //    return;
        //}

        //if (isDefending)
        //{
        //    float reducedDamage = damage * 0.5f;
        //    ShowResult("방어 성공!", Color.cyan);
        //    isDefending = false;
        //    inputTimer = 0f;
        //    damage=0f; // 방어 성공
        //    return;
        //}

        //ShowResult("피격!", Color.red);
        //TakeDamage(damage);
    }

    public void TakeDamage(float damage, string cardtype = "normal")
    {
        if (guard > 0)
        {
            if (guard >= damage)
            {
                guard -= damage;
                damage = 0;
            }
            else
            {
                damage -= guard;
                guard = 0;
            }
        }
        int finalDamage = Mathf.RoundToInt(damage);
        if (finalDamage > 0)
        {
            currentHp -= finalDamage;
            UpdateUI();

            if (currentHp <= 0)
            {
                Die();
            }
        }
    }

    public event Action<string, int> OnStatusChanged;

    public void AddStatus(string type, int amount)
    {
        if (!statusEffects.ContainsKey(type)) return;

        if (type == "freeze" && statusEffects["wet"] > 0)
        {
            statusEffects["freeze"] += statusEffects["wet"];
            statusEffects["wet"] = 0;
            OnStatusChanged?.Invoke("freeze", statusEffects["freeze"]);
            OnStatusChanged?.Invoke("wet", 0);
            return;
        }
        statusEffects[type] += amount;
        OnStatusChanged?.Invoke(type, statusEffects[type]);
    }

    public int GetStatus(string type)
    {
        return statusEffects.ContainsKey(type) ? statusEffects[type] : 0;
    }

    public void SetStatus(string type, int amount)
    {
        if (statusEffects.ContainsKey(type))
        {
            statusEffects[type] = amount;
            OnStatusChanged?.Invoke(type, amount);
        }

     }
    public float GetAttackDamage()
    {
        return attackDamage;
    }
    public void AddGuard(float amount)
    {
        guard += amount;
    }

    void Die()
    {
        Debug.Log("플레이어 사망!");
    }

    public void Heal(int amount)
    {
        currentHp = Mathf.Min(currentHp + amount, maxHp);
        UpdateUI();
    }

    public void UpdateUIForExternalSync()
    {
        UpdateUI();
    }

    void UpdateUI()
    {
        ResolveUiReferences();
        EnsureTopHpBarPlacement();

        //if (heartUI != null) heartUI.UpdateHearts(currentHp, maxHp);
        foreach (TMP_Text text in GetAllSceneHpTexts())
            text.text = $"{currentHp} / {maxHp}";

        if (hpText != null)
            hpText.text = $"{currentHp} / {maxHp}";

        foreach (Slider slider in GetAllScenePlayerHpBars())
        {
            slider.maxValue = maxHp;
            slider.value = currentHp;
        }

        if (hpBar != null)
        {
            hpBar.maxValue = maxHp;
            hpBar.value = currentHp;
        }
    }

    //void UpdateCooldownUI()
    //{
    //    if (cooldownText == null) return;

    //    if (isOnCooldown)
    //    {
    //        cooldownText.text = $"쿨타임: {cooldownTimer:F1}초";
    //        cooldownText.color = Color.yellow;

    //        // 쿨타임 슬라이더 업데이트
    //        if (cooldownBar != null)
    //        {
    //            cooldownBar.value = 1f - (cooldownTimer / defenseActionCooldown);
    //        }
    //    }
    //    else
    //    {
    //        cooldownText.text = "방어/회피 준비";
    //        cooldownText.color = Color.white;

    //        // 쿨타임 슬라이더 완전 채우기
    //        if (cooldownBar != null)
    //        {
    //            cooldownBar.value = 1f;
    //        }
    //    }
    //}

    //void StartCooldown()
    //{
    //    isOnCooldown = true;
    //    cooldownTimer = defenseActionCooldown;
    //    //UpdateCooldownUI();
    //}

    //void ShowResult(string message, Color color)
    //{
    //    if (resultText != null)
    //    {
    //        resultText.text = message;
    //        resultText.color = color;
    //        resultDisplayTimer = 0.5f; // 2초 동안 표시
    //    }
    //}

    public void PlayAttackEffect()
    {
        if (attackParticle != null)
            attackParticle.Play(); // 파티클 재생
    
    }
}
