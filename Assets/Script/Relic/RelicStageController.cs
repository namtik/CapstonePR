using Battle.Relic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;

public class RelicStageController : MonoBehaviour
{
    private const string DefaultSpriteOnlyRelicDescription = "개발중인 유물";

    [System.Serializable]
    private struct SpriteOnlyRelicEntry
    {
        public Sprite sprite;
        public string displayName;
        [TextArea(2, 4)] public string description;
    }

    [System.Serializable]
    private struct ChestSettings
    {
        [Header("References")]
        public Button chestButton;
        public RectTransform chestTransform;
        public Image chestImage;

        [Header("Sprites")]
        public Sprite closedChestSprite;
        public Sprite openedChestSprite;

        [Header("Shake")]
        public float shakeDuration;
        public float shakePositionAmplitude;
        public float shakeRotationAmplitude;
        public float shakeFrequency;
    }

    [System.Serializable]
    private struct ItemSettings
    {
        [Header("References")]
        public GameObject itemObject;
        public RectTransform itemTransform;
        public Image itemImage;

        [Header("Rise")]
        public float riseDuration;
        public float riseHeight;
        public AnimationCurve riseCurve;
        public AnimationCurve scaleCurve;

        [Header("Hover")]
        public float hoverSpeed;
        public float hoverAmplitude;
    }

    [System.Serializable]
    private struct RewardAbsorbSettings
    {
        [Header("References")]
        public RectTransform uiCanvasRect;
        public RectTransform targetSlotRect;
        public Image targetSlotImage;
        public Image flyingImageTemplate;

        [Header("Timing")]
        public float flyDuration;
        public AnimationCurve xMoveCurve;
        public AnimationCurve yMoveCurve;
        public AnimationCurve arcCurve;
        public float arcHeight;

        [Header("Scale")]
        public float startScale;
        public float endScale;
    }

    [Header("툴팁 팝업 (Optional)")]
    [SerializeField] private RectTransform tooltipPopupRoot;
    [SerializeField] private Image tooltipPopupBackground;
    [SerializeField] private TextMeshProUGUI tooltipNameText;
    [SerializeField] private TextMeshProUGUI tooltipDescriptionText;
    [SerializeField] private Vector2 tooltipPopupAnchoredPosition = new Vector2(0f, -60f);
    [SerializeField] private Vector2 tooltipPopupSize = new Vector2(820f, 220f);
    [SerializeField] private Vector2 tooltipPopupPadding = new Vector2(24f, 18f);
    [SerializeField] private Color tooltipPopupBackgroundColor = new Color(0f, 0f, 0f, 0.78f);

    [Header("상자 관련 설정")]
    [SerializeField] private ChestSettings chestSettings;

    [Header("아이템 관련 설정")]
    [SerializeField] private ItemSettings itemSettings;

    [Header("개발중 유물 이미지")]
    [SerializeField] private string relicSpriteFolderPath = "Assets/Sprite/Relic";
    [SerializeField] private bool autoCollectSpritesInEditor = true;
    [SerializeField] private bool avoidOwnedDuplicates = true;
    [SerializeField] private List<SpriteOnlyRelicEntry> spriteOnlyRelics = new List<SpriteOnlyRelicEntry>();

    [Header("획득 유물 상호작용")]
    [SerializeField] private float itemHoverScaleMultiplier = 1.08f;
    [SerializeField] private float itemHoverScaleLerpSpeed = 10f;
    [SerializeField] private Sprite fallbackRewardSprite;

    [Header("획득 연출: 월드 -> 좌측 상단 UI 슬롯")]
    [SerializeField] private RewardAbsorbSettings rewardAbsorbSettings;

    [Header("툴팁 이름 폰트 설정")]
    [SerializeField] private TMP_FontAsset tooltipNameFontAsset;
    [SerializeField] private float tooltipNameFontSize = 34f;
    [SerializeField] private FontStyles tooltipNameFontStyle = FontStyles.Bold;
    [SerializeField] private Color tooltipNameFontColor = Color.white;
    [SerializeField] private TextAlignmentOptions tooltipNameAlignment = TextAlignmentOptions.Top;

    [Header("툴팁 설명 폰트 설정")]
    [SerializeField] private TMP_FontAsset tooltipDescriptionFontAsset;
    [SerializeField] private float tooltipDescriptionFontSize = 28f;
    [SerializeField] private FontStyles tooltipDescriptionFontStyle = FontStyles.Normal;
    [SerializeField] private Color tooltipDescriptionFontColor = new Color(0.92f, 0.92f, 0.92f, 1f);
    [SerializeField] private TextAlignmentOptions tooltipDescriptionAlignment = TextAlignmentOptions.Top;

