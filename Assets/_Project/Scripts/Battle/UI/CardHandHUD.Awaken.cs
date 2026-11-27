using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using TMPro;
using Battle.Card;
using Battle.Deck;
using Battle;

namespace Battle.UI
{
    public partial class CardHandHUD
    {
        // 각성 카운트 텍스트를 설정
        public void SetAwakenText(string text)
        {
            if (awakenCountText != null) awakenCountText.text = text;
        }

        // 각성 게이지 부착 기준을 외부에서 지정
        public void SetAwakenGaugeAnchor(RectTransform anchor)
        {
            if (anchor == null) return;
            if (awakenGaugeAnchor == anchor && awakenGauge != null) return;
            awakenGaugeAnchor = anchor;
            EnsureAwakenGauge();
            RefitAwakenGaugeToAnchor();
        }

        // 첫 각성 칸의 왼쪽 아래 좌표.
        public bool TryGetAwakenStackAnchor(out RectTransform host, out Vector2 anchoredTopLeft)
        {
            host = awakenGauge != null ? awakenGauge.transform as RectTransform : null;
            anchoredTopLeft = new Vector2(AwakenCellX, -(AwakenCellY + AwakenCellH + 4f));
            return host != null && _awakenSkinReady;
        }

        // 각성 게이지 표시 여부를 제어
        public void SetAwakenGaugeVisible(bool visible)
        {
            if (visible) EnsureAwakenGauge();
            if (awakenGauge != null)
                awakenGauge.gameObject.SetActive(visible);

            if (awakenGaugeLabel != null && awakenGaugeLabel.transform.parent != (awakenGauge != null ? awakenGauge.transform : null))
                awakenGaugeLabel.gameObject.SetActive(visible);
        }

        // 충전/발동 상태에 따라 각성 게이지 fill·색·라벨을 갱신
        public void UpdateAwakenGauge(int chargeCur, int chargeMax, bool active, float timeRemaining, float timeMax)
        {
            EnsureAwakenGauge();
            if (awakenGauge == null) return;

            float fill;
            Color color;
            string label;
            int max = Mathf.Max(1, chargeMax);
            int filled;
            if (active)
            {
                float denom = timeMax > 0f ? timeMax : 1f;
                fill = Mathf.Clamp01(timeRemaining / denom);
                color = awakenActiveColor;
                label = $"{Mathf.Max(0f, timeRemaining):F1}s";
                filled = Mathf.RoundToInt(fill * max);
            }
            else
            {
                int cur = Mathf.Clamp(chargeCur, 0, max);
                fill = (float)cur / max;
                color = awakenChargeColor;
                label = $"{cur}/{max}";
                filled = cur;
            }

            if (_awakenSkinReady)
            {
                bool showTimer = active && _awakenTimerImage != null;
                if (_awakenSegmentRoot != null)
                    _awakenSegmentRoot.gameObject.SetActive(!showTimer);
                if (_awakenTimerImage != null)
                {
                    _awakenTimerImage.gameObject.SetActive(showTimer);
                    if (showTimer) _awakenTimerImage.fillAmount = fill;
                }
                if (!showTimer)
                {
                    EnsureAwakenSegments(max);
                    ApplyAwakenSegmentFill(filled);
                }
            }
            else
            {
                awakenGauge.value = fill;
                if (_awakenGaugeFill != null)
                    _awakenGaugeFill.color = new Color(color.r, color.g, color.b, 1f);
            }

            if (awakenGaugeLabel != null)
            {
                awakenGaugeLabel.gameObject.SetActive(!_awakenSkinReady);
                if (!_awakenSkinReady) awakenGaugeLabel.text = label;
            }
        }

