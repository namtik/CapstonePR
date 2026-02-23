using UnityEngine;
using UnityEngine.UI;

// Attach this to the Clear button (same GameObject). The component will ensure
// the Button's onClick is wired at runtime to a handler that finds the active
// BattleManger or GameManager and triggers a map return. This avoids broken
// inspector references when scenes are reloaded.
[RequireComponent(typeof(Button))]
public class ClearButtonProxy : MonoBehaviour
{
    Button button;

    void Awake()
    {
        button = GetComponent<Button>();
        if (button == null) return;

        // Ensure the runtime instance has a working listener (clears stale references)
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(OnClearClicked);
    }

    // Called by the button at runtime
    public void OnClearClicked()
    {
        // Try find the current BattleManger in the scene
        var bm = Object.FindFirstObjectByType<BattleManger>();
        if (bm != null)
        {
            bm.OnBattleClear();
            return;
        }

        // Fallback to GameManager if no BattleManger found
        var gm = Object.FindFirstObjectByType<GameManager>();
        if (gm != null)
        {
            gm.LoadMapScene();
            return;
        }

        Debug.LogWarning("ClearButtonProxy: No BattleManger or GameManager found to handle clear click.");
    }
}
