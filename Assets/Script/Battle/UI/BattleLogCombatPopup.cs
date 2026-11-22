using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Battle.Card;

namespace Battle.UI
{
    // 공격 기록에 커서를 올리면, 그 한 번에 난 피해·회복·스택을 카드 위에 올린다.
    public class BattleLogCombatPopup : MonoBehaviour
    {
        const float RowStep = 68f;
        const float ColStep = 168f;
        const float IconSize = 52f;
        const float NumberDrop = 14f;
        const int ComboBooksPerRow = 5;
        const int ComboBookMax = 20;
        const string ComboBookPrefabPath = "Assets/Prefab/UI/ComboBook.prefab";
        const string ChosunFontPath = "Assets/Fonts/ChosunCentennial_otf.otf";
        static readonly Color ComboOverflowColor = new Color(244f / 255f, 206f / 255f, 141f / 255f, 1f);
        static float _overflowOutlineWidth = 0.2f;
        static Color _overflowOutlineColor = Color.black;

        static readonly Color Loss = new Color(1f, 0f, 0f, 1f);
        static readonly Color Gain = new Color(0f, 0.863f, 0.129f, 1f);

        static BattleLogCombatPopup _instance;

        RectTransform _panel;
        int _openId = -1;
        TMP_FontAsset _font;
        Sprite _attack;
        Sprite _burn;
        Sprite _freeze;
        Sprite _shield;
        Sprite _launcher;
        Sprite _heal;
        GameObject _comboBookPrefab;
        TMP_FontAsset _chosunFont;

        const int MaxComboPreviewId = -3;
        const int OverflowComboPreviewId = -4;

        public static bool IsMaxComboPreviewOpen => _instance != null && _instance._openId == MaxComboPreviewId;
        public static bool IsOverflowComboPreviewOpen => _instance != null && _instance._openId == OverflowComboPreviewId;

        public static void ShowMaxComboPreview(Component anchor)
        {
            Ensure(anchor).OpenMaxComboPreview();
        }

        public static void HideMaxComboPreview()
        {
            Hide(MaxComboPreviewId);
        }

        public static void ShowOverflowComboPreview(Component anchor)
        {
            Ensure(anchor).OpenOverflowComboPreview();
        }

        public static void HideOverflowComboPreview()
        {
            Hide(OverflowComboPreviewId);
        }

        public static void Show(int entryId, Component anchor)
        {
            BattleLog.Entry entry = BattleLog.Find(entryId);
            if (entry == null || !ShouldOpen(entry))
            {
                Hide(entryId);
                return;
            }
            Ensure(anchor).Open(entry);
        }

        // 효과 없는 카드 기록도 커서를 올리면 같은 어두운 화면을 띄운다.
        static bool ShouldOpen(BattleLog.Entry entry)
        {
            if (entry.HasCombat || entry.DisruptionIndex >= 0 || entry.Category == BattleLog.Kind.Awaken)
                return true;
            return entry.Category == BattleLog.Kind.Card && !entry.HasMoves;
        }

        public static void Hide(int entryId)
        {
            if (_instance == null || _instance._openId != entryId) return;
            _instance._openId = -1;
            _instance.gameObject.SetActive(false);
        }

        static BattleLogCombatPopup Ensure(Component anchor)
        {
            Canvas canvas = anchor != null ? anchor.GetComponentInParent<Canvas>() : null;
            if (canvas != null) canvas = canvas.rootCanvas;
            if (canvas == null) canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas != null)
            {
                canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1
                    | AdditionalCanvasShaderChannels.Normal
                    | AdditionalCanvasShaderChannels.Tangent;
            }
            if (_instance != null) return _instance;
            var go = new GameObject("LogCombatPopup");
            if (canvas != null) go.transform.SetParent(canvas.transform, false);
            _instance = go.AddComponent<BattleLogCombatPopup>();
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

            var group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1f;
            group.blocksRaycasts = false;
            group.interactable = true;
            gameObject.SetActive(false);
        }