    private Roundmanager roundManager;
    private RelicRoundData currentData;

    private Vector3 chestInitialLocalPos;
    private Quaternion chestInitialLocalRot;
    private Vector3 itemInitialLocalPos;

    private bool isSequenceRunning;
    private bool isChestOpened;
    private bool isHovering;
    private bool rewardGranted;
    private float hoverStartTime;

    private Sprite grantedRewardSprite;
    private bool clickBound;
    private bool itemInteractBound;
    private bool isRewardPointerHover;
    private string grantedRewardName;
    private string grantedRewardDescription;
    private bool wasHoveringByMouse;
    private bool isAbsorbSequenceRunning;
    private bool hasPendingReward;
    private RewardCandidate pendingReward;

    void Awake()
    {
        EnsureRuntimeBindings();

        if (chestSettings.chestTransform != null)
        {
            chestInitialLocalPos = chestSettings.chestTransform.localPosition;
            chestInitialLocalRot = chestSettings.chestTransform.localRotation;
        }

        if (itemSettings.itemTransform != null)
            itemInitialLocalPos = itemSettings.itemTransform.localPosition;

        if (itemSettings.itemObject != null)
            itemSettings.itemObject.SetActive(false);
    }

    void OnEnable()
    {
        EnsureRuntimeBindings();
        ResetStageForEntry();
    }

    public void BeginRelic(RelicRoundData data, Roundmanager manager)
    {
        currentData = data;
        roundManager = manager;
        ResetStageForEntry();
    }

    void ResetStageForEntry()
    {
        EnsureRuntimeBindings();
        PrepareStageVisualState();
        HideTooltipPopup();
    }

    public void OnChestClicked()
    {
        if (isSequenceRunning || isChestOpened)
            return;

        StartCoroutine(PlayChestOpenSequence());
    }

    void EnsureRuntimeBindings()
    {
        EnsureTooltipPopupBindings();
        ApplyTooltipTextStyle();

        if (chestSettings.chestButton == null && chestSettings.chestImage != null)
            chestSettings.chestButton = chestSettings.chestImage.GetComponent<Button>();

        if (chestSettings.chestTransform == null)
        {
            if (chestSettings.chestButton != null)
                chestSettings.chestTransform = chestSettings.chestButton.transform as RectTransform;
            else if (chestSettings.chestImage != null)
                chestSettings.chestTransform = chestSettings.chestImage.transform as RectTransform;
        }

        if (chestSettings.chestImage == null && chestSettings.chestButton != null)
            chestSettings.chestImage = chestSettings.chestButton.GetComponent<Image>();

        if (!clickBound && chestSettings.chestButton != null)
        {
            chestSettings.chestButton.onClick.RemoveListener(OnChestClicked);
            chestSettings.chestButton.onClick.AddListener(OnChestClicked);
            clickBound = true;
        }

        if (!itemInteractBound && itemSettings.itemImage != null)
        {
            itemSettings.itemImage.raycastTarget = true;

            var interact = itemSettings.itemImage.GetComponent<RelicRewardInteractable>();
            if (interact == null)
                interact = itemSettings.itemImage.gameObject.AddComponent<RelicRewardInteractable>();

            interact.onPointerEnter -= OnRewardPointerEnter;
            interact.onPointerEnter += OnRewardPointerEnter;
            interact.onPointerExit -= OnRewardPointerExit;
            interact.onPointerExit += OnRewardPointerExit;
            interact.onPointerClick -= OnRewardPointerClick;
            interact.onPointerClick += OnRewardPointerClick;

            itemInteractBound = true;
        }
    }

    public void ReturnToMapAfterReward()
    {
        if (!isChestOpened)
            return;

        isHovering = false;
        isRewardPointerHover = false;
        wasHoveringByMouse = false;
        HideTooltipPopup();

        if (roundManager != null)
        {
            roundManager.ReturnToMap();
            return;
        }

        if (GameStateController.Instance != null)
            GameStateController.Instance.ShowMap();
    }

