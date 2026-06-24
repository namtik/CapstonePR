using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System;

public class Player : MonoBehaviour, IBattleUnit
{
    public static Player Instance { get; private set; } // 전역 싱글톤 인스턴스

    [Header("능력치")]
    public const int DefaultMaxHp = 100; // 프리팹 기본 최대 체력
    public int maxHp = DefaultMaxHp; // 최대 체력
    public int currentHp; // 현재 체력
    public float attackDamage = 10f; // 공격력

    [Header("UI")]
    public HeartUI heartUI; // 하트 표시 UI
    public TMP_Text hpText; // 체력 텍스트
    public Slider hpBar; // 체력 바
    public Slider cooldownBar; // 쿨타임 슬라이더
    public Text cooldownText; // 쿨타임 표시 텍스트
    public Text resultText; // 방어/회피 결과 텍스트

    [Header("상태이상 및 방어도")]
    public float guard = 0f; // 방어도
    public Dictionary<string, int> statusEffects = new Dictionary<string, int>(); // 상태이상 수치 맵

    public ParticleSystem attackParticle; // 공격 파티클 이펙트

    // 런타임 임시 생성된 PlayerLogic 폴백인지 판별한다
    static bool IsRuntimeFallback(Player player)
    {
        return player != null && player.gameObject != null && player.gameObject.name == "PlayerLogic";
    }

