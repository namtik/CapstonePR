using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Battle.Card;
using Random = UnityEngine.Random;

namespace Battle.UI
{
    /// <summary>
    /// 전투 승리 후 카드 획득 보상 UI. RelicHUD처럼 런타임 생성(프리팹/씬 작업 불필요).
    /// 등급 가중 랜덤으로 N장 제시 → 한 장 선택 시 콜백으로 카드 ID 전달.
    /// 카드 비주얼은 CardHandHUD가 보유한 NewCardView 프리팹을 재사용해 렌더한다.
    /// </summary>
    public class CardRewardUI : MonoBehaviour
    {
        public static CardRewardUI Instance { get; private set; }

        const int CHOICE_COUNT = 4;

        private System.Action<int> _onPicked;
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

        /// <summary>씬에 없으면 생성해서 Instance를 보장하고 반환.</summary>
        public static CardRewardUI EnsureExists()
        {
            if (Instance != null) return Instance;
            var existing = FindFirstObjectByType<CardRewardUI>(FindObjectsInactive.Include);
            if (existing != null) { Instance = existing; return existing; }
            var go = new GameObject("CardRewardUI");
            return go.AddComponent<CardRewardUI>();
        }

        /// <summary>
        /// 보상 제시. onPicked(cardId)는 한 장 선택 시(또는 보상 풀이 비어 스킵 시 cardId&lt;=0으로) 호출되고,
        /// 호출 직후 UI는 닫힌다.
        /// </summary>
        public void Present(NewCardView cardPrefab, System.Action<int> onPicked)
        {
            if (_panel != null) Cleanup(); // 중복 호출 방지

            _onPicked = onPicked;

            if (cardPrefab == null)
            {
                Debug.LogWarning("[CardReward] 카드 프리팹이 없어 보상 생략.");
                Finish(-1);
                return;
            }

            List<CardData> choices = RollChoices(CHOICE_COUNT);
            if (choices.Count == 0)
            {
                Debug.LogWarning("[CardReward] 보상 카드 풀이 비어 보상 생략.");
                Finish(-1);
                return;
            }

            BuildUI(cardPrefab, choices);
            Time.timeScale = 0f;
            Debug.Log($"[CardReward] 보상 카드 {choices.Count}장 제시");
        }

        // ─────────────────────────────────────────────────────────────
        // 카드 풀 선정 (등급 가중)
        // ─────────────────────────────────────────────────────────────

        List<CardData> RollChoices(int count)
        {
            CardDatabase.EnsureInit();

            var pool = new List<CardData>();
            foreach (var c in CardDatabase.All)
            {
                if (c == null) continue;
                // 덱 적격 = 4속성 카드만 (무속성 필러 500 / 파편 501~503 제외)
                switch (c.element)
                {
                    case CardElement.Fire:
                    case CardElement.Water:
                    case CardElement.Wind:
                    case CardElement.Earth:
                        pool.Add(c);
                        break;
                }
            }

            var result = new List<CardData>();
            var used = new HashSet<int>();
            int target = Mathf.Min(count, pool.Count);
            int guard = 0;
            while (result.Count < target && guard++ < 2000)
            {
                CardData pick = WeightedPick(pool);
                if (pick == null) break;
                if (used.Add(pick.id)) result.Add(pick);
            }
            return result;
        }

        static int RarityWeight(CardRarity r) => r switch
        {
            CardRarity.Normal => 60,
            CardRarity.Rare => 30,
            CardRarity.Epic => 10,
            _ => 30
        };

        static CardData WeightedPick(List<CardData> pool)
        {
            if (pool.Count == 0) return null;
            int total = 0;
            for (int i = 0; i < pool.Count; i++) total += RarityWeight(pool[i].rarity);
            if (total <= 0) return pool[Random.Range(0, pool.Count)];

            int roll = Random.Range(0, total);
            for (int i = 0; i < pool.Count; i++)
            {
                roll -= RarityWeight(pool[i].rarity);
                if (roll < 0) return pool[i];
            }
            return pool[pool.Count - 1];
        }