    void PrepareStageVisualState()
    {
        isSequenceRunning = false;
        isChestOpened = false;
        isHovering = false;
        rewardGranted = false;
        isRewardPointerHover = false;
        grantedRewardName = string.Empty;
        grantedRewardDescription = string.Empty;
        isAbsorbSequenceRunning = false;
        hasPendingReward = false;
        pendingReward = default;

        if (chestSettings.chestTransform != null)
        {
            chestSettings.chestTransform.localPosition = chestInitialLocalPos;
            chestSettings.chestTransform.localRotation = chestInitialLocalRot;
        }

        if (chestSettings.chestImage != null && chestSettings.closedChestSprite != null)
            chestSettings.chestImage.sprite = chestSettings.closedChestSprite;

        if (chestSettings.chestButton != null)
            chestSettings.chestButton.interactable = true;
        if (chestSettings.chestImage != null)
            chestSettings.chestImage.raycastTarget = true;

        if (itemSettings.itemTransform != null)
        {
            itemSettings.itemTransform.localPosition = itemInitialLocalPos;
            itemSettings.itemTransform.localScale = Vector3.zero;
        }

        if (itemSettings.itemObject != null)
            itemSettings.itemObject.SetActive(false);

        if (itemSettings.itemImage != null)
            itemSettings.itemImage.raycastTarget = true;

        wasHoveringByMouse = false;
        HideTooltipPopup();
    }

    System.Collections.IEnumerator PlayChestOpenSequence()
    {
        isSequenceRunning = true;

        // 1) Chest shake to build anticipation.
        float duration = Mathf.Max(0f, chestSettings.shakeDuration);
        float freq = Mathf.Max(0f, chestSettings.shakeFrequency);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float phase = elapsed * freq;

            if (chestSettings.chestTransform != null)
            {
                float xOffset = Mathf.Sin(phase) * chestSettings.shakePositionAmplitude;
                float zRot = Mathf.Sin(phase * 1.3f) * chestSettings.shakeRotationAmplitude;

                chestSettings.chestTransform.localPosition = chestInitialLocalPos + new Vector3(xOffset, 0f, 0f);
                chestSettings.chestTransform.localRotation = chestInitialLocalRot * Quaternion.Euler(0f, 0f, zRot);
            }

            yield return null;
        }

        if (chestSettings.chestTransform != null)
        {
            chestSettings.chestTransform.localPosition = chestInitialLocalPos;
            chestSettings.chestTransform.localRotation = chestInitialLocalRot;
        }

        // 2) Swap chest sprite from closed to opened.
        if (chestSettings.chestImage != null && chestSettings.openedChestSprite != null)
            chestSettings.chestImage.sprite = chestSettings.openedChestSprite;
        if (chestSettings.chestButton != null)
            chestSettings.chestButton.interactable = false;
        if (chestSettings.chestImage != null)
            chestSettings.chestImage.raycastTarget = false;

        // Reward is granted on chest click, not on stage entry.
        if (!rewardGranted)
        {
            rewardGranted = TryPickRewardAndPreview(out grantedRewardSprite);

            if (itemSettings.itemImage != null)
            {
                var spriteToShow = grantedRewardSprite != null ? grantedRewardSprite : fallbackRewardSprite;
                itemSettings.itemImage.sprite = spriteToShow;
                itemSettings.itemImage.enabled = spriteToShow != null;
            }
        }

        // 3) Item reveal from chest position with curve-driven rise.
        if (itemSettings.itemObject != null)
            itemSettings.itemObject.SetActive(true);
        if (itemSettings.itemTransform != null)
            itemSettings.itemTransform.SetAsLastSibling();
        if (itemSettings.itemImage != null)
            itemSettings.itemImage.raycastTarget = true;

        if (itemSettings.itemTransform != null)
        {
            itemSettings.itemTransform.localPosition = itemInitialLocalPos;
            itemSettings.itemTransform.localScale = Vector3.zero;
        }

        float riseDuration = Mathf.Max(0.01f, itemSettings.riseDuration);
        AnimationCurve riseCurve = itemSettings.riseCurve != null && itemSettings.riseCurve.length > 0
            ? itemSettings.riseCurve
            : AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        AnimationCurve scaleCurve = itemSettings.scaleCurve != null && itemSettings.scaleCurve.length > 0
            ? itemSettings.scaleCurve
            : AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        float riseElapsed = 0f;
        while (riseElapsed < riseDuration)
        {
            riseElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(riseElapsed / riseDuration);

            if (itemSettings.itemTransform != null)
            {
                float y = itemInitialLocalPos.y + itemSettings.riseHeight * riseCurve.Evaluate(t);
                float s = Mathf.Clamp01(scaleCurve.Evaluate(t));

                itemSettings.itemTransform.localPosition = new Vector3(itemInitialLocalPos.x, y, itemInitialLocalPos.z);
                itemSettings.itemTransform.localScale = Vector3.one * s;
            }

            yield return null;
        }

