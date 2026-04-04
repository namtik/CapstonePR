using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Game Over 패널 UI 컨트롤러.
/// 패널에 붙여두면 자동으로 사망 문구 표시 및 재시작 버튼을 연결합니다.
/// </summary>
public class GameOverUI : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text deathMessageText;
    public Button restartButton;
    public Button quitButton;

    void Awake()
    {
        if (restartButton != null)
            restartButton.onClick.AddListener(OnRestartClicked);

        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuitClicked);
    }

    void OnEnable()
    {
        if (deathMessageText != null)
            deathMessageText.text = "You Died";
    }

    void OnRestartClicked()
    {
        var stateController = GameStateController.Instance;
        if (stateController != null)
        {
            stateController.RestartGame();
        }
    }

    void OnQuitClicked()
    {
        Debug.Log("게임 종료");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
