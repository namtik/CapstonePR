using UnityEngine;
using UnityEngine.UI;

namespace Battle.UI
{
    // 슬레이더스파이어식 지정 화살표 — Overlay에서 카드→커서/적 베지어를 그린다.
    public class TargetArrowView : MonoBehaviour
    {
        [SerializeField] private int chevronCount = 14; // 곡선 위 체브론 개수
        [SerializeField] private float chevronSize = 30f; // 체브론 한 변 크기(px)
        [SerializeField] private float headSize = 64f; // 화살촉 크기(px)
        [SerializeField] private Color idleColor = new Color(0.90f, 0.16f, 0.18f, 0.88f); // 적 미지정 색
        [SerializeField] private Color hoverColor = new Color(1f, 0.32f, 0.18f, 1f); // 적 호버 색
        [SerializeField] private Color ringColor = new Color(1f, 0.38f, 0.22f, 0.35f); // 호버 링 색
        [SerializeField] private float minDrawDistance = 48f; // 이 거리 이하면 화살표 숨김

        private RectTransform _root; // 전체 화면을 덮는 루트
        private Image[] _chevrons; // 곡선 체브론
        private Image _head; // 화살촉
        private Image _ring; // 적 호버 링
        private static Sprite _triangleSprite; // 삼각형 스프라이트(런타임 생성)
        private static Sprite _discSprite; // 원형 스프라이트(런타임 생성)
        private bool _built; // 자식 생성 여부

        // 드래그 레이어 위에 전화면 Overlay로 생성
        public static TargetArrowView EnsureOn(RectTransform parent)
        {
            if (parent == null) return null;
            Transform existing = parent.Find("TargetArrowView");
            if (existing != null)
            {
                var found = existing.GetComponent<TargetArrowView>();
                if (found != null) return found;
            }

            var go = new GameObject("TargetArrowView", typeof(RectTransform), typeof(TargetArrowView));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            var view = go.GetComponent<TargetArrowView>();
            view._root = rect;
            view.Hide();
            return view;
        }

        void Awake()
        {
            if (_root == null) _root = transform as RectTransform;
        }

