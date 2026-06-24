using System.Collections.Generic;
using UnityEngine;
using Battle.Card;

namespace Battle.UI
{
    // 부적 탭 — 보유 덱 카드를 속성 필터/정렬해 그리드로 표시(보기 전용 + 호버 제자리 확대)
    public class InventoryCardTab : MonoBehaviour
    {
        // 정렬 기준
        public enum SortMode { Acquired, Name, Gauge }

        // 정렬 버튼 하나의 비주얼 — 버튼 이미지 전체를 오름/내림 두 장 사이에서 교체
        [System.Serializable]
        public struct SortButtonVisual
        {
            [Tooltip("이 정렬 버튼의 Image(버튼 전체 이미지).")]
            public UnityEngine.UI.Image image;   // 버튼 Image
            [Tooltip("오름차순일 때 이미지(예: 이름↑).")]
            public Sprite ascending;             // 오름차순 이미지
            [Tooltip("내림차순일 때 이미지(예: 이름↓).")]
            public Sprite descending;            // 내림차순 이미지
        }

        [Header("바인딩")]
        [Tooltip("카드 1장을 그릴 프리팹. 기존 카드(NewCardView) 프리팹을 그대로 사용.")]
        [SerializeField] private NewCardView cardPrefab;              // 카드 뷰 프리팹
        [Tooltip("카드들이 들어갈 부모. GridLayoutGroup 사용 권장.")]
        [SerializeField] private Transform gridRoot;                  // 카드 그리드 부모
        [Tooltip("보유 카드가 없을 때 표시할 안내 오브젝트(선택).")]
        [SerializeField] private GameObject emptyState;              // 빈 상태 표시

        [Header("옵션")]
        [Tooltip("카드 표시 스케일(1 = 프리팹 원본).")]
        [SerializeField] private float cardScale = 1f;              // 카드 표시 스케일
        [Tooltip("마우스 오버 시 카드가 제자리에서 커지는 배율.")]
        [SerializeField] private float hoverScale = 1.35f;          // 호버 확대 배율
        [Tooltip("호버 카드가 이웃 위로 그려질 정렬 순서(인벤토리 캔버스보다 높게).")]
        [SerializeField] private int hoverSortingOrder = 200;       // 호버 정렬 순서

        [Header("정렬 버튼 이미지 (선택) — 순서 0:획득, 1:이름, 2:호흡")]
        [Tooltip("각 정렬 버튼의 [버튼 Image + 오름/내림 스프라이트] 한 쌍. 누를 때마다 두 이미지가 교체된다.")]
        [SerializeField] private SortButtonVisual[] sortButtonVisuals = new SortButtonVisual[3]; // 정렬 버튼 비주얼

        CardElement? _elementFilter;                  // null = 전체 속성
        SortMode _sort = SortMode.Acquired;           // 현재 정렬 기준
        bool _sortDescending;                         // 내림차순 여부
        readonly List<GameObject> _spawned = new List<GameObject>(); // 생성된 카드 GO 목록

