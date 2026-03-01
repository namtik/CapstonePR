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
        // 각주: BattleManger를 통해 전투 클리어 처리
        var bm = Object.FindFirstObjectByType<BattleManger>();
        if (bm != null)
        {
            bm.OnBattleClear();
            return;
        }

        Debug.LogWarning("ClearButtonProxy: BattleManger를 찾을 수 없습니다.");
    }
}