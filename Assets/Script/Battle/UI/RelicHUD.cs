using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Battle.Relic;

namespace Battle.UI
{
    // 좌상단에 보유 유물 아이콘을 표시하고 호버 툴팁을 띄움
    public class RelicHUD : MonoBehaviour
    {
        public static RelicHUD Instance { get; private set; } // 싱글톤 인스턴스

        private const float ICON_SIZE     = 60f; // 아이콘 크기
        private const float ICON_SPACING  = 8f; // 아이콘 간격
        private const float TOOLTIP_PAD   = 12f; // 툴팁 내부 여백
        private const float TT_ICON_SIZE  = 48f; // 툴팁 아이콘 크기

        [Header("HUD 위치")]
        [Tooltip("유물 HUD 아이콘 컨테이너의 좌상단 앵커 기준 위치. Y를 더 작은(음수) 값으로 내리면 화면 아래로 이동")]
        [SerializeField] private Vector2 hudAnchoredPosition = new Vector2(10f, -10f); // HUD 위치

        [Header("HUD 배경 박스")]
        [Tooltip("유물 아이콘 뒤 박스 배경색(알파를 낮추면 반투명)")]
        [SerializeField] private Color hudBackgroundColor = new Color(0f, 0f, 0f, 0.45f); // HUD 배경 색
        [Tooltip("배경 박스 내부 여백 (Left, Right, Top, Bottom)")]
        [SerializeField] private Vector4 hudBackgroundPadding = new Vector4(10f, 10f, 8f, 8f); // 배경 내부 여백

        [Header("툴팁 글씨체")]
        [Tooltip("유물 이름/설명에 사용할 글씨체. 비워두면 TMP 기본 폰트 사용")]
        [SerializeField] private TMP_FontAsset tooltipFont; // 툴팁 폰트
        [Tooltip("유물 이름 글자 크기")]
        [SerializeField] private float nameFontSize = 18f; // 이름 글자 크기
        [Tooltip("유물 설명 글자 크기")]
        [SerializeField] private float descFontSize = 15f; // 설명 글자 크기

        [Header("툴팁 크기")]
        [Tooltip("툴팁 가로 폭(px). 세로 높이는 설명 길이에 맞춰 자동 조절된다")]
        [SerializeField] private float tooltipWidth = 340f; // 툴팁 가로 폭
        [Tooltip("툴팁 최소 세로 높이(px)")]
        [SerializeField] private float tooltipMinHeight = 90f; // 툴팁 최소 높이

        [Header("유물 발동 펄스 (좌상단 아이콘이 커졌다 돌아옴)")]
        [Tooltip("유물 발동 시 해당 아이콘이 커지는 최대 배율(원본 기준). 1.4 = 40% 커졌다 복귀.")]
        [SerializeField] private float relicPulseScale = 1.4f;    // 펄스 확대 배율
        [Tooltip("커졌다 원래대로 돌아오는 전체 시간(초). 이 펄스가 끝난 뒤 효과가 발동된다.")]
        [SerializeField] private float relicPulseDuration = 0.35f; // 펄스 전체 시간

        [Header("유물 발동 텍스트 (아이콘 아래 표시)")]
        [Tooltip("발동 시 아이콘 아래에 잠깐 표시할 텍스트 글자 크기.")]
        [SerializeField] private float relicLabelFontSize = 16f;  // 발동 텍스트 글자 크기
        [Tooltip("발동 텍스트 색.")]
        [SerializeField] private Color relicLabelColor = new Color(0.95f, 0.85f, 0.35f, 1f); // 발동 텍스트 색
        [Tooltip("발동 텍스트가 떠 있는 유지 시간(초) — 이후 페이드아웃.")]
        [SerializeField] private float relicLabelHoldTime = 1.0f; // 발동 텍스트 유지 시간
        [Tooltip("발동 텍스트 페이드아웃 시간(초).")]
        [SerializeField] private float relicLabelFadeTime = 0.35f; // 발동 텍스트 페이드 시간