        void Open(BattleLog.Entry entry)
        {
            _openId = entry.Id;
            LoadArt();
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyImmediate(transform.GetChild(i).gameObject);

            if (!entry.HasMoves) Dim();

            var playerHp = new List<BattleLog.CombatNote>();
            var playerStack = new List<BattleLog.CombatNote>();
            var enemies = new Dictionary<int, List<BattleLog.CombatNote>>();
            for (int i = 0; i < entry.Combat.Count; i++)
            {
                BattleLog.CombatNote note = entry.Combat[i];
                if (note.OnPlayer)
                {
                    if (note.Kind == BattleLog.CombatKind.Damage || note.Kind == BattleLog.CombatKind.Heal)
                        playerHp.Add(note);
                    else
                        playerStack.Add(note);
                }
                else
                {
                    List<BattleLog.CombatNote> list;
                    if (!enemies.TryGetValue(note.EnemySlot, out list))
                    {
                        list = new List<BattleLog.CombatNote>();
                        enemies[note.EnemySlot] = list;
                    }
                    list.Add(note);
                }
            }

            RectTransform parent = _panel.parent as RectTransform;
            Canvas.ForceUpdateCanvases();
            float width = parent != null && parent.rect.width > 10f ? parent.rect.width : 1920f;
            float height = parent != null && parent.rect.height > 10f ? parent.rect.height : 1080f;
            Vector2 hpOrigin;
            if (!TryPlayerHpOrigin(out hpOrigin))
                hpOrigin = new Vector2(-width * 0.5f + 48f, height * 0.5f - 130f);
            PlaceRuns(SplitSingle(playerHp), hpOrigin);
            Vector2 stackOrigin;
            if (!TryPlayerStackOrigin(out stackOrigin))
                stackOrigin = new Vector2(hpOrigin.x, hpOrigin.y - 120f);
            PlaceRuns(SplitSingle(playerStack), stackOrigin);

            foreach (var pair in enemies)
                PlaceEnemy(pair.Key, pair.Value);
            PlaceAwakenCombos(entry);
        }

        void PlaceAwakenCombos(BattleLog.Entry entry)
        {
            if (entry.Category != BattleLog.Kind.Awaken || entry.AwakenCombos == null || entry.AwakenCombos.Count == 0)
                return;
            GameObject prefab;
            float nativeWidth;
            float nativeHeight;
            if (!TryComboBook(out prefab, out nativeWidth, out nativeHeight)) return;
            Vector2 origin;
            Vector2 area;
            MeasureComboArea(out origin, out area);
            float width;
            float height;
            int columns;
            int rows;
            FitComboBooks(nativeWidth, nativeHeight, area, out width, out height, out columns, out rows);
            int total = entry.AwakenCombos.Count;
            int capacity = Mathf.Min(ComboBookMax, columns * rows);
            int count = Mathf.Min(total, capacity);
            var shown = new List<Battle.ComboSkillDef>(count);
            for (int i = 0; i < count; i++) shown.Add(entry.AwakenCombos[i]);
            bool overflow = total > ComboBookMax;
            PlaceComboBooks(shown, origin, columns, width, height, nativeWidth, prefab, overflow);
            if (overflow) PlaceComboOverflow(total, origin, columns, width, height, count);
        }

        void OpenMaxComboPreview()
        {
            _openId = MaxComboPreviewId;
            LoadArt();
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyImmediate(transform.GetChild(i).gameObject);
            Dim();

            GameObject prefab;
            float nativeWidth;
            float nativeHeight;
            if (!TryComboBook(out prefab, out nativeWidth, out nativeHeight)) return;
            Vector2 origin;
            Vector2 area;
            MeasureComboArea(out origin, out area);
            float width;
            float height;
            int columns;
            int rows;
            FitComboBooks(nativeWidth, nativeHeight, area, out width, out height, out columns, out rows);
            int count = columns * rows;
            var owned = Battle.NewBattleController.Instance != null
                ? Battle.NewBattleController.Instance.OwnedComboSkills
                : null;
            var shown = new List<Battle.ComboSkillDef>(count);
            for (int i = 0; i < count; i++)
            {
                Battle.ComboSkillDef skill = owned != null && owned.Count > 0 ? owned[i % owned.Count] : null;
                shown.Add(skill ?? PreviewCombo(i));
            }
            PlaceComboBooks(shown, origin, columns, width, height, nativeWidth, prefab, false);
        }

