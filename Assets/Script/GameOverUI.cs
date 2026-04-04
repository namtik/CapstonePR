using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameOverUI : MonoBehaviour
{
    [Header("UI References")]
    public TMP_Text deathMessageText;  // Inspector에서 사망 문구 텍스트 할당
    public Button restartButton;        // Inspector에서 재시작 버튼 할당
    public Button quitButton;           // Inspector에서 종료 버튼 할당

    void Awake()
    {
        // 버튼 클릭 이벤트 연결 (코드로 연결하면 Inspector에서 연결 안 해도 됨)
        if (restartButton != null)
            restartButton.onClick.AddListener(OnRestartClicked);
        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuitClicked);
    }

    void OnEnable()
    {
        // 패널이 활성화될 때마다 사망 문구 표시
        if (deathMessageText != null)
            deathMessageText.text = "You Died";
    }

    void OnRestartClicked()
    {
        var stateController = GameStateController.Instance;
        if (stateController != null)
            stateController.RestartGame();  // 싱글턴 파괴 + 씬 재로드
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