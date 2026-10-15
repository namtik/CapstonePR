using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Battle.UI
{
    // 사방비급 도감 탭 — 전체 콤보(효과 기준, 대표 커맨드)를 책 항목으로 표시(미발견은 실루엣)
    public class CodexComboTab : CodexTab
    {
        [Header("바인딩")]
        [Tooltip("콤보 1개를 그릴 항목 프리팹. 인벤토리와 같은 BookRewardItemUI 프리팹.")]
        [SerializeField] private BookRewardItemUI comboItemPrefab;   // 콤보 항목 프리팹
        [SerializeField] private Transform listRoot;                 // 항목 부모
        [Tooltip("수집률 표시 텍스트(선택).")]
        [SerializeField] private TMP_Text countText;                 // 수집률 텍스트
        [SerializeField] private GameObject emptyState;              // 빈 상태 표시(선택)
        [SerializeField] private Color undiscoveredColor = new Color(0f, 0f, 0f, 0.85f); // 미발견 실루엣 색

        [Header("속성 아이콘 (슬롯 표시용)")]
        [SerializeField] private Sprite fireElementSprite;           // 불 속성 아이콘
        [SerializeField] private Sprite waterElementSprite;          // 물 속성 아이콘
        [SerializeField] private Sprite windElementSprite;           // 바람 속성 아이콘
        [SerializeField] private Sprite earthElementSprite;          // 땅 속성 아이콘

        readonly List<GameObject> _spawned = new List<GameObject>(); // 생성된 항목 목록

        // 전체 콤보 목록을 다시 구성
        public override void Refresh()
        {
            ClearSpawned();

            // 인자 없이 호출하면 효과(refComboId)별 대표 행 전체를 refComboId 순으로 반환
            List<ComboSkillDef> combos = ComboSkillDatabase.BuildOwnedCombos();
            int discovered = 0;

            if (comboItemPrefab != null && listRoot != null)
            {
                // GridLayout이면 InventoryComboTab과 동일하게 셀 래퍼로 감싸 원본 배치를 유지
                var grid = listRoot.GetComponent<GridLayoutGroup>();
                Vector2 cell = grid != null ? grid.cellSize : Vector2.zero;

                foreach (var def in combos)
                {
                    if (def == null) continue;
                    bool known = CodexProgress.IsComboDiscovered(def.refComboId);
                    if (known) discovered++;

                    Transform parent = listRoot;
                    RectTransform cellRt = null;
                    if (grid != null)
                    {
                        var cellGo = new GameObject($"ComboCell_{def.refComboId}", typeof(RectTransform));
                        cellRt = (RectTransform)cellGo.transform;
                        cellRt.SetParent(listRoot, false);
                        parent = cellRt;
                    }

                    BookRewardItemUI item = Instantiate(comboItemPrefab, parent);
                    item.SetElementSprites(fireElementSprite, waterElementSprite, windElementSprite, earthElementSprite);
                    item.Bind(def, null);   // 보기 전용 — 선택 콜백 없음
                    item.SetSelected(false);
                    if (!known) ApplySilhouette(item.gameObject, undiscoveredColor);

                    if (grid != null)
                    {
                        RectTransform itemRt = item.GetComponent<RectTransform>();
                        if (itemRt != null)
                        {
                            Vector2 native = itemRt.sizeDelta;
                            if (native.x < 1f || native.y < 1f) native = new Vector2(600f, 400f);
                            itemRt.anchorMin = itemRt.anchorMax = new Vector2(0.5f, 0.5f);
                            itemRt.pivot = new Vector2(0.5f, 0.5f);
                            itemRt.anchoredPosition = Vector2.zero;
                            float fit = Mathf.Min(cell.x / native.x, cell.y / native.y);
                            itemRt.localScale = Vector3.one * fit;
                        }
                    }

                    _spawned.Add(cellRt != null ? cellRt.gameObject : item.gameObject);
                }
            }

            ShowCollection(countText, discovered, combos.Count);
            if (emptyState != null) emptyState.SetActive(combos.Count == 0);
        }

        void ClearSpawned()
        {
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Destroy(_spawned[i]);
            _spawned.Clear();
        }
    }
}
