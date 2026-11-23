using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 최종 보스(RequiredBossDefeats회) 클리어 시 게임 클리어 화면을 표시하는 컨트롤러
public class GameClearController : MonoBehaviour
{
    // 전역 싱글턴 인스턴스
    public static GameClearController Instance { get; private set; }

    [Header("게임 클리어 캔버스 (비활성 상태로 시작)")]
    [SerializeField] private GameObject gameClearCanvas; // 클리어 화면 캔버스

    [Header("버튼")]
    [Tooltip("클릭 시 런을 처음부터 다시 시작한다.")]
    [SerializeField] private Button retryButton; // 다시하기 버튼
    [Tooltip("클릭 시 진행 중이던 런을 정리하고 메인 메뉴로 돌아간다.")]
    [SerializeField] private Button mainMenuButton; // 메인 메뉴 버튼
    [Tooltip("클릭 시 게임을 종료한다.")]
    [SerializeField] private Button quitButton; // 게임 종료 버튼

    [Header("클리어 결과")]
    [Tooltip("문장 안의 N을 이번 런 처치 수로 바꾼다.")]
    [SerializeField] private TMP_Text winText;
    [Tooltip("처치한 몬스터 초상화를 왼쪽 위부터 채운다. 초상화는 전투 로그와 같은 스프라이트.")]
    [SerializeField] private RectTransform killedMonsters;

    string winTextTemplate; // N이 들어 있는 원문

    // 캔버스 연결 여부 (클리어 화면 표시 준비 상태)
    public bool IsReady => gameClearCanvas != null;

    // 초기화: 싱글턴 설정, 캔버스 비활성화, 버튼 리스너 연결
    void Awake()
    {
        Instance = this;

        if (gameClearCanvas != null)
            gameClearCanvas.SetActive(false);

        if (winText != null)
            winTextTemplate = winText.text.Replace("</color=#AC0013>", "</color>");

        if (retryButton != null)
        {
            retryButton.onClick.RemoveListener(OnRetryClicked);
            retryButton.onClick.AddListener(OnRetryClicked);
        }

        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.RemoveListener(OnMainMenuClicked);
            mainMenuButton.onClick.AddListener(OnMainMenuClicked);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(OnQuitClicked);
            quitButton.onClick.AddListener(OnQuitClicked);
        }
    }

    // 파괴 시 싱글턴 참조 해제
    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // 게임 클리어 화면을 표시한다 (최종 보스 클리어 시 RoundManager가 호출)
    public void ShowGameClear()
    {
        if (gameClearCanvas == null)
        {
            Debug.LogError("[GameClear] gameClearCanvas가 비어 있어 게임 클리어 화면을 표시할 수 없습니다. 인스펙터에 캔버스를 연결하세요.");
            return;
        }

        // 마지막 처치의 보상/카드 연출이 클리어 화면 위에 남지 않도록 정리
        GameStateController.ClearLingeringFx();

        gameClearCanvas.SetActive(true);
        GameAudio.Instance?.PlayWinBgm();
        Time.timeScale = 0f;
        ShowRunKills();
    }

    // 이번 런 처치 수를 문장에 넣고, 잡은 순서대로 초상화를 왼쪽 위부터 채운다
    void ShowRunKills()
    {
        GameStateController state = GameStateController.Instance;
        int count = state != null ? state.RunKillCount : 0;
        ApplyWinCount(count);
        RunKillGallery.Fill(killedMonsters, state != null ? state.RunKills : null);
    }

    // 클리어 문장의 N을 처치 수로 바꾼다
    void ApplyWinCount(int count)
    {
        if (winText == null) return;

        string body = string.IsNullOrEmpty(winTextTemplate) ? winText.text : winTextTemplate;
        winText.text = body.Replace(">N<", ">" + count + "<");
    }

    // 클리어 화면 [다시하기]: 런을 처음부터 재시작하고 맵으로 돌아간다
    public void OnRetryClicked()
    {
        Time.timeScale = 1f;

        if (gameClearCanvas != null)
            gameClearCanvas.SetActive(false);

        GameStateController state = GameStateController.Instance;
        if (state != null)
            state.RestartRunToMap();
        else
            Debug.LogError("[GameClear] GameStateController.Instance가 null이라 런 재시작을 수행할 수 없습니다.");
    }

    // 클리어 화면 [메인 메뉴]: 런을 정리하고 메인 메뉴로 복귀
    public void OnMainMenuClicked()
    {
        Time.timeScale = 1f;

        if (gameClearCanvas != null)
            gameClearCanvas.SetActive(false);

        GameStateController state = GameStateController.Instance;
        if (state != null)
            state.ReturnToMainMenu();
        else
            Debug.LogError("[GameClear] GameStateController.Instance가 null이라 메인 메뉴로 돌아갈 수 없습니다.");
    }

    // 클리어 화면 [게임 종료]: 애플리케이션 종료
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
