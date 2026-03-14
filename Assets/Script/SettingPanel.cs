using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// 이 스크립트는 항상 활성화된 오브젝트(예: GameStateController)에 붙여야 합니다.
/// settingCanvas는 비활성 상태로 시작하고, SettingButton을 누르면 활성화됩니다.
/// </summary>
public class SettingPanel : MonoBehaviour
{
    [Header("설정 캔버스 (비활성 상태로 시작)")]
    public GameObject settingCanvas;

    [Header("패널 내부 버튼")]
    public Button resumeButton;
    public Button restartButton;
    public Button reloadButton;
    public Button quitButton;

    void Start()
    {
        // 설정 캔버스 비활성화
        if (settingCanvas != null)
            settingCanvas.SetActive(false);

        // 패널 내부 버튼 연결
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
        if (settingCanvas != null)
        {
            settingCanvas.SetActive(true);
            Time.timeScale = 0f;
        }
    }

    public void CloseSettings()
    {
        if (settingCanvas != null)
        {
            settingCanvas.SetActive(false);
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