    // 씬에서 유효한 Player 인스턴스를 찾아 반환한다
    public static Player Resolve(bool includeInactive = true)
    {
        if (Instance != null)
        {
            if (includeInactive || Instance.gameObject.activeInHierarchy)
            {
                if (!IsRuntimeFallback(Instance))
                    return Instance;
            }
        }

        Player[] players = includeInactive
            ? FindObjectsByType<Player>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            : FindObjectsByType<Player>(FindObjectsSortMode.None);

        if (players == null || players.Length == 0)
            return includeInactive ? Instance : null;

        Player preferred = null;
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] == null) continue;
            if (!IsRuntimeFallback(players[i]))
            {
                preferred = players[i];
                break;
            }
        }

        if (preferred == null)
            preferred = players[0];

        Instance = preferred;
        return preferred;
    }

    // Player를 찾고 없으면 런타임 폴백 오브젝트를 생성해 반환한다
    public static Player GetOrCreateRuntime()
    {
        Player resolved = Resolve(true);
        if (resolved != null)
            return resolved;

        GameObject go = new GameObject("PlayerLogic");
        Player player = go.AddComponent<Player>();
        DontDestroyOnLoad(go);
        Instance = player;
        return player;
    }

    // 싱글톤 등록, 체력·상태이상 초기화, UI 갱신
    void Awake()
    {
        if (Instance == null || IsRuntimeFallback(Instance))
            Instance = this;

        ResolveUiReferences();
        if (currentHp <= 0)
            currentHp = maxHp;
        else
            currentHp = Mathf.Clamp(currentHp, 1, maxHp);

        EnsureLegacyStatusEffects();
        UpdateUI();
    }

    // menubar가 활성화될 때(ShopCanvas2·RelicStage 등) canonical HP로 맞추고 UI를 갱신한다
    void OnEnable()
    {
        Player canonical = Resolve(true);
        if (canonical == null || IsRuntimeFallback(this)) return;

        if (this != canonical)
        {
            maxHp = canonical.maxHp;
            currentHp = canonical.currentHp;
        }

        canonical.UpdateUIForExternalSync();
    }

    // 구 시스템(슬롯/발사 등)용 상태이상 키가 없으면 0으로 보장한다
    void EnsureLegacyStatusEffects()
    {
        EnsureStatusKey("launcher");
        EnsureStatusKey("fortify");
        EnsureStatusKey("charge");
        EnsureStatusKey("chain");
    }

    void EnsureStatusKey(string key)
    {
        if (!statusEffects.ContainsKey(key))
            statusEffects[key] = 0;
    }

    // UI 참조 재확인 및 미사용 쿨타임 UI 정리
    void Start()
    {
        ResolveUiReferences();
        HideUnusedCooldownUI();
        if (resultText != null) resultText.text = "";

    }

    // 씬에서 체력 텍스트/바 등 UI 참조를 찾아 연결한다
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

    // 오브젝트가 로드된 씬에 속하는지 판별한다
    bool IsSceneObject(GameObject go)
    {
        if (go == null) return false;
        return go.scene.IsValid() && go.scene.isLoaded;
    }

    // 씬 안의 "HpText" 텍스트들을 순회 반환한다
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

    // 씬 안의 "PlayerHpBar" 슬라이더들을 순회 반환한다
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

    // 구버전 "HpSlider" 오브젝트들을 비활성화한다
    void DisableLegacyHpSliders()
    {
        Slider[] sliders = FindObjectsByType<Slider>(FindObjectsSortMode.None);
        foreach (Slider slider in sliders)
        {
            if (slider != null && slider.gameObject.name == "HpSlider")
                slider.gameObject.SetActive(false);
        }
    }

    // 사용하지 않는 쿨타임 바/텍스트 UI를 숨긴다
    void HideUnusedCooldownUI()
    {
        if (cooldownBar != null)
            cooldownBar.gameObject.SetActive(false);

        if (cooldownText != null)
            cooldownText.gameObject.SetActive(false);
    }

    // 상단 체력바가 들어갈 컨테이너 RectTransform을 찾는다
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

    // 컨테이너 자식 중 "PlayerHpBar" 슬라이더를 찾는다
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

    // 상단 체력바를 올바른 컨테이너에 배치하고 상호작용을 끈다
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

    // 새 전투 시작 시 방어도와 모든 상태이상을 초기화한다
    public void ResetStatusForNewBattle()
    {
        guard = 0f;

        List<string> keys = new List<string>(statusEffects.Keys);
        foreach (string key in keys)
        {
            if (statusEffects[key] > 0)
            {
                statusEffects[key] = 0;

                OnStatusChanged?.Invoke(key, 0);
            }
        }
    }

    // 매 프레임 갱신(현재 동작 없음)
    void Update()
    {
    }

    // 피해를 받고 방어도 소모·HP 감소·사망 처리 및 트리거를 발생시킨다
    public void TakeDamage(float damage, string cardtype = "normal")
    {
        bool blockConsumed = false;
        bool disposableGuard = cardtype == "enemy_attack";
        if (guard > 0)
        {
            blockConsumed = true;
            if (disposableGuard)
            {
                // 몬스터 공격: 방어도는 1회 맞으면 전량 소멸(남은 방어도 이월 없음)
                if (guard >= damage)
                    damage = 0;
                else
                    damage -= guard;
                guard = 0;
            }
            else if (guard >= damage)
            {
                guard -= damage;
                damage = 0;
            }
            else
            {
                damage -= guard;
                guard = 0;
            }
            OnStatusChanged?.Invoke("guard", Mathf.RoundToInt(guard));
        }
        int finalDamage = Mathf.RoundToInt(damage);
        if (finalDamage > 0)
        {
            currentHp -= finalDamage;
            SyncAllInstancesFrom(this);
            OnHpDecreased?.Invoke(finalDamage);

            if (currentHp <= 0)
            {
                Die();
            }
        }

        if (cardtype != "self_loss")
        {
            OnPlayerHit?.Invoke();
            if (blockConsumed) OnBlockConsumedByAttack?.Invoke();
        }
    }

    public event Action OnPlayerHit; // 피격(방어도로 막힘 포함) 발생 이벤트
    public event Action<int> OnHpDecreased; // 실제 HP 감소 시(줄어든 양) 이벤트
    public event Action OnBlockConsumedByAttack; // 적 공격에 방어도가 소모됐을 때 이벤트
    public event Action<string, int> OnStatusChanged; // 상태이상 수치 변경 이벤트

    // 상태이상을 추가하며(젖음→빙결 변환 등), 최대 999로 제한한다
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
        statusEffects[type] = Mathf.Clamp(statusEffects[type] + amount, 0, 999);
        OnStatusChanged?.Invoke(type, statusEffects[type]);
    }

    // 지정한 상태이상의 현재 수치를 반환한다
    public int GetStatus(string type)
    {
        EnsureLegacyStatusEffects();
        return statusEffects.ContainsKey(type) ? statusEffects[type] : 0;
    }

    // 상태이상 수치를 지정값(0~999)으로 설정한다
    public void SetStatus(string type, int amount)
    {
        if (statusEffects.ContainsKey(type))
        {
            int v = Mathf.Clamp(amount, 0, 999);
            statusEffects[type] = v;
            OnStatusChanged?.Invoke(type, v);
        }

     }

    // 현재 공격력을 반환한다
    public float GetAttackDamage()
    {
        return attackDamage;
    }

    // 방어도를 더하며 최대 999로 제한한다
    public void AddGuard(float amount)
    {
        guard = Mathf.Clamp(guard + amount, 0f, 999f);
        OnStatusChanged?.Invoke("guard", Mathf.RoundToInt(guard));
    }

    // 사망 처리 — 게임오버 연출을 재생한다
    void Die()
    {
        Debug.Log("플레이어 사망!");

        GameOverController controller = GameOverController.Instance;
        if (controller == null)
            controller = FindFirstObjectByType<GameOverController>(FindObjectsInactive.Include);

        if (controller != null)
            controller.PlayDeathSequence();
        else
            Debug.LogWarning("[Player] GameOverController를 찾지 못해 사망 연출을 재생하지 못했습니다.");
    }

    // 체력을 회복하며 최대 체력을 넘지 않는다
    public void Heal(int amount)
    {
        currentHp = Mathf.Min(currentHp + amount, maxHp);
        SyncAllInstancesFrom(this);
    }

    // 외부에서 호출해 체력 UI를 강제 동기화한다
    public void UpdateUIForExternalSync()
    {
        UpdateUI();
    }

    // 씬에 있는 menubar Player들의 체력을 기본값(100)으로 되돌린다 (유물·런 리셋용)
    public static void ResetBaseHpOnAllInstances()
    {
        Player[] players = FindObjectsByType<Player>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (players == null) return;

        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] == null || IsRuntimeFallback(players[i])) continue;
            players[i].maxHp = DefaultMaxHp;
            players[i].currentHp = DefaultMaxHp;
        }
    }

    // 현재 maxHp까지 체력을 채우고 UI를 동기화한다
    public static void RestoreFullHpForRun()
    {
        Player player = Resolve(true);
        if (player == null) return;
        player.currentHp = player.maxHp;
        SyncAllInstancesFrom(player);
    }

    // canonical Player의 HP를 모든 menubar Player 복제본에 복사하고 UI를 갱신한다
    public static void SyncAllInstancesFromCanonical()
    {
        SyncAllInstancesFrom(Resolve(true));
    }

    public static void SyncAllInstancesFrom(Player source)
    {
        if (source == null || IsRuntimeFallback(source)) return;

        Player[] players = FindObjectsByType<Player>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (players != null)
        {
            for (int i = 0; i < players.Length; i++)
            {
                Player p = players[i];
                if (p == null || IsRuntimeFallback(p)) continue;
                p.maxHp = source.maxHp;
                p.currentHp = source.currentHp;
            }
        }

        source.UpdateUIForExternalSync();
    }

    // UI에 표시할 HP(canonical Player 기준)를 반환한다
    (int current, int max) GetDisplayHp()
    {
        Player canonical = Resolve(true);
        if (canonical != null && !IsRuntimeFallback(canonical))
            return (canonical.currentHp, canonical.maxHp);
        return (currentHp, maxHp);
    }

    // 체력 텍스트와 체력 바를 현재 값으로 갱신한다
    void UpdateUI()
    {
        ResolveUiReferences();
        EnsureTopHpBarPlacement();

        (int uiCurrentHp, int uiMaxHp) = GetDisplayHp();

        foreach (TMP_Text text in GetAllSceneHpTexts())
            text.text = $"{uiCurrentHp} / {uiMaxHp}";

        if (hpText != null)
            hpText.text = $"{uiCurrentHp} / {uiMaxHp}";

        foreach (Slider slider in GetAllScenePlayerHpBars())
        {
            slider.maxValue = uiMaxHp;
            slider.value = uiCurrentHp;
        }

        if (hpBar != null)
        {
            hpBar.maxValue = uiMaxHp;
            hpBar.value = uiCurrentHp;
        }
    }

    // 공격 파티클 이펙트를 재생한다
    public void PlayAttackEffect()
    {
        if (attackParticle != null)
            attackParticle.Play();

    }
}



