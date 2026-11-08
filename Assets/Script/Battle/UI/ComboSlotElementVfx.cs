using UnityEngine;
using UnityEngine.UI;
using Battle.Card;

namespace Battle.UI
{
    // 각성 콤보 칸 프레임 주변 속성 연출.
    // 불은 위로 치솟는 불길, 물은 느리게 도는 파도, 바람은 가느다란 줄기, 땅은 굴러가는 돌이다.
    [DisallowMultipleComponent]
    public class ComboSlotElementVfx : MonoBehaviour
    {
        const int MoteCount = 24;
        const int RingTexSize = 220;
        const float FrameSize = 150f;
        const float RingMargin = 35f;
        const float CornerRadiusPx = 14f;
        const float SurgeReturnSeconds = 2f;

        class Mote
        {
            public RectTransform rect;
            public Image image;
            public float phase;
            public float size;
            public float speed;
            public float seed;
            public float spin;
            public int band;
        }

        static Sprite _ringSprite;
        static Sprite _flameSprite;
        static Sprite _waveSprite;
        static Sprite _tearSprite;
        static Sprite _streakSprite;
        static Sprite _rockSprite;
        static Sprite _clodSprite;

        RectTransform _root;
        Image _ring;
        readonly Mote[] _motes = new Mote[MoteCount];
        CardElement _element;
        bool _shown;
        float _pulse;
        float _surge;
        float _clear;
        float _iconRestAlpha = 1f;
        bool _iconRestCaptured;
        Image _icon;

        // 슬롯에 속성이 들어왔을 때 켠다. 같은 속성이면 재생을 유지한다.
        public void Show(CardElement element)
        {
            if (!IsSupported(element))
            {
                Hide();
                return;
            }

            EnsureBuilt();
            bool changed = !_shown || _element != element;
            _element = element;
            _shown = true;
            _root.gameObject.SetActive(true);
            if (changed) RollMotes();
            KeepIconOnTop();
        }

        // 칸이 비면 끈다.
        public void Hide()
        {
            _shown = false;
            _pulse = 0f;
            _surge = 0f;
            _clear = 0f;
            RestoreIconAlpha();
            if (_root != null) _root.gameObject.SetActive(false);
            SettleRoot();
        }

        // 입력이 들어온 순간 한 번 크게 친다.
        public void Pulse()
        {
            if (!_shown) return;
            _pulse = 1f;
        }

        // 콤보 발동 순간. 속성 연출만 세지고, 이펙트와 아이콘은 불투명해졌다가 천천히 돌아온다.
        public void Surge()
        {
            if (!_shown) return;
            _surge = 1f;
            if (_clear <= 0.001f)
                CaptureIconRestAlpha();
            _clear = 1f;
        }

        void LateUpdate()
        {
            if (!_shown || _root == null) return;

            var rect = (RectTransform)transform;
            float width = rect.rect.width;
            float height = rect.rect.height;
            if (width < 2f || height < 2f) return;

            if (_pulse > 0f)
                _pulse = Mathf.Max(0f, _pulse - Time.deltaTime / 0.4f);

            float surge = 0f;
            if (_surge > 0f)
            {
                _surge = Mathf.Max(0f, _surge - Time.deltaTime / SurgeReturnSeconds);
                surge = Mathf.SmoothStep(0f, 1f, _surge);
            }
            else
                SettleRoot();

            float clear = 0f;
            if (_clear > 0f)
            {
                _clear = Mathf.Max(0f, _clear - Time.deltaTime / SurgeReturnSeconds);
                clear = Mathf.SmoothStep(0f, 1f, _clear);
            }

            float side = Mathf.Min(width, height);
            float radius = side * (CornerRadiusPx / FrameSize);
            float halfW = width * 0.5f;
            float halfH = height * 0.5f;
            float kick = _pulse;

            switch (_element)
            {
                case CardElement.Fire:
                    PlaceFire(halfW, halfH, radius, kick, surge);
                    break;
                case CardElement.Water:
                    PlaceWater(halfW, halfH, radius, kick, surge);
                    break;
                case CardElement.Wind:
                    PlaceWind(halfW, halfH, radius, kick, surge);
                    break;
                default:
                    PlaceEarth(halfW, halfH, radius, kick, surge);
                    break;
            }

            ApplyFullOpacity(clear);
        }

