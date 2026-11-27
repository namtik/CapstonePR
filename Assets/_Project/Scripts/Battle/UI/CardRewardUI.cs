using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Battle.Card;
using Random = UnityEngine.Random;

namespace Battle.UI
{
    // 전투 승리 후 카드 획득 보상 UI(런타임 생성, 등급 가중 랜덤 제시)
    public class CardRewardUI : MonoBehaviour
    {
        public static CardRewardUI Instance { get; private set; } // 싱글톤 인스턴스

        const int CHOICE_COUNT = 4; // 제시 카드 수

        [Header("보상창 프리팹")]
        [Tooltip("배경·슬롯·스킵 버튼이 배치된 보상창 프리팹. 비우면 기존 런타임 생성 UI(딤+제목 텍스트)로 폴백.")]
        [SerializeField] private CardRewardPanelView panelPrefab; // 보상창 프리팹

        [Header("상단 문구 (프리팹 미사용 시)")]
        [SerializeField] private Font titleFont; // 타이틀 폰트
        [SerializeField, Range(12, 96)] private int titleFontSize = 40; // 타이틀 글자 크기
        [SerializeField] private Color titleColor = Color.white; // 타이틀 색
        [Tooltip("상단 문구 위치(화면 중앙 기준 anchoredPosition). 기본 (0, 320).")]
        [SerializeField] private Vector2 titlePosition = new Vector2(0f, 320f); // 타이틀 위치

        [Header("선택하지 않기 버튼")]
        [SerializeField] private RewardSkipButtonStyle skipButtonStyle = new RewardSkipButtonStyle(); // 스킵 버튼 스타일

        [Header("카드 크기/배치")]
        [Tooltip("제시 카드 배율(1 = 기본 300x400). 키우면 간격도 함께 올려 겹침 방지.")]
        [SerializeField, Range(0.3f, 2f)] private float cardScale = 1f; // 카드 배율
        [Tooltip("카드 사이 가로 간격(px).")]
        [SerializeField] private float cardSpacing = 360f; // 카드 간 간격

        [Header("카드 호버 확대 (ButtonHoverScale)")]
        [Tooltip("마우스를 올린 카드를 확대할지 여부.")]
        [SerializeField] private bool enableHoverScale = true; // 호버 확대 사용 여부
        [Tooltip("호버 시 카드 배율(1 = 그대로). 1.08이면 8% 확대.")]
        [SerializeField, Range(1f, 1.5f)] private float hoverScale = 1.08f; // 호버 확대 배율
        [Tooltip("확대/복귀 보간 속도(클수록 빠름). 보상 화면(timeScale=0)에서도 동작.")]
        [SerializeField] private float hoverLerpSpeed = 14f; // 호버 보간 속도

        private System.Action<int> _onPicked; // 카드 선택 콜백
        private GameObject _panel; // 보상 패널 루트
        private static Font _builtinFont; // 내장 폰트 캐시

        // 타이틀 폰트/크기/색/위치를 외부에서 설정
        public void SetTitleStyle(Font font, int fontSize, Color color, Vector2 position)
        {
            titleFont = font;
            titleFontSize = Mathf.Clamp(fontSize, 12, 96);
            titleColor = color;
            titlePosition = position;
        }

        // 보상창 프리팹을 외부에서 주입(null이면 런타임 생성 UI)
        public void SetPanelPrefab(CardRewardPanelView prefab)
        {
            if (prefab != null) panelPrefab = prefab;
        }

        // 스킵 버튼 스타일을 외부에서 주입
        public void SetSkipButtonStyle(RewardSkipButtonStyle style)
        {
            if (style != null) skipButtonStyle = style;
        }

        // 카드 배율과 간격을 외부에서 주입
        public void SetCardLayout(float scale, float spacing)
        {
            cardScale = Mathf.Clamp(scale, 0.3f, 2f);
            cardSpacing = Mathf.Max(0f, spacing);
        }

