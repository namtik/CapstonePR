using Battle.Relic;
using Battle.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;

// 유물 보상 상자 연출 및 유물 지급 스테이지 컨트롤러
public class RelicStageController : MonoBehaviour
{
    private const string DefaultSpriteOnlyRelicDescription = "개발중인 유물"; // 스프라이트 전용 유물 기본 설명

    // 상점에 노출할 렐릭 후보 정보
    public struct ShopRelicCandidate
    {
        public bool isSpriteOnly; // 스프라이트 전용 여부
        public RelicSO relic; // 렐릭 정의
        public Sprite sprite; // 스프라이트
        public string displayName; // 표시 이름
        public string description; // 설명
    }

    // 스프라이트 기반(효과 미구현) 유물 인스펙터 엔트리
    [System.Serializable]
    private struct SpriteOnlyRelicEntry
    {
        public Sprite sprite; // 스프라이트
        public string displayName; // 표시 이름
        [TextArea(2, 4)] public string description; // 설명
    }

    // 상자 연출 설정
    [System.Serializable]
    private struct ChestSettings
    {
        [Header("References")]
        public Button chestButton; // 상자 버튼
        public RectTransform chestTransform; // 상자 트랜스폼
        public Image chestImage; // 상자 이미지

        [Header("Sprites")]
        public Sprite closedChestSprite; // 닫힌 상자 스프라이트
        public Sprite openedChestSprite; // 열린 상자 스프라이트

        [Header("Shake")]
        public float shakeDuration; // 흔들림 지속 시간
        public float shakePositionAmplitude; // 흔들림 위치 진폭
        public float shakeRotationAmplitude; // 흔들림 회전 진폭
        public float shakeFrequency; // 흔들림 주파수
    }

    // 보상 아이템 등장 연출 설정
    [System.Serializable]
    private struct ItemSettings
    {
        [Header("References")]
        public GameObject itemObject; // 아이템 오브젝트
        public RectTransform itemTransform; // 아이템 트랜스폼
        public Image itemImage; // 아이템 이미지

        [Header("Rise")]
        public float riseDuration; // 상승 지속 시간
        public float riseHeight; // 상승 높이
        public AnimationCurve riseCurve; // 상승 곡선
        public AnimationCurve scaleCurve; // 스케일 곡선

        [Header("Hover")]
        public float hoverSpeed; // 부유 속도
        public float hoverAmplitude; // 부유 진폭
    }

    [Header("툴팁 팝업")]
    [Tooltip("호버 팝업 배경. 비우면 아래 배경색 사각형을 쓴다.")]
    [SerializeField] private Sprite tooltipPopupSprite;
    [Tooltip("스프라이트가 없을 때 채우는 색.")]
    [SerializeField] private Color tooltipPopupBackgroundColor = new Color(0f, 0f, 0f, 0.78f);
    [Tooltip("스프라이트가 있을 때 곱하는 색. 원본 그대로면 흰색.")]
    [SerializeField] private Color tooltipPopupSpriteColor = Color.white;
    [SerializeField] private Image.Type tooltipPopupImageType = Image.Type.Sliced;
    [SerializeField] private Vector2 tooltipPopupAnchoredPosition = new Vector2(0f, -60f); // 툴팁 앵커 위치
    [SerializeField] private Vector2 tooltipPopupSize = new Vector2(820f, 220f); // 툴팁 크기
    [SerializeField] private Vector2 tooltipPopupPadding = new Vector2(24f, 18f); // 툴팁 패딩

    RectTransform tooltipPopupRoot; // 런타임에 만든 팝업
    Image tooltipPopupBackground;
    TextMeshProUGUI tooltipNameText;
    TextMeshProUGUI tooltipDescriptionText;

    [Header("상자 관련 설정")]
    [SerializeField] private ChestSettings chestSettings; // 상자 연출 설정

    [Header("아이템 관련 설정")]
    [SerializeField] private ItemSettings itemSettings; // 아이템 연출 설정

    [Header("개발중 유물 이미지")]
    [SerializeField] private string relicSpriteFolderPath = "Assets/_Project/Sprites/Relic"; // 유물 스프라이트 폴더 경로
    [SerializeField] private bool autoCollectSpritesInEditor = true; // 에디터에서 스프라이트 자동 수집 여부
    [SerializeField] private List<SpriteOnlyRelicEntry> spriteOnlyRelics = new List<SpriteOnlyRelicEntry>(); // 스프라이트 전용 유물 목록

