using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ClearButtonProxy : MonoBehaviour
{
    Button button;

    void Awake()
    {
        button = GetComponent<Button>();
        if (button == null) return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnClearClicked);
    }

    // 버튼이 런타임에 호출하는 핸들러
    public void OnClearClicked()
    {
        Debug.Log("=== ClearButtonProxy.OnClearClicked 호출됨 ===");

        // GameStateController를 먼저 시도
        var stateController = GameStateController.Instance;
        if (stateController != null)
        {
            Debug.Log("GameStateController.OnRoundClear 호출");
            stateController.OnRoundClear();
            return;
        }

    }
}