        void OpenOverflowComboPreview()
        {
            _openId = OverflowComboPreviewId;
            LoadArt();
            transform.SetAsLastSibling();
            gameObject.SetActive(true);
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyImmediate(transform.GetChild(i).gameObject);
            Dim();

            GameObject prefab;
            float nativeWidth;
            float nativeHeight;
            if (!TryComboBook(out prefab, out nativeWidth, out nativeHeight)) return;
            Vector2 origin;
            Vector2 area;
            MeasureComboArea(out origin, out area);
            float width;
            float height;
            int columns;
            int rows;
            FitComboBooks(nativeWidth, nativeHeight, area, out width, out height, out columns, out rows);
            int count = Mathf.Min(ComboBookMax, columns * rows);
            var owned = Battle.NewBattleController.Instance != null
                ? Battle.NewBattleController.Instance.OwnedComboSkills
                : null;
            var shown = new List<Battle.ComboSkillDef>(count);
            for (int i = 0; i < count; i++)
            {
                Battle.ComboSkillDef skill = owned != null && owned.Count > 0 ? owned[i % owned.Count] : null;
                shown.Add(skill ?? PreviewCombo(i));
            }
            PlaceComboBooks(shown, origin, columns, width, height, nativeWidth, prefab, true);
            PlaceComboOverflow(ComboBookMax + 1, origin, columns, width, height, count);
        }

        static Battle.ComboSkillDef PreviewCombo(int index)
        {
            CardElement[] elements =
            {
                CardElement.Fire,
                CardElement.Water,
                CardElement.Wind,
                CardElement.Earth
            };
            return new Battle.ComboSkillDef
            {
                displayName = "콤보 " + (index + 1),
                slot1 = elements[index % elements.Length],
                slot2 = elements[(index + 1) % elements.Length],
                slot3 = elements[(index + 2) % elements.Length]
            };
        }

        void MeasureComboArea(out Vector2 origin, out Vector2 area)
        {
            origin = new Vector2(-960f, 540f);
            area = new Vector2(1499f, 804f);
            BattleTabPanel panel = Object.FindFirstObjectByType<BattleTabPanel>(FindObjectsInactive.Include);
            if (panel == null || !panel.TryGetFrameRect(out RectTransform frame)) return;
            Vector3[] corners = new Vector3[4];
            frame.GetWorldCorners(corners);
            Vector2 topLeft;
            Vector2 topRight;
            Vector2 bottomLeft;
            if (!WorldPoint(corners[1], out topLeft) || !WorldPoint(corners[2], out topRight) || !WorldPoint(corners[0], out bottomLeft))
                return;
            origin = topLeft;
            area = new Vector2(Mathf.Abs(topRight.x - topLeft.x), Mathf.Abs(topLeft.y - bottomLeft.y));
        }

        bool TryComboBook(out GameObject prefab, out float width, out float height)
        {
            prefab = _comboBookPrefab;
            if (prefab == null)
            {
#if UNITY_EDITOR
                prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(ComboBookPrefabPath);
#endif
                _comboBookPrefab = prefab;
            }

            width = 600f;
            height = 400f;
            if (prefab == null)
            {
                Debug.LogWarning("[BattleLogCombatPopup] ComboBook 프리팹을 찾지 못했습니다.");
                return false;
            }

            RectTransform rect = prefab.GetComponent<RectTransform>();
            if (rect != null)
            {
                if (rect.sizeDelta.x > 1f) width = rect.sizeDelta.x;
                if (rect.sizeDelta.y > 1f) height = rect.sizeDelta.y;
            }
            return true;
        }

