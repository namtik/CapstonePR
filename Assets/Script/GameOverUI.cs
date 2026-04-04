using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class GameOverUI : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text deathMessageText;
    public Button restartButton;
    public Button quitButton;

    private CanvasGroup canvasGroup;

    void Awake()
    {
        // CanvasGroup으로 보이지 않게 처리 (gameObject는 활성 상태 유지)
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        Hide();
    }

    void OnEnable()
    {
        if (restartButton != null)
            restartButton.onClick.AddListener(RestartGame);
        if (quitButton != null)
            quitButton.onClick.AddListener(QuitGame);
    }

    void OnDisable()
    {
        if (restartButton != null)
            restartButton.onClick.RemoveListener(RestartGame);
        if (quitButton != null)
            quitButton.onClick.RemoveListener(QuitGame);
    }

    void Hide()
    {
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }

    public void Show(string message = "당신은 사망했습니다...")
    {
        if (deathMessageText != null)
            deathMessageText.text = message;

        // 패널 표시
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;

        // 최상위에 렌더링
        EnsureCanvasOverlay();

        // 전투 파티클/이펙트 정지
        StopAllBattleEffects();

        Time.timeScale = 0f;
    }

    void EnsureCanvasOverlay()
    {
        var canvas = GetComponent<Canvas>();
        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = 999;

        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
    }

    void StopAllBattleEffects()
    {
        // 씬 내 모든 파티클 시스템 정지
        ParticleSystem[] particles = FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
        foreach (ParticleSystem ps in particles)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    void RestartGame()
    {
        Time.timeScale = 1f;

        // DontDestroyOnLoad 싱글톤들의 상태 초기화
        ResetAllSingletons();

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void ResetAllSingletons()
    {
        // 돈 초기화
        if (MoneyManager.Instance != null)
            MoneyManager.Instance.SetMoney(0);

        // 스킬/콤보 초기화
        if (ComboSystem.Instance != null)
        {
            ComboSystem.Instance.ResetForNewGame();
        }

        // 엘리먼트 슬롯 덱 초기화
        if (ElementSlotSystem.Instance != null)
        {
            ElementSlotSystem.Instance.EndBattle();
            ElementSlotSystem.Instance.InitRunDeck();
            ElementSlotSystem.Instance.elementUpgradeLevels = new System.Collections.Generic.Dictionary<string, int>
            {
                { "fire", 0 }, { "water", 0 }, { "wind", 0 }, { "earth", 0 }
            };
        }

        // 맵 진행도 초기화
        if (GameManager.Instance != null)
        {
            GameManager.Instance.lastVisitedNodeIndex = -1;
            GameManager.Instance.clearedNodes.Clear();
        }

        // DontDestroyOnLoad로 생성된 Player 제거 (씬 재로드 시 새로 생성되도록)
        Player[] allPlayers = FindObjectsByType<Player>(FindObjectsSortMode.None);
        foreach (Player p in allPlayers)
        {
            Destroy(p.gameObject);
        }
    }

    void QuitGame()
    {
        Debug.Log("게임 종료");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