        // 화살표를 감추고 모든 이미지를 끄다
        public void Hide()
        {
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        // 스크린 좌표 기준 시작/끝점으로 화살표를 그린다. hoveringTarget면 끝점에 링을 띄운다.
        public void Show(Vector2 startScreen, Vector2 endScreen, bool hoveringTarget, float hoverRadiusScreen = 0f)
        {
            EnsureBuilt();
            if (!gameObject.activeSelf) gameObject.SetActive(true);

            Canvas canvas = GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera : null;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, startScreen, cam, out Vector2 start))
                return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, endScreen, cam, out Vector2 end))
                return;

            float dist = Vector2.Distance(start, end);
            bool draw = dist >= minDrawDistance;
            Color col = hoveringTarget ? hoverColor : idleColor;
            Vector2 control = ComputeControl(start, end);

            int n = _chevrons != null ? _chevrons.Length : 0;
            for (int i = 0; i < n; i++)
            {
                var img = _chevrons[i];
                if (img == null) continue;
                if (!draw)
                {
                    img.gameObject.SetActive(false);
                    continue;
                }

                float t = n <= 1 ? 0.5f : Mathf.Lerp(0.16f, 0.78f, i / (float)(n - 1));
                Vector2 p = Bezier(start, control, end, t);
                Vector2 d = BezierDeriv(start, control, end, t);
                float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f;
                float sz = Mathf.Lerp(chevronSize * 0.65f, chevronSize * 1.2f, t);

                var rt = img.rectTransform;
                rt.anchoredPosition = p;
                rt.localRotation = Quaternion.Euler(0f, 0f, ang);
                rt.sizeDelta = new Vector2(sz, sz);
                img.color = col;
                img.gameObject.SetActive(true);
            }

            if (_head != null)
            {
                if (!draw) _head.gameObject.SetActive(false);
                else
                {
                    Vector2 d = BezierDeriv(start, control, end, 0.98f);
                    float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f;
                    _head.rectTransform.anchoredPosition = end;
                    _head.rectTransform.localRotation = Quaternion.Euler(0f, 0f, ang);
                    _head.rectTransform.sizeDelta = new Vector2(headSize, headSize);
                    _head.color = col;
                    _head.gameObject.SetActive(true);
                }
            }

            if (_ring != null)
            {
                if (!hoveringTarget || !draw)
                {
                    _ring.gameObject.SetActive(false);
                }
                else
                {
                    float localR = hoverRadiusScreen;
                    if (hoverRadiusScreen > 1f &&
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(
                            _root, endScreen + Vector2.right * hoverRadiusScreen, cam, out Vector2 edge))
                    {
                        localR = Vector2.Distance(end, edge);
                    }
                    if (localR < 40f) localR = 90f;
                    _ring.rectTransform.anchoredPosition = end;
                    _ring.rectTransform.sizeDelta = new Vector2(localR * 2f, localR * 2f);
                    _ring.color = ringColor;
                    _ring.gameObject.SetActive(true);
                }
            }
        }

        // 시작에서 위로 붙어 끝점으로 휘어지는 STS식 제어점
        static Vector2 ComputeControl(Vector2 start, Vector2 end)
        {
            Vector2 delta = end - start;
            float dist = delta.magnitude;
            if (dist < 1f) return start + Vector2.up * 80f;
            Vector2 n = delta / dist;
            Vector2 perp = new Vector2(-n.y, n.x);
            if (perp.y < 0f) perp = -perp;
            float bulge = Mathf.Clamp(dist * 0.38f, 90f, 280f);
            return Vector2.Lerp(start, end, 0.32f) + perp * bulge;
        }

        static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        static Vector2 BezierDeriv(Vector2 a, Vector2 b, Vector2 c, float t)
        {
            return 2f * (1f - t) * (b - a) + 2f * t * (c - b);
        }

        void EnsureBuilt()
        {
            if (_built) return;
            if (_root == null) _root = transform as RectTransform;
            EnsureSprites();

            int n = Mathf.Max(4, chevronCount);
            _chevrons = new Image[n];
            for (int i = 0; i < n; i++)
                _chevrons[i] = CreateImage("Chevron_" + i, _triangleSprite, chevronSize);

            _head = CreateImage("ArrowHead", _triangleSprite, headSize);
            _head.transform.SetAsLastSibling();

            _ring = CreateImage("HoverRing", _discSprite, 180f);
            _ring.transform.SetAsFirstSibling();
            _ring.gameObject.SetActive(false);

            _built = true;
        }

        Image CreateImage(string name, Sprite sprite, float size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = gameObject.layer;
            go.transform.SetParent(_root, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.color = idleColor;
            img.raycastTarget = false;
            img.preserveAspect = true;
            return img;
        }

        static void EnsureSprites()
        {
            if (_triangleSprite == null) _triangleSprite = CreateTriangleSprite();
            if (_discSprite == null) _discSprite = CreateDiscSprite();
        }

        // 위로 향한 삼각형(밑면이 아래, 꼭짓점이 위 → 카드에서 적 방향으로 회전)
        static Sprite CreateTriangleSprite()
        {
            const int s = 48;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            tex.name = "TargetArrowTriangle";
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
            {
                float ny = y / (float)(s - 1); // 0=텍스처 아래, 1=위
                float half = (1f - ny) * 0.5f; // 아래가 넓고 위로 갈수록 뾰족
                for (int x = 0; x < s; x++)
                {
                    float nx = x / (float)(s - 1);
                    bool inside = ny < 0.96f && Mathf.Abs(nx - 0.5f) <= half * 0.92f;
                    px[y * s + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0f, 0f, s, s), new Vector2(0.5f, 0.2f), s);
        }

        // 호버 링용 부드러운 원
        static Sprite CreateDiscSprite()
        {
            const int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            tex.name = "TargetArrowDisc";
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[s * s];
            float c = (s - 1) * 0.5f;
            float r = c - 1f;
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / r;
                byte a = d <= 1f ? (byte)Mathf.Clamp(Mathf.RoundToInt((1f - d) * 180f), 0, 180) : (byte)0;
                px[y * s + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0f, 0f, s, s), new Vector2(0.5f, 0.5f), s);
        }
    }
}
