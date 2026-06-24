using UnityEngine;
using UnityEngine.UI;

namespace Battle.UI
{
    // 각 스테이지 menubar 의 DeckButton 에 붙여 공용 인벤토리(InventoryPanelController.Instance)를 여는 프록시.
    // 같은 GameObject 의 Button OnClick 에 자동 연결되므로, 버튼마다 패널 참조를 따로 끌어줄 필요가 없다.
    // (인벤토리 패널/컨트롤러는 어떤 스테이지에도 속하지 않는 항상 켜진 캔버스에 1개만 둔다.)
    [RequireComponent(typeof(Button))]
    public class InventoryOpenButton : MonoBehaviour
    {
        // 클릭 시 동작
        public enum ClickAction { Toggle, Open }

        [Tooltip("Toggle = 누를 때마다 열기/닫기, Open = 항상 열기.")]
        [SerializeField] private ClickAction action = ClickAction.Toggle; // 클릭 동작

        // 같은 오브젝트의 Button 클릭에 자동 연결
        void Awake()
        {
            var btn = GetComponent<Button>();
            if (btn != null) btn.onClick.AddListener(HandleClick);
        }

        // 공용 인벤토리 컨트롤러를 찾아 열기/토글
        void HandleClick()
        {
            InventoryPanelController inv = InventoryPanelController.Instance;
            if (inv == null)
            {
                Debug.LogWarning("[InventoryOpenButton] InventoryPanelController.Instance가 없습니다. " +
                                 "공용 인벤토리 컨트롤러가 항상 켜진 캔버스에 배치돼 있는지 확인하세요.");
                return;
            }

            if (action == ClickAction.Toggle) inv.Toggle();
            else inv.Open();
        }
    }
}
