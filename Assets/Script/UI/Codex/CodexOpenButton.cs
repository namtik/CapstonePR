using UnityEngine;
using UnityEngine.UI;

namespace Battle.UI
{
    // 각 스테이지 menubar 의 CodexButton 에 붙여 공용 도감(CodexPanelController.Instance)을 여는 프록시.
    // 같은 GameObject 의 Button OnClick 에 자동 연결된다. 아이콘은 이 버튼 Image 스프라이트만 바꾸면 된다.
    [RequireComponent(typeof(Button))]
    public class CodexOpenButton : MonoBehaviour
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

        // 공용 도감 컨트롤러를 찾아 열기/토글
        void HandleClick()
        {
            CodexPanelController codex = CodexPanelController.Instance;
            if (codex == null)
            {
                Debug.LogWarning("[CodexOpenButton] CodexPanelController.Instance가 없습니다. " +
                                 "공용 도감 컨트롤러가 항상 켜진 오브젝트에 배치돼 있는지 확인하세요.");
                return;
            }

            if (action == ClickAction.Toggle) codex.Toggle();
            else codex.Open();
        }
    }
}
