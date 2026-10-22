using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 적 처치 시 자리에 +N을 띄우고, 파란 영혼이 우측 상단 재화 표시로 날아간다.
public class KillRewardFx : MonoBehaviour
{
    const float SoulHoverTime = 0.75f;

    static KillRewardFx _instance;

    RectTransform _root;
    RectTransform _flyRoot;

    public static void Play(Vector2 screenPos, int amount, Action onCollected)
    {
        if (amount <= 0)
        {
            onCollected?.Invoke();
            return;
        }
        Instance.Begin(screenPos, amount, onCollected);
    }

    static KillRewardFx Instance
    {
        get
        {
            if (_instance != null) return _instance;
            var go = new GameObject("KillRewardFx");
            _instance = go.AddComponent<KillRewardFx>();
            DontDestroyOnLoad(go);
            return _instance;
        }
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        BuildCanvas();
    }

    void BuildCanvas()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 220;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _root = transform as RectTransform;
    }

    void Begin(Vector2 screenPos, int amount, Action onCollected)
    {
        if (_root == null) BuildCanvas();
        var flight = new Flight();
        StartCoroutine(Popup(ToLocal(screenPos), amount, flight));
        StartCoroutine(Souls(screenPos, ResolveMoneyScreen(), amount, onCollected, flight));
    }

    Vector2 ToLocal(Vector2 screen)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out Vector2 local);
        return local;
    }

    static Vector2 ResolveMoneyScreen()
    {
        MoneyUI ui = FindVisibleMoney();
        if (ui == null)
            ui = FindFirstObjectByType<MoneyUI>(FindObjectsInactive.Exclude);
        if (ui != null) return ui.ScreenCenter();
        return new Vector2(Screen.width - 90f, Screen.height - 40f);
    }

    IEnumerator Popup(Vector2 origin, int amount, Flight flight)
    {
        var go = new GameObject("KillGoldText", typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(_root, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(320f, 96f);
        rt.anchoredPosition = origin + new Vector2(0f, 40f);

        var text = go.AddComponent<TextMeshProUGUI>();
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 48f;
        text.fontStyle = FontStyles.Bold;
        text.color = new Color(0.62f, 0.86f, 1f, 1f);
        text.text = $"+{amount}";
        TMP_FontAsset font = ResolveFont();
        if (font != null) text.font = font;
        text.outlineWidth = 0.18f;
        text.outlineColor = new Color(0.05f, 0.12f, 0.22f, 0.9f);

        Vector2 from = rt.anchoredPosition;
        float t = 0f;
        const float popTime = 0.16f;
        while (t < popTime)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / popTime);
            rt.anchoredPosition = from + new Vector2(0f, 16f * u);
            rt.localScale = Vector3.one * Mathf.Lerp(0.86f, 1f, u);
            yield return null;
        }
        rt.localScale = Vector3.one;

        float waited = 0f;
        while (!flight.done && waited < 3f)
        {
            waited += Time.deltaTime;
            yield return null;
        }

        t = 0f;
        const float fade = 0.22f;
        while (t < fade)
        {
            t += Time.deltaTime;
            var c = text.color;
            c.a = Mathf.Lerp(1f, 0f, t / fade);
            text.color = c;
            yield return null;
        }
        Destroy(go);
    }

    IEnumerator Souls(Vector2 origin, Vector2 target, int amount, Action onCollected, Flight flight)
    {
        if (FlyRoot() == null)
        {
            flight.done = true;
            onCollected?.Invoke();
            yield break;
        }

        int count = Mathf.Clamp(5 + amount / 35, 6, 12);
        var delays = new float[count];
        var durations = new float[count];
        int payer = 0;
        float soonest = float.MaxValue;
        float latest = 0f;
        for (int i = 0; i < count; i++)
        {
            delays[i] = UnityEngine.Random.Range(0f, 0.12f);
            durations[i] = UnityEngine.Random.Range(0.62f, 0.92f);
            float arrive = delays[i] + SoulHoverTime + durations[i];
            if (arrive < soonest)
            {
                soonest = arrive;
                payer = i;
            }
            if (arrive > latest) latest = arrive;
        }

        flight.left = count;
        bool collected = false;
        for (int i = 0; i < count; i++)
        {
            bool pays = i == payer;
            StartCoroutine(FlySoul(origin, target, delays[i], durations[i], pays, () =>
            {
                if (collected) return;
                collected = true;
                var ui = FindVisibleMoney();
                if (ui != null) ui.PlayReceivePulse();
                onCollected?.Invoke();
            }, flight));
        }
        yield return new WaitForSeconds(latest + 0.25f);
        if (!collected)
        {
            collected = true;
            onCollected?.Invoke();
        }
    }

    IEnumerator FlySoul(Vector2 origin, Vector2 target, float delay, float duration, bool pays, Action onArrive, Flight flight)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        RectTransform rt = CreateMoneyIcon();
        if (rt == null)
        {
            if (pays) onArrive?.Invoke();
            flight.FinishOne();
            yield break;
        }
        var image = rt.GetComponent<Image>();

        Vector2 from = origin + UnityEngine.Random.insideUnitCircle * 56f;
        Place(rt, from);

        float hover = 0f;
        float bobPhase = UnityEngine.Random.Range(0f, 6.28f);
        Vector2 hoverPos = from;
        while (hover < SoulHoverTime)
        {
            hover += Time.deltaTime;
            float u = Mathf.Clamp01(hover / SoulHoverTime);
            float bob = Mathf.Sin(bobPhase + hover * 6.5f) * 7f;
            hoverPos = from + new Vector2(bob * 0.35f, 22f * u + bob);
            Place(rt, hoverPos);
            rt.localScale = Vector3.one * Mathf.Lerp(0.86f, 1.05f, u);
            yield return null;
        }
        from = hoverPos;

        Vector2 dir = target - from;
        Vector2 side = dir.sqrMagnitude > 0.01f ? new Vector2(-dir.y, dir.x).normalized : Vector2.up;
        Vector2 control = Vector2.Lerp(from, target, 0.35f) + side * UnityEngine.Random.Range(-110f, 110f);

        bool arrived = false;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            float e = u * u;
            Vector2 p = Vector2.Lerp(Vector2.Lerp(from, control, e), Vector2.Lerp(control, target, e), e);
            Place(rt, p);
            rt.localScale = Vector3.one * Mathf.Lerp(1.05f, 0.72f, e);
            if (image != null)
            {
                float fadeOut = u > 0.9f ? Mathf.Lerp(1f, 0f, (u - 0.9f) / 0.1f) : 1f;
                var c = Color.white;
                c.a = fadeOut;
                image.color = c;
            }
            if (pays && !arrived && u >= 0.94f)
            {
                arrived = true;
                onArrive?.Invoke();
            }
            yield return null;
        }
        if (pays && !arrived) onArrive?.Invoke();
        if (rt != null) Destroy(rt.gameObject);
        flight.FinishOne();
    }

    // 체력바가 그려지는 전투 캔버스에 붙인다. +N 전용 캔버스에서는 Image가 화면에 안 나온다.
    RectTransform FlyRoot()
    {
        if (_flyRoot != null) return _flyRoot;

        Canvas host = FindHostCanvas();
        if (host == null) return null;

        var go = new GameObject("KillRewardFly", typeof(RectTransform));
        go.layer = host.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(host.transform, false);
        Stretch(rt);
        rt.SetAsLastSibling();
        _flyRoot = rt;
        return rt;
    }

    static Canvas FindHostCanvas()
    {
        GameObject stage = GameObject.Find("CombatStage");
        if (stage != null)
        {
            var canvases = stage.GetComponentsInChildren<Canvas>(false);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas != null && canvas.isActiveAndEnabled && canvas.isRootCanvas)
                    return canvas;
            }
        }

        MoneyUI ui = FindVisibleMoney();
        if (ui != null)
        {
            Canvas canvas = ui.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.isActiveAndEnabled)
                return canvas.rootCanvas;
        }

        return null;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
    }

    static void Place(RectTransform rt, Vector2 screen)
    {
        rt.position = new Vector3(screen.x, screen.y, 0f);
    }

    RectTransform CreateMoneyIcon()
    {
        RectTransform parent = FlyRoot();
        if (parent == null) return null;

        Sprite sprite = null;
        MoneyUI ui = FindVisibleMoney();
        if (ui != null) sprite = ui.IconSprite;
        if (sprite == null && MoneyManager.Instance != null)
            sprite = MoneyManager.Instance.MoneyIcon;
        if (sprite == null)
        {
            Debug.LogWarning("[KillRewardFx] 재화 아이콘 스프라이트를 찾지 못했습니다.");
            return null;
        }

        var go = new GameObject("Soul", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = parent.gameObject.layer;
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        rt.sizeDelta = new Vector2(64f, 64f);
        rt.SetAsLastSibling();

        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.useSpriteMesh = false;
        image.raycastTarget = false;
        image.maskable = false;
        image.color = Color.white;
        image.enabled = true;
        return rt;
    }

    static MoneyUI FindVisibleMoney()
    {
        var all = FindObjectsByType<MoneyUI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            MoneyUI ui = all[i];
            if (ui != null && ui.isActiveAndEnabled && ui.IconSprite != null)
                return ui;
        }
        return null;
    }

    static TMP_FontAsset ResolveFont()
    {
        var ui = FindFirstObjectByType<MoneyUI>(FindObjectsInactive.Include);
        if (ui != null)
        {
            var text = ui.GetComponentInChildren<TMP_Text>(true);
            if (text != null && text.font != null) return text.font;
        }
        return TMP_Settings.defaultFontAsset;
    }

    // 한 번의 처치에서 아이콘이 전부 도착했는지 센다.
    sealed class Flight
    {
        public int left;
        public bool done;

        public void FinishOne()
        {
            left = Mathf.Max(0, left - 1);
            if (left <= 0) done = true;
        }
    }
}
