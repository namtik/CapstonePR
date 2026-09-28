using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Battle.Card;

namespace Battle.UI
{
    // 부적 도감 탭 — 획득 가능한 전체 카드를 속성 필터/정렬해 그리드로 표시(미발견은 실루엣, 발견 카드만 호버 확대)
    public class CodexCardTab : CodexTab
    {
        // 정렬 기준 — Acquired는 도감 발견 순서
        public enum SortMode { Acquired, Name, Gauge }

        [Header("바인딩")]
        [Tooltip("카드 1장을 그릴 프리팹. 인벤토리와 같은 NewCardView 프리팹.")]
        [SerializeField] private NewCardView cardPrefab;              // 카드 뷰 프리팹
        [Tooltip("카드들이 들어갈 부모. GridLayoutGroup 사용 권장.")]
        [SerializeField] private Transform gridRoot;                  // 카드 그리드 부모
        [Tooltip("수집률 표시 텍스트(선택).")]
        [SerializeField] private TMP_Text countText;                  // 수집률 텍스트
        [SerializeField] private GameObject emptyState;               // 빈 상태 표시(선택)

        [Header("옵션")]
        [SerializeField] private float cardScale = 1f;                // 카드 표시 스케일(1 = 셀에 맞춤)
        [SerializeField] private float hoverScale = 1.35f;            // 호버 확대 배율
        [SerializeField] private int hoverSortingOrder = 200;         // 호버 정렬 순서
        [SerializeField] private Color undiscoveredColor = new Color(0f, 0f, 0f, 0.85f); // 미발견 실루엣 색

        [Header("정렬 버튼 이미지 (선택) — 순서 0:획득, 1:이름, 2:호흡")]
        [SerializeField] private InventoryCardTab.SortButtonVisual[] sortButtonVisuals = new InventoryCardTab.SortButtonVisual[3];

        CardElement? _elementFilter;                  // null = 전체 속성
        SortMode _sort = SortMode.Acquired;           // 현재 정렬 기준
        bool _sortDescending;                         // 내림차순 여부
        readonly List<GameObject> _spawned = new List<GameObject>(); // 생성된 셀 목록

        // 전체 카드를 필터/정렬해 그리드를 다시 구성
        public override void Refresh()
        {
            ClearSpawned();
            UpdateSortArrows();

            List<CardData> items = BuildItems();
            int discovered = 0;

            if (cardPrefab != null && gridRoot != null)
            {
                var grid = gridRoot.GetComponent<GridLayoutGroup>();
                Vector2 cell = grid != null ? grid.cellSize : new Vector2(150f, 200f);
                RectTransform clipRect = FindClipRect(gridRoot);

                foreach (var data in items)
                {
                    bool known = CodexProgress.IsCardDiscovered(data.id);
                    if (known) discovered++;
                    _spawned.Add(CreateCell(data, known, cell, clipRect));
                }
            }

            ShowCollection(countText, discovered, items.Count);
            if (emptyState != null) emptyState.SetActive(items.Count == 0);
        }

        // 셀 래퍼(투명 raycast) 안에 원본 크기의 카드를 넣고 셀에 맞게 균일 축소 — InventoryCardTab과 동일 방식
        GameObject CreateCell(CardData data, bool known, Vector2 cell, RectTransform clipRect)
        {
            var cellGo = new GameObject($"CardCell_{data.id}", typeof(RectTransform), typeof(Image));
            var cellRt = (RectTransform)cellGo.transform;
            cellRt.SetParent(gridRoot, false);
            var cellImg = cellGo.GetComponent<Image>();
            cellImg.color = new Color(0f, 0f, 0f, 0f);
            cellImg.raycastTarget = true;

            NewCardView view = Instantiate(cardPrefab, cellRt);
            view.ApplyShopPreview(data);

            RectTransform cardRt = view.GetComponent<RectTransform>();
            if (cardRt != null)
            {
                Vector2 native = cardRt.sizeDelta;
                if (native.x < 1f || native.y < 1f) native = new Vector2(300f, 400f);
                cardRt.anchorMin = cardRt.anchorMax = new Vector2(0.5f, 0.5f);
                cardRt.pivot = new Vector2(0.5f, 0.5f);
                cardRt.anchoredPosition = Vector2.zero;
                float fit = Mathf.Min(cell.x / native.x, cell.y / native.y);
                cardRt.localScale = Vector3.one * (fit * (cardScale > 0f ? cardScale : 1f));
            }

            if (known)
            {
                var slot = cellGo.AddComponent<InventoryCardSlot>();
                slot.Configure(hoverScale > 1f ? hoverScale : 1.35f, hoverSortingOrder, clipRect);
            }
            else
            {
                ApplySilhouette(view.gameObject, undiscoveredColor);
            }

            return cellGo;
        }