        if (itemSettings.itemTransform != null)
        {
            itemSettings.itemTransform.localPosition = new Vector3(
                itemInitialLocalPos.x,
                itemInitialLocalPos.y + itemSettings.riseHeight,
                itemInitialLocalPos.z);
            itemSettings.itemTransform.localScale = Vector3.one;
        }

        // 4) Start sine-wave hovering.
        hoverStartTime = Time.time;
        isHovering = true;
        isChestOpened = true;
        isSequenceRunning = false;
    }

    void Update()
    {
        if (!isHovering || itemSettings.itemTransform == null)
            return;

        float speed = Mathf.Max(0f, itemSettings.hoverSpeed);
        float amp = Mathf.Max(0f, itemSettings.hoverAmplitude);

        float sine = Mathf.Sin((Time.time - hoverStartTime) * speed);
        float y = itemInitialLocalPos.y + itemSettings.riseHeight + sine * amp;
        itemSettings.itemTransform.localPosition = new Vector3(itemInitialLocalPos.x, y, itemInitialLocalPos.z);

        float scaleMul = isRewardPointerHover
            ? Mathf.Max(1f, itemHoverScaleMultiplier)
            : 1f;
        float lerpSpeed = Mathf.Max(0f, itemHoverScaleLerpSpeed);
        Vector3 targetScale = Vector3.one * scaleMul;
        itemSettings.itemTransform.localScale = Vector3.Lerp(itemSettings.itemTransform.localScale, targetScale, Time.deltaTime * lerpSpeed);

        SyncHoverStateFromMousePosition();
    }

    bool TryPickRewardAndPreview(out Sprite rewardSprite)
    {
        rewardSprite = null;

        if (RelicManager.Instance == null)
        {
            Debug.LogWarning("[RelicStage] RelicManager.Instance가 없어 유물 선택을 건너뜁니다.");
            hasPendingReward = false;
            return false;
        }

        var manager = RelicManager.Instance;
        List<RewardCandidate> candidates = BuildCandidates(manager);

        if (candidates.Count == 0)
        {
            Debug.LogWarning("[RelicStage] 지급 가능한 유물 후보가 없습니다.");
            grantedRewardName = "신규 유물 없음";
            grantedRewardDescription = "이미 획득한 유물을 제외하면 남은 유물이 없습니다.";
            hasPendingReward = false;
            return false;
        }

        var picked = candidates[Random.Range(0, candidates.Count)];
        pendingReward = picked;
        hasPendingReward = true;
        grantedRewardName = string.IsNullOrWhiteSpace(picked.displayName) ? "유물" : picked.displayName;
        grantedRewardDescription = string.IsNullOrWhiteSpace(picked.description)
            ? DefaultSpriteOnlyRelicDescription
            : picked.description;
        rewardSprite = picked.sprite != null
            ? picked.sprite
            : (picked.relic != null ? picked.relic.icon : null);
        return true;
    }

    void GrantPendingRewardIfAny()
    {
        if (!hasPendingReward)
            return;

        if (RelicManager.Instance == null)
        {
            Debug.LogWarning("[RelicStage] RelicManager.Instance가 없어 유물 최종 지급을 건너뜁니다.");
            hasPendingReward = false;
            return;
        }

        var manager = RelicManager.Instance;
        var picked = pendingReward;

        if (picked.isSpriteOnly)
        {
            if (picked.sprite != null)
            {
                if (manager.TryAddSpriteOnlyRelic(picked.sprite, out var granted))
                    grantedRewardSprite = granted != null && granted.icon != null ? granted.icon : picked.sprite;
                else
                    grantedRewardSprite = picked.sprite;
            }
        }
        else if (picked.relic != null)
        {
            if (!manager.HasRelicId(picked.relic.id))
                manager.AddRelic(picked.relic);

            grantedRewardSprite = picked.sprite != null
                ? picked.sprite
                : (picked.relic.icon != null ? picked.relic.icon : grantedRewardSprite);
        }

        hasPendingReward = false;
    }

    List<RewardCandidate> BuildCandidates(RelicManager manager)
    {
        var result = new List<RewardCandidate>();

        var preferredEffects = currentData != null ? currentData.candidateRelics : null;

        var defs = manager.RelicDefinitions;
        for (int i = 0; i < defs.Count; i++)
        {
            var def = defs[i];
            if (def == null || string.IsNullOrWhiteSpace(def.id)) continue;
            if (def.icon == null) continue;

            if (preferredEffects != null && preferredEffects.Count > 0)
            {
                bool matched = false;
                for (int p = 0; p < preferredEffects.Count; p++)
                {
                    if (def.effect == preferredEffects[p])
                    {
                        matched = true;
                        break;
                    }
                }
                if (!matched) continue;
            }

            if (manager.HasRelicId(def.id))
                continue;

            result.Add(new RewardCandidate
            {
                isSpriteOnly = false,
                relic = def,
                sprite = def.icon,
                displayName = def.displayName,
                description = def.description,
            });
        }

        for (int i = 0; i < spriteOnlyRelics.Count; i++)
        {
            var entry = spriteOnlyRelics[i];
            var sprite = entry.sprite;
            if (sprite == null) continue;

            string id = BuildSpriteRelicId(sprite);
            if (manager.HasRelicId(id))
                continue;

            result.Add(new RewardCandidate
            {
                isSpriteOnly = true,
                relic = null,
                sprite = sprite,
                displayName = !string.IsNullOrWhiteSpace(entry.displayName) ? entry.displayName : sprite.name,
                description = !string.IsNullOrWhiteSpace(entry.description) ? entry.description : DefaultSpriteOnlyRelicDescription,
            });
        }

        return result;
    }

    void OnRewardPointerEnter()
    {
        if (!isChestOpened)
            return;

        isRewardPointerHover = true;
        UpdateRewardDescriptionText();
    }

    void OnRewardPointerExit()
    {
        if (!isChestOpened)
            return;

        isRewardPointerHover = false;
        HideTooltipPopup();
    }

    void OnRewardPointerClick()
    {
        if (!isChestOpened || isAbsorbSequenceRunning)
            return;

        StartCoroutine(PlayRewardAbsorbAndReturn());
    }

    IEnumerator PlayRewardAbsorbAndReturn()
    {
        isAbsorbSequenceRunning = true;
        isHovering = false;
        isRewardPointerHover = false;
        wasHoveringByMouse = false;
        HideTooltipPopup();

        Sprite rewardSprite = null;
        if (itemSettings.itemImage != null)
            rewardSprite = itemSettings.itemImage.sprite;
        if (rewardSprite == null)
            rewardSprite = grantedRewardSprite != null ? grantedRewardSprite : fallbackRewardSprite;

        bool played = false;
        if (rewardSprite != null)
            played = TryCreateAndPlayAbsorbFx(rewardSprite);

        if (!played && itemSettings.itemObject != null)
            itemSettings.itemObject.SetActive(false);

        if (played)
            yield return StartCoroutine(AnimateFlyingImageToSlot());

        GrantPendingRewardIfAny();
        EnableTargetSlotImage();
        isAbsorbSequenceRunning = false;
        ReturnToMapAfterReward();
    }

    RectTransform flyingImageRect;
    Image flyingImage;
    Vector2 flyStartLocalPos;
    Vector2 flyEndLocalPos;

    bool TryCreateAndPlayAbsorbFx(Sprite sprite)
    {
        if (sprite == null)
            return false;

        Canvas canvas = ResolveCanvas();
        RectTransform canvasRect = rewardAbsorbSettings.uiCanvasRect;
        if (canvasRect == null && canvas != null)
            canvasRect = canvas.transform as RectTransform;

        if (canvasRect == null || rewardAbsorbSettings.targetSlotRect == null)
            return false;

        Image runtimeFlyImage = null;
        if (rewardAbsorbSettings.flyingImageTemplate != null)
        {
            runtimeFlyImage = Instantiate(rewardAbsorbSettings.flyingImageTemplate, canvasRect);
            runtimeFlyImage.name = "FlyingRelicImage(Clone)";
        }
        else
        {
            var go = new GameObject("FlyingRelicImage(Clone)", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(canvasRect, false);
            runtimeFlyImage = go.GetComponent<Image>();
        }

        if (runtimeFlyImage == null)
            return false;

        runtimeFlyImage.sprite = sprite;
        runtimeFlyImage.enabled = true;
        runtimeFlyImage.raycastTarget = false;
        runtimeFlyImage.color = Color.white;
        flyingImage = runtimeFlyImage;
        flyingImageRect = runtimeFlyImage.rectTransform;
        flyingImageRect.SetAsLastSibling();

        Vector2 fromLocal;
        Vector2 toLocal;
        if (!TryResolveLocalPositions(canvasRect, canvas, out fromLocal, out toLocal))
        {
            Destroy(runtimeFlyImage.gameObject);
            flyingImageRect = null;
            flyingImage = null;
            return false;
        }

        flyStartLocalPos = fromLocal;
        flyEndLocalPos = toLocal;

        flyingImageRect.anchorMin = new Vector2(0.5f, 0.5f);
        flyingImageRect.anchorMax = new Vector2(0.5f, 0.5f);
        flyingImageRect.pivot = new Vector2(0.5f, 0.5f);
        flyingImageRect.anchoredPosition = flyStartLocalPos;

        if (itemSettings.itemTransform != null)
            flyingImageRect.sizeDelta = itemSettings.itemTransform.rect.size;
        else if (runtimeFlyImage.sprite != null)
            flyingImageRect.sizeDelta = new Vector2(runtimeFlyImage.sprite.rect.width, runtimeFlyImage.sprite.rect.height);
        else
            flyingImageRect.sizeDelta = new Vector2(96f, 96f);

        float startScale = Mathf.Max(0.01f, rewardAbsorbSettings.startScale <= 0f ? 1f : rewardAbsorbSettings.startScale);
        flyingImageRect.localScale = Vector3.one * startScale;

        if (itemSettings.itemObject != null)
            itemSettings.itemObject.SetActive(false);

        return true;
    }

    bool TryResolveLocalPositions(RectTransform canvasRect, Canvas canvas, out Vector2 fromLocal, out Vector2 toLocal)
    {
        fromLocal = Vector2.zero;
        toLocal = Vector2.zero;

        Camera cam = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;

        Vector3 sourceWorld = itemSettings.itemTransform != null
            ? itemSettings.itemTransform.position
            : transform.position;
        Vector3 sourceScreen = RectTransformUtility.WorldToScreenPoint(cam, sourceWorld);

        Vector3 targetScreen;
        if (rewardAbsorbSettings.targetSlotRect != null)
        {
            Vector3 targetWorld = rewardAbsorbSettings.targetSlotRect.position;
            targetScreen = RectTransformUtility.WorldToScreenPoint(cam, targetWorld);
        }
        else
        {
            // Fallback target near top-left so the effect is still visible when target reference is missing.
            Vector2 fallback = new Vector2(80f, Screen.height - 80f);
            targetScreen = new Vector3(fallback.x, fallback.y, 0f);
        }

        bool okFrom = RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sourceScreen, cam, out fromLocal);
        bool okTo = RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, targetScreen, cam, out toLocal);
        if (!okFrom && itemSettings.itemTransform != null)
        {
            // UI source fallback: convert world point directly from the item rect transform.
            okFrom = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                RectTransformUtility.WorldToScreenPoint(cam, itemSettings.itemTransform.position),
                cam,
                out fromLocal);
        }

        return okFrom && okTo;
    }

    IEnumerator AnimateFlyingImageToSlot()
    {
        if (flyingImageRect == null)
            yield break;

        float duration = Mathf.Max(0.01f, rewardAbsorbSettings.flyDuration <= 0f ? 0.55f : rewardAbsorbSettings.flyDuration);
        float startScale = Mathf.Max(0.01f, rewardAbsorbSettings.startScale <= 0f ? 1f : rewardAbsorbSettings.startScale);
        float endScale = Mathf.Max(0.01f, rewardAbsorbSettings.endScale <= 0f ? 0.5f : rewardAbsorbSettings.endScale);

        AnimationCurve xCurve = rewardAbsorbSettings.xMoveCurve != null && rewardAbsorbSettings.xMoveCurve.length > 0
            ? rewardAbsorbSettings.xMoveCurve
            : AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        AnimationCurve yCurve = rewardAbsorbSettings.yMoveCurve != null && rewardAbsorbSettings.yMoveCurve.length > 0
            ? rewardAbsorbSettings.yMoveCurve
            : AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        AnimationCurve arcCurve = rewardAbsorbSettings.arcCurve != null && rewardAbsorbSettings.arcCurve.length > 0
            ? rewardAbsorbSettings.arcCurve
            : AnimationCurve.EaseInOut(0f, 0f, 1f, 0f);

        float arcHeight = rewardAbsorbSettings.arcHeight;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            float xT = Mathf.Clamp01(xCurve.Evaluate(t));
            float yT = Mathf.Clamp01(yCurve.Evaluate(t));
            float arc = arcCurve.Evaluate(t) * arcHeight;

            float x = Mathf.LerpUnclamped(flyStartLocalPos.x, flyEndLocalPos.x, xT);
            float y = Mathf.LerpUnclamped(flyStartLocalPos.y, flyEndLocalPos.y, yT) + arc;
            flyingImageRect.anchoredPosition = new Vector2(x, y);

            float s = Mathf.Lerp(startScale, endScale, t);
            flyingImageRect.localScale = Vector3.one * s;

            yield return null;
        }

        flyingImageRect.anchoredPosition = flyEndLocalPos;
        flyingImageRect.localScale = Vector3.one * endScale;

        if (flyingImage != null)
            Destroy(flyingImage.gameObject);

        flyingImageRect = null;
        flyingImage = null;
    }

    void EnableTargetSlotImage()
    {
        var slotImage = rewardAbsorbSettings.targetSlotImage;
        if (slotImage == null)
            return;

        if (!slotImage.gameObject.activeSelf)
            slotImage.gameObject.SetActive(true);

        if (!slotImage.enabled)
            slotImage.enabled = true;

        if (slotImage.sprite == null)
            slotImage.sprite = grantedRewardSprite != null ? grantedRewardSprite : fallbackRewardSprite;
    }

    Canvas ResolveCanvas()
    {
        if (rewardAbsorbSettings.uiCanvasRect != null)
            return rewardAbsorbSettings.uiCanvasRect.GetComponentInParent<Canvas>();

        if (itemSettings.itemImage != null && itemSettings.itemImage.canvas != null)
            return itemSettings.itemImage.canvas;

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
            return canvas;

        return FindFirstObjectByType<Canvas>();
    }

    void UpdateRewardDescriptionText()
    {
        if (tooltipNameText == null || tooltipDescriptionText == null)
            return;

        string nameText = string.IsNullOrWhiteSpace(grantedRewardName) ? "유물" : grantedRewardName;
        string descText = string.IsNullOrWhiteSpace(grantedRewardDescription) ? DefaultSpriteOnlyRelicDescription : grantedRewardDescription;
        tooltipNameText.text = nameText;
        tooltipDescriptionText.text = descText;
        ShowTooltipPopup();
    }

    void SyncHoverStateFromMousePosition()
    {
        if (!isChestOpened || itemSettings.itemTransform == null || itemSettings.itemImage == null)
            return;

        var canvas = itemSettings.itemImage.canvas;
        Camera cam = null;
        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            cam = canvas.worldCamera;

        bool isInside = RectTransformUtility.RectangleContainsScreenPoint(
            itemSettings.itemTransform,
            Input.mousePosition,
            cam);

        if (isInside == wasHoveringByMouse)
            return;

        wasHoveringByMouse = isInside;
        if (isInside) OnRewardPointerEnter();
        else OnRewardPointerExit();
    }

    void EnsureTooltipPopupBindings()
    {
        if (tooltipPopupRoot != null && tooltipNameText != null && tooltipDescriptionText != null)
        {
            if (tooltipPopupBackground == null)
                tooltipPopupBackground = tooltipPopupRoot.GetComponent<Image>();
            return;
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
            return;

        if (tooltipPopupRoot == null)
        {
            var popupGo = new GameObject("RelicTooltipPopup", typeof(RectTransform), typeof(Image));
            popupGo.transform.SetParent(canvas.transform, false);
            tooltipPopupRoot = popupGo.GetComponent<RectTransform>();
            tooltipPopupBackground = popupGo.GetComponent<Image>();
        }

        tooltipPopupRoot.anchorMin = new Vector2(0.5f, 1f);
        tooltipPopupRoot.anchorMax = new Vector2(0.5f, 1f);
        tooltipPopupRoot.pivot = new Vector2(0.5f, 1f);
        tooltipPopupRoot.anchoredPosition = tooltipPopupAnchoredPosition;
        tooltipPopupRoot.sizeDelta = tooltipPopupSize;

        if (tooltipPopupBackground != null)
        {
            tooltipPopupBackground.color = tooltipPopupBackgroundColor;
            tooltipPopupBackground.raycastTarget = false;
        }

        if (tooltipNameText == null)
        {
            var nameGo = new GameObject("Name", typeof(RectTransform));
            nameGo.transform.SetParent(tooltipPopupRoot, false);
            tooltipNameText = nameGo.AddComponent<TextMeshProUGUI>();
            tooltipNameText.raycastTarget = false;
        }

        if (tooltipDescriptionText == null)
        {
            var descGo = new GameObject("Description", typeof(RectTransform));
            descGo.transform.SetParent(tooltipPopupRoot, false);
            tooltipDescriptionText = descGo.AddComponent<TextMeshProUGUI>();
            tooltipDescriptionText.raycastTarget = false;
        }

        var nameRect = tooltipNameText.transform as RectTransform;
        if (nameRect != null)
        {
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0.5f, 1f);
            nameRect.offsetMin = new Vector2(tooltipPopupPadding.x, -72f);
            nameRect.offsetMax = new Vector2(-tooltipPopupPadding.x, -tooltipPopupPadding.y);
        }

        var descRect = tooltipDescriptionText.transform as RectTransform;
        if (descRect != null)
        {
            descRect.anchorMin = new Vector2(0f, 0f);
            descRect.anchorMax = new Vector2(1f, 1f);
            descRect.pivot = new Vector2(0.5f, 1f);
            descRect.offsetMin = new Vector2(tooltipPopupPadding.x, tooltipPopupPadding.y);
            descRect.offsetMax = new Vector2(-tooltipPopupPadding.x, -78f);
        }

        HideTooltipPopup();
    }

    void ApplyTooltipTextStyle()
    {
        if (tooltipPopupBackground != null)
            tooltipPopupBackground.color = tooltipPopupBackgroundColor;

        if (tooltipNameText != null)
        {
            if (tooltipNameFontAsset != null)
                tooltipNameText.font = tooltipNameFontAsset;

            tooltipNameText.fontSize = Mathf.Max(1f, tooltipNameFontSize);
            tooltipNameText.fontStyle = tooltipNameFontStyle;
            tooltipNameText.color = tooltipNameFontColor;
            tooltipNameText.alignment = tooltipNameAlignment;
        }

        if (tooltipDescriptionText != null)
        {
            if (tooltipDescriptionFontAsset != null)
                tooltipDescriptionText.font = tooltipDescriptionFontAsset;

            tooltipDescriptionText.fontSize = Mathf.Max(1f, tooltipDescriptionFontSize);
            tooltipDescriptionText.fontStyle = tooltipDescriptionFontStyle;
            tooltipDescriptionText.color = tooltipDescriptionFontColor;
            tooltipDescriptionText.alignment = tooltipDescriptionAlignment;
        }
    }

    void ShowTooltipPopup()
    {
        if (tooltipPopupRoot == null)
            return;

        if (!tooltipPopupRoot.gameObject.activeSelf)
            tooltipPopupRoot.gameObject.SetActive(true);

        tooltipPopupRoot.SetAsLastSibling();
    }

    void HideTooltipPopup()
    {
        if (tooltipPopupRoot != null)
            tooltipPopupRoot.gameObject.SetActive(false);
    }

    static string BuildSpriteRelicId(Sprite sprite)
    {
        if (sprite == null || string.IsNullOrWhiteSpace(sprite.name))
            return "sprite_relic";

        char[] chars = sprite.name.Trim().ToLowerInvariant().ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            bool allowed = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_' || c == '-';
            if (!allowed) chars[i] = '_';
        }

        return $"sprite_{new string(chars)}";
    }

    struct RewardCandidate
    {
        public bool isSpriteOnly;
        public RelicDef relic;
        public Sprite sprite;
        public string displayName;
        public string description;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!autoCollectSpritesInEditor)
            return;

        if (spriteOnlyRelics != null && spriteOnlyRelics.Count > 0)
            return;

        CollectSpriteOnlyRelicsFromFolder();
    }

    [ContextMenu("Collect Sprite-Only Relics")]
    void CollectSpriteOnlyRelicsFromFolder()
    {
        var existingById = new Dictionary<string, SpriteOnlyRelicEntry>();
        for (int i = 0; i < spriteOnlyRelics.Count; i++)
        {
            var entry = spriteOnlyRelics[i];
            if (entry.sprite == null) continue;

            string id = BuildSpriteRelicId(entry.sprite);
            if (!existingById.ContainsKey(id))
                existingById.Add(id, entry);
        }

        spriteOnlyRelics.Clear();

        if (string.IsNullOrWhiteSpace(relicSpriteFolderPath))
            return;

        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Sprite", new[] { relicSpriteFolderPath });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
            var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
            {
                string id = BuildSpriteRelicId(sprite);
                if (existingById.TryGetValue(id, out var existing))
                {
                    existing.sprite = sprite;
                    if (string.IsNullOrWhiteSpace(existing.displayName))
                        existing.displayName = sprite.name;
                    if (string.IsNullOrWhiteSpace(existing.description))
                        existing.description = DefaultSpriteOnlyRelicDescription;
                    spriteOnlyRelics.Add(existing);
                }
                else
                {
                    spriteOnlyRelics.Add(new SpriteOnlyRelicEntry
                    {
                        sprite = sprite,
                        displayName = sprite.name,
                        description = DefaultSpriteOnlyRelicDescription,
                    });
                }
            }
        }
    }
#endif
}
