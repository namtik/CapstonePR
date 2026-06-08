using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어 사망 시 눈꺼풀이 감기는 연출을 재생하고,
/// 다 감긴 뒤 다시하기/게임종료 버튼을 표시한다.
/// 이 스크립트는 항상 활성화된 오브젝트(예: GameStateController)에 붙여야 한다.
/// gameOverCanvas는 비활성 상태로 시작하고, 사망 시 활성화된다.
/// </summary>
public class GameOverController : MonoBehaviour
{
    public static GameOverController Instance { get; private set; }

    [Header("게임오버 캔버스 (비활성 상태로 시작)")]
    [SerializeField] private GameObject gameOverCanvas;

    [Header("눈꺼풀 패널 (닫힌 위치 = 각자 화면 절반을 덮는 위치로 배치)")]
    [SerializeField] private RectTransform topEyelid;
    [SerializeField] private RectTransform bottomEyelid;

    [Header("버튼 그룹 (눈 감긴 뒤 표시)")]
    [SerializeField] private GameObject buttonGroup;
    [SerializeField] private Button retryButton;
    [SerializeField] private Button quitButton;

    [Header("연출 설정")]
    [SerializeField] private float closeDuration = 1.4f;
    [SerializeField] private float openDuration = 0.5f;
    [Tooltip("닫힘 시 중앙에서 약간 겹치게 해 검은 틈을 방지한다.")]
    [SerializeField] private float centerOverlap = 4f;

    private Vector2 topClosedPos;
    private Vector2 topOpenPos;
    private Vector2 bottomClosedPos;
    private Vector2 bottomOpenPos;
    private bool positionsCached;
    private bool isDead;
    private Coroutine animRoutine;

    void Awake()
    {
        Instance = this;
        CacheEyelidPositions();

        if (gameOverCanvas != null)
            gameOverCanvas.SetActive(false);

        if (retryButton != null)
        {
            retryButton.onClick.RemoveListener(OnRetryClicked);
            retryButton.onClick.AddListener(OnRetryClicked);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(OnQuitClicked);
            quitButton.onClick.AddListener(OnQuitClicked);
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void CacheEyelidPositions()
    {
        if (positionsCached) return;

        if (topEyelid != null)
        {
            Vector2 placed = topEyelid.anchoredPosition;
            float height = topEyelid.rect.height;
            if (height < 1f) height = Screen.height; // 레이아웃 전이면 화면 높이로 대체
            topClosedPos = placed + Vector2.down * centerOverlap;     // 살짝 중앙을 넘어 겹침
            topOpenPos = placed + Vector2.up * (height + centerOverlap); // 화면 위로 완전히 숨김
        }

        if (bottomEyelid != null)
        {
            Vector2 placed = bottomEyelid.anchoredPosition;
            float height = bottomEyelid.rect.height;
            if (height < 1f) height = Screen.height;
            bottomClosedPos = placed + Vector2.up * centerOverlap;
            bottomOpenPos = placed + Vector2.down * (height + centerOverlap);
        }

        positionsCached = true;
    }

    /// <summary>플레이어 사망 연출 시작.</summary>
    public void PlayDeathSequence()
    {
        if (isDead) return;
        isDead = true;

        CacheEyelidPositions();

        if (gameOverCanvas != null)
            gameOverCanvas.SetActive(true);

        if (buttonGroup != null)
            buttonGroup.SetActive(false);

        // 눈 뜬 상태(패널이 화면 밖)에서 시작 — 첫 프레임 깜빡임 방지
        if (topEyelid != null) topEyelid.anchoredPosition = topOpenPos;
        if (bottomEyelid != null) bottomEyelid.anchoredPosition = bottomOpenPos;

        Time.timeScale = 0f;

        if (animRoutine != null) StopCoroutine(animRoutine);
        animRoutine = StartCoroutine(CloseEyelids());
    }

    IEnumerator CloseEyelids()
    {
        float dur = Mathf.Max(0.01f, closeDuration);
        float t = 0f;

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            float eased = k * k * (3f - 2f * k); // smoothstep — 부드럽게 감김

            if (topEyelid != null)
                topEyelid.anchoredPosition = Vector2.LerpUnclamped(topOpenPos, topClosedPos, eased);
            if (bottomEyelid != null)
                bottomEyelid.anchoredPosition = Vector2.LerpUnclamped(bottomOpenPos, bottomClosedPos, eased);

            yield return null;
        }

        if (topEyelid != null) topEyelid.anchoredPosition = topClosedPos;
        if (bottomEyelid != null) bottomEyelid.anchoredPosition = bottomClosedPos;

        if (buttonGroup != null)
            buttonGroup.SetActive(true);

        animRoutine = null;
    }

    public void OnRetryClicked()
    {
        if (!isDead) return;

        if (animRoutine != null) StopCoroutine(animRoutine);
        animRoutine = StartCoroutine(RetrySequence());
    }

    IEnumerator RetrySequence()
    {
        if (buttonGroup != null)
            buttonGroup.SetActive(false);

        // 눈을 감은 상태에서 맵으로 전환 (HP 복구 포함)
        RestartRun();

        // 눈 뜨기 — 맵이 드러남
        float dur = Mathf.Max(0.01f, openDuration);
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            float eased = k * k * (3f - 2f * k);

            if (topEyelid != null)
                topEyelid.anchoredPosition = Vector2.LerpUnclamped(topClosedPos, topOpenPos, eased);
            if (bottomEyelid != null)
                bottomEyelid.anchoredPosition = Vector2.LerpUnclamped(bottomClosedPos, bottomOpenPos, eased);

            yield return null;
        }

        if (gameOverCanvas != null)
            gameOverCanvas.SetActive(false);

        animRoutine = null;
        isDead = false;
    }

    void RestartRun()
    {
        Time.timeScale = 1f;

        // 플레이어 체력 복구
        Player player = Player.Resolve(true);
        if (player != null)
        {
            player.currentHp = player.maxHp;
            player.UpdateUIForExternalSync();
        }

        GameStateController state = GameStateController.Instance;
        if (state != null)
            state.RestartRunToMap();
        else
            Debug.LogError("[GameOver] GameStateController.Instance가 null이라 런 재시작을 수행할 수 없습니다.");
    }

    public void OnQuitClicked()
    {
        Debug.Log("게임 종료");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