        // ─────────────────────────────────────────────────────────────
        // UI 구성 (런타임 생성)
        // ─────────────────────────────────────────────────────────────

        void BuildUI(NewCardView cardPrefab, List<CardData> choices)
        {
            Canvas canvas = ResolveCombatCanvas();
            if (canvas == null)
            {
                Debug.LogWarning("[CardReward] 전투 캔버스를 찾지 못해 보상 생략.");
                Finish(-1);
                return;
            }

            _panel = new GameObject("CardRewardPanel", typeof(RectTransform));
            var prt = (RectTransform)_panel.transform;
            prt.SetParent(canvas.transform, false);
            StretchFull(prt);
            prt.SetAsLastSibling();

            // 딤 배경 (뒤쪽 클릭 차단)
            var dim = NewRect("Dim", prt);
            StretchFull(dim);
            var dimImg = dim.gameObject.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.78f);
            dimImg.raycastTarget = true;

            // 타이틀
            var titleRt = NewRect("Title", prt);
            CenterAnchor(titleRt);
            titleRt.sizeDelta = new Vector2(900f, 80f);
            titleRt.anchoredPosition = new Vector2(0f, 320f);
            var titleText = titleRt.gameObject.AddComponent<Text>();
            titleText.font = BuiltinFont();
            titleText.fontSize = 40;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = Color.white;
            titleText.text = "카드 획득 — 한 장 선택";
            titleText.raycastTarget = false;

            // 카드 행
            int n = choices.Count;
            const float spacing = 360f;
            float startX = -spacing * (n - 1) / 2f;
            for (int i = 0; i < n; i++)
            {
                CardData data = choices[i];

                var slot = NewRect($"Choice{i}", prt);
                CenterAnchor(slot);
                slot.sizeDelta = new Vector2(300f, 400f);
                slot.anchoredPosition = new Vector2(startX + spacing * i, 0f);

                // 카드 비주얼 — NewCardView 프리팹 재사용
                var view = Instantiate(cardPrefab, slot);
                var vrt = view.GetComponent<RectTransform>();
                if (vrt != null)
                {
                    CenterAnchor(vrt);
                    vrt.anchoredPosition = Vector2.zero;
                    vrt.localScale = Vector3.one;
                }
                view.SetCard(new CardInstance(data));
                view.Refresh();
                view.enabled = false; // 드래그/호버 핸들러 비활성
                foreach (var g in view.GetComponentsInChildren<Graphic>(true))
                    g.raycastTarget = false;

                // 클릭용 투명 버튼 (카드 위 전체)
                var btnRt = NewRect("ClickCatcher", slot);
                StretchFull(btnRt);
                btnRt.SetAsLastSibling();
                var btnImg = btnRt.gameObject.AddComponent<Image>();
                btnImg.color = new Color(1f, 1f, 1f, 0f); // 투명하지만 raycast 수신
                btnImg.raycastTarget = true;
                var btn = btnRt.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                int cardId = data.id;
                btn.onClick.AddListener(() => Pick(cardId));
            }
        }

        void Pick(int cardId)
        {
            if (_panel == null) return; // 이미 선택됨 — 중복 클릭 무시
            Finish(cardId);
        }

        void Finish(int cardId)
        {
            Time.timeScale = 1f;
            var cb = _onPicked;
            _onPicked = null;
            Cleanup();
            cb?.Invoke(cardId);
        }

        void Cleanup()
        {
            if (_panel != null)
            {
                Destroy(_panel);
                _panel = null;
            }
        }

        // ─────────────────────────────────────────────────────────────
        // 헬퍼
        // ─────────────────────────────────────────────────────────────

        static Canvas ResolveCombatCanvas()
        {
            GameObject stage = GameObject.Find("CombatStage");
            if (stage != null)
            {
                var c = stage.GetComponentInChildren<Canvas>(true);
                if (c != null) return c;
            }
            return FindFirstObjectByType<Canvas>();
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
        }

        static Font BuiltinFont()
        {
            if (_builtinFont == null)
                _builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return _builtinFont;
        }
    }
}