        static void FitComboBooks(float nativeWidth, float nativeHeight, Vector2 area, out float width, out float height, out int columns, out int rows)
        {
            columns = ComboBooksPerRow;
            float scale = 1f;
            if (nativeWidth > 1f && area.x > 1f)
                scale = area.x / (nativeWidth * columns);
            width = nativeWidth * scale;
            height = nativeHeight * scale;
            rows = height > 1f ? Mathf.Max(1, Mathf.FloorToInt((area.y + 0.5f) / height)) : 1;
        }

        void PlaceComboBooks(List<Battle.ComboSkillDef> skills, Vector2 origin, int columns, float width, float height, float nativeWidth, GameObject prefab, bool darken)
        {
            if (skills == null || prefab == null) return;
            if (columns < 1) columns = 1;
            float scale = nativeWidth > 1f ? width / nativeWidth : 1f;
            NewCardView card = Object.FindFirstObjectByType<NewCardView>(FindObjectsInactive.Include);
            for (int i = 0; i < skills.Count; i++)
            {
                int col = i % columns;
                int row = i / columns;
                PlaceComboBook(skills[i], origin + new Vector2(col * width, -row * height), scale, prefab, card, darken);
            }
        }

        void PlaceComboOverflow(int total, Vector2 origin, int columns, float width, float height, int shown)
        {
            if (transform.Find("Dim") == null) Dim();
            int shownRows = columns > 0 ? Mathf.CeilToInt(shown / (float)columns) : 1;
            Vector2 center = origin + new Vector2(columns * width * 0.5f, -shownRows * height * 0.5f);

            var go = new GameObject("ComboOverflow", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(900f, 180f);
            rect.anchoredPosition = center;

            TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
            label.text = "+" + total;
            TMP_FontAsset font = ChosunFont();
            if (font != null) label.font = font;
            label.fontSize = 128f;
            label.color = ComboOverflowColor;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            ApplyOverflowOutline(label);
        }

        public static void SetOverflowOutline(float width, Color color)
        {
            _overflowOutlineWidth = Mathf.Clamp01(width);
            _overflowOutlineColor = color;
            if (_instance == null) return;
            Transform found = _instance.transform.Find("ComboOverflow");
            if (found == null) return;
            TextMeshProUGUI label = found.GetComponent<TextMeshProUGUI>();
            if (label == null) return;
            ApplyOverflowOutline(label);
        }

        static void ApplyOverflowOutline(TextMeshProUGUI label)
        {
            if (label == null) return;
            float outline = Mathf.Clamp01(_overflowOutlineWidth);
            label.extraPadding = true;
            ShaderUtilities.GetShaderPropertyIDs();

            Material material = label.font != null ? label.font.material : label.fontSharedMaterial;
            WriteOutline(material, outline);
            if (material != null) label.fontSharedMaterial = material;

            label.outlineWidth = outline;
            label.outlineColor = _overflowOutlineColor;
            WriteOutline(label.fontMaterial, outline);
            label.ForceMeshUpdate();
        }

        static void WriteOutline(Material material, float outline)
        {
            if (material == null) return;
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, outline);
            material.SetColor(ShaderUtilities.ID_OutlineColor, _overflowOutlineColor);
            ShaderUtilities.UpdateShaderRatios(material);
        }

