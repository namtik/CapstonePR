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
        [SerializeField] private Color lockOnColor = new Color(1f, 0.18f, 0.18f, 1f); // 록온 브래킷 색
        [SerializeField] private float lockOnBracketSize = 42f; // 코너 브래킷 한 변
        [Tooltip("적 조준 반경에 곱하는 값. 몬스터가 클 때 프레임이 화면 밖으로 나가면 낮춘다.")]
        [SerializeField] private float lockOnRadiusScale = 0.45f; // 임시: 프레임을 가운데로 축소
        [SerializeField] private float lockOnMaxHalf = 130f; // 록온 프레임 반경 상한(px)
        [SerializeField] private float minDrawDistance = 48f; // 이 거리 이하면 화살표 숨김

        private RectTransform _root; // 전체 화면을 덮는 루트
        private Image[] _chevrons; // 곡선 체브론
        private Image _head; // 화살촉
        private RectTransform _lockOnRoot; // 록온 프레임 루트
        private Image[] _lockOnCorners; // 네 코너 L자 브래킷
        private static Sprite _triangleSprite; // 삼각형 스프라이트(런타임 생성)
        private static Sprite _bracketSprite; // L자 브래킷 스프라이트(런타임 생성)
        private Sprite _arrowOverride; // HUD에서 넣은 화살표 이미지
        private Sprite _bracketOverride; // HUD에서 넣은 브래킷 이미지
        private bool _built; // 자식 생성 여부

        // CardHandHUD에서 커스텀 아트를 넣는다. 비우면 기본 도형을 쓴다.
        public void SetCustomSprites(Sprite arrow, Sprite bracket)
        {
            _arrowOverride = arrow;
            _bracketOverride = bracket;
            if (!_built) return;
            Sprite arrowSp = ResolveArrowSprite();
            Sprite bracketSp = ResolveBracketSprite();
            if (_chevrons != null)
            {
                for (int i = 0; i < _chevrons.Length; i++)
                    if (_chevrons[i] != null) _chevrons[i].sprite = arrowSp;
            }
            if (_head != null) _head.sprite = arrowSp;
            if (_lockOnCorners != null)
            {
                for (int i = 0; i < _lockOnCorners.Length; i++)
                    if (_lockOnCorners[i] != null) _lockOnCorners[i].sprite = bracketSp;
            }
        }

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

        // 스크린 좌표 기준 시작/끝점으로 화살표를 그린다. hoveringTarget면 적 주위에 록온 브래킷을 띄운다.
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

            if (_lockOnRoot != null)
            {
                if (!hoveringTarget)
                {
                    _lockOnRoot.gameObject.SetActive(false);
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
                    localR *= Mathf.Clamp(lockOnRadiusScale, 0.15f, 1f);
                    if (localR < 40f) localR = 40f;
                    if (localR > lockOnMaxHalf) localR = lockOnMaxHalf;
                    _lockOnRoot.anchoredPosition = end;
                    _lockOnRoot.gameObject.SetActive(true);
                    PlaceLockOnCorners(localR);
                }
            }
        }

        // 네 코너에 L자 브래킷을 배치한다(좌상/우상/우하/좌하)
        void PlaceLockOnCorners(float half)
        {
            if (_lockOnCorners == null) return;
            Vector2[] pos =
            {
                new Vector2(-half, half),
                new Vector2(half, half),
                new Vector2(half, -half),
                new Vector2(-half, -half),
            };
            float[] rot = { 0f, -90f, 180f, 90f };
            float size = Mathf.Max(28f, lockOnBracketSize);
            for (int i = 0; i < _lockOnCorners.Length; i++)
            {
                var img = _lockOnCorners[i];
                if (img == null) continue;
                var rt = img.rectTransform;
                rt.anchoredPosition = pos[i];
                rt.localRotation = Quaternion.Euler(0f, 0f, rot[i]);
                rt.sizeDelta = new Vector2(size, size);
                img.color = lockOnColor;
                img.gameObject.SetActive(true);
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

            Sprite arrowSp = ResolveArrowSprite();
            int n = Mathf.Max(4, chevronCount);
            _chevrons = new Image[n];
            for (int i = 0; i < n; i++)
                _chevrons[i] = CreateImage("Chevron_" + i, arrowSp, chevronSize);

            _head = CreateImage("ArrowHead", arrowSp, headSize);
            _head.transform.SetAsLastSibling();

            BuildLockOnFrame();
            _built = true;
        }

        void BuildLockOnFrame()
        {
            var go = new GameObject("LockOnFrame", typeof(RectTransform));
            go.layer = gameObject.layer;
            go.transform.SetParent(_root, false);
            _lockOnRoot = (RectTransform)go.transform;
            _lockOnRoot.anchorMin = _lockOnRoot.anchorMax = _lockOnRoot.pivot = new Vector2(0.5f, 0.5f);
            _lockOnRoot.sizeDelta = Vector2.zero;
            _lockOnRoot.SetAsFirstSibling();

            _lockOnCorners = new Image[4];
            string[] names = { "TL", "TR", "BR", "BL" };
            for (int i = 0; i < 4; i++)
            {
                _lockOnCorners[i] = CreateImage("LockOn_" + names[i], ResolveBracketSprite(), lockOnBracketSize);
                _lockOnCorners[i].transform.SetParent(_lockOnRoot, false);
                _lockOnCorners[i].color = lockOnColor;
                var rt = _lockOnCorners[i].rectTransform;
                rt.pivot = new Vector2(0f, 1f); // L자의 바깥 꼭짓점
            }
            _lockOnRoot.gameObject.SetActive(false);
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

        void EnsureSprites()
        {
            if (_arrowOverride == null && _triangleSprite == null)
                _triangleSprite = CreateTriangleSprite();
            if (_bracketOverride == null && _bracketSprite == null)
                _bracketSprite = CreateBracketSprite();
        }

        Sprite ResolveArrowSprite() => _arrowOverride != null ? _arrowOverride : _triangleSprite;
        Sprite ResolveBracketSprite() => _bracketOverride != null ? _bracketOverride : _bracketSprite;

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

        // 좌상단 L자 — 위쪽 가로 + 왼쪽 세로. 피벗을 (0,1)로 두면 네 코너에 회전 배치 가능
        static Sprite CreateBracketSprite()
        {
            const int s = 64;
            const int thickness = 7;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            tex.name = "TargetLockOnBracket";
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                bool on = (y >= s - thickness) || (x < thickness);
                px[y * s + x] = on ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0f, 0f, s, s), new Vector2(0f, 1f), s);
        }
    }
}
