using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Battle.Relic;

namespace Battle.UI
{
    // 유물 도감 탭 — 전체 유물을 그리드로 표시(미발견은 실루엣/???), 분류 필터 + 수집률 + 호버 툴팁
    public class CodexRelicTab : CodexTab
    {
        [Header("바인딩")]
        [Tooltip("유물 셀들이 들어갈 부모. GridLayoutGroup 사용 권장(셀 크기 = cellSize).")]
        [SerializeField] private Transform gridRoot;                 // 셀 그리드 부모
        [Tooltip("호버 시 띄울 툴팁.")]
        [SerializeField] private CodexRelicTooltip tooltip;          // 툴팁
        [Tooltip("수집률 표시 텍스트(선택). 예: 수집 12 / 21")]
        [SerializeField] private TMP_Text countText;                 // 수집률 텍스트
        [Tooltip("표시할 유물이 없을 때 안내 오브젝트(선택).")]
        [SerializeField] private GameObject emptyState;              // 빈 상태 표시

        [Header("필터 버튼 Image (선택) — 순서 0:전체, 1:전투형, 2:비전투형")]
        [SerializeField] private Image[] filterButtonImages = new Image[3]; // 활성 필터 하이라이트용
        [SerializeField] private Color filterActiveColor = Color.white;                        // 활성 버튼 색
        [SerializeField] private Color filterInactiveColor = new Color(0.55f, 0.55f, 0.55f, 1f); // 비활성 버튼 색

        [Header("셀 모양")]
        [SerializeField] private TMP_FontAsset font;                 // 이름 폰트
        [SerializeField] private float nameFontSize = 30f;           // 이름 글자 크기
        [SerializeField] private float nameHeight = 44f;             // 셀 하단 이름 영역 높이
        [SerializeField] private string unknownName = "???";         // 미발견 이름
        [SerializeField] private Color undiscoveredIconColor = new Color(0f, 0f, 0f, 0.85f); // 미발견 실루엣 색

        [Header("호버")]
        [SerializeField] private float hoverScale = 1.15f;           // 호버 확대 배율
        [SerializeField] private int hoverSortingOrder = 200;        // 호버 셀 정렬 순서

        RelicCategory? _categoryFilter;                              // null = 전체
        readonly List<GameObject> _spawned = new List<GameObject>(); // 생성된 셀 목록

        void OnDisable()
        {
            if (tooltip != null) tooltip.Hide();
        }

        // 전체 유물을 필터/정렬해 셀 그리드를 다시 구성
        public override void Refresh()
        {
            ClearSpawned();
            if (tooltip != null) tooltip.Hide();

            List<RelicSO> items = BuildItems();
            int discovered = 0;
            foreach (var r in items) if (CodexProgress.IsRelicDiscovered(r.id)) discovered++;

            if (gridRoot != null)
            {
                RectTransform clipRect = FindClipRect(gridRoot);
                foreach (var relic in items)
                    _spawned.Add(CreateCell(relic, CodexProgress.IsRelicDiscovered(relic.id), clipRect));
            }

            ShowCollection(countText, discovered, items.Count);
            if (emptyState != null) emptyState.SetActive(items.Count == 0);
            UpdateFilterButtons();
        }

        // 분류 필터 설정 — 같은 분류를 다시 누르면 전체로 토글(0=전투형, 1=비전투형)
        public void SetCategoryFilter(int category)
        {
            RelicCategory c = (RelicCategory)Mathf.Clamp(category, 0, 1);
            if (_categoryFilter.HasValue && _categoryFilter.Value == c) _categoryFilter = null;
            else _categoryFilter = c;
            Refresh();
        }

        // 분류 필터 해제(전체 표시)
        public void ClearCategoryFilter()
        {
            _categoryFilter = null;
            Refresh();
        }