        TMP_FontAsset ChosunFont()
        {
            if (_chosunFont != null) return _chosunFont;
#if UNITY_EDITOR
            Font source = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>(ChosunFontPath);
            if (source != null)
            {
                _chosunFont = TMP_FontAsset.CreateFontAsset(
                    source, 72, 24, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                if (_chosunFont != null)
                {
                    _chosunFont.TryAddCharacters("+0123456789");
                    ShaderUtilities.GetShaderPropertyIDs();
                    TMP_FontAsset reference = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NYJUnheo SDF.asset");
                    if (reference != null && reference.material != null && reference.material.shader != null && _chosunFont.material != null)
                        _chosunFont.material.shader = reference.material.shader;
                    if (_chosunFont.material != null)
                        _chosunFont.material.SetFloat(ShaderUtilities.ID_GradientScale, _chosunFont.atlasPadding + 1f);
                }
            }
#endif
            if (_chosunFont == null) _chosunFont = _font;
            return _chosunFont;
        }

        static void PlaceComboBook(Battle.ComboSkillDef skill, Vector2 topLeft, float scale, GameObject prefab, NewCardView card, bool darken)
        {
            if (skill == null) return;
            GameObject root = Object.Instantiate(prefab, _instance.transform, false);
            root.name = "ComboBook";
            RectTransform rect = root.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0f, 1f);
                rect.localScale = new Vector3(scale, scale, 1f);
                rect.anchoredPosition = topLeft;
            }

            BookRewardItemUI item = root.GetComponent<BookRewardItemUI>();
            if (item != null)
            {
                if (card != null)
                {
                    item.SetElementSprites(
                        card.GetElementSprite(CardElement.Fire),
                        card.GetElementSprite(CardElement.Water),
                        card.GetElementSprite(CardElement.Wind),
                        card.GetElementSprite(CardElement.Earth));
                }
                item.Bind(skill, null);
            }

            Button button = root.GetComponent<Button>();
            if (button != null) button.enabled = false;
            ButtonHoverScale hover = root.GetComponent<ButtonHoverScale>();
            if (hover != null) hover.enabled = false;
            if (!darken || rect == null) return;

            var shadeGo = new GameObject("Shade", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            shadeGo.transform.SetParent(rect, false);
            RectTransform shade = shadeGo.GetComponent<RectTransform>();
            shade.anchorMin = Vector2.zero;
            shade.anchorMax = Vector2.one;
            shade.offsetMin = Vector2.zero;
            shade.offsetMax = Vector2.zero;
            Image shadeImage = shadeGo.GetComponent<Image>();
            shadeImage.color = new Color(0f, 0f, 0f, 0.55f);
            shadeImage.raycastTarget = false;
        }

        void PlaceEnemy(int slot, List<BattleLog.CombatNote> notes)
        {
            BattleTabPanel panel = Object.FindFirstObjectByType<BattleTabPanel>(FindObjectsInactive.Include);
            if (panel == null || !panel.TryGetEnemyPortrait(slot, out RectTransform portrait)) return;
            if (!WorldCenter(portrait, out Vector2 center, out Vector2 size)) return;

            List<Run> runs = SplitRuns(notes);
            var byKind = new Dictionary<BattleLog.CombatKind, List<Run>>();
            for (int i = 0; i < runs.Count; i++)
            {
                List<Run> list;
                if (!byKind.TryGetValue(runs[i].Kind, out list))
                {
                    list = new List<Run>();
                    byKind[runs[i].Kind] = list;
                }
                list.Add(runs[i]);
            }

            foreach (var pair in byKind)
            {
                Vector2 anchor = Zone(pair.Key, center, size);
                for (int r = 0; r < pair.Value.Count; r++)
                    PlaceRun(pair.Value[r], anchor + new Vector2(r * 96f, 0f));
            }
        }

        static Vector2 Zone(BattleLog.CombatKind kind, Vector2 center, Vector2 size)
        {
            float foot = center.y - size.y * 0.5f + 78f;
            if (kind == BattleLog.CombatKind.Heal) return new Vector2(center.x - size.x * 0.16f, foot);
            if (kind == BattleLog.CombatKind.Freeze) return new Vector2(center.x - size.x * 0.34f, foot);
            if (kind == BattleLog.CombatKind.Burn) return new Vector2(center.x + size.x * 0.34f, foot);
            return new Vector2(center.x - IconSize * 0.5f, foot);
        }

