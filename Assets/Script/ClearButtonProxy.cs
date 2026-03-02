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

    // 버튼을 런타임에 호출하는 핸들러
    public void OnClearClicked()
    {
        Debug.Log("=== ClearButtonProxy.OnClearClicked 호출됨 ===");
        
        // GameStateController를 직접 사용
        var stateController = GameStateController.Instance;
        if (stateController != null)
        {
            Debug.Log("GameStateController.OnBattleClear 호출");
            stateController.OnBattleClear();
            return;
        }
        
        // 하위 호환: BattleManger를 통해 처리 시도
        var bm = FindFirstObjectByType<BattleManger>();
        if (bm != null)
        {
            Debug.Log("BattleManger.OnBattleClear 호출");
            bm.OnBattleClear();
            return;
        }

        Debug.LogWarning("ClearButtonProxy: GameStateController와 BattleManger를 찾을 수 없습니다.");
    }
}