        // 각성 게이지가 없으면 슬라이더/배경/채움/라벨을 코드로 생성
        void EnsureAwakenGauge()
        {
            if (awakenGauge != null)
            {
                if (_awakenGaugeFill == null) _awakenGaugeFill = ResolveGaugeFill(awakenGauge);
                ApplyAwakenGaugeSkin();
                return;
            }

            // anchor가 있으면 그 부모, 없으면 캔버스를 부모로 결정
            Transform parent = null;
            if (awakenGaugeAnchor != null)
                parent = awakenGaugeAnchor.parent != null ? awakenGaugeAnchor.parent : (Transform)awakenGaugeAnchor;
            if (parent == null)
            {
                Canvas canvas = ResolveTargetCanvas();
                parent = canvas != null ? canvas.transform : transform;
            }
            Debug.Log($"[CardHandHUD] AwakenGauge 자동 생성 — parent='{(parent != null ? parent.name : "(null)")}', anchor='{(awakenGaugeAnchor != null ? awakenGaugeAnchor.name : "(없음, 캔버스 중앙 fallback)")}'");

            // 게이지 루트 생성
            var go = new GameObject("AwakenGauge", typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = awakenGaugeSize;

            // 배경 생성
            var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(rect, false);
            var bgRect = (RectTransform)bgGo.transform;
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImg = bgGo.GetComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.6f);
            bgImg.raycastTarget = false;

            // Fill Area 생성
            var fillAreaGo = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaGo.transform.SetParent(rect, false);
            var fillAreaRect = (RectTransform)fillAreaGo.transform;
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = new Vector2(2f, 2f);
            fillAreaRect.offsetMax = new Vector2(-2f, -2f);

            // Fill 생성
            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(fillAreaRect, false);
            var fillRect = (RectTransform)fillGo.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImg = fillGo.GetComponent<Image>();
            fillImg.color = awakenChargeColor;
            fillImg.raycastTarget = false;

            var slider = go.GetComponent<Slider>();
            slider.transition = Selectable.Transition.None;
            slider.interactable = false;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.fillRect = fillRect;
            slider.targetGraphic = fillImg;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
            slider.direction = Slider.Direction.LeftToRight;

            awakenGauge = slider;
            _awakenGaugeFill = fillImg;
            ApplyAwakenGaugeSkin();

            // 라벨이 없으면 생성
            if (awakenGaugeLabel == null)
            {
                var labelGo = new GameObject("Label", typeof(RectTransform));
                labelGo.transform.SetParent(rect, false);
                var labelRect = (RectTransform)labelGo.transform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;
                var tmp = labelGo.AddComponent<TextMeshProUGUI>();
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontSize = 16;
                tmp.fontStyle = FontStyles.Bold;
                tmp.color = Color.white;
                tmp.text = "";
                tmp.raycastTarget = false;
                awakenGaugeLabel = tmp;
            }

            RefitAwakenGaugeToAnchor();

            // anchor 미배선 시 상단 중앙 가시 위치로 배치
            if (awakenGaugeAnchor == null && awakenGauge != null)
            {
                var grect = (RectTransform)awakenGauge.transform;
                grect.anchorMin = grect.anchorMax = grect.pivot = new Vector2(0.5f, 1f);
                grect.anchoredPosition = awakenGaugeFallbackPos;
                grect.SetAsLastSibling();
            }
        }

        // 각성 게이지를 anchor의 정렬/위치에 맞춰 재배치
        void RefitAwakenGaugeToAnchor()
        {
            if (awakenGauge == null || awakenGaugeAnchor == null) return;
            var rect = (RectTransform)awakenGauge.transform;
            var anchorParent = awakenGaugeAnchor.parent != null
                ? awakenGaugeAnchor.parent
                : (Transform)awakenGaugeAnchor;
            if (rect.parent != anchorParent) rect.SetParent(anchorParent, false);

            // LayoutGroup의 위치 덮어쓰기를 막기 위해 layout에서 분리
            var le = awakenGauge.gameObject.GetComponent<LayoutElement>();
            if (le == null) le = awakenGauge.gameObject.AddComponent<LayoutElement>();
            le.ignoreLayout = true;

            if (_awakenSkinReady)
            {
                // HP바 위에 겹쳐 표의 좌표(왼쪽 위 기준)를 그대로 쓴다
                if (rect.parent != awakenGaugeAnchor)
                    rect.SetParent(awakenGaugeAnchor, false);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                rect.anchoredPosition = Vector2.zero;
                rect.localScale = Vector3.one;
                rect.SetAsLastSibling();
                return;
            }

            // 스킨이 없으면 예전처럼 HP바 아래에 둔다
            rect.anchorMin = awakenGaugeAnchor.anchorMin;
            rect.anchorMax = awakenGaugeAnchor.anchorMax;
            rect.pivot = awakenGaugeAnchor.pivot;
            rect.anchoredPosition = awakenGaugeAnchor.anchoredPosition + awakenGaugeOffset;
            rect.sizeDelta = awakenGaugeSize;
            rect.SetAsLastSibling();
        }

