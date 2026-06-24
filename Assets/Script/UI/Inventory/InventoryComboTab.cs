using System.Collections.Generic;
using UnityEngine;

namespace Battle.UI
{
    // 사방비급 탭 — 보유 콤보 스킬을 책 형태 항목(이미지+텍스트)으로 표시(보기 전용)
    public class InventoryComboTab : MonoBehaviour
    {
        [Header("바인딩")]
        [Tooltip("콤보 1개를 그릴 항목 프리팹. 콤보 보상에서 쓰는 BookRewardItemUI 프리팹을 그대로 사용.")]
        [SerializeField] private BookRewardItemUI comboItemPrefab;   // 콤보 항목 프리팹
        [Tooltip("콤보 항목들이 들어갈 부모.")]
        [SerializeField] private Transform listRoot;                 // 항목 부모
        [Tooltip("보유 콤보가 없을 때 표시할 안내 오브젝트(선택).")]
        [SerializeField] private GameObject emptyState;              // 빈 상태 표시

        [Header("속성 아이콘 (슬롯 표시용)")]
        [SerializeField] private Sprite fireElementSprite;           // 불 속성 아이콘
        [SerializeField] private Sprite waterElementSprite;          // 물 속성 아이콘
        [SerializeField] private Sprite windElementSprite;           // 바람 속성 아이콘
        [SerializeField] private Sprite earthElementSprite;          // 땅 속성 아이콘

        readonly List<GameObject> _spawned = new List<GameObject>(); // 생성된 항목 GO 목록

        // 보유 콤보 목록을 다시 구성
        public void Refresh()
        {
            ClearSpawned();

            List<ComboSkillDef> combos = GetOwnedCombos();

            if (comboItemPrefab != null && listRoot != null)
            {
                // GridLayout이면 카드와 동일하게 셀 래퍼로 감싸 원본 배치가 안 깨지게 한다.
                // (VerticalLayout 등 다른 레이아웃이면 그대로 직접 배치)
                var grid = listRoot.GetComponent<UnityEngine.UI.GridLayoutGroup>();
                Vector2 cell = grid != null ? grid.cellSize : Vector2.zero;

                foreach (var def in combos)
                {
                    if (def == null) continue;

                    Transform parent = listRoot;
                    RectTransform cellRt = null;
                    if (grid != null)
                    {
                        var cellGo = new GameObject("ComboCell", typeof(RectTransform));
                        cellRt = (RectTransform)cellGo.transform;
                        cellRt.SetParent(listRoot, false);
                        parent = cellRt;
                    }

                    BookRewardItemUI item = Instantiate(comboItemPrefab, parent);
                    item.SetElementSprites(fireElementSprite, waterElementSprite, windElementSprite, earthElementSprite);
                    item.Bind(def, null);   // 보기 전용 — 선택 콜백 없음
                    item.SetSelected(false);

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

            if (emptyState != null) emptyState.SetActive(combos.Count == 0);
        }

        // 현재 런에서 보유 중인 콤보 정의 목록 수집
        List<ComboSkillDef> GetOwnedCombos()
        {
            var result = new List<ComboSkillDef>();
            var nb = NewBattleController.Instance;
            if (nb != null && nb.OwnedComboSkills != null)
            {
                for (int i = 0; i < nb.OwnedComboSkills.Count; i++)
                {
                    ComboSkillDef def = nb.OwnedComboSkills[i];
                    if (def != null) result.Add(def);
                }
            }
            return result;
        }

        // 생성된 항목들을 정리
        void ClearSpawned()
        {
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Destroy(_spawned[i]);
            _spawned.Clear();
        }
    }
}