        // 보유 덱을 필터/정렬해 카드 그리드를 다시 구성
        public void Refresh()
        {
            ClearSpawned();

            var items = BuildItems();
            UpdateSortArrows();

            if (cardPrefab != null && gridRoot != null)
            {
                // GridLayout이 잡는 셀 크기(카드를 여기에 맞춰 균일 축소)
                var grid = gridRoot.GetComponent<UnityEngine.UI.GridLayoutGroup>();
                Vector2 cell = grid != null ? grid.cellSize : new Vector2(150f, 200f);

                // 스크롤 뷰포트(마스크) 영역 — 이 안에서만 호버되게 슬롯에 넘긴다
                RectTransform clipRect = FindClipRect(gridRoot);

                foreach (var data in items)
                {
                    // 핵심: 카드 프리팹을 GridLayout 자식으로 직접 두면 앵커/크기가 강제 변경돼
                    //       내부 배치가 깨진다. 그래서 빈 "셀 래퍼"를 만들어 그것만 리사이즈되게 하고,
                    //       카드는 원본(중앙 앵커·고정 크기)을 유지한 채 셀에 맞게 균일 축소한다.
                    //       래퍼에 투명 raycast 이미지를 깔아 카드 내부 raycast 설정과 무관하게 호버를 받는다.
                    var cellGo = new GameObject("CardCell", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                    var cellRt = (RectTransform)cellGo.transform;
                    cellRt.SetParent(gridRoot, false);
                    var cellImg = cellGo.GetComponent<UnityEngine.UI.Image>();
                    cellImg.color = new Color(0f, 0f, 0f, 0f); // 완전 투명
                    cellImg.raycastTarget = true;

                    NewCardView view = Instantiate(cardPrefab, cellRt);
                    view.ApplyShopPreview(data);

                    RectTransform cardRt = view.GetComponent<RectTransform>();
                    if (cardRt != null)
                    {
                        Vector2 native = cardRt.sizeDelta;
                        if (native.x < 1f || native.y < 1f) native = new Vector2(300f, 400f);

                        // 셀 중앙에 배치
                        cardRt.anchorMin = cardRt.anchorMax = new Vector2(0.5f, 0.5f);
                        cardRt.pivot = new Vector2(0.5f, 0.5f);
                        cardRt.anchoredPosition = Vector2.zero;

                        // 원본 비율 유지하며 셀에 맞게 균일 축소 (cardScale로 여백 조절)
                        float fit = Mathf.Min(cell.x / native.x, cell.y / native.y);
                        cardRt.localScale = Vector3.one * (fit * (cardScale > 0f ? cardScale : 1f));
                    }

                    // 호버 확대는 래퍼에 부착(래퍼를 키우면 카드도 함께 확대)
                    // hoverScale이 1 이하(인스펙터 미설정/0 직렬화)면 기본 1.35 사용 → 확대가 안 보이는 것 방지
                    var slot = cellGo.AddComponent<InventoryCardSlot>();
                    slot.Configure(hoverScale > 1f ? hoverScale : 1.35f, hoverSortingOrder, clipRect);

                    _spawned.Add(cellGo);
                }
            }

            if (emptyState != null) emptyState.SetActive(items.Count == 0);
        }

        // gridRoot 위쪽에서 스크롤 마스크(RectMask2D/Mask) RectTransform을 찾는다(없으면 null)
        RectTransform FindClipRect(Transform from)
        {
            Transform t = from;
            while (t != null)
            {
                if (t.GetComponent<UnityEngine.UI.RectMask2D>() != null ||
                    t.GetComponent<UnityEngine.UI.Mask>() != null)
                    return t as RectTransform;
                t = t.parent;
            }
            return null;
        }

        // 현재 필터/정렬 기준에 맞는 카드 목록(중복 = 보유 장수만큼) 구성
        List<CardData> BuildItems()
        {
            var items = new List<CardData>();

            RunDeckState state = RunDeckState.EnsureExists();
            if (state == null) return items;
            state.EnsureSeeded();

            // 덱 순서 = 획득 순서 → 그대로 펼쳐 담는다
            foreach (var entry in state.RunDeck)
            {
                if (entry.count <= 0) continue;
                CardData data = CardDatabase.GetById(entry.cardId);
                if (data == null) continue;
                if (_elementFilter.HasValue && data.element != _elementFilter.Value) continue;
                for (int i = 0; i < entry.count; i++) items.Add(data);
            }

            switch (_sort)
            {
                case SortMode.Name:
                    items.Sort((a, b) => string.Compare(a.displayName, b.displayName, System.StringComparison.CurrentCulture));
                    break;
                case SortMode.Gauge:
                    items.Sort((a, b) => a.gauge.CompareTo(b.gauge));
                    break;
                // Acquired: 덱(획득) 순서 유지
            }

            if (_sortDescending) items.Reverse();
            return items;
        }

        // 속성 필터 설정 — 같은 속성을 다시 누르면 전체로 토글(버튼 0=불,1=물,2=바람,3=땅)
        public void SetElementFilter(int element)
        {
            CardElement e = (CardElement)Mathf.Clamp(element, 0, 3);
            if (_elementFilter.HasValue && _elementFilter.Value == e)
                _elementFilter = null;
            else
                _elementFilter = e;
            Refresh();
        }

        // 속성 필터 해제(전체 표시)
        public void ClearElementFilter()
        {
            _elementFilter = null;
            Refresh();
        }

        // 정렬 기준 설정 — 같은 기준을 다시 누르면 오름/내림 토글(0=획득,1=이름,2=호흡)
        public void SetSort(int mode)
        {
            SortMode m = (SortMode)Mathf.Clamp(mode, 0, 2);
            if (_sort == m)
                _sortDescending = !_sortDescending;
            else
            {
                _sort = m;
                _sortDescending = false;
            }
            Refresh(); // Refresh 안에서 UpdateSortArrows 호출됨
        }

        // 정렬 버튼 이미지를 현재 기준/방향에 맞게 갱신(누를 때마다 ↑/↓ 이미지 교체)
        void UpdateSortArrows()
        {
            if (sortButtonVisuals == null) return;
            for (int i = 0; i < sortButtonVisuals.Length; i++)
            {
                var v = sortButtonVisuals[i];
                if (v.image == null) continue;

                bool active = (i == (int)_sort);
                // 활성 기준은 방향(오름/내림)에 따라, 비활성은 오름차순(기본) 이미지
                Sprite s = active
                    ? (_sortDescending ? v.descending : v.ascending)
                    : v.ascending;

                if (s != null)
                {
                    v.image.sprite = s;
                    v.image.enabled = true;
                }
            }
        }

        // 현재 활성 속성 필터(없으면 -1) — 버튼 하이라이트용
        public int ActiveElementFilter => _elementFilter.HasValue ? (int)_elementFilter.Value : -1;

        // 생성된 카드들을 정리
        void ClearSpawned()
        {
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Destroy(_spawned[i]);
            _spawned.Clear();
        }
    }
}
