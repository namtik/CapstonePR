using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SettingPanel : MonoBehaviour
{
    [Header("패널")]
    public GameObject settingPanel;

    [Header("패널 내부 버튼")]
    public Button resumeButton;    // 계속하기
    public Button restartButton;   // 게임 재시작
    public Button reloadButton;    // 현재 시점 재로드
    public Button quitButton;      // 게임 종료

    void Start()
    {
        if (settingPanel != null)
            settingPanel.SetActive(false);

        if (resumeButton != null)
            resumeButton.onClick.AddListener(CloseSettings);
        if (restartButton != null)
            restartButton.onClick.AddListener(RestartGame);
        if (reloadButton != null)
            reloadButton.onClick.AddListener(ReloadCurrentScene);
        if (quitButton != null)
            quitButton.onClick.AddListener(QuitGame);

        // 모든 스테이지의 SettingButton을 자동으로 찾아서 연결
        BindAllSettingButtons();
    }

    /// <summary>
    /// 씬 내 모든 "SettingButton" 이름의 버튼을 찾아 OpenSettings에 연결
    /// 각 menubar에 있는 SettingButton이 이 패널을 열 수 있도록 함
    /// </summary>
    void BindAllSettingButtons()
    {
        var allButtons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var btn in allButtons)
        {
            if (btn.gameObject.name == "SettingButton")
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(OpenSettings);
            }
        }
    }

    public void OpenSettings()
    {
        if (settingPanel != null)
        {
            settingPanel.SetActive(true);
            Time.timeScale = 0f;
        }
    }

    public void CloseSettings()
    {
        if (settingPanel != null)
        {
            settingPanel.SetActive(false);
            Time.timeScale = 1f;
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