        private RectTransform   _iconContainer; // 아이콘 컨테이너
        private GameObject      _tooltip; // 툴팁 루트
        private Image           _tooltipIcon; // 툴팁 아이콘
        private TextMeshProUGUI _tooltipName; // 툴팁 이름 텍스트
        private TextMeshProUGUI _tooltipDesc; // 툴팁 설명 텍스트
        private RectTransform   _tooltipRect; // 툴팁 RectTransform
        private float           _textX; // 툴팁 텍스트 시작 X
        private float           _textW; // 툴팁 텍스트 폭
        private float           _nameH; // 이름 줄 높이

        private readonly List<RelicSO>    _relics    = new List<RelicSO>(); // 보유 유물 목록
        private readonly List<GameObject> _iconItems = new List<GameObject>(); // 생성된 아이콘 목록

        private Coroutine       _pulseCo;         // 진행 중인 아이콘 펄스 코루틴
        private TextMeshProUGUI _activationLabel; // 발동 텍스트(아이콘 아래)
        private Coroutine       _labelCo;         // 진행 중인 발동 텍스트 페이드 코루틴

        // 싱글톤 인스턴스를 설정
        void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(gameObject); return; }
        }

        // 싱글톤 참조를 해제
        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // 레이아웃을 구성하고 보유 유물을 초기 표시
        void Start()
        {
            BuildLayout();
            if (RelicManager.Instance != null)
                Refresh(RelicManager.Instance.OwnedRelics);
        }

        // 보유 유물 목록을 갱신하고 아이콘을 재구성
        public void Refresh(IReadOnlyList<RelicSO> relics)
        {
            _relics.Clear();
            _relics.AddRange(relics);
            RebuildIcons();
        }

        // 아이콘 컨테이너와 툴팁 패널을 런타임으로 생성
        void BuildLayout()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            Transform canvasT = canvas.transform;

            // 아이콘 컨테이너를 좌상단에 고정 생성
            var containerGo = new GameObject("RelicIconContainer", typeof(RectTransform));
            containerGo.transform.SetParent(canvasT, false);
            _iconContainer = (RectTransform)containerGo.transform;
            _iconContainer.anchorMin        = new Vector2(0f, 1f);
            _iconContainer.anchorMax        = new Vector2(0f, 1f);
            _iconContainer.pivot            = new Vector2(0f, 1f);
            _iconContainer.anchoredPosition = hudAnchoredPosition;

            var panelBg = containerGo.AddComponent<Image>();
            panelBg.color = hudBackgroundColor;
            panelBg.raycastTarget = false;

            var layout = containerGo.AddComponent<HorizontalLayoutGroup>();
            layout.spacing              = ICON_SPACING;
            layout.childForceExpandWidth  = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth      = false;
            layout.childControlHeight     = false;
            layout.padding = new RectOffset(
                Mathf.RoundToInt(hudBackgroundPadding.x),
                Mathf.RoundToInt(hudBackgroundPadding.y),
                Mathf.RoundToInt(hudBackgroundPadding.z),
                Mathf.RoundToInt(hudBackgroundPadding.w));

            var sizeFitter = containerGo.AddComponent<ContentSizeFitter>();
            sizeFitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 유물이 없으면 컨테이너를 숨김
            containerGo.SetActive(_relics.Count > 0);

            // 툴팁 패널을 좌상단 앵커로 생성
            var tooltipGo = new GameObject("RelicTooltip", typeof(RectTransform));
            tooltipGo.transform.SetParent(canvasT, false);
            tooltipGo.transform.SetAsLastSibling();
            _tooltipRect = (RectTransform)tooltipGo.transform;
            _tooltipRect.anchorMin  = new Vector2(0f, 1f);
            _tooltipRect.anchorMax  = new Vector2(0f, 1f);
            _tooltipRect.pivot      = new Vector2(0f, 1f);
            _tooltipRect.sizeDelta  = new Vector2(tooltipWidth, tooltipMinHeight);
            _tooltip = tooltipGo;

            var bg = tooltipGo.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.12f, 0.93f);

            _textX = TOOLTIP_PAD + TT_ICON_SIZE + 8f;
            _textW = tooltipWidth - _textX - TOOLTIP_PAD;
            _nameH = Mathf.Ceil(nameFontSize * 1.4f);

            // 툴팁 좌상단 아이콘 생성
            var iconGo = new GameObject("TT_Icon", typeof(RectTransform));
            iconGo.transform.SetParent(tooltipGo.transform, false);
            var iconRect = (RectTransform)iconGo.transform;
            iconRect.anchorMin        = new Vector2(0f, 1f);
            iconRect.anchorMax        = new Vector2(0f, 1f);
            iconRect.pivot            = new Vector2(0f, 1f);
            iconRect.anchoredPosition = new Vector2(TOOLTIP_PAD, -TOOLTIP_PAD);
            iconRect.sizeDelta        = new Vector2(TT_ICON_SIZE, TT_ICON_SIZE);
            _tooltipIcon = iconGo.AddComponent<Image>();
            _tooltipIcon.color = new Color(0.6f, 0.5f, 0.2f, 1f); // 아이콘 없을 때 기본색

            // 툴팁 이름 텍스트 생성
            var nameGo = new GameObject("TT_Name", typeof(RectTransform));
            nameGo.transform.SetParent(tooltipGo.transform, false);
            var nameRect = (RectTransform)nameGo.transform;
            nameRect.anchorMin        = new Vector2(0f, 1f);
            nameRect.anchorMax        = new Vector2(0f, 1f);
            nameRect.pivot            = new Vector2(0f, 1f);
            nameRect.anchoredPosition = new Vector2(_textX, -TOOLTIP_PAD);
            nameRect.sizeDelta        = new Vector2(_textW, _nameH);
            _tooltipName = nameGo.AddComponent<TextMeshProUGUI>();
            _tooltipName.fontSize     = nameFontSize;
            _tooltipName.fontStyle    = FontStyles.Bold;
            _tooltipName.color        = Color.white;
            _tooltipName.overflowMode = TextOverflowModes.Overflow;
            if (tooltipFont != null) _tooltipName.font = tooltipFont;

            // 툴팁 설명 텍스트 생성
            var descGo = new GameObject("TT_Desc", typeof(RectTransform));
            descGo.transform.SetParent(tooltipGo.transform, false);
            var descRect = (RectTransform)descGo.transform;
            descRect.anchorMin        = new Vector2(0f, 1f);
            descRect.anchorMax        = new Vector2(0f, 1f);
            descRect.pivot            = new Vector2(0f, 1f);
            descRect.anchoredPosition = new Vector2(_textX, -TOOLTIP_PAD - _nameH - 4f);
            descRect.sizeDelta        = new Vector2(_textW, 64f);
            _tooltipDesc = descGo.AddComponent<TextMeshProUGUI>();
            _tooltipDesc.fontSize     = descFontSize;
            _tooltipDesc.color        = new Color(0.85f, 0.85f, 0.85f, 1f);
            _tooltipDesc.overflowMode = TextOverflowModes.Overflow;
            if (tooltipFont != null) _tooltipDesc.font = tooltipFont;

            _tooltip.SetActive(false);
        }

        // 기존 아이콘을 제거하고 보유 유물로 다시 생성
        void RebuildIcons()
        {
            if (_iconContainer == null) return;
            foreach (var go in _iconItems) if (go != null) Destroy(go);
            _iconItems.Clear();
            _tooltip?.SetActive(false);

            for (int i = 0; i < _relics.Count; i++)
                _iconItems.Add(CreateIconItem(_relics[i], i));

            // 유물이 없으면 빈 박스가 보이지 않도록 컨테이너 숨김
            _iconContainer.gameObject.SetActive(_relics.Count > 0);
        }

        // 유물 1개의 아이콘 오브젝트를 생성하고 호버 트리거를 연결
        GameObject CreateIconItem(RelicSO relic, int index)
        {
            var go = new GameObject($"Relic_{relic.id}", typeof(RectTransform));
            go.transform.SetParent(_iconContainer, false);

            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(ICON_SIZE, ICON_SIZE);

            var img = go.AddComponent<Image>();
            if (relic.icon != null)
            {
                img.sprite = relic.icon;
                img.color  = Color.white;
            }
            else
            {
                img.color = new Color(0.6f, 0.5f, 0.2f, 1f);
            }

            var outline = go.AddComponent<Outline>();
            outline.effectColor    = new Color(0.9f, 0.8f, 0.3f, 0.8f);
            outline.effectDistance = new Vector2(2f, -2f);

            var trigger = go.AddComponent<EventTrigger>();
            int captured = index;
            var relicCaptured = relic;

            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => ShowTooltip(relicCaptured, captured));
            trigger.triggers.Add(enter);

            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => _tooltip?.SetActive(false));
            trigger.triggers.Add(exit);

            return go;
        }

        // 해당 유물의 툴팁 내용을 채우고 아이콘 위치에 표시
        void ShowTooltip(RelicSO relic, int iconIndex)
        {
            if (_tooltip == null || _tooltipRect == null) return;

            // 아이콘/텍스트 갱신
            if (_tooltipIcon != null)
            {
                _tooltipIcon.sprite = relic.icon;
                _tooltipIcon.color  = relic.icon != null ? Color.white : new Color(0.6f, 0.5f, 0.2f, 1f);
            }
            if (_tooltipName != null) _tooltipName.text = relic.displayName;
            if (_tooltipDesc != null)
            {
                _tooltipDesc.text = relic.description;

                // 설명 길이에 맞춰 툴팁 세로 크기 자동 조절
                float descH = _tooltipDesc.GetPreferredValues(
                    relic.description ?? string.Empty, _textW, 0f).y;
                _tooltipDesc.rectTransform.sizeDelta = new Vector2(_textW, descH);

                float contentH = TOOLTIP_PAD + _nameH + 4f + descH + TOOLTIP_PAD;
                float iconH    = TOOLTIP_PAD + TT_ICON_SIZE + TOOLTIP_PAD;
                _tooltipRect.sizeDelta = new Vector2(
                    tooltipWidth, Mathf.Max(contentH, iconH, tooltipMinHeight));
            }

            // 동일 좌상단 앵커 기준으로 아이콘 위치를 계산
            float iconX = hudAnchoredPosition.x + hudBackgroundPadding.x + iconIndex * (ICON_SIZE + ICON_SPACING);
            float iconY = hudAnchoredPosition.y - hudBackgroundPadding.z; // 패딩 적용된 아이콘 상단 Y

            _tooltipRect.anchoredPosition = new Vector2(iconX, iconY - ICON_SIZE - 4f);
            _tooltip.SetActive(true);
        }

        // 유물 발동 시 좌상단 해당 아이콘을 펄스(커졌다 복귀)하고 아래에 텍스트를 띄운 뒤, 끝나면 onShown 호출
        public void PulseRelicIcon(RelicSO relic, string labelText, System.Action onShown)
        {
            if (relic == null) { onShown?.Invoke(); return; }

            int idx = _relics.IndexOf(relic);
            GameObject iconGo = (idx >= 0 && idx < _iconItems.Count) ? _iconItems[idx] : null;
            if (iconGo == null) { onShown?.Invoke(); return; } // 아이콘이 없으면 즉시 효과

            if (!string.IsNullOrEmpty(labelText)) ShowActivationLabel(idx, labelText);

            if (_pulseCo != null) StopCoroutine(_pulseCo);
            _pulseCo = StartCoroutine(PulseRoutine(iconGo.transform, onShown));
        }

        // 아이콘을 커졌다 원래대로 돌려놓는 펄스 코루틴(복귀 후 onShown 호출)
        IEnumerator PulseRoutine(Transform iconTr, System.Action onShown)
        {
            Vector3 baseScale = iconTr != null ? iconTr.localScale : Vector3.one;
            Vector3 peak = baseScale * Mathf.Max(1f, relicPulseScale);
            float dur = Mathf.Max(0.01f, relicPulseDuration);
            const float upPortion = 0.4f; // 앞 40%는 확대, 나머지는 복귀

            float t = 0f;
            while (t < dur)
            {
                if (iconTr == null) { onShown?.Invoke(); _pulseCo = null; yield break; }
                t += Time.unscaledDeltaTime;
                float n = Mathf.Clamp01(t / dur);

                // 0→peak(ease-out) 후 peak→base(선형) 엔벨로프
                float env = n < upPortion
                    ? 1f - (1f - n / upPortion) * (1f - n / upPortion)
                    : 1f - (n - upPortion) / (1f - upPortion);
                iconTr.localScale = Vector3.LerpUnclamped(baseScale, peak, Mathf.Clamp01(env));
                yield return null;
            }

            if (iconTr != null) iconTr.localScale = baseScale;
            _pulseCo = null;

            // 펄스가 끝난 뒤 효과 발동(빙결 부여 → 이펙트)
            onShown?.Invoke();
        }

        // iconIndex번째 아이콘 아래에 발동 텍스트를 띄우고 유지 후 페이드아웃
        void ShowActivationLabel(int iconIndex, string text)
        {
            EnsureActivationLabel();
            if (_activationLabel == null) return;

            // 툴팁과 동일한 좌상단 앵커 기준으로 아이콘 중앙 아래에 배치
            float iconX = hudAnchoredPosition.x + hudBackgroundPadding.x + iconIndex * (ICON_SIZE + ICON_SPACING);
            float iconY = hudAnchoredPosition.y - hudBackgroundPadding.z;
            var rect = (RectTransform)_activationLabel.transform;
            rect.SetAsLastSibling();

            _activationLabel.text = text;
            _activationLabel.color = relicLabelColor;
            _activationLabel.gameObject.SetActive(true);

            // 텍스트 상자를 즉시 글자 크기에 맞게 재계산한 뒤 위치 지정(아이콘 왼쪽 아래)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            rect.anchoredPosition = new Vector2(iconX, iconY - ICON_SIZE - 4f);

            if (_labelCo != null) StopCoroutine(_labelCo);
            _labelCo = StartCoroutine(LabelFadeRoutine());
        }

        // 발동 텍스트를 유지 시간 후 페이드아웃하고 숨김
        IEnumerator LabelFadeRoutine()
        {
            float hold = Mathf.Max(0f, relicLabelHoldTime);
            while (hold > 0f) { hold -= Time.unscaledDeltaTime; yield return null; }

            float fade = Mathf.Max(0.0001f, relicLabelFadeTime);
            float t = 0f;
            while (t < 1f)
            {
                if (_activationLabel == null) { _labelCo = null; yield break; }
                t += Time.unscaledDeltaTime / fade;
                var c = relicLabelColor;
                c.a = relicLabelColor.a * (1f - Mathf.Clamp01(t));
                _activationLabel.color = c;
                yield return null;
            }

            if (_activationLabel != null)
            {
                _activationLabel.gameObject.SetActive(false);
                _activationLabel.color = relicLabelColor;
            }
            _labelCo = null;
        }

        // 발동 텍스트(아이콘 아래)를 1회 생성
        void EnsureActivationLabel()
        {
            if (_activationLabel != null) return;

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            var go = new GameObject("RelicActivationLabel", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f); // 아이콘 왼쪽 아래에서 오른쪽으로 흐름(좌상단이라 화면 밖 방지)

            _activationLabel = go.AddComponent<TextMeshProUGUI>();
            _activationLabel.fontSize = relicLabelFontSize;
            _activationLabel.fontStyle = FontStyles.Bold;
            _activationLabel.alignment = TextAlignmentOptions.TopLeft;
            _activationLabel.color = relicLabelColor;
            _activationLabel.raycastTarget = false;
            _activationLabel.overflowMode = TextOverflowModes.Overflow;
            if (tooltipFont != null) _activationLabel.font = tooltipFont;

            // 글자 크기/길이에 맞춰 텍스트 상자를 자동으로 늘림(작아서 잘리는 문제 방지)
            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

            go.SetActive(false);
        }
    }
}