        bool TryPlayerHpOrigin(out Vector2 origin)
        {
            origin = Vector2.zero;
            RectTransform rect = FindCombatHpBar();
            if (rect == null) return false;
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 topRight;
            if (!WorldPoint(corners[2], out topRight)) return false;
            // 막대 오른쪽 끝 바로 위. 막대가 화면 꼭대기라 밖으로 나가면 화면 안에 붙인다.
            float x = topRight.x - IconSize;
            float y = topRight.y + IconSize * 0.5f + 6f;
            Rect area = _panel != null ? _panel.rect : new Rect(-960f, -540f, 1920f, 1080f);
            float half = IconSize * 0.5f;
            x = Mathf.Clamp(x, area.xMin + 8f, area.xMax - IconSize - 8f);
            y = Mathf.Clamp(y, area.yMin + half + NumberDrop, area.yMax - half - 4f);
            origin = new Vector2(x, y);
            return true;
        }

        static RectTransform FindCombatHpBar()
        {
            GameObject stage = GameObject.Find("CombatStage");
            if (stage != null)
            {
                Slider[] sliders = stage.GetComponentsInChildren<Slider>(true);
                for (int i = 0; i < sliders.Length; i++)
                {
                    Slider slider = sliders[i];
                    if (slider == null || slider.gameObject.name != "PlayerHpBar") continue;
                    if (!slider.gameObject.activeInHierarchy) continue;
                    return slider.transform as RectTransform;
                }
            }

            Player player = Player.Resolve();
            if (player != null && player.hpBar != null && player.hpBar.gameObject.activeInHierarchy)
                return player.hpBar.transform as RectTransform;
            return null;
        }

        bool TryPlayerStackOrigin(out Vector2 origin)
        {
            origin = Vector2.zero;
            CardHandHUD hud = Object.FindFirstObjectByType<CardHandHUD>(FindObjectsInactive.Include);
            if (hud == null || !hud.TryGetAwakenStackAnchor(out RectTransform host, out Vector2 topLeft))
                return false;
            Vector3 local = new Vector3(host.rect.xMin + topLeft.x, host.rect.yMax + topLeft.y - 28f, 0f);
            return WorldPoint(host.TransformPoint(local), out origin);
        }

        float PlaceRuns(List<Run> runs, Vector2 origin)
        {
            float x = origin.x;
            float bottom = origin.y;
            for (int i = 0; i < runs.Count; i++)
            {
                float low = PlaceRun(runs[i], new Vector2(x, origin.y));
                if (low < bottom) bottom = low;
                x += ColStep;
            }
            return bottom;
        }