        // 싱글톤 인스턴스를 설정
        void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(this); return; }
        }

        // 싱글톤 참조를 해제
        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // 인스턴스가 없으면 찾거나 생성해 보장
        public static CardRewardUI EnsureExists()
        {
            if (Instance != null) return Instance;
            var existing = FindFirstObjectByType<CardRewardUI>(FindObjectsInactive.Include);
            if (existing != null) { Instance = existing; return existing; }
            var go = new GameObject("CardRewardUI");
            return go.AddComponent<CardRewardUI>();
        }

        // 카드 보상을 제시하고 선택 시 콜백으로 카드 ID 전달
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

            // 프리팹이면 슬롯 수만큼 제시
            int choiceCount = CHOICE_COUNT;
            if (panelPrefab != null && panelPrefab.CardSlots.Count > 0) choiceCount = panelPrefab.CardSlots.Count;
            List<CardData> choices = RollChoices(choiceCount);
            if (choices.Count == 0)
            {
                Debug.LogWarning("[CardReward] 보상 카드 풀이 비어 보상 생략.");
                Finish(-1);
                return;
            }

            BuildUI(cardPrefab, choices);
            Time.timeScale = 0f;
            if (_panel != null)
                SfxManager.Instance?.PlayGetReward();
            Debug.Log($"[CardReward] 보상 카드 {choices.Count}장 제시");
        }

        // 보상 풀에서 등급 가중으로 중복 없이 count장을 뽑음
        List<CardData> RollChoices(int count)
        {
            CardDatabase.EnsureInit();

            var pool = new List<CardData>();
            foreach (var c in CardDatabase.All)
            {
                if (c == null) continue;
                // 기본 덱 카드(BASIC)는 보상 제외
                if (c.HasTag("BASIC")) continue;
                // 전투 중 임시 생성되는 카드(파편 등)는 보상 풀에서 제외
                if (c.HasTag("TEMPORARY_ON_CREATE") || c.HasTag("FRAGMENT")) continue;
                // 4속성 카드만 보상 대상
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

            // 전투 보상 등급: 파워 카드 = 레어, 나머지 = 일반 (카드 DB의 rarity는 보지 않음)
            // 일반은 다시 공격/스킬로 나눠 정해진 비율로 뽑는다.
            var attack = new List<CardData>();
            var skill = new List<CardData>();
            var rare = new List<CardData>();
            foreach (var c in pool)
            {
                if (c.type == CardType.Power) rare.Add(c);
                else if (c.type == CardType.Attack) attack.Add(c);
                else skill.Add(c);
            }

            // 칸마다 등급을 먼저 뽑고(일반 75 : 레어 25), 일반이면 공격 50 : 스킬 50으로 다시 나눈 뒤
            // 그 묶음 안에서 무작위 1장. 고른 묶음이 바닥나면 남은 묶음에서 뽑는다.
            var result = new List<CardData>();
            int target = Mathf.Min(count, pool.Count);
            while (result.Count < target)
            {
                List<CardData> from;
                if (Random.Range(0, 100) < RARE_TIER_PERCENT)
                    from = rare;
                else
                    from = Random.Range(0, 100) < ATTACK_IN_NORMAL_PERCENT ? attack : skill;

                if (from.Count == 0) from = FirstNonEmpty(attack, skill, rare);
                if (from == null) break;

                int idx = Random.Range(0, from.Count);
                result.Add(from[idx]);
                from.RemoveAt(idx); // 같은 카드 중복 제시 방지
            }

            EnsureMainElementChoice(result, attack, skill, rare);
            return result;
        }

        // 주력 속성 보장: 제시 카드에 덱의 주력 속성 카드가 한 장도 없으면 한 칸을 주력 속성 카드로 바꾼다.
        // 레어(파워) 칸은 유지하고 일반 칸을 바꾸며, 가능하면 바뀌는 카드와 같은 종류(공격/스킬/파워)로 넣어 등급·종류 비율을 지킨다.
        static void EnsureMainElementChoice(List<CardData> result, List<CardData> attack, List<CardData> skill, List<CardData> rare)
        {
            if (result.Count == 0) return;
            CardElement? main = ResolveMainElement();
            if (!main.HasValue) return;
            for (int i = 0; i < result.Count; i++)
                if (result[i] != null && result[i].element == main.Value) return; // 이미 있음

            var slots = new List<int>();
            for (int i = 0; i < result.Count; i++)
                if (result[i] != null && result[i].type != CardType.Power) slots.Add(i);
            if (slots.Count == 0)
                for (int i = 0; i < result.Count; i++) slots.Add(i);
            int slot = slots[Random.Range(0, slots.Count)];
            CardData replaced = result[slot];

            // 남은 후보(아직 제시되지 않은 카드)에서 주력 속성만 모은다
            var same = new List<CardData>();
            var any = new List<CardData>();
            foreach (var list in new[] { attack, skill, rare })
            {
                foreach (var c in list)
                {
                    if (c == null || c.element != main.Value) continue;
                    any.Add(c);
                    if (replaced != null && RewardGroup(c) == RewardGroup(replaced)) same.Add(c);
                }
            }
            List<CardData> from = same.Count > 0 ? same : any;
            if (from.Count == 0) return; // 주력 속성 카드가 풀에 없으면 그대로
            result[slot] = from[Random.Range(0, from.Count)];
        }

        static int RewardGroup(CardData c) => c.type == CardType.Power ? 2 : c.type == CardType.Attack ? 0 : 1;

        // 런 덱의 주력 속성 — 기본(BASIC) 카드를 뺀 획득 카드 중 가장 많은 속성.
        // 획득 카드가 없으면 기본 카드까지 포함해 센다. 동률이면 그중 무작위.
        static CardElement? ResolveMainElement()
        {
            var run = RunDeckState.Instance;
            if (run == null || run.RunDeck == null) return null;

            var acquired = new Dictionary<CardElement, int>();
            var all = new Dictionary<CardElement, int>();
            foreach (var entry in run.RunDeck)
            {
                CardData data = CardDatabase.GetById(entry.cardId);
                if (data == null || entry.count <= 0) continue;
                CardElement el = data.element;
                if (el != CardElement.Fire && el != CardElement.Water && el != CardElement.Wind && el != CardElement.Earth) continue;
                all[el] = (all.TryGetValue(el, out int a) ? a : 0) + entry.count;
                if (!data.HasTag("BASIC"))
                    acquired[el] = (acquired.TryGetValue(el, out int b) ? b : 0) + entry.count;
            }

            var counts = acquired.Count > 0 ? acquired : all;
            if (counts.Count == 0) return null;
            int max = 0;
            foreach (var kv in counts) if (kv.Value > max) max = kv.Value;
            var ties = new List<CardElement>();
            foreach (var kv in counts) if (kv.Value == max) ties.Add(kv.Key);
            return ties[Random.Range(0, ties.Count)];
        }

        static List<CardData> FirstNonEmpty(params List<CardData>[] lists)
        {
            foreach (var l in lists)
                if (l.Count > 0) return l;
            return null;
        }

        const int RARE_TIER_PERCENT = 25;       // 레어(파워 카드) 등급이 뽑힐 확률(%). 나머지는 일반.
        const int ATTACK_IN_NORMAL_PERCENT = 50; // 일반 등급 안에서 공격 카드 비율(%). 나머지는 스킬 카드.

        // 딤/타이틀/카드 행/스킵 버튼을 런타임으로 구성
        void BuildUI(NewCardView cardPrefab, List<CardData> choices)
        {
            Canvas canvas = ResolveCombatCanvas();
            if (canvas == null)
            {
                Debug.LogWarning("[CardReward] 전투 캔버스를 찾지 못해 보상 생략.");
                Finish(-1);
                return;
            }

            if (panelPrefab != null)
            {
                BuildFromPrefab(canvas, cardPrefab, choices);
                return;
            }

            _panel = new GameObject("CardRewardPanel", typeof(RectTransform));
            var prt = (RectTransform)_panel.transform;
            prt.SetParent(canvas.transform, false);
            StretchFull(prt);
            prt.SetAsLastSibling();

            // 딤 배경 생성(뒤쪽 클릭 차단)
            var dim = NewRect("Dim", prt);
            StretchFull(dim);
            var dimImg = dim.gameObject.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.78f);
            dimImg.raycastTarget = true;

            // 타이틀 텍스트 생성
            var titleRt = NewRect("Title", prt);
            CenterAnchor(titleRt);
            titleRt.sizeDelta = new Vector2(900f, 80f);
            titleRt.anchoredPosition = titlePosition;
            var titleText = titleRt.gameObject.AddComponent<Text>();
            titleText.font = titleFont != null ? titleFont : BuiltinFont();
            titleText.fontSize = titleFontSize;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = titleColor;
            titleText.text = "카드 획득 — 한 장 선택";
            titleText.raycastTarget = false;

            // 카드 행 배치
            int n = choices.Count;
            float spacing = cardSpacing;
            float startX = -spacing * (n - 1) / 2f;
            for (int i = 0; i < n; i++)
            {
                CardData data = choices[i];

                var slot = NewRect($"Choice{i}", prt);
                CenterAnchor(slot);
                slot.sizeDelta = new Vector2(300f, 400f);
                slot.anchoredPosition = new Vector2(startX + spacing * i, 0f);
                slot.localScale = Vector3.one * cardScale; // 비주얼+클릭영역 동시 스케일

                // 카드 프리팹 재사용해 비주얼 생성
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

                // 카드 전체를 덮는 투명 클릭 버튼 생성
                var btnRt = NewRect("ClickCatcher", slot);
                StretchFull(btnRt);
                btnRt.SetAsLastSibling();
                var btnImg = btnRt.gameObject.AddComponent<Image>();
                btnImg.color = new Color(1f, 1f, 1f, 0f); // 투명하되 raycast 수신
                btnImg.raycastTarget = true;
                var btn = btnRt.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                int cardId = data.id;
                btn.onClick.AddListener(() => Pick(cardId));
                AttachKeywordTooltip(btnRt.gameObject, view, data); // 빙결·화상 등 키워드 설명 창

                // 슬롯에 호버 확대 컴포넌트 부착
                if (enableHoverScale)
                {
                    var hover = slot.gameObject.AddComponent<ButtonHoverScale>();
                    hover.Configure(hoverScale, hoverScale, hoverLerpSpeed);
                }
            }

            // 선택하지 않기 버튼 생성
            if (skipButtonStyle == null) skipButtonStyle = new RewardSkipButtonStyle();
            skipButtonStyle.Build(prt, Skip);
            InventoryOpenButton.BringMenubarsToFront(); // 상단 바는 보상창 위에
        }

        // 프리팹 보상창을 띄우고 슬롯마다 카드를 깐다
        void BuildFromPrefab(Canvas canvas, NewCardView cardPrefab, List<CardData> choices)
        {
            var view = Instantiate(panelPrefab, canvas.transform, false);
            _panel = view.gameObject;
            _panel.SetActive(true);
            var prt = (RectTransform)_panel.transform;
            StretchFull(prt);
            prt.SetAsLastSibling();

            var slots = view.CardSlots;
            for (int i = 0; i < slots.Count; i++)
            {
                RectTransform slot = slots[i];
                if (slot == null) continue;
                bool used = i < choices.Count;
                slot.gameObject.SetActive(used);
                if (!used) continue;

                CardData data = choices[i];

                // 카드 프리팹을 슬롯 크기에 맞춰 비율 유지로 배치
                var cardView = Instantiate(cardPrefab, slot);
                var vrt = cardView.GetComponent<RectTransform>();
                if (vrt != null)
                {
                    Vector2 native = vrt.sizeDelta;
                    if (native.x < 1f || native.y < 1f) native = new Vector2(300f, 400f);
                    Vector2 area = slot.rect.size;
                    float fit = (area.x > 1f && area.y > 1f) ? Mathf.Min(area.x / native.x, area.y / native.y) : 1f;
                    CenterAnchor(vrt);
                    vrt.anchoredPosition = Vector2.zero;
                    vrt.localRotation = Quaternion.identity;
                    vrt.localScale = Vector3.one * fit;
                }
                cardView.SetCard(new CardInstance(data));
                cardView.Refresh();
                cardView.enabled = false; // 드래그/호버 핸들러 비활성
                foreach (var g in cardView.GetComponentsInChildren<Graphic>(true))
                    g.raycastTarget = false;

                // 슬롯 전체를 덮는 투명 클릭 버튼
                var btnRt = NewRect("ClickCatcher", slot);
                StretchFull(btnRt);
                btnRt.SetAsLastSibling();
                var btnImg = btnRt.gameObject.AddComponent<Image>();
                btnImg.color = new Color(1f, 1f, 1f, 0f); // 투명하되 raycast 수신
                btnImg.raycastTarget = true;
                var btn = btnRt.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                int cardId = data.id;
                btn.onClick.AddListener(() => Pick(cardId));
                AttachKeywordTooltip(btnRt.gameObject, cardView, data); // 빙결·화상 등 키워드 설명 창

                if (enableHoverScale && slot.GetComponent<ButtonHoverScale>() == null)
                {
                    var hover = slot.gameObject.AddComponent<ButtonHoverScale>();
                    hover.Configure(hoverScale, hoverScale, hoverLerpSpeed);
                }
            }

            if (view.SkipButton != null)
            {
                view.SkipButton.onClick.RemoveAllListeners();
                view.SkipButton.onClick.AddListener(Skip);
            }
            else
            {
                Debug.LogWarning("[CardReward] 보상창 프리팹에 SkipButton이 없습니다 — 스타일 버튼으로 대체.");
                if (skipButtonStyle == null) skipButtonStyle = new RewardSkipButtonStyle();
                skipButtonStyle.Build(prt, Skip);
            }
            InventoryOpenButton.BringMenubarsToFront(); // 상단 바는 보상창 위에
        }

        // 보상 카드에 마우스를 올리면 덱 보기·인벤토리와 같은 키워드(빙결·화상 등) 설명 창을 띄운다
        static void AttachKeywordTooltip(GameObject catcher, NewCardView cardView, CardData data)
        {
            if (catcher == null || cardView == null || data == null) return;
            var trigger = catcher.GetComponent<UnityEngine.EventSystems.EventTrigger>();
            if (trigger == null) trigger = catcher.AddComponent<UnityEngine.EventSystems.EventTrigger>();

            var enter = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ =>
            {
                var label = cardView.GetComponentInChildren<TMPro.TMP_Text>(true);
                CardKeywordTooltip.Show(cardView, data, CardDatabase.GetDisplayDescription(data), label != null ? label.font : null);
            });
            trigger.triggers.Add(enter);

            var exit = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => CardKeywordTooltip.Hide());
            trigger.triggers.Add(exit);
        }

        // 카드를 선택해 보상을 종료
        void Pick(int cardId)
        {
            if (_panel == null) return; // 이미 선택됨
            Finish(cardId);
        }

        // 카드를 고르지 않고 보상을 종료
        void Skip()
        {
            if (_panel == null) return; // 이미 처리됨
            Finish(-1);
        }

        // 시간을 복구하고 콜백 발화 후 UI를 정리
        void Finish(int cardId)
        {
            Time.timeScale = 1f;
            var cb = _onPicked;
            _onPicked = null;
            Cleanup();
            cb?.Invoke(cardId);
        }

        // 런 포기(메인 메뉴·재시작) 시 열려 있던 보상창을 콜백 없이 닫는다.
        // 남겨 두면 다음 런에서 이 창을 누를 때 이전 런의 '맵으로 걸어가기'가 실행돼, 몬스터가 있는데도 앞으로 걸어갔다.
        public static void ForceCloseAll()
        {
            if (Instance == null) return;
            Instance._onPicked = null;
            Instance.Cleanup();
        }

        // 보상 패널을 파괴
        void Cleanup()
        {
            CardKeywordTooltip.Hide(); // 호버 중 선택·종료해도 설명 창이 남지 않게
            if (_panel != null)
            {
                Destroy(_panel);
                _panel = null;
            }
        }

        // 전투 스테이지 또는 씬에서 캔버스를 탐색
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

        // 이름과 부모로 RectTransform 오브젝트를 생성
        static RectTransform NewRect(string goName, Transform parent)
        {
            var go = new GameObject(goName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        // RectTransform을 부모 전체에 늘림
        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        // RectTransform 앵커/피벗을 중앙으로 설정
        static void CenterAnchor(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        // 내장 폰트를 로드(캐시)해 반환
        static Font BuiltinFont()
        {
            if (_builtinFont == null)
                _builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return _builtinFont;
        }
    }
}
