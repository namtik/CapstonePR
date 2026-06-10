using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Battle.Card;
using Random = UnityEngine.Random;

namespace Battle.UI
{
    // 책 형태 콤보 보상 패널 — 선택지에 콤보 데이터를 바인딩
    public class ComboBookRewardPanelUI : MonoBehaviour
    {
        [Header("Slots")]
        [SerializeField] private Transform bookListRoot; // 선택지 항목 부모
        [SerializeField] private BookRewardItemUI bookItemPrefab; // 선택지 항목 프리팹
        [SerializeField] private List<BookRewardItemUI> bookItems = new List<BookRewardItemUI>(); // 선택지 항목 목록
        [SerializeField, Range(1, 5)] private int choiceCount = 3; // 제시 선택지 수

        [Header("Element Icons")]
        [SerializeField] private Sprite fireElementSprite; // 불 속성 아이콘
        [SerializeField] private Sprite waterElementSprite; // 물 속성 아이콘
        [SerializeField] private Sprite windElementSprite; // 바람 속성 아이콘
        [SerializeField] private Sprite earthElementSprite; // 땅 속성 아이콘

        [Header("Runtime")]
        [SerializeField] private bool pauseTimeWhenOpen = true; // 열릴 때 시간 정지 여부

        [Header("선택하지 않고 나가기 버튼")]
        [SerializeField] private RewardSkipButtonStyle skipButtonStyle = new RewardSkipButtonStyle { label = "선택하지 않고 나가기" }; // 스킵 버튼 스타일

        [Header("상단 문구 위치 (선택)")]
        [Tooltip("패널 제목 텍스트가 있으면 그 RectTransform을 연결. 보상 표시 시 아래 위치로 이동시킨다. 비우면 무시.")]
        [SerializeField] private RectTransform titleTransform; // 제목 RectTransform
        [Tooltip("titleTransform 연결 시 적용할 위치(부모 중앙 기준 anchoredPosition).")]
        [SerializeField] private Vector2 titlePosition = new Vector2(0f, 320f); // 제목 위치

        System.Action<ComboSkillDef> _onPicked; // 콤보 선택 콜백
        readonly List<ComboSkillDef> _choices = new List<ComboSkillDef>(); // 이번 선택지 목록
        Button _skipButton; // 생성된 스킵 버튼

        // 콤보 보상을 제시하고 슬롯/스킵 버튼을 구성
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

        // 스킵 버튼을 스타일대로 재생성
        void EnsureSkipButton()
        {
            if (_skipButton != null) Destroy(_skipButton.gameObject);
            if (skipButtonStyle == null) skipButtonStyle = new RewardSkipButtonStyle();
            _skipButton = skipButtonStyle.Build(transform, Skip);
            _skipButton.transform.SetAsLastSibling();
        }

        // 제목이 연결돼 있으면 지정 위치로 정렬
        void ApplyTitlePosition()
        {
            if (titleTransform != null)
                titleTransform.anchoredPosition = titlePosition;
        }

        // 콤보를 고르지 않고 나가기(콜백에 null 전달)
        void Skip()
        {
            if (pauseTimeWhenOpen) Time.timeScale = 1f;
            gameObject.SetActive(false);
            var cb = _onPicked;
            _onPicked = null;
            cb?.Invoke(null);
        }

        // 패널을 숨기고 시간을 복구
        public void Hide()
        {
            if (pauseTimeWhenOpen) Time.timeScale = 1f;
            gameObject.SetActive(false);
        }

        // 미보유 콤보 후보에서 선택지를 중복 없이 구성
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

            // 모두 보유 중이면 전체에서 다시 뽑음
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

        // 전투 컨트롤러에서 보유 중인 콤보 refId 집합을 수집
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

        // 선택지 수만큼 슬롯을 확보하고 활성/비활성 처리
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

        // 각 슬롯에 선택지 콤보와 속성 아이콘을 바인딩
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

        // 콤보 선택 시 선택 표시 후 패널을 닫고 콜백 발화
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