        // 각성 게이지를 형제 중 최상단으로 올림
        // 상단 바(체력·스테이지·인벤토리·설정)도 함께 올린다 — 카드 선택/더미 보기 중에도 보이고 눌리도록
        public void BringAwakenGaugeToFront()
        {
            InventoryOpenButton.BringMenubarsToFront();
            if (awakenGauge != null)
                ((RectTransform)awakenGauge.transform).SetAsLastSibling();
        }

        // 슬라이더의 fill Image를 찾아 반환
        static Image ResolveGaugeFill(Slider slider)
        {
            if (slider == null) return null;
            if (slider.fillRect != null)
            {
                var img = slider.fillRect.GetComponent<Image>();
                if (img != null) return img;
            }
            return slider.GetComponentInChildren<Image>();
        }

        // 찬 칸·빈 칸·타이머 스프라이트로 HP바 안쪽 각성 게이지를 만든다
        void ApplyAwakenGaugeSkin()
        {
            if (awakenGauge == null || _awakenSkinReady) return;
            ResolveAwakenSprites();
            if (awakenFillSprite == null || awakenEmptySprite == null)
                return;

            Transform root = awakenGauge.transform;
            HideLegacyAwakenSliderVisuals(root);
            HideAwakenChild(root, "Icon");
            HideAwakenChild(root, "Frame");

            Transform segmentT = root.Find("Segments");
            if (segmentT == null)
            {
                GameObject segGo = new GameObject("Segments", typeof(RectTransform));
                segGo.layer = root.gameObject.layer;
                segGo.transform.SetParent(root, false);
                segmentT = segGo.transform;
            }
            _awakenSegmentRoot = segmentT as RectTransform;
            _awakenSegmentRoot.anchorMin = Vector2.zero;
            _awakenSegmentRoot.anchorMax = Vector2.one;
            _awakenSegmentRoot.offsetMin = Vector2.zero;
            _awakenSegmentRoot.offsetMax = Vector2.zero;
            _awakenSegmentRoot.pivot = new Vector2(0.5f, 0.5f);
            HorizontalLayoutGroup layout = _awakenSegmentRoot.GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
            {
                if (Application.isPlaying) Destroy(layout);
                else DestroyImmediate(layout);
            }

            if (awakenTimerSprite != null)
            {
                Image timer = GetOrCreateAwakenImage(root, "Timer", awakenTimerSprite);
                RectTransform timerRect = timer.rectTransform;
                PlaceTopLeft(timerRect, AwakenTimerX, AwakenTimerY, AwakenTimerW, AwakenTimerH);
                timer.type = Image.Type.Filled;
                timer.fillMethod = Image.FillMethod.Horizontal;
                timer.fillOrigin = (int)Image.OriginHorizontal.Left;
                timer.fillAmount = 1f;
                timer.preserveAspect = false;
                timer.gameObject.SetActive(false);
                _awakenTimerImage = timer;
            }

            _awakenSkinReady = true;
        }

