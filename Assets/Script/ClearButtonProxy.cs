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
        var bm = Object.FindFirstObjectByType<BattleManger>();
        if (bm != null)
        {
            bm.OnBattleClear();
            return;
        }

        //BattleManger가 없으면 GameManager를 대체로 사용
        var gm = Object.FindFirstObjectByType<GameManager>();
        if (gm != null)
        {
            gm.LoadMapScene();
            return;
        }

        Debug.LogWarning("ClearButtonProxy: BattleManger 또는 GameManager를 찾을 수 없습니다.");
    }
}