        // 필터에 맞는 전체 유물 목록(id 순)
        List<RelicSO> BuildItems()
        {
            var items = new List<RelicSO>();
            IReadOnlyList<RelicSO> all = RelicManager.Instance != null
                ? RelicManager.Instance.RelicDefinitions
                : LoadDatabaseRelics();

            foreach (var r in all)
            {
                if (r == null) continue;
                if (_categoryFilter.HasValue && r.category != _categoryFilter.Value) continue;
                items.Add(r);
            }

            items.Sort(CompareById);
            return items;
        }

        static IReadOnlyList<RelicSO> LoadDatabaseRelics()
        {
            var db = Resources.Load<RelicDatabase>("RelicDatabase");
            return db != null ? db.Relics : System.Array.Empty<RelicSO>();
        }

        // 숫자 id는 숫자 크기로, 그 외는 문자열로 비교
        static int CompareById(RelicSO a, RelicSO b)
        {
            bool aNum = int.TryParse(a.id, out int ai);
            bool bNum = int.TryParse(b.id, out int bi);
            if (aNum && bNum) return ai.CompareTo(bi);
            if (aNum != bNum) return aNum ? -1 : 1;
            return string.CompareOrdinal(a.id, b.id);
        }

        // 셀 1개 생성 — 투명 raycast 래퍼 + 아이콘 + 하단 이름, 호버 확대/툴팁 연결
        GameObject CreateCell(RelicSO relic, bool discovered, RectTransform clipRect)
        {
            var cellGo = new GameObject($"RelicCell_{relic.id}", typeof(RectTransform), typeof(Image));
            var cellRt = (RectTransform)cellGo.transform;
            cellRt.SetParent(gridRoot, false);
            var cellImg = cellGo.GetComponent<Image>();
            cellImg.color = new Color(0f, 0f, 0f, 0f);
            cellImg.raycastTarget = true;

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            var iconRt = (RectTransform)iconGo.transform;
            iconRt.SetParent(cellRt, false);
            iconRt.anchorMin = Vector2.zero;
            iconRt.anchorMax = Vector2.one;
            iconRt.offsetMin = new Vector2(0f, nameHeight);
            iconRt.offsetMax = Vector2.zero;
            var icon = iconGo.GetComponent<Image>();
            icon.sprite = relic.icon;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.color = relic.icon == null ? new Color(0.6f, 0.5f, 0.2f, 1f)
                       : discovered ? Color.white : undiscoveredIconColor;

            var nameGo = new GameObject("Name", typeof(RectTransform));
            var nameRt = (RectTransform)nameGo.transform;
            nameRt.SetParent(cellRt, false);
            nameRt.anchorMin = Vector2.zero;
            nameRt.anchorMax = new Vector2(1f, 0f);
            nameRt.pivot = new Vector2(0.5f, 0f);
            nameRt.sizeDelta = new Vector2(0f, nameHeight);
            var nameText = nameGo.AddComponent<TextMeshProUGUI>();
            nameText.text = discovered ? relic.DisplayLabel : unknownName;
            nameText.fontSize = nameFontSize;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.color = discovered ? Color.white : new Color(0.7f, 0.7f, 0.7f, 1f);
            nameText.raycastTarget = false;
            nameText.textWrappingMode = TextWrappingModes.NoWrap;
            nameText.overflowMode = TextOverflowModes.Ellipsis;
            if (font != null) nameText.font = font;

            var slot = cellGo.AddComponent<InventoryCardSlot>();
            slot.Configure(hoverScale, hoverSortingOrder, clipRect);
            slot.HoverChanged += over =>
            {
                if (tooltip == null) return;
                if (over) tooltip.Show(relic, discovered, cellRt);
                else tooltip.Hide();
            };

            return cellGo;
        }

        // 활성 필터 버튼만 밝게
        void UpdateFilterButtons()
        {
            if (filterButtonImages == null) return;
            int active = _categoryFilter.HasValue ? (int)_categoryFilter.Value + 1 : 0;
            for (int i = 0; i < filterButtonImages.Length; i++)
                if (filterButtonImages[i] != null)
                    filterButtonImages[i].color = i == active ? filterActiveColor : filterInactiveColor;
        }

        void ClearSpawned()
        {
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Destroy(_spawned[i]);
            _spawned.Clear();
        }
    }
}
