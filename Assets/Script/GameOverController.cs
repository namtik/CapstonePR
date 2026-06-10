using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 플레이어 사망 시 눈꺼풀 감김 연출과 다시하기/종료 버튼을 처리하는 컨트롤러
public class GameOverController : MonoBehaviour
{
    // 전역 싱글턴 인스턴스
    public static GameOverController Instance { get; private set; }

    [Header("게임오버 캔버스 (비활성 상태로 시작)")]
    [SerializeField] private GameObject gameOverCanvas; // 게임오버 화면 캔버스

    [Header("눈꺼풀 패널 (닫힌 위치 = 각자 화면 절반을 덮는 위치로 배치)")]
    [SerializeField] private RectTransform topEyelid; // 위쪽 눈꺼풀 패널
    [SerializeField] private RectTransform bottomEyelid; // 아래쪽 눈꺼풀 패널

    [Header("버튼 그룹 (눈 감긴 뒤 표시)")]
    [SerializeField] private GameObject buttonGroup; // 버튼 묶음 오브젝트
    [SerializeField] private Button retryButton; // 다시하기 버튼
    [SerializeField] private Button quitButton; // 게임 종료 버튼
    [Tooltip("클릭 시 진행 중이던 런을 정리하고 메인 메뉴로 돌아간다.")]
    [SerializeField] private Button mainMenuButton; // 메인 메뉴 버튼

    [Header("연출 설정")]
    [SerializeField] private float closeDuration = 1.4f; // 눈 감기는 시간(초)
    [SerializeField] private float openDuration = 0.5f; // 눈 뜨는 시간(초)
    [Tooltip("닫힘 시 중앙에서 약간 겹치게 해 검은 틈을 방지한다.")]
    [SerializeField] private float centerOverlap = 4f; // 중앙 겹침 보정값

    private Vector2 topClosedPos; // 위 눈꺼풀 닫힘 위치
    private Vector2 topOpenPos; // 위 눈꺼풀 열림 위치
    private Vector2 bottomClosedPos; // 아래 눈꺼풀 닫힘 위치
    private Vector2 bottomOpenPos; // 아래 눈꺼풀 열림 위치
    private bool positionsCached; // 눈꺼풀 위치 캐시 완료 여부
    private bool isDead; // 사망 연출 진행 중 여부
    private Coroutine animRoutine; // 진행 중인 연출 코루틴

    // 초기화: 싱글턴 설정, 위치 캐시, 캔버스 비활성화, 버튼 리스너 연결
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

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveListener(OnMainMenuClicked);
            mainMenuButton.onClick.AddListener(OnMainMenuClicked);
        }
    }

    // 파괴 시 싱글턴 참조 해제
    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // 눈꺼풀의 열림/닫힘 위치를 계산해 캐시한다
    void CacheEyelidPositions()
    {
        if (positionsCached) return;

        if (topEyelid != null)
        {
            Vector2 placed = topEyelid.anchoredPosition;
            float height = topEyelid.rect.height;
            if (height < 1f) height = Screen.height; // 레이아웃 전이면 화면 높이로 대체
            topClosedPos = placed + Vector2.down * centerOverlap; // 살짝 중앙을 넘어 겹침
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

    // 플레이어 사망 연출을 시작한다
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

    // 눈꺼풀을 닫는 코루틴 (감긴 뒤 버튼 그룹 표시)
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

    // [다시하기] 버튼 콜백: 재시작 연출 시작
    public void OnRetryClicked()
    {
        if (!isDead) return;

        if (animRoutine != null) StopCoroutine(animRoutine);
        animRoutine = StartCoroutine(RetrySequence());
    }

    // 런을 재시작하고 눈을 뜨는 연출 코루틴
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

    // 플레이어 HP를 복구하고 맵으로 런을 재시작한다
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

    // [메인 메뉴] 버튼 콜백: 런을 정리하고 메인 메뉴로 복귀
    public void OnMainMenuClicked()
    {
        if (!isDead) return;

        if (animRoutine != null)
        {
            StopCoroutine(animRoutine);
            animRoutine = null;
        }

        Time.timeScale = 1f;

        if (buttonGroup != null)
            buttonGroup.SetActive(false);
        if (gameOverCanvas != null)
            gameOverCanvas.SetActive(false);

        isDead = false;

        GameStateController state = GameStateController.Instance;
        if (state != null)
            state.ReturnToMainMenu();
        else
            Debug.LogError("[GameOver] GameStateController.Instance가 null이라 메인 메뉴로 돌아갈 수 없습니다.");
    }

    // [게임 종료] 버튼 콜백: 애플리케이션 종료
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