        // 필터에 맞는 카드 목록 — 발견 카드를 정렬 기준대로 앞에, 미발견 카드는 id순으로 뒤에
        List<CardData> BuildItems()
        {
            var known = new List<CardData>();
            var unknown = new List<CardData>();

            foreach (var data in CardDatabase.All)
            {
                if (!IsCollectible(data)) continue;
                if (_elementFilter.HasValue && data.element != _elementFilter.Value) continue;
                if (CodexProgress.IsCardDiscovered(data.id)) known.Add(data);
                else unknown.Add(data);
            }

            switch (_sort)
            {
                case SortMode.Name:
                    known.Sort((a, b) => string.Compare(a.displayName, b.displayName, System.StringComparison.CurrentCulture));
                    break;
                case SortMode.Gauge:
                    known.Sort((a, b) => a.gauge != b.gauge ? a.gauge.CompareTo(b.gauge) : a.id.CompareTo(b.id));
                    break;
                default:
                    known.Sort((a, b) => CodexProgress.CardDiscoveryOrder(a.id).CompareTo(CodexProgress.CardDiscoveryOrder(b.id)));
                    break;
            }
            if (_sortDescending) known.Reverse();

            unknown.Sort((a, b) => a.id.CompareTo(b.id));
            known.AddRange(unknown);
            return known;
        }

        // 도감 대상 카드 — 4속성 카드만(id 500번대 탈진/파편/오염 등 전투 중 생성 카드는 제외)
        static bool IsCollectible(CardData data)
        {
            if (data == null || data.id >= CardDatabase.NEUTRAL_FILLER) return false;
            return data.element == CardElement.Fire || data.element == CardElement.Water ||
                   data.element == CardElement.Wind || data.element == CardElement.Earth;
        }

        // 속성 필터 설정 — 같은 속성을 다시 누르면 전체로 토글(0=불,1=물,2=바람,3=땅)
        public void SetElementFilter(int element)
        {
            CardElement e = (CardElement)Mathf.Clamp(element, 0, 3);
            if (_elementFilter.HasValue && _elementFilter.Value == e) _elementFilter = null;
            else _elementFilter = e;
            Refresh();
        }

        public void ClearElementFilter()
        {
            _elementFilter = null;
            Refresh();
        }

        // 정렬 기준 설정 — 같은 기준을 다시 누르면 오름/내림 토글(0=획득,1=이름,2=호흡)
        public void SetSort(int mode)
        {
            SortMode m = (SortMode)Mathf.Clamp(mode, 0, 2);
            if (_sort == m) _sortDescending = !_sortDescending;
            else
            {
                _sort = m;
                _sortDescending = false;
            }
            Refresh();
        }

        // 정렬 버튼 이미지를 현재 기준/방향에 맞게 갱신
        void UpdateSortArrows()
        {
            if (sortButtonVisuals == null) return;
            for (int i = 0; i < sortButtonVisuals.Length; i++)
            {
                var v = sortButtonVisuals[i];
                if (v.image == null) continue;
                bool active = i == (int)_sort;
                Sprite s = active && _sortDescending ? v.descending : v.ascending;
                if (s != null) v.image.sprite = s;
            }
        }

        void ClearSpawned()
        {
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Destroy(_spawned[i]);
            _spawned.Clear();
        }
    }
}
