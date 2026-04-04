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

    void Awake()
    {
        // 시작 시 게임오버 패널 비활성화
        gameObject.SetActive(false);
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

    public void Show(string message = "당신은 사망했습니다...")
    {
        if (deathMessageText != null)
            deathMessageText.text = message;

        gameObject.SetActive(true);
        Time.timeScale = 0f;
    }

    void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
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