        // 디자인 표의 왼쪽 위 좌표를 RectTransform에 넣는다. y는 아래로 증가
        static void PlaceTopLeft(RectTransform rect, float x, float yDown, float w, float h)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -yDown);
            rect.sizeDelta = new Vector2(w, h);
            rect.localScale = Vector3.one;
        }

        static void HideAwakenChild(Transform root, string name)
        {
            Transform child = root.Find(name);
            if (child != null) child.gameObject.SetActive(false);
        }

        // Resources 폴백으로 각성 게이지 스프라이트를 채운다
        void ResolveAwakenSprites()
        {
            if (awakenIconSprite == null)
                awakenIconSprite = Resources.Load<Sprite>(AwakenSpriteResourcePath + "awaken_icon");
            if (awakenFillSprite == null)
                awakenFillSprite = Resources.Load<Sprite>(AwakenSpriteResourcePath + "awaken_fill");
            if (awakenEmptySprite == null)
                awakenEmptySprite = Resources.Load<Sprite>(AwakenSpriteResourcePath + "awaken_empty");
            if (awakenFrameSprite == null)
                awakenFrameSprite = Resources.Load<Sprite>(AwakenSpriteResourcePath + "awaken_frame");
            if (awakenTimerSprite == null)
                awakenTimerSprite = Resources.Load<Sprite>(AwakenSpriteResourcePath + "awaken_timer");
        }

        // 예전 슬라이더 배경/채움을 숨긴다
        static void HideLegacyAwakenSliderVisuals(Transform root)
        {
            Transform bg = root.Find("Background");
            if (bg != null)
            {
                Image img = bg.GetComponent<Image>();
                if (img != null) img.enabled = false;
            }
            Transform fillArea = root.Find("Fill Area");
            if (fillArea != null) fillArea.gameObject.SetActive(false);
        }

        // 자식 Image를 찾거나 만든다
        static Image GetOrCreateAwakenImage(Transform parent, string name, Sprite sprite)
        {
            Transform existing = parent.Find(name);
            GameObject go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            if (existing == null)
            {
                go.layer = parent.gameObject.layer;
                go.transform.SetParent(parent, false);
            }
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = Color.white;
            image.raycastTarget = false;
            return image;
        }

        // 칸 개수를 맞추고 2·3번 스프라이트를 연결한다
        void EnsureAwakenSegments(int count)
        {
            if (_awakenSegmentRoot == null || count <= 0) return;
            if (_awakenSegmentCount == count && _awakenSegments.Count == count) return;

            for (int i = _awakenSegmentRoot.childCount - 1; i >= 0; i--)
            {
                GameObject child = _awakenSegmentRoot.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
            _awakenSegments.Clear();

            for (int i = 0; i < count; i++)
            {
                GameObject go = new GameObject("Seg" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.layer = _awakenSegmentRoot.gameObject.layer;
                go.transform.SetParent(_awakenSegmentRoot, false);
                Image image = go.GetComponent<Image>();
                image.sprite = awakenEmptySprite;
                image.color = Color.white;
                image.raycastTarget = false;
                image.preserveAspect = false;
                image.type = Image.Type.Simple;
                PlaceTopLeft(image.rectTransform, AwakenCellX + AwakenCellStep * i, AwakenCellY, AwakenCellW, AwakenCellH);
                _awakenSegments.Add(image);
            }
            _awakenSegmentCount = count;
        }

        // 왼쪽부터 filled개만큼 2번(찬 칸), 나머지는 3번(빈 칸)
        void ApplyAwakenSegmentFill(int filled)
        {
            for (int i = 0; i < _awakenSegments.Count; i++)
            {
                if (_awakenSegments[i] == null) continue;
                _awakenSegments[i].sprite = i < filled ? awakenFillSprite : awakenEmptySprite;
            }
        }

        // 각성 활성/비활성에 따라 콤보 UI와 딤을 토글
        public void SetAwakenMode(bool active)
        {
            if (comboSlotPanel != null) comboSlotPanel.SetActive(active);
            if (comboSkillPanel != null) comboSkillPanel.SetActive(active);
            if (awakenHistoryContainer != null) awakenHistoryContainer.gameObject.SetActive(active && awakenHistoryEnabled);

            if (active)
            {
                // 열려 있던 뽑을/버린 더미 보기는 닫는다(런 덱 보기는 유지)
                if (_viewerMode && (_viewerPile == 0 || _viewerPile == 1)) CloseViewer();
                ShowAwakenDim();
                EnsureComboSlotFrames();
            }
            else HideAwakenDim();

            SetTalismanHand(active);
        }

        // 손패 카드를 부적으로(또는 원래 카드로) 불태우며 전환. 같은 상태면 무시.
        void SetTalismanHand(bool on)
        {
            if (_talismanHand == on) return;
            _talismanHand = on;
            int order = 0;
            for (int i = 0; i < _cardViews.Count; i++)
            {
                var view = _cardViews[i];
                if (view == null) continue;
                bool visible = view.gameObject.activeInHierarchy && view.Card != null;
                // 방금 뽑혀 등장 중인 카드는 원래 카드로 올라온 뒤 다시 타서 바뀌느라 늦게 보였다 —
                // 처음부터 바뀐 모습으로 올라오게 즉시 전환한다(이미 손에 있던 카드만 불타는 전환 연출)
                bool animate = visible && !view.IsPlayingDrawIntro;
                view.SetTalismanMode(on, animate: animate, delay: animate ? order++ * Mathf.Max(0f, talismanBurnStagger) : 0f);
            }
        }

        // 각성 전용 딤을 켜고 손패/콤보 UI를 그 위로 올림
        void ShowAwakenDim()
        {
            EnsureDimOverlay();
            _awakenDimActive = true;
            if (dimOverlay != null)
            {
                var img = dimOverlay.GetComponent<Image>();
                if (img != null)
                {
                    img.color = awakenDimColor;
                    Material mat = EnsureAwakenDimMaterial();
                    if (mat != null) img.material = mat;
                    ApplyAwakenDimGradient();
                }
                dimOverlay.gameObject.SetActive(true);
                dimOverlay.SetAsLastSibling();
            }
            // 각성 표식 레이어를 딤 바로 위에 둠(아래 손패/콤보 UI보다는 뒤 → 다른 UI를 가리지 않음)
            EnsureAwakenMarkerLayer();
            if (_awakenMarkerLayer != null)
            {
                _awakenMarkerLayer.gameObject.SetActive(true);
                _awakenMarkerLayer.SetAsLastSibling();
            }

            // 손패/콤보 UI를 딤·표식 위로 올려 어두워지거나 가려지지 않게 함
            if (handRoot != null) handRoot.SetAsLastSibling();
            if (comboSlotPanel != null) comboSlotPanel.transform.SetAsLastSibling();
            if (comboSkillPanel != null) comboSkillPanel.transform.SetAsLastSibling();
            if (awakenHistoryContainer != null) awakenHistoryContainer.SetAsLastSibling();
            if (awakenCountText != null) awakenCountText.transform.SetAsLastSibling();
            BringAwakenGaugeToFront();
        }

        // 각성 표식 레이어가 없으면 전투 캔버스(딤과 같은 캔버스)에 풀스크린으로 생성
        void EnsureAwakenMarkerLayer()
        {
            if (_awakenMarkerLayer != null) return;
            Canvas canvas = ResolveTargetCanvas();
            Transform parent = canvas != null ? canvas.transform : transform;

            var go = new GameObject("AwakenMarkerLayer", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            _awakenMarkerLayer = rect;
            _awakenMarkerLayer.gameObject.SetActive(false);
        }

        // 각성 딤을 끄고 선택/픽커용 단색 딤으로 복구
        void HideAwakenDim()
        {
            _awakenDimActive = false;
            if (dimOverlay != null)
            {
                dimOverlay.gameObject.SetActive(false);
                var img = dimOverlay.GetComponent<Image>();
                if (img != null)
                {
                    img.color = dimColor;
                    if (_awakenDimMaterial != null && img.material == _awakenDimMaterial)
                        img.material = null;
                }
            }
            if (handRoot != null) handRoot.SetAsLastSibling();
        }

        // 각성 딤용 방사형 그라데이션 머티리얼을 준비한다
        Material EnsureAwakenDimMaterial()
        {
            if (_awakenDimMaterial != null) return _awakenDimMaterial;

            Shader shader = Resources.Load<Shader>(AwakenDimShaderResourcePath);
            if (shader == null)
                shader = Shader.Find("UI/AwakenRadialDim");
            if (shader == null)
            {
                Debug.LogWarning("[CardHandHUD] 각성 방사형 딤 셰이더를 찾지 못했습니다. 단색 딤으로 대체합니다.");
                return null;
            }

            _awakenDimMaterial = new Material(shader) { name = "AwakenRadialDim" };
            _awakenDimMaterial.SetColor("_Color", Color.white);
            return _awakenDimMaterial;
        }

        // 화면 비율에 맞춰 중심은 밝고 가장자리는 어두운 원형 폴오프를 갱신한다
        void ApplyAwakenDimGradient()
        {
            if (_awakenDimMaterial == null || dimOverlay == null) return;

            float width = dimOverlay.rect.width;
            float height = dimOverlay.rect.height;
            float aspect = (width > 1f && height > 1f)
                ? width / height
                : (Screen.height > 0 ? (float)Screen.width / Screen.height : 1.777f);

            _awakenDimMaterial.SetFloat("_Aspect", Mathf.Max(0.01f, aspect));
            _awakenDimMaterial.SetFloat("_InnerRadius", awakenDimBrightRadius);
            _awakenDimMaterial.SetFloat("_Softness", Mathf.Max(awakenDimBrightRadius + 0.001f, awakenDimFadeEnd));
        }

        // 각성 입력 히스토리 표시 여부를 런타임에 토글
        public void SetAwakenHistoryEnabled(bool enabled)
        {
            awakenHistoryEnabled = enabled;
            if (!enabled && awakenHistoryContainer != null)
                awakenHistoryContainer.gameObject.SetActive(false);
        }

        // 각성 입력 히스토리를 좌측 세로 격자로 갱신
        public void UpdateAwakenInputHistory(IList<CardElement> history)
        {
            // 비활성화 상태면 컨테이너를 생성하지 않고 숨김
            if (!awakenHistoryEnabled)
            {
                if (awakenHistoryContainer != null) awakenHistoryContainer.gameObject.SetActive(false);
                return;
            }

            EnsureAwakenHistoryContainer();
            if (awakenHistoryContainer == null) return;

            int total = history != null ? history.Count : 0;
            int max = Mathf.Max(1, awakenHistoryMaxItems);
            // 최근 max개만 표시(오래된 것은 잘림)
            int start = Mathf.Max(0, total - max);
            int visible = total - start;

            // 필요 만큼 뷰 풀 확장
            while (_awakenHistoryViews.Count < visible)
            {
                var go = new GameObject($"AwakenHist_{_awakenHistoryViews.Count}", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(awakenHistoryContainer, false);
                var rect = (RectTransform)go.transform;
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = awakenHistoryItemSize;
                var img = go.GetComponent<Image>();
                img.raycastTarget = false;
                _awakenHistoryViews.Add(img);
            }

            // 한 열에 itemsPerColumn개씩 배치하고 초과 시 새 열로 wrap
            int itemsPerCol = Mathf.Max(1, awakenHistoryItemsPerColumn);
            float colW = awakenHistoryItemSize.x + awakenHistoryColumnSpacing;
            float rowH = awakenHistoryItemSize.y + awakenHistoryItemSpacing;
            for (int i = 0; i < _awakenHistoryViews.Count; i++)
            {
                var img = _awakenHistoryViews[i];
                if (i < visible)
                {
                    img.gameObject.SetActive(true);
                    var rect = (RectTransform)img.transform;
                    int col = i / itemsPerCol;
                    int row = i % itemsPerCol;
                    rect.anchoredPosition = new Vector2(col * colW, -row * rowH);

                    var element = history[start + i];
                    var sp = cardPrefab != null ? cardPrefab.GetElementSprite(element) : null;
                    if (sp != null)
                    {
                        img.sprite = sp;
                        img.color = Color.white;
                    }
                    else
                    {
                        img.sprite = null;
                        img.color = ColorForElement(element);
                    }
                }
                else
                {
                    img.gameObject.SetActive(false);
                }
            }
        }

        // 각성 입력 히스토리 컨테이너가 없으면 좌측 중앙에 생성
        void EnsureAwakenHistoryContainer()
        {
            if (awakenHistoryContainer != null) return;
            Canvas canvas = ResolveTargetCanvas();
            Transform parent = canvas != null ? canvas.transform : transform;

            var go = new GameObject("AwakenHistoryContainer", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            awakenHistoryContainer = (RectTransform)go.transform;
            // 좌측 중앙 정렬
            awakenHistoryContainer.anchorMin = new Vector2(0f, 0.5f);
            awakenHistoryContainer.anchorMax = new Vector2(0f, 0.5f);
            awakenHistoryContainer.pivot = new Vector2(0f, 0.5f);
            awakenHistoryContainer.anchoredPosition = awakenHistoryAnchoredPos;
            awakenHistoryContainer.sizeDelta = new Vector2(awakenHistoryItemSize.x, 600f);
        }
    }
}