    [Header("유물 호리병(10018) 설정")]
    [Tooltip("유물 호리병 보유(또는 이번 상자에서 호리병 획득) 시 한 라운드에 지급하는 유물 수. 상자에는 그중 1개를 보여주고, 클릭 시 제시된 유물을 모두 지급한다.")]
    [SerializeField] private int grantAllRelicMaxCount = 3; // 유물 호리병 라운드 획득 상한

    [Header("획득 유물 상호작용")]
    [SerializeField] private float itemHoverScaleMultiplier = 1.08f; // 호버 시 확대 배율
    [SerializeField] private float itemHoverScaleLerpSpeed = 10f; // 호버 스케일 보간 속도
    [SerializeField] private Sprite fallbackRewardSprite; // 보상 폴백 스프라이트

    [Header("받지 않기 버튼")]
    [SerializeField] private RewardSkipButtonStyle declineButtonStyle = new RewardSkipButtonStyle
    {
        label = "받지 않기",
        anchoredPosition = new Vector2(0f, -420f),
    };

    [Header("툴팁 제목")]
    [SerializeField] private TMP_FontAsset tooltipNameFontAsset; // 제목 폰트
    [Tooltip("TMP 폰트 프리셋(머티리얼). 비우면 폰트 기본값.")]
    [SerializeField] private Material tooltipNameFontPreset;
    [SerializeField] private float tooltipNameFontSize = 34f; // 제목 크기
    [SerializeField] private FontStyles tooltipNameFontStyle = FontStyles.Bold; // 제목 스타일
    [SerializeField] private Color tooltipNameFontColor = Color.white; // 제목 색
    [SerializeField] private TextAlignmentOptions tooltipNameAlignment = TextAlignmentOptions.Top; // 제목 정렬

    [Header("툴팁 내용")]
    [SerializeField] private TMP_FontAsset tooltipDescriptionFontAsset; // 내용 폰트
    [Tooltip("TMP 폰트 프리셋(머티리얼). 비우면 폰트 기본값.")]
    [SerializeField] private Material tooltipDescriptionFontPreset;
    [SerializeField] private float tooltipDescriptionFontSize = 28f; // 내용 크기
    [SerializeField] private FontStyles tooltipDescriptionFontStyle = FontStyles.Normal; // 내용 스타일
    [SerializeField] private Color tooltipDescriptionFontColor = new Color(0.92f, 0.92f, 0.92f, 1f); // 내용 색
    [SerializeField] private TextAlignmentOptions tooltipDescriptionAlignment = TextAlignmentOptions.Top; // 내용 정렬

    private RoundManager roundManager; // 라운드 매니저 참조
    private RelicRoundData currentData; // 현재 라운드 데이터

    private Vector3 chestInitialLocalPos; // 상자 초기 위치
    private Quaternion chestInitialLocalRot; // 상자 초기 회전
    private Vector3 itemInitialLocalPos; // 아이템 초기 위치

    private bool isSequenceRunning; // 상자 열기 시퀀스 진행 중 여부
    private bool isChestOpened; // 상자 열림 여부
    private bool isHovering; // 아이템 부유 중 여부
    private bool rewardGranted; // 보상 선택 완료 여부
    private float hoverStartTime; // 부유 시작 시각

    private Sprite grantedRewardSprite; // 지급 보상 스프라이트
    private bool clickBound; // 상자 클릭 바인딩 여부
    private bool itemInteractBound; // 아이템 상호작용 바인딩 여부
    private bool isRewardPointerHover; // 보상 포인터 호버 여부
    private string grantedRewardName; // 지급 보상 이름
    private string grantedRewardDescription; // 지급 보상 설명
    private bool wasHoveringByMouse; // 직전 마우스 호버 상태
    private bool isAbsorbSequenceRunning; // 흡수 시퀀스 진행 중 여부
    private bool hasPendingReward; // 대기 중 보상 존재 여부
    private RewardCandidate pendingReward; // 지급 대기 보상(상자에 표시할 대표 유물)
    private readonly List<RewardCandidate> pendingPresentedRelics = new List<RewardCandidate>(); // 호리병 포함 시 함께 지급할 제시 유물
    private Button _declineButton; // 받지 않기 버튼

