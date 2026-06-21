using Battle.Relic;
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

    // 보상 흡수(아이템 -> UI 슬롯) 연출 설정
    [System.Serializable]
    private struct RewardAbsorbSettings
    {
        [Header("References")]
        public RectTransform uiCanvasRect; // UI 캔버스 Rect
        public RectTransform targetSlotRect; // 목표 슬롯 Rect
        public Image targetSlotImage; // 목표 슬롯 이미지
        public Image flyingImageTemplate; // 날아가는 이미지 템플릿

        [Header("Timing")]
        public float flyDuration; // 이동 지속 시간
        public AnimationCurve xMoveCurve; // X 이동 곡선
        public AnimationCurve yMoveCurve; // Y 이동 곡선
        public AnimationCurve arcCurve; // 포물선 곡선
        public float arcHeight; // 포물선 높이

        [Header("Scale")]
        public float startScale; // 시작 스케일
        public float endScale; // 종료 스케일
    }

    [Header("툴팁 팝업 (Optional)")]
    [SerializeField] private RectTransform tooltipPopupRoot; // 툴팁 팝업 루트
    [SerializeField] private Image tooltipPopupBackground; // 툴팁 배경 이미지
    [SerializeField] private TextMeshProUGUI tooltipNameText; // 툴팁 이름 텍스트
    [SerializeField] private TextMeshProUGUI tooltipDescriptionText; // 툴팁 설명 텍스트
    [SerializeField] private Vector2 tooltipPopupAnchoredPosition = new Vector2(0f, -60f); // 툴팁 앵커 위치
    [SerializeField] private Vector2 tooltipPopupSize = new Vector2(820f, 220f); // 툴팁 크기
    [SerializeField] private Vector2 tooltipPopupPadding = new Vector2(24f, 18f); // 툴팁 패딩
    [SerializeField] private Color tooltipPopupBackgroundColor = new Color(0f, 0f, 0f, 0.78f); // 툴팁 배경 색

    [Header("상자 관련 설정")]
    [SerializeField] private ChestSettings chestSettings; // 상자 연출 설정

    [Header("아이템 관련 설정")]
    [SerializeField] private ItemSettings itemSettings; // 아이템 연출 설정

    [Header("개발중 유물 이미지")]
    [SerializeField] private string relicSpriteFolderPath = "Assets/Sprite/Relic"; // 유물 스프라이트 폴더 경로
    [SerializeField] private bool autoCollectSpritesInEditor = true; // 에디터에서 스프라이트 자동 수집 여부
    [SerializeField] private bool avoidOwnedDuplicates = true; // 보유 중복 회피 여부
    [SerializeField] private List<SpriteOnlyRelicEntry> spriteOnlyRelics = new List<SpriteOnlyRelicEntry>(); // 스프라이트 전용 유물 목록

    [Header("획득 유물 상호작용")]
    [SerializeField] private float itemHoverScaleMultiplier = 1.08f; // 호버 시 확대 배율
    [SerializeField] private float itemHoverScaleLerpSpeed = 10f; // 호버 스케일 보간 속도
    [SerializeField] private Sprite fallbackRewardSprite; // 보상 폴백 스프라이트

    [Header("획득 연출: 월드 -> 좌측 상단 UI 슬롯")]
    [SerializeField] private RewardAbsorbSettings rewardAbsorbSettings; // 보상 흡수 연출 설정

    [Header("툴팁 이름 폰트 설정")]
    [SerializeField] private TMP_FontAsset tooltipNameFontAsset; // 이름 폰트 에셋
    [SerializeField] private float tooltipNameFontSize = 34f; // 이름 폰트 크기
    [SerializeField] private FontStyles tooltipNameFontStyle = FontStyles.Bold; // 이름 폰트 스타일
    [SerializeField] private Color tooltipNameFontColor = Color.white; // 이름 폰트 색
    [SerializeField] private TextAlignmentOptions tooltipNameAlignment = TextAlignmentOptions.Top; // 이름 정렬

    [Header("툴팁 설명 폰트 설정")]
    [SerializeField] private TMP_FontAsset tooltipDescriptionFontAsset; // 설명 폰트 에셋
    [SerializeField] private float tooltipDescriptionFontSize = 28f; // 설명 폰트 크기
    [SerializeField] private FontStyles tooltipDescriptionFontStyle = FontStyles.Normal; // 설명 폰트 스타일
    [SerializeField] private Color tooltipDescriptionFontColor = new Color(0.92f, 0.92f, 0.92f, 1f); // 설명 폰트 색
    [SerializeField] private TextAlignmentOptions tooltipDescriptionAlignment = TextAlignmentOptions.Top; // 설명 정렬

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
    private RewardCandidate pendingReward; // 지급 대기 보상

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

    // 대기 중 보상을 실제로 보유 목록에 지급
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

        // 유물(유물 호리병 10018): 보유 시 이 라운드의 후보 유물을 전부 추가 획득
        if (manager.HasGrantAllRelicsInStage())
        {
            var all = BuildCandidates(manager);
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c.isSpriteOnly)
                {
                    if (c.sprite != null) manager.TryAddSpriteOnlyRelic(c.sprite, out _);
                }
                else if (c.relic != null && !manager.HasRelicId(c.relic.id))
                {
                    manager.AddRelic(c.relic);
                }
            }
            Debug.Log("[유물] 유물 호리병 — 라운드 후보 유물 전부 획득");
        }

        hasPendingReward = false;
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

    // 보상 클릭 시 흡수 연출 시작
    void OnRewardPointerClick()
    {
        if (!isChestOpened || isAbsorbSequenceRunning)
            return;

        StartCoroutine(PlayRewardAbsorbAndReturn());
    }

    // 보상 흡수 연출 후 지급하고 맵으로 복귀
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

    RectTransform flyingImageRect; // 날아가는 이미지 Rect
    Image flyingImage; // 날아가는 이미지
    Vector2 flyStartLocalPos; // 이동 시작 로컬 위치
    Vector2 flyEndLocalPos; // 이동 종료 로컬 위치

    // 흡수 연출용 날아가는 이미지를 생성하고 시작 위치 설정
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

    // 아이템과 목표 슬롯의 캔버스 로컬 좌표를 계산
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
            Vector2 fallback = new Vector2(80f, Screen.height - 80f);
            targetScreen = new Vector3(fallback.x, fallback.y, 0f);
        }

        bool okFrom = RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sourceScreen, cam, out fromLocal);
        bool okTo = RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, targetScreen, cam, out toLocal);
        if (!okFrom && itemSettings.itemTransform != null)
        {
            okFrom = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                RectTransformUtility.WorldToScreenPoint(cam, itemSettings.itemTransform.position),
                cam,
                out fromLocal);
        }

        return okFrom && okTo;
    }

    // 날아가는 이미지를 목표 슬롯까지 곡선 이동·축소시키는 코루틴
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

    // 목표 슬롯 이미지를 활성화하고 보상 스프라이트 설정
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

    // 연출에 사용할 캔버스를 탐색해 반환
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

    // 툴팁 텍스트 폰트/색/정렬 등 스타일 적용
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