        // 불: 테두리를 따라 옮겨 붙으며 혀가 위로 길어진다. 콤보 때는 기둥만 높아진다.
        void PlaceFire(float halfW, float halfH, float radius, float kick, float surge)
        {
            TintRing(new Color(1f, 0.28f, 0.04f, 1f), 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 16f)), 1f);

            for (int i = 0; i < _motes.Length; i++)
            {
                Mote mote = _motes[i];
                mote.phase = Mathf.Repeat(mote.phase + Time.deltaTime * 0.7f * mote.speed * (1f + kick * 0.35f), 1f);
                SampleBorder(mote.phase, halfW, halfH, radius, out Vector2 pos, out Vector2 tangent, out _);

                float tongue = Mathf.Pow(Mathf.Abs(Mathf.Sin(Time.time * (7f + mote.seed * 6f) + mote.phase * 9f)), 1.5f);
                bool ember = mote.size < 0.38f;
                float lift = surge * (ember ? 42f : 76f);
                float height = ember ? 8f + tongue * 26f + lift : 12f + tongue * 20f + lift;
                float width = (ember ? 4f + mote.size * 6f : 9f + mote.size * 8f) * (1f + surge * 0.45f);

                mote.rect.anchoredPosition = pos + Vector2.up * (height * 0.12f + kick * 4f) + tangent * (mote.seed - 0.5f) * 4f;
                mote.rect.sizeDelta = new Vector2(width, height);
                mote.rect.localRotation = Quaternion.Euler(0f, 0f, tangent.x * 28f + Mathf.Sin(Time.time * 13f + mote.seed * 20f) * 12f);

                Color color = Color.Lerp(new Color(0.85f, 0.1f, 0.02f, 1f), new Color(1f, 0.9f, 0.25f, 1f), Mathf.Clamp01(tongue + surge * 0.35f));
                color.a = ember ? 0.75f : 0.95f;
                mote.image.color = color;
            }
        }

        // 물: 테두리를 따라 파도 두 개가 천천히 밀려간다. 콤보 때는 마루만 높아진다.
        void PlaceWater(float halfW, float halfH, float radius, float kick, float surge)
        {
            TintRing(new Color(0.04f, 0.32f, 0.78f, 1f), 0.22f, 1f);

            for (int i = 0; i < _motes.Length; i++)
            {
                Mote mote = _motes[i];
                bool foam = mote.band == 2;
                mote.phase = Mathf.Repeat(mote.phase + Time.deltaTime * 0.32f * mote.speed * (1f + kick * 0.15f), 1f);
                SampleBorder(mote.phase, halfW, halfH, radius, out Vector2 pos, out Vector2 tangent, out Vector2 outward);

                float swell = Mathf.Sin((mote.phase * 2f - Time.time * 0.28f) * Mathf.PI * 2f);
                float crest = Mathf.SmoothStep(0.05f, 0.75f, Mathf.Clamp01(swell));
                float outAng = Mathf.Atan2(outward.y, outward.x) * Mathf.Rad2Deg - 90f;

                if (crest <= 0.02f)
                {
                    mote.rect.localScale = Vector3.zero;
                    continue;
                }

                mote.rect.localScale = Vector3.one;
                mote.rect.localRotation = Quaternion.Euler(0f, 0f, outAng + tangent.x * 8f);
                if (!foam)
                {
                    float w = Mathf.Lerp(34f, 48f, mote.size) * Mathf.Lerp(0.75f, 1.15f, crest) * (1f + surge * 0.3f);
                    float h = Mathf.Lerp(26f, 40f, crest) * (1f + surge * 1.45f);
                    mote.rect.sizeDelta = new Vector2(w, h);
                    mote.rect.anchoredPosition = pos + outward * (2f + crest * (3f + surge * 34f));
                    Color color = Color.Lerp(new Color(0.05f, 0.38f, 0.82f, 1f), new Color(0.82f, 0.96f, 1f, 1f), crest * 0.85f);
                    color.a = Mathf.Lerp(0.15f, 0.95f, crest);
                    mote.image.color = color;
                }
                else
                {
                    float s = Mathf.Lerp(5f, 9f, mote.size) * crest * (1f + surge * 1f);
                    mote.rect.sizeDelta = new Vector2(s, s * 1.3f);
                    mote.rect.anchoredPosition = pos + outward * (10f + crest * (16f + surge * 30f));
                    Color color = new Color(0.85f, 0.97f, 1f, crest);
                    mote.image.color = color;
                }
            }
        }

        // 바람: 가늘고 긴 줄기가 테두리를 빠르게 휩쓴다. 콤보 때는 줄기가 길어지고 더 바깥까지 친다.
        void PlaceWind(float halfW, float halfH, float radius, float kick, float surge)
        {
            TintRing(new Color(0.6f, 1f, 0.7f, 1f), 0.28f + surge * 0.2f, 1f);

            for (int i = 0; i < _motes.Length; i++)
            {
                Mote mote = _motes[i];
                float speed = (mote.band == 1 ? 2.6f : 1.9f) * mote.speed * (1f + kick * 0.25f + surge * 0.95f);
                mote.phase = Mathf.Repeat(mote.phase + Time.deltaTime * speed, 1f);
                SampleBorder(mote.phase, halfW, halfH, radius, out Vector2 pos, out Vector2 tangent, out Vector2 outward);

                float whip = Mathf.Sin(mote.phase * Mathf.PI * 8f + Time.time * 3f);
                mote.rect.anchoredPosition = pos + outward * (5f + mote.band * 8f + whip * 12f + surge * 34f);
                mote.rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg);
                float length = Mathf.Lerp(26f, mote.band == 1 ? 40f : 54f, mote.size) * (1f + surge * 1.15f);
                float thickness = Mathf.Lerp(2.5f, 5f, mote.seed) * (1f + surge * 0.7f);
                mote.rect.sizeDelta = new Vector2(length, thickness);

                Color color = Color.Lerp(new Color(0.45f, 0.9f, 0.55f, 1f), new Color(0.92f, 1f, 0.9f, 1f), mote.size);
                color.a = 0.45f + 0.4f * Mathf.Clamp01(whip * 0.5f + 0.5f);
                mote.image.color = color;
            }
        }

        // 땅: 각진 바위가 테두리를 굴러가고, 그 사이에 흙덩이가 튀어 오른다. 콤보 때는 흙이 더 멀리 튄다.
        void PlaceEarth(float halfW, float halfH, float radius, float kick, float surge)
        {
            float beat = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Time.time * 2.2f)), 12f);
            TintRing(new Color(0.42f, 0.36f, 0.28f, 1f), 0.35f + beat * 0.35f, 1f);

            for (int i = 0; i < _motes.Length; i++)
            {
                Mote mote = _motes[i];
                bool rock = mote.band == 0;
                float speed = rock ? 0.28f : 0.7f;
                mote.phase = Mathf.Repeat(mote.phase + Time.deltaTime * speed * mote.speed, 1f);
                SampleBorder(mote.phase, halfW, halfH, radius, out Vector2 pos, out Vector2 tangent, out Vector2 outward);

                if (rock)
                {
                    float hop = Mathf.Abs(Mathf.Sin(mote.phase * Mathf.PI * 2f)) * 4f;
                    float side = Mathf.Lerp(20f, 34f, mote.size);
                    mote.rect.sizeDelta = new Vector2(side, side * Mathf.Lerp(0.82f, 1.05f, mote.seed));
                    mote.rect.anchoredPosition = pos + outward * (1f + hop + beat * 9f + surge * 10f);
                    mote.rect.localRotation = Quaternion.Euler(0f, 0f, mote.seed * 360f + mote.phase * 360f * Mathf.Sign(mote.spin));
                    mote.image.color = Color.Lerp(new Color(0.32f, 0.3f, 0.27f, 1f), new Color(0.62f, 0.56f, 0.46f, 1f), mote.seed);
                }
                else
                {
                    float bounce = Mathf.Abs(Mathf.Sin(Time.time * (3f + mote.seed * 4f) + mote.phase * 8f));
                    float side = Mathf.Lerp(7f, 13f, mote.size) * (0.85f + bounce * 0.3f + surge * 0.95f);
                    mote.rect.sizeDelta = new Vector2(side, side * 0.8f);
                    mote.rect.anchoredPosition = pos + outward * (6f + bounce * 7f + surge * 54f) + tangent * (mote.seed - 0.5f) * (6f + surge * 24f);
                    mote.rect.localRotation = Quaternion.Euler(0f, 0f, mote.seed * 180f + bounce * 25f);
                    Color soil = Color.Lerp(new Color(0.36f, 0.22f, 0.1f, 1f), new Color(0.55f, 0.38f, 0.18f, 1f), mote.size);
                    soil.a = 0.8f;
                    mote.image.color = soil;
                }
            }
        }

        void SettleRoot()
        {
            if (_root == null) return;
            _root.localScale = Vector3.one;
            _root.localRotation = Quaternion.identity;
            _root.anchoredPosition = Vector2.zero;
        }

        // 콤보 순간 링, 파티클, 속성 아이콘의 알파를 1로 올렸다가 원래 값으로 되돌린다.
        void ApplyFullOpacity(float amount)
        {
            if (amount <= 0f)
            {
                RestoreIconAlpha();
                return;
            }

            if (_ring != null)
            {
                Color ring = _ring.color;
                ring.a = Mathf.Lerp(ring.a, 1f, amount);
                _ring.color = ring;
            }

            for (int i = 0; i < _motes.Length; i++)
            {
                Image image = _motes[i].image;
                if (image == null) continue;
                Color color = image.color;
                color.a = Mathf.Lerp(color.a, 1f, amount);
                image.color = color;
            }

            Image icon = IconImage();
            if (icon == null || !icon.enabled) return;
            Color iconColor = icon.color;
            iconColor.a = Mathf.Lerp(_iconRestAlpha, 1f, amount);
            icon.color = iconColor;
        }

        void CaptureIconRestAlpha()
        {
            Image icon = IconImage();
            if (icon == null) return;
            _iconRestAlpha = icon.color.a;
            _iconRestCaptured = true;
        }

        void RestoreIconAlpha()
        {
            if (!_iconRestCaptured) return;
            Image icon = IconImage();
            _iconRestCaptured = false;
            if (icon == null) return;
            Color color = icon.color;
            color.a = _iconRestAlpha;
            icon.color = color;
        }

        Image IconImage()
        {
            if (_icon == null)
            {
                Transform icon = transform.Find("ElementIcon");
                if (icon != null) _icon = icon.GetComponent<Image>();
            }
            return _icon;
        }

        void TintRing(Color color, float alpha, float scale)
        {
            if (_ring == null) return;
            color.a = Mathf.Clamp01(alpha);
            _ring.color = color;
            _ring.rectTransform.localScale = new Vector3(scale, scale, 1f);
        }

        void EnsureBuilt()
        {
            if (_root != null) return;

            var rootGo = new GameObject("ElementVfx", typeof(RectTransform));
            rootGo.transform.SetParent(transform, false);
            _root = rootGo.GetComponent<RectTransform>();
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = Vector2.zero;
            _root.localScale = Vector3.one;

            var ringGo = new GameObject("ElementVfxRing", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            ringGo.transform.SetParent(_root, false);
            var ringRect = ringGo.GetComponent<RectTransform>();
            float overflow = RingMargin / FrameSize;
            ringRect.anchorMin = new Vector2(-overflow, -overflow);
            ringRect.anchorMax = new Vector2(1f + overflow, 1f + overflow);
            ringRect.offsetMin = Vector2.zero;
            ringRect.offsetMax = Vector2.zero;
            _ring = ringGo.GetComponent<Image>();
            _ring.sprite = RingSprite();
            _ring.raycastTarget = false;

            for (int i = 0; i < _motes.Length; i++)
            {
                var go = new GameObject("Mote", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(_root, false);
                var moteRect = go.GetComponent<RectTransform>();
                moteRect.anchorMin = moteRect.anchorMax = new Vector2(0.5f, 0.5f);
                moteRect.pivot = new Vector2(0.5f, 0.5f);
                var image = go.GetComponent<Image>();
                image.raycastTarget = false;
                _motes[i] = new Mote { rect = moteRect, image = image, phase = i / (float)MoteCount };
            }
        }

        void RollMotes()
        {
            for (int i = 0; i < _motes.Length; i++)
            {
                Mote mote = _motes[i];
                mote.phase = Mathf.Repeat(i / (float)MoteCount + Random.Range(0f, 0.03f), 1f);
                mote.size = Random.value;
                mote.speed = Random.Range(0.85f, 1.15f);
                mote.seed = Random.value;
                mote.spin = Random.Range(70f, 160f) * (Random.value < 0.5f ? -1f : 1f);
                mote.band = 0;
                mote.rect.localScale = Vector3.one;

                switch (_element)
                {
                    case CardElement.Fire:
                        mote.image.sprite = FlameSprite();
                        mote.rect.pivot = new Vector2(0.5f, 0f);
                        break;
                    case CardElement.Water:
                        if (i % 6 == 0)
                        {
                            mote.band = 2;
                            mote.image.sprite = TearSprite();
                            mote.rect.pivot = new Vector2(0.5f, 0.15f);
                        }
                        else
                        {
                            mote.band = 0;
                            mote.image.sprite = WaveSprite();
                            mote.rect.pivot = new Vector2(0.62f, 0.12f);
                        }
                        break;
                    case CardElement.Wind:
                        mote.band = i % 4 == 0 ? 1 : 0;
                        mote.image.sprite = StreakSprite();
                        mote.rect.pivot = new Vector2(1f, 0.5f);
                        break;
                    default:
                        mote.band = i < 8 ? 0 : 1;
                        mote.image.sprite = mote.band == 0 ? RockSprite() : ClodSprite();
                        mote.rect.pivot = new Vector2(0.5f, 0.5f);
                        break;
                }
            }
        }

        void KeepIconOnTop()
        {
            Transform icon = transform.Find("ElementIcon");
            if (icon != null) icon.SetAsLastSibling();
        }

        static bool IsSupported(CardElement element)
        {
            return element == CardElement.Fire
                || element == CardElement.Water
                || element == CardElement.Wind
                || element == CardElement.Earth;
        }

        // 시계 방향으로 둥근 사각형 테두리를 돈다. +Y가 위다.
        static void SampleBorder(float u, float halfW, float halfH, float radius, out Vector2 pos, out Vector2 tangent, out Vector2 outward)
        {
            radius = Mathf.Clamp(radius, 0f, Mathf.Min(halfW, halfH));
            float straightW = Mathf.Max(0f, (halfW - radius) * 2f);
            float straightH = Mathf.Max(0f, (halfH - radius) * 2f);
            float arc = Mathf.PI * 0.5f * radius;
            float perimeter = 2f * (straightW + straightH) + 4f * arc;
            float d = Mathf.Repeat(u, 1f) * Mathf.Max(0.001f, perimeter);

            if (d < straightW)
            {
                float t = straightW > 0f ? d / straightW : 0f;
                pos = new Vector2(Mathf.Lerp(-halfW + radius, halfW - radius, t), halfH);
                tangent = Vector2.right;
            }
            else if ((d -= straightW) < arc)
            {
                Corner(d, arc, new Vector2(halfW - radius, halfH - radius), Mathf.PI * 0.5f, 0f, radius, out pos, out tangent);
            }
            else if ((d -= arc) < straightH)
            {
                float t = straightH > 0f ? d / straightH : 0f;
                pos = new Vector2(halfW, Mathf.Lerp(halfH - radius, -halfH + radius, t));
                tangent = Vector2.down;
            }
            else if ((d -= straightH) < arc)
            {
                Corner(d, arc, new Vector2(halfW - radius, -halfH + radius), 0f, -Mathf.PI * 0.5f, radius, out pos, out tangent);
            }
            else if ((d -= arc) < straightW)
            {
                float t = straightW > 0f ? d / straightW : 0f;
                pos = new Vector2(Mathf.Lerp(halfW - radius, -halfW + radius, t), -halfH);
                tangent = Vector2.left;
            }
            else if ((d -= straightW) < arc)
            {
                Corner(d, arc, new Vector2(-halfW + radius, -halfH + radius), -Mathf.PI * 0.5f, -Mathf.PI, radius, out pos, out tangent);
            }
            else if ((d -= arc) < straightH)
            {
                float t = straightH > 0f ? d / straightH : 0f;
                pos = new Vector2(-halfW, Mathf.Lerp(-halfH + radius, halfH - radius, t));
                tangent = Vector2.up;
            }
            else
            {
                d -= straightH;
                Corner(d, arc, new Vector2(-halfW + radius, halfH - radius), Mathf.PI, Mathf.PI * 0.5f, radius, out pos, out tangent);
            }

            outward = new Vector2(-tangent.y, tangent.x);
        }

        static void Corner(float d, float arc, Vector2 center, float from, float to, float radius, out Vector2 pos, out Vector2 tangent)
        {
            float t = arc > 0f ? Mathf.Clamp01(d / arc) : 0f;
            float ang = Mathf.Lerp(from, to, t);
            pos = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius;
            tangent = new Vector2(Mathf.Sin(ang), -Mathf.Cos(ang));
            if (to > from) tangent = -tangent;
        }

        static Sprite RingSprite()
        {
            if (_ringSprite != null) return _ringSprite;

            const int n = RingTexSize;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.name = "ComboSlotElementRing";

            Vector2 center = new Vector2((n - 1) * 0.5f, (n - 1) * 0.5f);
            float half = (n - 1) * 0.5f - RingMargin;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dist = SdRoundedBox(new Vector2(x, y) - center, new Vector2(half, half), CornerRadiusPx);
                    float band = Mathf.Clamp01(1f - Mathf.Abs(dist - 4f) / 16f);
                    float glow = dist > -8f ? Mathf.Clamp01(1f - (dist + 8f) / 40f) : 0f;
                    float alpha = Mathf.Clamp01(Mathf.Max(band, glow * 0.8f));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            tex.Apply();
            _ringSprite = Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), 100f);
            return _ringSprite;
        }

        static float SdRoundedBox(Vector2 p, Vector2 b, float r)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - b + new Vector2(r, r);
            return Mathf.Min(Mathf.Max(q.x, q.y), 0f) + new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude - r;
        }

        // 위가 뾰족한 불길. 피벗은 바닥.
        static Sprite FlameSprite()
        {
            if (_flameSprite != null) return _flameSprite;
            const int n = 48;
            var tex = NewTex(n, "ComboSlotFlame");
            float c = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
            {
                float ny = y / (n - 1f);
                float halfW = Mathf.Lerp(0.46f, 0.05f, ny * ny) * c;
                for (int x = 0; x < n; x++)
                {
                    float dx = Mathf.Abs(x - c) / Mathf.Max(0.01f, halfW);
                    float a = Mathf.Clamp01(1f - dx);
                    a *= Mathf.Clamp01(ny * 1.8f);
                    a = Mathf.Pow(a, 0.8f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            _flameSprite = Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0f), 100f);
            return _flameSprite;
        }

        // 왼쪽 골에서 오른쪽 마루로 넘어가며 끝이 말리는 파도.
        static Sprite WaveSprite()
        {
            if (_waveSprite != null) return _waveSprite;
            const int n = 64;
            var tex = NewTex(n, "ComboSlotWave");
            Vector2 crest = new Vector2(0.74f, 0.7f);
            Vector2 curl = new Vector2(0.5f, 0.52f);
            float curlRadius = Vector2.Distance(curl, crest);
            float curlStart = Mathf.Atan2(crest.y - curl.y, crest.x - curl.x);
            for (int y = 0; y < n; y++)
            {
                float ny = y / (n - 1f);
                for (int x = 0; x < n; x++)
                {
                    float nx = x / (n - 1f);
                    float t = Mathf.Clamp01(nx / crest.x);
                    float surface = Mathf.Lerp(0.18f, crest.y, Mathf.Pow(t, 1.35f));
                    if (nx > crest.x)
                        surface = Mathf.Lerp(crest.y, 0.22f, (nx - crest.x) / (1f - crest.x));

                    float body = ny <= surface ? 1f : Mathf.Clamp01((surface + 0.025f - ny) / 0.025f);
                    body *= Mathf.SmoothStep(0f, 0.04f, nx) * Mathf.SmoothStep(1f, 0.92f, nx);

                    Vector2 fromCurl = new Vector2(nx, ny) - curl;
                    float ang = Mathf.Atan2(fromCurl.y, fromCurl.x);
                    float sweep = Mathf.Repeat(ang - curlStart, Mathf.PI * 2f);
                    float hook = 0f;
                    if (sweep < 2.1f && ny > surface - 0.02f)
                        hook = Mathf.Clamp01(1f - Mathf.Abs(fromCurl.magnitude - curlRadius) / 0.055f);

                    float a = Mathf.Clamp01(Mathf.Max(body, hook));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            _waveSprite = Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.62f, 0.12f), 100f);
            return _waveSprite;
        }

        // 위가 뾰족하고 아래가 둥근 물방울.
        static Sprite TearSprite()
        {
            if (_tearSprite != null) return _tearSprite;
            const int n = 40;
            var tex = NewTex(n, "ComboSlotTear");
            float c = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
            {
                float ny = y / (n - 1f);
                float half = ny < 0.42f
                    ? Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((ny - 0.28f) / 0.32f, 2f))) * c * 0.46f
                    : Mathf.Lerp(c * 0.42f, 0.4f, (ny - 0.42f) / 0.58f);
                for (int x = 0; x < n; x++)
                {
                    float dx = Mathf.Abs(x - c);
                    float a = half > 0.2f ? Mathf.Clamp01(1f - dx / half) : 0f;
                    a = Mathf.Pow(a, 0.55f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            _tearSprite = Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.32f), 100f);
            return _tearSprite;
        }

        // 오른쪽이 머리인 바람 줄기.
        static Sprite StreakSprite()
        {
            if (_streakSprite != null) return _streakSprite;
            const int n = 32;
            var tex = NewTex(n, "ComboSlotStreak");
            float c = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float head = x / (n - 1f);
                    float across = Mathf.Abs(y - c) / c;
                    float a = Mathf.Clamp01(head * head) * Mathf.Clamp01(1f - across * 2.2f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            _streakSprite = Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(1f, 0.5f), 100f);
            return _streakSprite;
        }

        // 모서리가 각진 바위.
        static Sprite RockSprite()
        {
            if (_rockSprite != null) return _rockSprite;
            const int n = 48;
            var tex = NewTex(n, "ComboSlotRock");
            float c = (n - 1) * 0.5f;
            float[] radii = { 0.98f, 0.58f, 0.9f, 0.52f, 0.94f, 0.64f, 0.86f };
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float ang = Mathf.Atan2(y - c, x - c);
                    float u = (ang + Mathf.PI) / (Mathf.PI * 2f) * radii.Length;
                    int i0 = Mathf.FloorToInt(u);
                    float f = u - i0;
                    float r0 = radii[((i0 % radii.Length) + radii.Length) % radii.Length];
                    float r1 = radii[(((i0 + 1) % radii.Length) + radii.Length) % radii.Length];
                    float limit = Mathf.Lerp(r0, r1, f);
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / (c * limit);
                    float a = Mathf.Clamp01((1.04f - d) * 8f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            _rockSprite = Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), 100f);
            return _rockSprite;
        }

        // 울퉁불퉁한 흙덩이.
        static Sprite ClodSprite()
        {
            if (_clodSprite != null) return _clodSprite;
            const int n = 40;
            var tex = NewTex(n, "ComboSlotClod");
            float c = (n - 1) * 0.5f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float ang = Mathf.Atan2(y - c, x - c);
                    float lobe = 0.62f + 0.2f * Mathf.Sin(ang * 3f) + 0.1f * Mathf.Sin(ang * 7f + 0.6f);
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c)) / (c * lobe);
                    float a = Mathf.Clamp01(1f - d);
                    a = Mathf.Pow(a, 0.45f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            _clodSprite = Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), 100f);
            return _clodSprite;
        }

        static Texture2D NewTex(int n, string name)
        {
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            tex.name = name;
            return tex;
        }
    }
}
