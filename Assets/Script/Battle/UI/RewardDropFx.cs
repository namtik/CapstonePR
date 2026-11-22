using System.Collections;
using Battle.UI;
using UnityEngine;
using UnityEngine.UI;

// 마지막 적 자리에 보상 점이 떨어져 반짝인 뒤, 인벤토리 버튼으로 날아간다.
// 클릭은 받지 않고, 보상창은 따로 자동으로 열린다.
public class RewardDropFx : MonoBehaviour
{
    static readonly Color CardLight = new Color(0.78f, 0.94f, 1f, 1f);
    static readonly Color ComboLight = new Color(1f, 0.78f, 0.16f, 1f);
    static readonly Color RelicLight = new Color(1f, 0.16f, 0.12f, 1f);

    const float FallTime = 0.4f;
    const float SparkleTime = 0.42f;
    const float FlyTime = 0.7f;
    const float FallHeight = 168f;

    static RewardDropFx _instance;
    static Sprite _disc;
    static Material _additive;

    RectTransform _root;

    public static void Play(Vector2 screenPos, bool withCombo, bool withRelic = false)
    {
        Instance.Begin(screenPos, withCombo, withRelic);
    }

    static RewardDropFx Instance
    {
        get
        {
            if (_instance != null) return _instance;
            var go = new GameObject("RewardDropFx");
            _instance = go.AddComponent<RewardDropFx>();
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
        canvas.sortingOrder = 480;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _root = transform as RectTransform;
    }

    void Begin(Vector2 screenPos, bool withCombo, bool withRelic)
    {
        if (_root == null) BuildCanvas();

        if (withRelic && withCombo)
        {
            StartCoroutine(Drop(screenPos + new Vector2(-130f, 0f), CardLight, 0f));
            StartCoroutine(Drop(screenPos, ComboLight, 0.07f));
            StartCoroutine(Drop(screenPos + new Vector2(130f, 0f), RelicLight, 0.14f));
            return;
        }

        if (withRelic)
        {
            StartCoroutine(Drop(screenPos + new Vector2(-78f, 0f), CardLight, 0f));
            StartCoroutine(Drop(screenPos + new Vector2(78f, 0f), RelicLight, 0.07f));
            return;
        }

        if (!withCombo)
        {
            StartCoroutine(Drop(screenPos, CardLight, 0f));
            return;
        }

        StartCoroutine(Drop(screenPos + new Vector2(-78f, 0f), CardLight, 0f));
        StartCoroutine(Drop(screenPos + new Vector2(78f, 0f), ComboLight, 0.07f));
    }

    IEnumerator Drop(Vector2 landScreen, Color light, float delay)
    {
        if (delay > 0f)
            yield return new WaitForSecondsRealtime(delay);

        Vector2 land = ToLocal(landScreen);
        Vector2 start = land + new Vector2(0f, FallHeight);

        var rootGo = new GameObject("RewardOrb", typeof(RectTransform), typeof(CanvasGroup));
        var rt = rootGo.GetComponent<RectTransform>();
        rt.SetParent(_root, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = start;
        rt.localScale = Vector3.one;

        var group = rootGo.GetComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;
        group.alpha = 1f;

        Image bloom = MakeDisc(rt, "Bloom", 220f, light);
        Image glow = MakeDisc(rt, "Glow", 132f, light);
        Image core = MakeDisc(rt, "Core", 42f, Color.Lerp(light, Color.white, 0.35f));
        Image hot = MakeDisc(rt, "Hot", 16f, Color.white);
        Image ring = MakeDisc(rt, "Ring", 48f, light);
        ring.enabled = false;

        var specks = new Image[9];
        var phases = new float[specks.Length];
        var angles = new float[specks.Length];
        for (int i = 0; i < specks.Length; i++)
        {
            specks[i] = MakeDisc(rt, "Speck" + i, Random.Range(7f, 12f), Color.Lerp(light, Color.white, 0.55f));
            phases[i] = Random.Range(0f, 6.28f);
            angles[i] = (Mathf.PI * 2f * i) / specks.Length + Random.Range(-0.2f, 0.2f);
        }

        float elapsed = 0f;
        while (elapsed < FallTime)
        {
            elapsed += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(elapsed / FallTime);
            rt.anchoredPosition = Vector2.Lerp(start, land, u * u);
            Sparkle(bloom, glow, core, hot, specks, phases, angles, elapsed);
            yield return null;
        }

        rt.anchoredPosition = land;
        ring.enabled = true;

        float held = 0f;
        while (held < SparkleTime)
        {
            held += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(held / 0.22f);
            ring.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.4f, 2.8f, u);
            SetAlpha(ring, Mathf.Lerp(0.95f, 0f, u));
            float squash = 1f + Mathf.Sin(Mathf.Clamp01(held / 0.16f) * Mathf.PI) * 0.12f;
            rt.localScale = new Vector3(squash, 2f - squash, 1f);
            Sparkle(bloom, glow, core, hot, specks, phases, angles, FallTime + held);
            yield return null;
        }

        rt.localScale = Vector3.one;
        InventoryOpenButton.TryGetTarget(null, out Vector2 targetScreen, out InventoryOpenButton button);
        yield return FlyToInventory(rt, group, bloom, glow, core, hot, specks, phases, angles, ToLocal(targetScreen), button);
    }

    IEnumerator FlyToInventory(RectTransform rt, CanvasGroup group, Image bloom, Image glow, Image core, Image hot, Image[] specks, float[] phases, float[] angles, Vector2 target, InventoryOpenButton button)
    {
        Vector2 from = rt.anchoredPosition;
        Vector2 dir = target - from;
        Vector2 side = dir.sqrMagnitude > 1f ? new Vector2(-dir.y, dir.x).normalized : Vector2.up;
        float arc = Mathf.Clamp(dir.magnitude * 0.18f, 48f, 170f);
        Vector2 control = Vector2.Lerp(from, target, 0.38f) + side * arc;

        float t = 0f;
        float clock = FallTime + SparkleTime;
        while (t < FlyTime)
        {
            float dt = Time.unscaledDeltaTime;
            t += dt;
            clock += dt;
            float u = Mathf.Clamp01(t / FlyTime);
            float e = u * u * (3f - 2f * u);
            rt.anchoredPosition = Quad(from, control, target, e);
            float s = Mathf.Lerp(1f, 0.18f, e);
            rt.localScale = new Vector3(s, s, 1f);
            group.alpha = u > 0.88f ? Mathf.Lerp(1f, 0f, (u - 0.88f) / 0.12f) : 1f;
            Sparkle(bloom, glow, core, hot, specks, phases, angles, clock);
            yield return null;
        }

        if (button != null)
            button.PlayReceivePulse();
        if (rt != null)
            Destroy(rt.gameObject);
    }

    static Vector2 Quad(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    static void Sparkle(Image bloom, Image glow, Image core, Image hot, Image[] specks, float[] phases, float[] angles, float time)
    {
        float pulse = 0.82f + 0.18f * Mathf.Sin(time * 14f);
        float twinkle = 0.7f + 0.3f * Mathf.Abs(Mathf.Sin(time * 22f));

        bloom.rectTransform.localScale = Vector3.one * (0.92f + 0.22f * Mathf.Sin(time * 7f));
        SetAlpha(bloom, 0.72f * pulse);
        glow.rectTransform.localScale = Vector3.one * (0.9f + 0.2f * Mathf.Sin(time * 9f));
        SetAlpha(glow, 0.95f * pulse);
        core.rectTransform.localScale = Vector3.one * (0.9f + 0.22f * twinkle);
        SetAlpha(core, 1f);
        hot.rectTransform.localScale = Vector3.one * (0.85f + 0.5f * twinkle);
        SetAlpha(hot, 1f);

        for (int i = 0; i < specks.Length; i++)
        {
            float cycle = Mathf.Repeat(time * 1.7f + phases[i] * 0.15f, 1f);
            float pop = Mathf.Sin(cycle * Mathf.PI);
            float dist = Mathf.Lerp(18f, 64f, cycle);
            float ang = angles[i] + time * (i % 2 == 0 ? 1.6f : -1.15f);
            var speckRt = specks[i].rectTransform;
            speckRt.anchoredPosition = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist;
            speckRt.localScale = Vector3.one * Mathf.Lerp(0.4f, 1.35f, pop);
            SetAlpha(specks[i], pop);
        }
    }

    Vector2 ToLocal(Vector2 screen)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out Vector2 local);
        return local;
    }

    static Image MakeDisc(RectTransform parent, string name, float size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one;

        var image = go.GetComponent<Image>();
        image.sprite = Disc();
        image.material = Additive();
        image.raycastTarget = false;
        image.color = color;
        return image;
    }

    static Material Additive()
    {
        if (_additive != null) return _additive;
        Shader shader = Shader.Find("UI/RewardDropAdditive");
        if (shader == null)
            shader = Resources.Load<Shader>("RewardDropAdditive");
        if (shader == null) return null;
        _additive = new Material(shader);
        return _additive;
    }

    static void SetAlpha(Image image, float alpha)
    {
        if (image == null) return;
        Color c = image.color;
        c.a = alpha;
        image.color = c;
    }

    static Sprite Disc()
    {
        if (_disc != null) return _disc;

        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        float center = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                float a = Mathf.Clamp01(1f - d);
                a *= a;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        _disc = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        return _disc;
    }
}
