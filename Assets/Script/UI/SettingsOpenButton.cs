using UnityEngine;
using UnityEngine.UI;

// 메뉴바 설정 아이콘. 클릭하면 ESC와 같은 설정 창을 연다.
[RequireComponent(typeof(Button))]
public class SettingsOpenButton : MonoBehaviour
{
    void Awake()
    {
        Button button = GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(Open);
    }

    void Open()
    {
        SettingPanel panel = FindFirstObjectByType<SettingPanel>();
        if (panel == null)
        {
            Debug.LogWarning("[SettingsOpenButton] SettingPanel이 씬에 없습니다.");
            return;
        }

        panel.OpenSettings();
    }
}
