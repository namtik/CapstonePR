using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Battle.UI
{
    /// <summary>
    /// 신규 전투 전용 콤보 스킬 보상 오버레이.
    /// 씬의 RewardHub/SkillRewardUI 연결 상태와 무관하게 런타임으로 생성한다.
    /// </summary>
    public class ComboSkillRewardOverlayUI : MonoBehaviour
    {
        public static ComboSkillRewardOverlayUI Instance { get; private set; }

        const int DEFAULT_CHOICE_COUNT = 3;

        private System.Action<SkillDataParser.SkillData> _onPicked;
        private GameObject _panel;
        private static Font _builtinFont;

        void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(this); return; }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static ComboSkillRewardOverlayUI EnsureExists()
        {
            if (Instance != null) return Instance;
            var existing = FindFirstObjectByType<ComboSkillRewardOverlayUI>(FindObjectsInactive.Include);
            if (existing != null) { Instance = existing; return existing; }

            var go = new GameObject("ComboSkillRewardOverlayUI");
            return go.AddComponent<ComboSkillRewardOverlayUI>();
        }

        public void Present(System.Action<SkillDataParser.SkillData> onPicked)
        {
            Present(DEFAULT_CHOICE_COUNT, onPicked);
        }

        public void Present(int choiceCount, System.Action<SkillDataParser.SkillData> onPicked)
        {
            if (_panel != null) Cleanup();
            _onPicked = onPicked;

            if (SkillDataParser.Instance == null)
            {
                Debug.LogWarning("[ComboSkillRewardOverlayUI] SkillDataParser.Instance가 없어 보상을 생략합니다.");
                Finish(null);
                return;
            }

            HashSet<int> learnedIds = ComboSkillRepository.GetLearnedSkillIds();
            List<SkillDataParser.SkillData> choices = SkillDataParser.Instance.GetRandomSkills(choiceCount, learnedIds);
            if (choices == null || choices.Count == 0)
            {
                Debug.LogWarning("[ComboSkillRewardOverlayUI] 선택 가능한 콤보 스킬이 없어 보상을 생략합니다.");
                Finish(null);
                return;
            }

            BuildUI(choices);
            Time.timeScale = 0f;
            Debug.Log($"[ComboSkillRewardOverlayUI] 콤보 스킬 보상 {choices.Count}개 표시");
        }

        void BuildUI(List<SkillDataParser.SkillData> choices)
        {
            Canvas canvas = ResolveCanvas();
            if (canvas == null)
            {
                Debug.LogWarning("[ComboSkillRewardOverlayUI] Canvas를 찾지 못해 보상을 생략합니다.");
                Finish(null);
                return;
            }

            _panel = new GameObject("ComboSkillRewardPanel", typeof(RectTransform));
            var panelRt = (RectTransform)_panel.transform;
            panelRt.SetParent(canvas.transform, false);
            StretchFull(panelRt);
            panelRt.SetAsLastSibling();

            var dim = NewRect("Dim", panelRt);
            StretchFull(dim);
            var dimImg = dim.gameObject.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.8f);
            dimImg.raycastTarget = true;

            var titleRt = NewRect("Title", panelRt);
            CenterAnchor(titleRt);
            titleRt.sizeDelta = new Vector2(1000f, 90f);
            titleRt.anchoredPosition = new Vector2(0f, 300f);
            var title = titleRt.gameObject.AddComponent<Text>();
            title.font = BuiltinFont();
            title.fontSize = 42;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = Color.white;
            title.text = "콤보 스킬 선택 - 한 개 획득";

            int n = choices.Count;
            const float spacing = 380f;
            float startX = -spacing * (n - 1) / 2f;

            for (int i = 0; i < n; i++)
            {
                SkillDataParser.SkillData skill = choices[i];

                var slot = NewRect($"SkillChoice_{i}", panelRt);
                CenterAnchor(slot);
                slot.sizeDelta = new Vector2(320f, 430f);
                slot.anchoredPosition = new Vector2(startX + spacing * i, -10f);

                var bg = slot.gameObject.AddComponent<Image>();
                bg.color = new Color(0.15f, 0.15f, 0.18f, 0.95f);

                if (skill.skillIcon != null)
                {
                    var iconRt = NewRect("Icon", slot);
                    iconRt.anchorMin = new Vector2(0.5f, 1f);
                    iconRt.anchorMax = new Vector2(0.5f, 1f);
                    iconRt.pivot = new Vector2(0.5f, 1f);
                    iconRt.anchoredPosition = new Vector2(0f, -22f);
                    iconRt.sizeDelta = new Vector2(220f, 150f);
                    var icon = iconRt.gameObject.AddComponent<Image>();
                    icon.sprite = skill.skillIcon;
                    icon.preserveAspect = true;
                }

                var nameRt = NewRect("Name", slot);
                nameRt.anchorMin = new Vector2(0.5f, 1f);
                nameRt.anchorMax = new Vector2(0.5f, 1f);
                nameRt.pivot = new Vector2(0.5f, 1f);
                nameRt.anchoredPosition = new Vector2(0f, -190f);
                nameRt.sizeDelta = new Vector2(280f, 40f);
                var nameText = nameRt.gameObject.AddComponent<Text>();
                nameText.font = BuiltinFont();
                nameText.fontSize = 28;
                nameText.alignment = TextAnchor.MiddleCenter;
                nameText.color = Color.white;
                nameText.text = skill.name;

                var comboRt = NewRect("Combo", slot);
                comboRt.anchorMin = new Vector2(0.5f, 1f);
                comboRt.anchorMax = new Vector2(0.5f, 1f);
                comboRt.pivot = new Vector2(0.5f, 1f);
                comboRt.anchoredPosition = new Vector2(0f, -236f);
                comboRt.sizeDelta = new Vector2(280f, 35f);
                var comboText = comboRt.gameObject.AddComponent<Text>();
                comboText.font = BuiltinFont();
                comboText.fontSize = 22;
                comboText.alignment = TextAnchor.MiddleCenter;
                comboText.color = new Color(0.9f, 0.95f, 1f, 1f);
                comboText.text = $"Combo: {skill.combo}";

                var descRt = NewRect("Desc", slot);
                descRt.anchorMin = new Vector2(0.5f, 1f);
                descRt.anchorMax = new Vector2(0.5f, 1f);
                descRt.pivot = new Vector2(0.5f, 1f);
                descRt.anchoredPosition = new Vector2(0f, -286f);
                descRt.sizeDelta = new Vector2(280f, 80f);
                var descText = descRt.gameObject.AddComponent<Text>();
                descText.font = BuiltinFont();
                descText.fontSize = 18;
                descText.alignment = TextAnchor.UpperCenter;
                descText.color = new Color(0.95f, 0.95f, 0.95f, 1f);
                descText.horizontalOverflow = HorizontalWrapMode.Wrap;
                descText.verticalOverflow = VerticalWrapMode.Truncate;
                descText.text = skill.description;

                var pickRt = NewRect("PickButton", slot);
                pickRt.anchorMin = new Vector2(0.5f, 0f);
                pickRt.anchorMax = new Vector2(0.5f, 0f);
                pickRt.pivot = new Vector2(0.5f, 0f);
                pickRt.anchoredPosition = new Vector2(0f, 14f);
                pickRt.sizeDelta = new Vector2(220f, 52f);
                var pickImg = pickRt.gameObject.AddComponent<Image>();
                pickImg.color = new Color(0.2f, 0.5f, 0.95f, 1f);
                var pickBtn = pickRt.gameObject.AddComponent<Button>();
                pickBtn.transition = Selectable.Transition.ColorTint;

                var btnTextRt = NewRect("ButtonText", pickRt);
                StretchFull(btnTextRt);
                var btnText = btnTextRt.gameObject.AddComponent<Text>();
                btnText.font = BuiltinFont();
                btnText.fontSize = 24;
                btnText.alignment = TextAnchor.MiddleCenter;
                btnText.color = Color.white;
                btnText.text = "선택";
                btnText.raycastTarget = false;

                SkillDataParser.SkillData captured = skill;
                pickBtn.onClick.AddListener(() => Pick(captured));
            }
        }

        void Pick(SkillDataParser.SkillData skill)
        {
            if (_panel == null) return;
            Finish(skill);
        }

        void Finish(SkillDataParser.SkillData skill)
        {
            Time.timeScale = 1f;
            var cb = _onPicked;
            _onPicked = null;
            Cleanup();
            cb?.Invoke(skill);
        }

        void Cleanup()
        {
            if (_panel != null)
            {
                Destroy(_panel);
                _panel = null;
            }
        }

        static Canvas ResolveCanvas()
        {
            GameObject stage = GameObject.Find("CombatStage");
            if (stage != null)
            {
                var c = stage.GetComponentInChildren<Canvas>(true);
                if (c != null) return c;
            }
            return FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        }

        static RectTransform NewRect(string goName, Transform parent)
        {
            var go = new GameObject(goName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        static void CenterAnchor(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one;
        }

        static Font BuiltinFont()
        {
            if (_builtinFont == null)
                _builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return _builtinFont;
        }
    }
}