        float PlaceRun(Run run, Vector2 bottomAnchor)
        {
            int count = run.Notes.Count;
            float bottom = bottomAnchor.y;
            for (int i = 0; i < count; i++)
            {
                float y = bottomAnchor.y + i * RowStep;
                Token(run.Notes[i], new Vector2(bottomAnchor.x, y));
            }
            return bottom;
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

        static List<Run> SplitRuns(List<BattleLog.CombatNote> notes)
        {
            var runs = new List<Run>();
            for (int i = 0; i < notes.Count; i++)
            {
                BattleLog.CombatNote note = notes[i];
                if (runs.Count > 0 && runs[runs.Count - 1].Kind == note.Kind)
                    runs[runs.Count - 1].Notes.Add(note);
                else
                    runs.Add(new Run(note));
            }
            return runs;
        }

        static List<Run> SplitSingle(List<BattleLog.CombatNote> notes)
        {
            var runs = new List<Run>();
            for (int i = 0; i < notes.Count; i++)
                runs.Add(new Run(notes[i]));
            return runs;
        }

        void Token(BattleLog.CombatNote note, Vector2 pos)
        {
            var root = new GameObject("Combat", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(220f, 70f);
            rect.anchoredPosition = pos;

            Sprite icon = IconFor(note.Kind);
            float iconWidth = IconSize;
            if (icon != null)
            {
                float aspect = icon.rect.height > 1f ? icon.rect.width / icon.rect.height : 1f;
                iconWidth = IconSize * aspect;
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconGo.transform.SetParent(root.transform, false);
                RectTransform iconRect = iconGo.GetComponent<RectTransform>();
                iconRect.anchorMin = new Vector2(0f, 0.5f);
                iconRect.anchorMax = new Vector2(0f, 0.5f);
                iconRect.pivot = new Vector2(0f, 0.5f);
                iconRect.sizeDelta = new Vector2(iconWidth, IconSize);
                iconRect.anchoredPosition = Vector2.zero;
                Image image = iconGo.GetComponent<Image>();
                image.sprite = icon;
                image.color = Color.white;
                image.preserveAspect = true;
                image.raycastTarget = false;
            }

            var textGo = new GameObject("Value", typeof(RectTransform));
            textGo.transform.SetParent(root.transform, false);
            textGo.transform.SetAsLastSibling();
            RectTransform textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 0.5f);
            textRect.anchorMax = new Vector2(0f, 0.5f);
            textRect.pivot = new Vector2(0f, 0.5f);
            textRect.sizeDelta = new Vector2(150f, 72f);
            textRect.anchoredPosition = new Vector2(iconWidth * 0.42f, -NumberDrop);

            var label = textGo.AddComponent<TextMeshProUGUI>();
            bool gain = note.Gained || note.Kind == BattleLog.CombatKind.Heal;
            label.text = note.Amount.ToString();
            if (_font != null) label.font = _font;
            label.fontStyle = FontStyles.Bold;
            label.fontSize = 64f;
            label.color = gain ? Gain : Loss;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            label.outlineWidth = 0.15f;
            label.outlineColor = Color.black;
        }

        Sprite IconFor(BattleLog.CombatKind kind)
        {
            if (kind == BattleLog.CombatKind.Burn) return _burn;
            if (kind == BattleLog.CombatKind.Freeze) return _freeze;
            if (kind == BattleLog.CombatKind.Guard) return _shield;
            if (kind == BattleLog.CombatKind.Launcher) return _launcher;
            if (kind == BattleLog.CombatKind.Heal) return _heal;
            return _attack;
        }

        bool WorldCenter(RectTransform target, out Vector2 center, out Vector2 size)
        {
            center = Vector2.zero;
            size = new Vector2(200f, 200f);
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null || target == null) return false;
            RectTransform canvasRect = canvas.transform as RectTransform;
            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector3[] corners = new Vector3[4];
            target.GetWorldCorners(corners);
            Vector2 a;
            Vector2 b;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, RectTransformUtility.WorldToScreenPoint(cam, corners[0]), cam, out a))
                return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, RectTransformUtility.WorldToScreenPoint(cam, corners[2]), cam, out b))
                return false;
            center = (a + b) * 0.5f;
            size = new Vector2(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y));
            return true;
        }

        bool WorldPoint(Vector3 world, out Vector2 local)
        {
            local = Vector2.zero;
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return false;
            RectTransform canvasRect = canvas.transform as RectTransform;
            Camera cam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                RectTransformUtility.WorldToScreenPoint(cam, world),
                cam,
                out local);
        }

        void LoadArt()
        {
            if (_font == null)
            {
#if UNITY_EDITOR
                _font = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/NYJUnheo SDF.asset");
#endif
            }
            if (_attack == null) _attack = Resources.Load<Sprite>("UI/Tab/EnemyAttackIcon");
            if (_burn == null) _burn = StatusIconDatabase.LoadIcon("burn");
            if (_freeze == null) _freeze = Resources.Load<Sprite>("VFX/Cartoon Coffee/2D Deluxe VFX/Textures/Snowflake_001");
            if (_shield == null) _shield = Resources.Load<Sprite>("UI/Tab/Shield");
            if (_launcher == null) _launcher = Resources.Load<Sprite>("UI/Tab/Launcher");
            if (_heal == null) _heal = Resources.Load<Sprite>("UI/Tab/Heal");
        }

        sealed class Run
        {
            public BattleLog.CombatKind Kind;
            public readonly List<BattleLog.CombatNote> Notes = new List<BattleLog.CombatNote>();

            public Run(BattleLog.CombatNote first)
            {
                Kind = first.Kind;
                Notes.Add(first);
            }
        }
    }
}
