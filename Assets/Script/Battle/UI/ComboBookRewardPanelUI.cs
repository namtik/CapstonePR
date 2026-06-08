using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
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

        [Header("선택하지 않고 나가기 버튼")]
        [SerializeField] private RewardSkipButtonStyle skipButtonStyle = new RewardSkipButtonStyle { label = "선택하지 않고 나가기" };

        [Header("상단 문구 위치 (선택)")]
        [Tooltip("패널 제목 텍스트가 있으면 그 RectTransform을 연결. 보상 표시 시 아래 위치로 이동시킨다. 비우면 무시.")]
        [SerializeField] private RectTransform titleTransform;
        [Tooltip("titleTransform 연결 시 적용할 위치(부모 중앙 기준 anchoredPosition).")]
        [SerializeField] private Vector2 titlePosition = new Vector2(0f, 320f);

        System.Action<ComboSkillDef> _onPicked;
        readonly List<ComboSkillDef> _choices = new List<ComboSkillDef>();
        Button _skipButton;

        public void Present(System.Action<ComboSkillDef> onPicked)
        {
            _onPicked = onPicked;

            BuildChoices();
            EnsureSlots(_choices.Count);
            BindSlots();
            EnsureSkipButton();
            ApplyTitlePosition();

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            if (pauseTimeWhenOpen) Time.timeScale = 0f;

            Debug.Log($"[ComboBookRewardPanelUI] 콤보 선택지 {_choices.Count}개 표시");
        }

        /// <summary>인스펙터 스타일대로 "선택하지 않고 나가기" 버튼을 (재)생성한다.</summary>
        void EnsureSkipButton()
        {
            if (_skipButton != null) Destroy(_skipButton.gameObject);
            if (skipButtonStyle == null) skipButtonStyle = new RewardSkipButtonStyle();
            _skipButton = skipButtonStyle.Build(transform, Skip);
            _skipButton.transform.SetAsLastSibling();
        }

        /// <summary>제목 텍스트가 연결돼 있으면 인스펙터 지정 위치로 정렬한다.</summary>
        void ApplyTitlePosition()
        {
            if (titleTransform != null)
                titleTransform.anchoredPosition = titlePosition;
        }

        /// <summary>콤보를 고르지 않고 나가기 — 맵으로 복귀(콜백에 null 전달).</summary>
        void Skip()
        {
            if (pauseTimeWhenOpen) Time.timeScale = 1f;
            gameObject.SetActive(false);
            var cb = _onPicked;
            _onPicked = null;
            cb?.Invoke(null);
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
