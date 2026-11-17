using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Battle.Card;

namespace Battle.UI
{
    // 탭 상단 파워카드에 커서를 올리면 그 카드를 화면 가운데에 보여 준다.
    public class BattlePowerCardPopup : MonoBehaviour
    {
        const float CardWidth = 450f;
        const float CardHeight = 576f;

        static BattlePowerCardPopup _instance;

        RectTransform _panel;
        Component _owner;
        NewCardView _view;

        public static void Show(CardInstance card, Component owner)
        {
            if (card == null || card.data == null || owner == null) return;
            Ensure(owner).Open(card, owner);
        }

        public static void Hide(Component owner)
        {
            if (_instance == null || _instance._owner != owner) return;
            _instance._owner = null;
            if (_instance._panel != null) _instance._panel.gameObject.SetActive(false);
        }

        static BattlePowerCardPopup Ensure(Component anchor)
        {
            if (_instance != null) return _instance;
            Canvas canvas = anchor != null ? anchor.GetComponentInParent<Canvas>() : null;
            if (canvas != null) canvas = canvas.rootCanvas;
            if (canvas == null) canvas = Object.FindFirstObjectByType<Canvas>();
            var go = new GameObject("PowerCardPopup");
            if (canvas != null) go.transform.SetParent(canvas.transform, false);
            _instance = go.AddComponent<BattlePowerCardPopup>();
            return _instance;
        }

        void Awake()
        {
            _panel = gameObject.GetComponent<RectTransform>();
            if (_panel == null) _panel = gameObject.AddComponent<RectTransform>();
            _panel.anchorMin = Vector2.zero;
            _panel.anchorMax = Vector2.one;
            _panel.offsetMin = Vector2.zero;
            _panel.offsetMax = Vector2.zero;
            _panel.pivot = new Vector2(0.5f, 0.5f);

            var group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = false;
            group.interactable = true;
            gameObject.SetActive(false);
        }

        void Open(CardInstance card, Component owner)
        {
            _owner = owner;
            CanvasGroup group = GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 1f;
                group.blocksRaycasts = false;
                group.interactable = true;
            }

            transform.SetAsLastSibling();
            gameObject.SetActive(true);
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyImmediate(transform.GetChild(i).gameObject);
            _view = null;

            Dim();
            Place(card);
        }

        void Dim()
        {
            var go = new GameObject("Dim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            Image image = go.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.55f);
            image.raycastTarget = false;
        }

        void Place(CardInstance card)
        {
            CardHandHUD hud = Object.FindFirstObjectByType<CardHandHUD>(FindObjectsInactive.Include);
            if (hud == null || hud.CardPrefab == null) return;

            _view = Instantiate(hud.CardPrefab, transform);
            Button button = _view.GetComponent<Button>();
            if (button != null) button.enabled = false;
            _view.SetCard(card);

            RectTransform rect = _view.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            Vector2 native = rect.sizeDelta;
            float sx = native.x > 1f ? CardWidth / native.x : 1f;
            float sy = native.y > 1f ? CardHeight / native.y : 1f;
            rect.localScale = new Vector3(sx, sy, 1f);
            rect.anchoredPosition = Vector2.zero;

            CanvasGroup cardGroup = _view.GetComponent<CanvasGroup>();
            if (cardGroup == null) cardGroup = _view.gameObject.AddComponent<CanvasGroup>();
            cardGroup.alpha = 1f;
            cardGroup.blocksRaycasts = false;
            cardGroup.interactable = true;

            Graphic[] graphics = _view.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                graphics[i].raycastTarget = false;
                graphics[i].canvasRenderer.SetAlpha(1f);
            }
        }
    }

    public sealed class PowerCardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public CardInstance Card;

        public void OnPointerEnter(PointerEventData eventData)
        {
            BattlePowerCardPopup.Show(Card, this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            BattlePowerCardPopup.Hide(this);
        }

        void OnDisable()
        {
            BattlePowerCardPopup.Hide(this);
        }
    }
}
