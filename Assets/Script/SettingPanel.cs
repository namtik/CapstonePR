using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SettingPanel : MonoBehaviour
{
    [Header("패널")]
    public GameObject settingPanel;

    [Header("버튼")]
    public Button settingButton;   // 설정 열기 (menubar의 SettingButton)
    public Button resumeButton;    // 계속하기
    public Button restartButton;   // 게임 재시작
    public Button reloadButton;    // 현재 시점 재로드
    public Button quitButton;      // 게임 종료

    void Start()
    {
        if (settingPanel != null)
            settingPanel.SetActive(false);

        if (settingButton != null)
            settingButton.onClick.AddListener(OpenSettings);
        if (resumeButton != null)
            resumeButton.onClick.AddListener(CloseSettings);
        if (restartButton != null)
            restartButton.onClick.AddListener(RestartGame);
        if (reloadButton != null)
            reloadButton.onClick.AddListener(ReloadCurrentScene);
        if (quitButton != null)
            quitButton.onClick.AddListener(QuitGame);
    }

    public void OpenSettings()
    {
        if (settingPanel != null)
        {
            settingPanel.SetActive(true);
            Time.timeScale = 0f; // 게임 일시정지
        }
    }

    public void CloseSettings()
    {
        if (settingPanel != null)
        {
            settingPanel.SetActive(false);
            Time.timeScale = 1f; // 게임 재개
        }
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ReloadCurrentScene()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void QuitGame()
    {
        Debug.Log("게임 종료");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
