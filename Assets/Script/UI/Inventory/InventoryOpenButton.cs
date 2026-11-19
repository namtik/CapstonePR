using System.Collections.Generic;
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

        static readonly List<InventoryOpenButton> _instances = new List<InventoryOpenButton>(); // 활성 인벤토리 버튼 레지스트리

        Vector3 _baseScale = Vector3.one;
        Coroutine _pulseCo;

        // 같은 오브젝트의 Button 클릭에 자동 연결
        void Awake()
        {
            var btn = GetComponent<Button>();
            if (btn != null) btn.onClick.AddListener(HandleClick);
            _baseScale = transform.localScale;
        }

        void OnEnable()
        {
            if (!_instances.Contains(this)) _instances.Add(this);
            _baseScale = transform.localScale;
        }

        void OnDisable()
        {
            _instances.Remove(this);
            if (_pulseCo != null)
            {
                StopCoroutine(_pulseCo);
                _pulseCo = null;
                transform.localScale = _baseScale;
            }
        }

        // 유물 획득 연출의 도착점. preferUnder 자식을 우선하고, 없으면 켜져 있는 버튼을 쓴다.
        public static bool TryGetTarget(Transform preferUnder, out Vector2 screen, out InventoryOpenButton button)
        {
            button = null;
            screen = new Vector2(Screen.width - 72f, Screen.height - 48f);
            InventoryOpenButton fallback = null;
            for (int i = 0; i < _instances.Count; i++)
            {
                InventoryOpenButton btn = _instances[i];
                if (btn == null || !btn.isActiveAndEnabled) continue;
                if (fallback == null) fallback = btn;
                if (preferUnder != null && btn.transform.IsChildOf(preferUnder))
                {
                    button = btn;
                    break;
                }
            }
            if (button == null) button = fallback;
            if (button == null) return false;

            var rt = button.transform as RectTransform;
            if (rt != null) screen = ScreenCenter(rt);
            return true;
        }

        // 아이콘이 버튼에 닿으면 잠깐 커졌다 돌아온다.
        public void PlayReceivePulse()
        {
            if (!isActiveAndEnabled) return;
            if (_pulseCo != null) StopCoroutine(_pulseCo);
            _pulseCo = StartCoroutine(ReceivePulse());
        }

        System.Collections.IEnumerator ReceivePulse()
        {
            Vector3 from = _baseScale.sqrMagnitude < 0.0001f ? Vector3.one : _baseScale;
            Vector3 peak = from * 1.18f;
            float t = 0f;
            const float up = 0.08f;
            while (t < up)
            {
                t += Time.unscaledDeltaTime;
                transform.localScale = Vector3.Lerp(from, peak, Mathf.Clamp01(t / up));
                yield return null;
            }
            t = 0f;
            const float down = 0.14f;
            while (t < down)
            {
                t += Time.unscaledDeltaTime;
                transform.localScale = Vector3.Lerp(peak, from, Mathf.Clamp01(t / down));
                yield return null;
            }
            transform.localScale = from;
            _pulseCo = null;
        }

        static Vector2 ScreenCenter(RectTransform rt)
        {
            Canvas canvas = rt.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            return RectTransformUtility.WorldToScreenPoint(cam, rt.TransformPoint(rt.rect.center));
        }

        // 전체화면 보상/콤보 패널이 menubar를 덮어 인벤토리 버튼이 안 눌리는 문제 방지 —
        // 각 인벤토리 버튼이 속한 menubar(캔버스 직속 조상)를 형제 맨 뒤로 올려 패널 위에 그린다.
        public static void BringMenubarsToFront()
        {
            for (int i = 0; i < _instances.Count; i++)
            {
                var btn = _instances[i];
                if (btn == null || !btn.isActiveAndEnabled) continue;

                Canvas canvas = btn.GetComponentInParent<Canvas>();
                if (canvas == null) continue;

                // 버튼에서 캔버스 직속 자식(menubar 루트)까지 거슬러 올라간다
                Transform root = btn.transform;
                while (root.parent != null && root.parent != canvas.transform)
                    root = root.parent;
                root.SetAsLastSibling();
            }
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
