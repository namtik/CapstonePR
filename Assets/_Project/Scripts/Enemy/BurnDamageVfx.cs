using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// One short burn hit. Rear fire is drawn behind the enemy and the core is drawn in front.
[AddComponentMenu("")]
public sealed class BurnDamageVfx : MonoBehaviour
{
    const int FrameCount = 32;
    const float FramesPerSecond = 28f;
    static Sprite[] frames;

    SpriteRenderer rearWorld, frontWorld;
    Image rearUi, frontUi;
    Transform follow;
    Camera stageCamera;
    Vector3 centerInTarget, extents;
    BurnDamageSettings settings;
    float elapsed;
    int lastFrame = -1;

    static bool HasFrames()
    {
        if (frames != null) return frames.Length == 2 * FrameCount;
        frames = Resources.LoadAll<Sprite>("CardEffects/BurnDamageHit");
        Array.Sort(frames, (a, b) => string.CompareOrdinal(a.name, b.name));
        if (frames.Length == 2 * FrameCount) return true;
        Debug.LogWarning("[BurnDamageVfx] BurnDamageHit requires 32 Back and 32 Front sprites.");
        return false;
    }

    public static void PlayWorld(Transform target, Camera camera, Bounds body, int layer)
    {
        PlayWorld(target, camera, body, layer, null);
    }

    internal static void PlayWorld(Transform target, Camera camera, Bounds body, int layer, SpriteRenderer spriteBody)
    {
        if (!camera || !target || !HasFrames()) return;
        var root = new GameObject("BurnDamageHit") { layer = layer };
        SceneManager.MoveGameObjectToScene(root, target.gameObject.scene);
        var fx = root.AddComponent<BurnDamageVfx>();
        fx.follow = target;
        fx.stageCamera = camera;
        fx.centerInTarget = target.InverseTransformPoint(body.center + Vector3.up * (body.size.y * 0.075f));
        fx.extents = body.extents;
        fx.rearWorld = root.AddComponent<SpriteRenderer>();
        var core = new GameObject("Flame core") { layer = layer };
        core.transform.SetParent(root.transform, false);
        fx.frontWorld = core.AddComponent<SpriteRenderer>();
        if (spriteBody)
        {
            fx.rearWorld.sortingLayerID = fx.frontWorld.sortingLayerID = spriteBody.sortingLayerID;
            fx.rearWorld.sortingOrder = Mathf.Max(short.MinValue, spriteBody.sortingOrder - 1);
            fx.frontWorld.sortingOrder = Mathf.Min(short.MaxValue, spriteBody.sortingOrder + 1);
        }
        fx.PlaceWorld();
        fx.SetFrame(0);
    }

    void PlaceWorld()
    {
        Vector3 center = follow.TransformPoint(centerInTarget);
        Transform cam = stageCamera.transform;
        Vector3 ray = stageCamera.orthographic ? cam.forward : (center - cam.position).normalized;
        float depth = Mathf.Abs(ray.x) * extents.x + Mathf.Abs(ray.y) * extents.y + Mathf.Abs(ray.z) * extents.z;
        float distance = Vector3.Dot(center - cam.position, cam.forward);
        float offset = Mathf.Min(depth + 0.01f, Mathf.Max(0f, distance - stageCamera.nearClipPlane) * 0.8f);
        float size = Mathf.Max(0.05f, Mathf.Max(extents.y * 1.62f, Mathf.Max(extents.x, extents.z) * 1.15f));
        transform.SetPositionAndRotation(center + ray * offset, cam.rotation);
        transform.localScale = Vector3.one * size;
        frontWorld.transform.position = center - ray * offset;
    }

    public static void PlayUi(RectTransform body)
    {
        if (!body || !body.parent || !HasFrames()) return;
        var rear = NewImage("BurnDamageHit", body.parent);
        rear.transform.SetSiblingIndex(body.GetSiblingIndex());
        var fx = rear.gameObject.AddComponent<BurnDamageVfx>();
        fx.follow = body;
        fx.rearUi = rear;
        fx.frontUi = NewImage("Flame core", body);
        fx.frontUi.rectTransform.anchorMin = fx.frontUi.rectTransform.anchorMax = new Vector2(0.5f, 0.575f);
        fx.frontUi.rectTransform.anchoredPosition = Vector2.zero;
        fx.frontUi.transform.SetAsFirstSibling();
        fx.PlaceUi(body);
        fx.SetFrame(0);
    }

    static Image NewImage(string label, Transform parent)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        if (parent.GetComponent<LayoutGroup>()) go.AddComponent<LayoutElement>().ignoreLayout = true;
        var image = go.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }

    void PlaceUi(RectTransform body)
    {
        var size = Vector2.one * Mathf.Max(body.rect.height * 0.81f, body.rect.width * 0.58f);
        frontUi.rectTransform.sizeDelta = rearUi.rectTransform.sizeDelta = size;
        transform.SetPositionAndRotation(frontUi.transform.position, body.rotation);
        transform.localScale = body.localScale;
    }

    void Awake()
    {
        settings = BurnDamageSettings.Load();
    }

    void LateUpdate()
    {
        elapsed += Time.deltaTime * (settings ? settings.PlaybackSpeed : 1f);
        int frame = (int)(elapsed * FramesPerSecond);
        if (frame >= FrameCount || (rearUi && (!follow || !frontUi || !follow.gameObject.activeInHierarchy)))
        {
            Destroy(gameObject);
            return;
        }
        if (rearWorld && follow && stageCamera) PlaceWorld();
        if (rearUi) PlaceUi((RectTransform)follow);
        SetFrame(frame);
    }

    void SetFrame(int frame)
    {
        if (lastFrame == frame) return;
        lastFrame = frame;
        if (rearWorld)
        {
            rearWorld.sprite = frames[frame];
            frontWorld.sprite = frames[FrameCount + frame];
        }
        else
        {
            rearUi.sprite = frames[frame];
            frontUi.sprite = frames[FrameCount + frame];
        }
    }

    void OnDestroy()
    {
        if (frontUi) Destroy(frontUi.gameObject);
    }
}