    // 바인딩 및 상자/아이템 초기 상태 캡처
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

    // 활성화 시 바인딩 및 스테이지 초기화
    void OnEnable()
    {
        EnsureRuntimeBindings();
        ResetStageForEntry();
    }

    // 라운드 데이터/매니저를 받아 렐릭 스테이지 시작
    public void BeginRelic(RelicRoundData data, RoundManager manager)
    {
        currentData = data;
        roundManager = manager;
        ResetStageForEntry();
    }

    // 진입 시 스테이지 시각 상태 초기화
    void ResetStageForEntry()
    {
        EnsureRuntimeBindings();
        PrepareStageVisualState();
        HideTooltipPopup();
    }

    // 상자 클릭 시 열기 시퀀스 시작
    public void OnChestClicked()
    {
        if (isSequenceRunning || isChestOpened)
            return;

        StartCoroutine(PlayChestOpenSequence());
    }

    // 상자/아이템 참조 및 이벤트 리스너 바인딩
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

    // 보상 획득 후 맵으로 복귀
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

    // 상자/아이템/툴팁을 초기 시각 상태로 리셋
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
        pendingPresentedRelics.Clear();

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
        SetDeclineButtonVisible(false);
    }

    // 상자 흔들림 -> 열림 -> 보상 선택 -> 아이템 등장 -> 부유까지의 연출 시퀀스
    System.Collections.IEnumerator PlayChestOpenSequence()
    {
        isSequenceRunning = true;

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

        if (chestSettings.chestImage != null && chestSettings.openedChestSprite != null)
            chestSettings.chestImage.sprite = chestSettings.openedChestSprite;
        if (chestSettings.chestButton != null)
            chestSettings.chestButton.interactable = false;
        if (chestSettings.chestImage != null)
            chestSettings.chestImage.raycastTarget = false;

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

        hoverStartTime = Time.time;
        isHovering = true;
        isChestOpened = true;
        isSequenceRunning = false;
        SetDeclineButtonVisible(true);
    }

    // 부유 애니메이션 및 호버 스케일/마우스 호버 동기화
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

    // 후보 중 하나를 무작위 선택해 미리보기 정보 설정(아직 지급 전)
    bool TryPickRewardAndPreview(out Sprite rewardSprite)
    {
        rewardSprite = null;

        if (RelicManager.Instance == null)
        {
            Debug.LogWarning("[RelicStage] RelicManager.Instance가 없어 유물 선택을 건너뜁니다.");
            hasPendingReward = false;
            pendingPresentedRelics.Clear();
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
            pendingPresentedRelics.Clear();
            return false;
        }

        RewardCandidate picked = candidates[Random.Range(0, candidates.Count)];
        bool grantAll = manager.HasGrantAllRelicsInStage()
            || (picked.relic != null && picked.relic.GrantsAllRelicsInStage);

        pendingPresentedRelics.Clear();
        if (grantAll)
            FillPresentedRelics(candidates, picked);
        else
            pendingPresentedRelics.Add(picked);

        pendingReward = pendingPresentedRelics[0];
        hasPendingReward = true;
        ApplyPresentedRewardPreview(out rewardSprite);
        return true;
    }

    // 호리병 효과용으로 제시 유물 N장을 고른다. picked가 호리병이면 그걸 포함하고 나머지를 채운다.
    void FillPresentedRelics(List<RewardCandidate> candidates, RewardCandidate picked)
    {
        int maxCount = Mathf.Max(1, grantAllRelicMaxCount);
        bool pickedIsGourd = picked.relic != null && picked.relic.GrantsAllRelicsInStage;

        if (pickedIsGourd)
            pendingPresentedRelics.Add(picked);

        var pool = new List<RewardCandidate>(candidates);
        ShuffleCandidates(pool);
        for (int i = 0; i < pool.Count && pendingPresentedRelics.Count < maxCount; i++)
        {
            RewardCandidate candidate = pool[i];
            if (ContainsPresentedRelic(candidate))
                continue;
            pendingPresentedRelics.Add(candidate);
        }

        if (pendingPresentedRelics.Count == 0)
            pendingPresentedRelics.Add(picked);
    }

    bool ContainsPresentedRelic(RewardCandidate candidate)
    {
        for (int i = 0; i < pendingPresentedRelics.Count; i++)
        {
            RewardCandidate existing = pendingPresentedRelics[i];
            if (candidate.relic != null && existing.relic != null && existing.relic.id == candidate.relic.id)
                return true;
            if (candidate.isSpriteOnly && existing.isSpriteOnly && existing.sprite == candidate.sprite)
                return true;
        }
        return false;
    }

    void ApplyPresentedRewardPreview(out Sprite rewardSprite)
    {
        RewardCandidate shown = pendingPresentedRelics[0];
        rewardSprite = shown.sprite != null
            ? shown.sprite
            : (shown.relic != null ? shown.relic.icon : null);

        // 팝업에는 상자에 보이는 유물 1개의 설명만 띄운다(호리병으로 여러 개를 받아도 목록을 나열하지 않음).
        grantedRewardName = string.IsNullOrWhiteSpace(shown.displayName) ? "유물" : shown.displayName;
        grantedRewardDescription = string.IsNullOrWhiteSpace(shown.description)
            ? DefaultSpriteOnlyRelicDescription
            : shown.description;
        if (pendingPresentedRelics.Count > 1)
            grantedRewardDescription += $"\n(유물 호리병: 이번에 유물 {pendingPresentedRelics.Count}개를 함께 획득)";
    }

    // 대기 중 보상을 실제로 보유 목록에 지급
    void GrantPendingRewardIfAny()
    {
        if (!hasPendingReward)
            return;

        if (RelicManager.Instance == null)
        {
            Debug.LogWarning("[RelicStage] RelicManager.Instance가 없어 유물 최종 지급을 건너뜁니다.");
            hasPendingReward = false;
            pendingPresentedRelics.Clear();
            return;
        }

        var manager = RelicManager.Instance;
        int grantedCount = 0;
        for (int i = 0; i < pendingPresentedRelics.Count; i++)
        {
            if (TryGrantCandidate(manager, pendingPresentedRelics[i], out Sprite grantedSprite))
            {
                grantedCount++;
                if (grantedSprite != null)
                    grantedRewardSprite = grantedSprite;
            }
        }

        Debug.Log($"[유물] 상자 보상 지급 {grantedCount}개" +
                  (pendingPresentedRelics.Count > 1 ? " (유물 호리병)" : string.Empty));

        hasPendingReward = false;
        pendingPresentedRelics.Clear();
    }

    bool TryGrantCandidate(RelicManager manager, RewardCandidate candidate, out Sprite grantedSprite)
    {
        grantedSprite = null;
        if (candidate.isSpriteOnly)
        {
            if (candidate.sprite == null)
                return false;

            if (manager.TryAddSpriteOnlyRelic(candidate.sprite, out var granted))
                grantedSprite = granted != null && granted.icon != null ? granted.icon : candidate.sprite;
            else
                grantedSprite = candidate.sprite;
            return true;
        }

        if (candidate.relic == null)
            return false;

        if (!manager.HasRelicId(candidate.relic.id))
            manager.AddRelic(candidate.relic);

        grantedSprite = candidate.sprite != null
            ? candidate.sprite
            : candidate.relic.icon;
        return true;
    }

    // 후보 목록을 제자리에서 무작위로 섞음(Fisher-Yates)
    static void ShuffleCandidates(List<RewardCandidate> list)
    {
        if (list == null) return;
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }

    // 미보유 실제 유물(RelicSO)로 보상 후보 목록 구성 (상자·상점 공통)
    List<RewardCandidate> BuildCandidates(RelicManager manager)
    {
        var result = new List<RewardCandidate>();

        var preferred = currentData != null ? currentData.candidateRelics : null;

        var defs = manager.RelicDefinitions;
        for (int i = 0; i < defs.Count; i++)
        {
            var def = defs[i];
            if (def == null || string.IsNullOrWhiteSpace(def.id)) continue;
            if (def.icon == null) continue;
            if (!RelicManager.IsOfferable(def)) continue; // 등장 제외(호리병 등)

            // 후보 유물이 지정돼 있으면 그 목록에 든 유물만 등장
            if (preferred != null && preferred.Count > 0 && !preferred.Contains(def))
                continue;

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

        // 주의: 효과 없는 sprite-only placeholder는 더 이상 후보로 넣지 않는다.
        // (진짜 유물과 아이콘이 겹쳐 상자↔상점 중복 등장 문제를 유발했음 — 전부 RelicSO로 대체됨)

        return result;
    }

    // 보상 후보를 상점용 후보 목록으로 변환해 반환
    public List<ShopRelicCandidate> GetShopRelicCandidates(RelicManager manager)
    {
        var result = new List<ShopRelicCandidate>();
        if (manager == null) return result;

        var raw = BuildCandidates(manager);
        for (int i = 0; i < raw.Count; i++)
        {
            RewardCandidate candidate = raw[i];
            result.Add(new ShopRelicCandidate
            {
                isSpriteOnly = candidate.isSpriteOnly,
                relic = candidate.relic,
                sprite = candidate.sprite,
                displayName = candidate.displayName,
                description = candidate.description,
            });
        }

        return result;
    }

    // 보상 위로 포인터 진입 시 툴팁 표시
    void OnRewardPointerEnter()
    {
        if (!isChestOpened)
            return;

        isRewardPointerHover = true;
        UpdateRewardDescriptionText();
    }

    // 보상에서 포인터 이탈 시 툴팁 숨김
    void OnRewardPointerExit()
    {
        if (!isChestOpened)
            return;

        isRewardPointerHover = false;
        HideTooltipPopup();
    }

    // 유물 아이콘을 인벤토리 버튼으로 날린 뒤 지급하고 맵으로 돌아간다
    void OnRewardPointerClick()
    {
        if (!isChestOpened || isAbsorbSequenceRunning || RelicAcquireConfirm.IsOpen)
            return;

        RelicAcquireConfirm.Show(RelicAcquireConfirm.AcquireMessage(grantedRewardName), ConfirmRewardPick);
    }

    // 확인 팝업에서 '예' — 유물 아이콘을 날려 지급
    void ConfirmRewardPick()
    {
        if (!isChestOpened || isAbsorbSequenceRunning || !isActiveAndEnabled)
            return;

        isAbsorbSequenceRunning = true;
        SetDeclineButtonVisible(false);
        isHovering = false;
        isRewardPointerHover = false;
        wasHoveringByMouse = false;
        HideTooltipPopup();

        Sprite sprite = grantedRewardSprite;
        if (sprite == null && itemSettings.itemImage != null)
            sprite = itemSettings.itemImage.sprite;

        Vector2 origin = ScreenCenter(itemSettings.itemTransform);
        Vector2 startSize = ItemScreenSize(itemSettings.itemTransform);

        if (itemSettings.itemObject != null)
            itemSettings.itemObject.SetActive(false);
        if (itemSettings.itemImage != null)
            itemSettings.itemImage.raycastTarget = false;

        StartCoroutine(AbsorbToInventory(sprite, origin, startSize));
    }

    // 스테이지가 연출 중에 꺼져도 보상은 남긴다
    void OnDisable()
    {
        if (!isAbsorbSequenceRunning || !hasPendingReward)
            return;
        GrantPendingRewardIfAny();
        isAbsorbSequenceRunning = false;
    }

    IEnumerator AbsorbToInventory(Sprite sprite, Vector2 origin, Vector2 startSize)
    {
        InventoryOpenButton.TryGetTarget(transform, out Vector2 target, out InventoryOpenButton button);
        if (sprite != null)
            yield return FlyIconTo(sprite, origin, target, startSize);

        GrantPendingRewardIfAny();
        if (button != null)
            button.PlayReceivePulse();

        yield return new WaitForSecondsRealtime(0.12f);
        isAbsorbSequenceRunning = false;
        ReturnToMapAfterReward();
    }

    // 상자 위 아이콘이 오른쪽 위 인벤토리 버튼까지 곡선을 그리며 작아진다
    IEnumerator FlyIconTo(Sprite sprite, Vector2 originScreen, Vector2 targetScreen, Vector2 startSize)
    {
        Canvas canvas = itemSettings.itemTransform != null
            ? itemSettings.itemTransform.GetComponentInParent<Canvas>()
            : GetComponentInParent<Canvas>();
        if (canvas == null)
            yield break;
        canvas = canvas.rootCanvas;

        var go = new GameObject("RelicAbsorbIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = canvas.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(canvas.transform, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.localRotation = Quaternion.identity;
        rt.localScale = Vector3.one;
        float factor = canvas.scaleFactor > 0.01f ? canvas.scaleFactor : 1f;
        float w = startSize.x > 8f ? startSize.x : 160f;
        float h = startSize.y > 8f ? startSize.y : 160f;
        rt.sizeDelta = new Vector2(w / factor, h / factor);
        rt.SetAsLastSibling();

        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = Color.white;

        // 곡선 비행 자체는 공용 연출(InventoryFlyFx) — 이벤트 카드 획득도 같은 연출을 쓴다
        yield return InventoryFlyFx.Fly(rt, originScreen, targetScreen, a =>
        {
            var c = image.color;
            c.a = a;
            image.color = c;
        });

        if (go != null)
            Destroy(go);
    }

    static Vector2 ScreenCenter(RectTransform rt) => InventoryFlyFx.ScreenCenter(rt);

    static Vector2 ItemScreenSize(RectTransform rt) => InventoryFlyFx.ScreenSize(rt);

    // 받지 않기 — 유물 지급 없이 맵으로 복귀
    void OnDeclineRewardClicked()
    {
        if (!isChestOpened || isAbsorbSequenceRunning || isSequenceRunning)
            return;

        isHovering = false;
        isRewardPointerHover = false;
        wasHoveringByMouse = false;
        HideTooltipPopup();
        hasPendingReward = false;
        pendingReward = default;
        pendingPresentedRelics.Clear();

        if (itemSettings.itemObject != null)
            itemSettings.itemObject.SetActive(false);
        if (itemSettings.itemImage != null)
            itemSettings.itemImage.raycastTarget = false;

        SetDeclineButtonVisible(false);
        ReturnToMapAfterReward();
    }

    void EnsureDeclineButton()
    {
        if (_declineButton != null)
            return;

        Canvas canvas = ResolveCanvas();
        if (canvas == null)
            return;

        if (declineButtonStyle == null)
            declineButtonStyle = new RewardSkipButtonStyle { label = "받지 않기" };

        _declineButton = declineButtonStyle.Build(canvas.transform, OnDeclineRewardClicked);
        _declineButton.gameObject.SetActive(false);
    }

    void SetDeclineButtonVisible(bool visible)
    {
        if (!visible)
        {
            if (_declineButton != null)
                _declineButton.gameObject.SetActive(false);
            return;
        }

        EnsureDeclineButton();
        if (_declineButton == null)
            return;

        _declineButton.gameObject.SetActive(true);
        _declineButton.interactable = !isAbsorbSequenceRunning && !isSequenceRunning;
        _declineButton.transform.SetAsLastSibling();
    }

    // 버튼이 붙을 캔버스를 찾는다
    Canvas ResolveCanvas()
    {
        // 아이템이 비활성일 때도 찾도록 includeInactive로 부모를 훑는다(Graphic.canvas는 비활성이면 null).
        if (itemSettings.itemImage != null)
        {
            Canvas itemCanvas = itemSettings.itemImage.GetComponentInParent<Canvas>(true);
            if (itemCanvas != null)
                return itemCanvas;
        }

        Canvas canvas = GetComponentInChildren<Canvas>(true);
        if (canvas != null)
            return canvas;

        canvas = GetComponentInParent<Canvas>(true);
        if (canvas != null)
            return canvas;

        return FindFirstObjectByType<Canvas>();
    }

    public Vector2 StageTooltipAnchoredPosition => tooltipPopupAnchoredPosition;
    public Vector2 StageTooltipSize => tooltipPopupSize;
    public Vector2 StageTooltipPadding => tooltipPopupPadding;
    public float StageItemHoverSpeed => itemSettings.hoverSpeed;
    public float StageItemHoverAmplitude => itemSettings.hoverAmplitude;
    public float StageItemHoverScale => itemHoverScaleMultiplier;
    public float StageItemHoverScaleLerp => itemHoverScaleLerpSpeed;

    // 보스 드랍 툴팁이 유물 스테이지와 같은 스프라이트·글꼴로 보이게 맞춘다.
    public void ApplyStageTooltipStyle(Image background, TMP_Text nameText, TMP_Text descriptionText)
    {
        ApplyPopupBackground(background);
        ApplyTooltipLabel(nameText, tooltipNameFontAsset, tooltipNameFontPreset, tooltipNameFontSize, tooltipNameFontStyle, tooltipNameFontColor, tooltipNameAlignment);
        ApplyTooltipLabel(descriptionText, tooltipDescriptionFontAsset, tooltipDescriptionFontPreset, tooltipDescriptionFontSize, tooltipDescriptionFontStyle, tooltipDescriptionFontColor, tooltipDescriptionAlignment);
    }

    void ApplyPopupBackground(Image background)
    {
        if (background == null)
            return;

        background.sprite = tooltipPopupSprite;
        background.type = tooltipPopupSprite != null ? tooltipPopupImageType : Image.Type.Simple;
        background.color = tooltipPopupSprite != null ? tooltipPopupSpriteColor : tooltipPopupBackgroundColor;
        background.raycastTarget = false;
        background.preserveAspect = false;
    }

    static void ApplyTooltipLabel(TMP_Text label, TMP_FontAsset font, Material preset, float size, FontStyles style, Color color, TextAlignmentOptions alignment)
    {
        if (label == null)
            return;

        if (font != null)
            label.font = font;

        label.fontSize = Mathf.Max(1f, size);
        label.fontStyle = style;
        label.color = color;
        label.alignment = alignment;

        if (preset == null || label.font == null)
            return;

        try
        {
            label.fontSharedMaterial = preset;
        }
        catch (System.Exception)
        {
            // 폰트와 맞지 않는 프리셋은 기본 머티리얼을 유지한다.
        }
    }

    // 보상 이름/설명을 툴팁에 반영하고 표시
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

    // 마우스가 아이템 위에 있는지 검사해 호버 상태 동기화
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

    // 툴팁 팝업 오브젝트를 확보하거나 런타임으로 생성·배치
    void EnsureTooltipPopupBindings()
    {
        if (tooltipPopupRoot != null && tooltipNameText != null && tooltipDescriptionText != null)
        {
            if (tooltipPopupBackground == null)
                tooltipPopupBackground = tooltipPopupRoot.GetComponent<Image>();
            return;
        }

        // RelicStage는 캔버스 밖 루트 오브젝트라 GetComponentInParent로는 못 찾는다.
        // 아무 캔버스나 잡으면 팝업이 다른 화면에 생겨 안 보이므로, 유물 이미지가 속한 캔버스에 만든다.
        Canvas canvas = ResolveCanvas();
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

    // 툴팁 스프라이트와 제목·내용 글꼴을 인스펙터 값으로 적용
    void ApplyTooltipTextStyle()
    {
        ApplyStageTooltipStyle(tooltipPopupBackground, tooltipNameText, tooltipDescriptionText);
    }

    // 툴팁 팝업을 표시하고 최상단으로 정렬
    void ShowTooltipPopup()
    {
        if (tooltipPopupRoot == null)
            return;

        if (!tooltipPopupRoot.gameObject.activeSelf)
            tooltipPopupRoot.gameObject.SetActive(true);

        tooltipPopupRoot.SetAsLastSibling();
    }

    // 툴팁 팝업 숨김
    void HideTooltipPopup()
    {
        if (tooltipPopupRoot != null)
            tooltipPopupRoot.gameObject.SetActive(false);
    }

    // 스프라이트 이름을 정규화해 유물 id 생성
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

    // 보상 후보 한 건의 데이터
    struct RewardCandidate
    {
        public bool isSpriteOnly; // 스프라이트 전용 여부
        public RelicSO relic; // 렐릭 정의
        public Sprite sprite; // 스프라이트
        public string displayName; // 표시 이름
        public string description; // 설명
    }

#if UNITY_EDITOR
    // 에디터에서 목록이 비어 있으면 폴더에서 스프라이트 자동 수집
    void OnValidate()
    {
        if (!autoCollectSpritesInEditor)
            return;

        if (spriteOnlyRelics != null && spriteOnlyRelics.Count > 0)
            return;

        CollectSpriteOnlyRelicsFromFolder();
    }

    // 지정 폴더의 스프라이트들을 스프라이트 전용 유물 목록으로 수집
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
