using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ClearButtonProxy : MonoBehaviour
{
    Button button; // 연결된 버튼

    // 버튼 클릭 리스너를 등록
    void Awake()
    {
        button = GetComponent<Button>();
        if (button == null) return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnClearClicked);
    }

    // 라운드 클리어 처리를 호출
    public void OnClearClicked()
    {
        Debug.Log("=== ClearButtonProxy.OnClearClicked 호출됨 ===");

        var stateController = GameStateController.Instance;
        if (stateController != null)
        {
            Debug.Log("GameStateController.OnRoundClear 호출");
            stateController.OnRoundClear();
            return;
        }

    }
}
