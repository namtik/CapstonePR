using System.Collections.Generic;
using UnityEngine;
using Battle.Card;
using Random = UnityEngine.Random;

namespace Battle.UI
{
    /// <summary>
    /// 책 형태 콤보 보상 패널. BookRewardItemUI 3개에 콤보 데이터를 바인딩한다.
    /// </summary>
    public class ComboBookRewardPanelUI : MonoBehaviour
    {
        [Header("Slots")]
        [SerializeField] private Transform bookListRoot;
        [SerializeField] private BookRewardItemUI bookItemPrefab;
        [SerializeField] private List<BookRewardItemUI> bookItems = new List<BookRewardItemUI>();
        [SerializeField, Range(1, 5)] private int choiceCount = 3;

        [Header("Element Icons")]
        [SerializeField] private Sprite fireElementSprite;
        [SerializeField] private Sprite waterElementSprite;
        [SerializeField] private Sprite windElementSprite;
        [SerializeField] private Sprite earthElementSprite;

        [Header("Runtime")]
        [SerializeField] private bool pauseTimeWhenOpen = true;

        System.Action<ComboSkillDef> _onPicked;
        readonly List<ComboSkillDef> _choices = new List<ComboSkillDef>();

        public void Present(System.Action<ComboSkillDef> onPicked)
        {
            _onPicked = onPicked;

            BuildChoices();
            EnsureSlots(_choices.Count);
            BindSlots();

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            if (pauseTimeWhenOpen) Time.timeScale = 0f;

            Debug.Log($"[ComboBookRewardPanelUI] 콤보 선택지 {_choices.Count}개 표시");
        }

        public void Hide()
        {
            if (pauseTimeWhenOpen) Time.timeScale = 1f;
            gameObject.SetActive(false);
        }

        void BuildChoices()
        {
            _choices.Clear();

            List<ComboSkillDef> all = Battle.ComboSkillDatabase.BuildOwnedCombos();
            if (all == null || all.Count == 0)
                return;

            HashSet<int> ownedRefIds = GetOwnedRefIdsFromBattle();
            var candidates = new List<ComboSkillDef>();
            for (int i = 0; i < all.Count; i++)
            {
                ComboSkillDef skill = all[i];
                if (skill == null) continue;
                if (ownedRefIds.Contains(skill.refComboId)) continue;
                candidates.Add(skill);
            }

            // 모두 보유 중이면 전체에서 다시 뽑는다.
            if (candidates.Count == 0)
                candidates = all;

            int target = Mathf.Min(choiceCount, candidates.Count);
            var used = new HashSet<int>();
            int guard = 0;
            while (_choices.Count < target && guard++ < 200)
            {
                ComboSkillDef pick = candidates[Random.Range(0, candidates.Count)];
                if (pick == null) continue;
                if (!used.Add(pick.refComboId)) continue;
                _choices.Add(pick);
            }
        }

        HashSet<int> GetOwnedRefIdsFromBattle()
        {
            var result = new HashSet<int>();
            var nb = Battle.NewBattleController.Instance;
            if (nb == null || nb.OwnedComboSkills == null)
                return result;

            for (int i = 0; i < nb.OwnedComboSkills.Count; i++)
            {
                ComboSkillDef skill = nb.OwnedComboSkills[i];
                if (skill == null) continue;
                result.Add(skill.refComboId);
            }
            return result;
        }

        void EnsureSlots(int count)
        {
            if (bookListRoot == null)
                bookListRoot = transform;

            if (bookItems == null)
                bookItems = new List<BookRewardItemUI>();

            bookItems.RemoveAll(x => x == null);

            if (bookItems.Count == 0)
            {
                BookRewardItemUI[] children = bookListRoot.GetComponentsInChildren<BookRewardItemUI>(true);
                for (int i = 0; i < children.Length; i++)
                    if (!bookItems.Contains(children[i])) bookItems.Add(children[i]);
            }

            while (bookItems.Count < count && bookItemPrefab != null)
            {
                var created = Instantiate(bookItemPrefab, bookListRoot);
                bookItems.Add(created);
            }

            for (int i = 0; i < bookItems.Count; i++)
            {
                if (bookItems[i] != null)
                    bookItems[i].gameObject.SetActive(i < count);
            }
        }

        void BindSlots()
        {
            for (int i = 0; i < _choices.Count; i++)
            {
                BookRewardItemUI item = i < bookItems.Count ? bookItems[i] : null;
                if (item == null) continue;

                item.SetElementSprites(fireElementSprite, waterElementSprite, windElementSprite, earthElementSprite);
                item.Bind(_choices[i], OnPick);
                item.SetSelected(false);
            }
        }

        void OnPick(ComboSkillDef picked)
        {
            for (int i = 0; i < bookItems.Count; i++)
            {
                if (bookItems[i] == null) continue;
                bookItems[i].SetSelected(bookItems[i].BoundSkill == picked);
            }

            if (pauseTimeWhenOpen) Time.timeScale = 1f;
            gameObject.SetActive(false);
            _onPicked?.Invoke(picked);
        }
    }
}
