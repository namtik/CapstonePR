#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using Battle;
using UnityEngine;

// 개발용 스테이지/노드 테스트 패널. 에디터·개발 빌드에서만 존재(정식 빌드에는 컴파일되지 않음).
// F1로 열고 닫는다. 맵 화면에서 원하는 노드(일반/정예/보스 전투·상점·휴식·이벤트·유물)로 바로 들어가고,
// 전투 중에는 즉시 승리·각성·방해행동 강제 등 재현용 치트를 쓴다.
// 테스트로 들어간 라운드는 맵 노드 클리어·보스 처치 수·바퀴 진행에 반영되지 않는다(RoundManager.DebugRoundActive).
// 씬 배치가 필요 없도록 첫 씬 로드 후 스스로 생성된다.
public class StageTestPanel : MonoBehaviour
{
    const KeyCode ToggleKey = KeyCode.F1;
    const int MaxPartySize = 3;

    static readonly string[] NodeLabels = { "일반 전투", "정예 전투", "보스 전투", "상점", "휴식", "이벤트", "유물" };
    static readonly NodeType[] NodeTypes = { NodeType.Combat, NodeType.Elite, NodeType.Boss, NodeType.Shop, NodeType.Rest, NodeType.Event, NodeType.Relic };
    static readonly string[] DisruptionLabels = { "저주", "탈진", "흡수", "강화", "회복", "버리기" };

    bool _open;
    Rect _window = new Rect(20, 20, 460, 640);
    Vector2 _scroll;
    Vector2 _enemyScroll;

    int _nodeIndex;                      // 선택한 노드 종류
    int _column;                         // 진행(난이도) 컬럼
    int _stageIndex;                     // 0=자동, 1..=스테이지 씬
    int _timeIndex;                      // 0=자동, 1..=시간대
    int _eventIndex;                     // 0=무작위, 1..=이벤트 번호
    bool _showAllEnemies;                // 종류와 무관하게 모든 적 표시
    readonly List<EnemyData> _party = new List<EnemyData>();

    List<EnemyData> _enemies;            // 프로젝트의 적 데이터 목록(최초 열 때 수집)
    string _message = "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindFirstObjectByType<StageTestPanel>() != null) return;
        var go = new GameObject("[StageTestPanel]");
        DontDestroyOnLoad(go);
        go.AddComponent<StageTestPanel>();
    }

    void Update()
    {
        if (Input.GetKeyDown(ToggleKey)) _open = !_open;
    }

    void OnGUI()
    {
        if (!_open) return;
        GUI.depth = -1000; // 다른 IMGUI보다 위
        _window = GUILayout.Window(GetInstanceID(), _window, DrawWindow, "스테이지 테스트 (F1)");
    }

    void DrawWindow(int id)
    {
        _scroll = GUILayout.BeginScrollView(_scroll);

        GUILayout.Label($"현재: {DescribeState()}");
        if (!string.IsNullOrEmpty(_message)) GUILayout.Label(_message);
        GUILayout.Space(6);

        DrawNodeEntry();
        GUILayout.Space(10);
        DrawBattleCheats();
        GUILayout.Space(10);
        DrawMisc();

        GUILayout.EndScrollView();
        GUI.DragWindow(new Rect(0, 0, 10000, 22));
    }

    // ── 노드 진입 ──

    void DrawNodeEntry()
    {
        GUILayout.Label("■ 노드 바로 진입 (맵 화면에서만)");
        bool canEnter = IsOnMap() && !IsInBattle();

        _nodeIndex = GUILayout.SelectionGrid(_nodeIndex, NodeLabels, 4);
        NodeType type = NodeTypes[_nodeIndex];
        bool isBattle = type == NodeType.Combat || type == NodeType.Elite || type == NodeType.Boss;

        if (isBattle)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"진행 컬럼(난이도·시간대): {_column}", GUILayout.Width(220));
            _column = Mathf.RoundToInt(GUILayout.HorizontalSlider(_column, 0, 29));
            GUILayout.EndHorizontal();

            var stages = StageManager.Instance != null ? StageManager.Instance.StageSceneNames : new List<string>();
            var stageLabels = new List<string> { "자동(적 속성)" };
            stageLabels.AddRange(stages);
            _stageIndex = Mathf.Clamp(_stageIndex, 0, stageLabels.Count - 1);
            GUILayout.Label("스테이지 씬");
            _stageIndex = GUILayout.SelectionGrid(_stageIndex, stageLabels.ToArray(), 2);

            var times = System.Enum.GetNames(typeof(StageLightingManager.StageTime));
            var timeLabels = new List<string> { "자동(진행 컬럼)" };
            timeLabels.AddRange(times);
            GUILayout.Label("시간대");
            _timeIndex = GUILayout.SelectionGrid(_timeIndex, timeLabels.ToArray(), 4);

            DrawEnemyPicker(type);
        }
        else if (type == NodeType.Event)
        {
            var labels = new List<string> { "무작위" };
            for (int i = 1; i <= EventStageController.EventCount; i++) labels.Add($"이벤트 {i}");
            _eventIndex = GUILayout.SelectionGrid(_eventIndex, labels.ToArray(), 6);
        }

        GUI.enabled = canEnter;
        if (GUILayout.Button(canEnter ? $"{NodeLabels[_nodeIndex]} 진입" : "맵 화면에서만 진입할 수 있습니다", GUILayout.Height(30)))
            EnterNode(type);
        GUI.enabled = true;
    }

    void DrawEnemyPicker(NodeType type)
    {
        EnsureEnemyList();
        int limit = type == NodeType.Boss ? 1 : MaxPartySize;

        GUILayout.BeginHorizontal();
        GUILayout.Label($"적 구성 ({_party.Count}/{limit}) — 비우면 평소 등장 구성");
        if (GUILayout.Button("비우기", GUILayout.Width(60))) _party.Clear();
        GUILayout.EndHorizontal();
        if (_party.Count > limit) _party.RemoveRange(limit, _party.Count - limit);

        for (int i = 0; i < _party.Count; i++)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"  {i + 1}. {EnemyLabel(_party[i])}");
            if (GUILayout.Button("X", GUILayout.Width(26))) { _party.RemoveAt(i); i--; }
            GUILayout.EndHorizontal();
        }

        _showAllEnemies = GUILayout.Toggle(_showAllEnemies, "종류와 관계없이 모든 적 보기");
        EnemyClass want = type == NodeType.Boss ? EnemyClass.Boss : type == NodeType.Elite ? EnemyClass.Elite : EnemyClass.Normal;

        _enemyScroll = GUILayout.BeginScrollView(_enemyScroll, GUILayout.Height(150));
        foreach (var e in _enemies)
        {
            if (e == null) continue;
            if (!_showAllEnemies && e.enemyClass != want) continue;
            GUI.enabled = _party.Count < limit;
            if (GUILayout.Button(EnemyLabel(e))) _party.Add(e);
            GUI.enabled = true;
        }
        GUILayout.EndScrollView();
    }

    void EnterNode(NodeType type)
    {
        var gs = GameStateController.Instance;
        var rm = FindFirstObjectByType<RoundManager>(FindObjectsInactive.Include);
        if (gs == null || rm == null) { _message = "GameStateController/RoundManager를 찾지 못했습니다."; return; }

        RoundData data = BuildRoundData(type);
        if (data == null) return;
        data.roundName = $"[테스트] {NodeLabels[_nodeIndex]}";
        data.roundType = type;

        // 맵 노드 진입(MapManager.OnNodeSelected)과 같은 순서. lastVisitedNodeIndex는 건드리지 않아 맵 위치가 유지된다.
        gs.SetCurrentMapStageFromColumn(_column % 10);
        gs.ShowCanvasForNodeType(type, type == NodeType.Boss);

        RoundManager.DebugRoundActive = true;
        RoundManager.DebugRosterOverride = _party.Count > 0 ? new List<EnemyData>(_party) : null;
        if (_stageIndex > 0 && StageManager.Instance != null)
            StageManager.DebugSceneOverride = StageManager.Instance.StageSceneNames[_stageIndex - 1];
        if (_timeIndex > 0)
            StageManager.DebugTimeOverride = (StageLightingManager.StageTime)(_timeIndex - 1);
        EventStageController.DebugEventOverride = type == NodeType.Event ? _eventIndex - 1 : -1;

        try
        {
            rm.StartRound(data);
        }
        finally
        {
            // 스테이지 씬/시간대는 StartRound 안에서 바로 결정되므로 즉시 해제(다음 정상 전투에 남지 않게)
            StageManager.DebugSceneOverride = null;
            StageManager.DebugTimeOverride = null;
        }
        _message = $"{NodeLabels[_nodeIndex]} 진입";
    }

    RoundData BuildRoundData(NodeType type)
    {
        switch (type)
        {
            case NodeType.Combat:
            {
                var d = ScriptableObject.CreateInstance<CombatRoundData>();
                d.enemies = new List<EnemyData>(_party);
                d.columnIndex = _column;
                return d;
            }
            case NodeType.Elite:
            {
                var d = ScriptableObject.CreateInstance<EliteRoundData>();
                d.enemies = new List<EnemyData>(_party);
                d.columnIndex = _column;
                return d;
            }
            case NodeType.Boss:
            {
                EnemyData boss = _party.Count > 0 ? _party[0] : FirstEnemyOfClass(EnemyClass.Boss);
                if (boss == null) { _message = "보스 적 데이터를 찾지 못했습니다. 목록에서 골라 주세요."; return null; }
                var d = ScriptableObject.CreateInstance<BossRoundData>();
                d.bossEnemy = boss;
                d.columnIndex = _column;
                return d;
            }
            case NodeType.Shop:  return ScriptableObject.CreateInstance<ShopRoundData>();
            case NodeType.Rest:  return ScriptableObject.CreateInstance<RestRoundData>();
            case NodeType.Event:
            {
                var d = ScriptableObject.CreateInstance<EventRoundData>();
                d.eventDescription = "테스트 이벤트";
                return d;
            }
            case NodeType.Relic: return ScriptableObject.CreateInstance<RelicRoundData>();
        }
        return null;
    }

    // ── 전투 치트 ──

    void DrawBattleCheats()
    {
        GUILayout.Label("■ 전투 치트 (전투 중에만)");
        var nbc = NewBattleController.Instance;
        GUI.enabled = IsInBattle();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("즉시 승리")) KillAllEnemies();
        if (GUILayout.Button("대상 적 HP 1")) SetTargetHpToOne();
        if (GUILayout.Button("모든 적 게이지 최대-1")) FillAllGauges();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("각성 게이지 최대-1") && nbc != null)
            _message = nbc.DebugFillAwakenGaugeAlmost() ? "각성 게이지 최대-1 — 속성 카드 1장으로 발동" : "각성 중이거나 전투 중이 아님";
        if (GUILayout.Button("플레이어 HP 회복")) HealPlayer();
        GUILayout.EndHorizontal();

        GUILayout.Label("방해행동 강제 발동 (적 행동 대기열로 처리)");
        int pick = GUILayout.SelectionGrid(-1, DisruptionLabels, 6);
        if (pick >= 0 && nbc != null)
        {
            nbc.DebugTriggerDisruption(pick);
            _message = $"방해행동 '{DisruptionLabels[pick]}' 발동 요청";
        }
        GUI.enabled = true;
    }

    void KillAllEnemies()
    {
        var alive = new List<EnemyController>();
        EnemyCombatParty.CollectAlive(alive);
        foreach (var e in alive)
        {
            var stat = e != null ? e.GetComponent<EnemyStat>() : null;
            if (stat != null && stat.IsAlive) stat.TakeDamage(stat.currentHp + 1f);
        }
        _message = $"적 {alive.Count}마리 처치";
    }

    EnemyStat TargetStat()
    {
        var nbc = NewBattleController.Instance;
        EnemyController e = nbc != null && nbc.CurrentEnemy != null ? nbc.CurrentEnemy : EnemyCombatParty.FirstAlive();
        return e != null ? e.GetComponent<EnemyStat>() : null;
    }

    void SetTargetHpToOne()
    {
        var stat = TargetStat();
        if (stat == null || !stat.IsAlive) return;
        if (stat.currentHp > 1f) stat.TakeDamage(stat.currentHp - 1f);
        _message = "대상 적 HP 1";
    }

    // 살아 있는 모든 적의 게이지를 최대-1까지 채운다
    void FillAllGauges()
    {
        var alive = new List<EnemyController>();
        EnemyCombatParty.CollectAlive(alive);
        int count = 0;
        foreach (var e in alive)
        {
            var stat = e != null ? e.GetComponent<EnemyStat>() : null;
            if (stat == null || !stat.IsAlive) continue;
            stat.FillGaugeToAlmostFull();
            count++;
        }
        _message = $"적 {count}마리 게이지 최대-1";
    }

    void HealPlayer()
    {
        var p = Player.Resolve(true);
        if (p == null) return;
        p.Heal(p.maxHp);
        _message = "플레이어 HP 회복";
    }

    // ── 기타 ──

    void DrawMisc()
    {
        GUILayout.Label("■ 기타");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("골드 +100") && MoneyManager.Instance != null)
        {
            MoneyManager.Instance.AddMoney(100);
            _message = "골드 +100";
        }
        GUI.enabled = IsInBattle();
        if (GUILayout.Button("전투 중단 → 맵")) AbortToMap();
        GUI.enabled = true;
        GUILayout.EndHorizontal();
    }

    void AbortToMap()
    {
        var rm = FindFirstObjectByType<RoundManager>(FindObjectsInactive.Include);
        if (rm != null) rm.AbortActiveCombat();
        GameStateController.Instance?.ShowMap();
        _message = "전투를 중단하고 맵으로 돌아왔습니다(보상·노드 클리어 없음).";
    }

    // ── 상태/데이터 ──

    static bool IsInBattle()
    {
        var nbc = NewBattleController.Instance;
        return nbc != null && nbc.InBattle;
    }

    static bool IsOnMap()
    {
        var gs = GameStateController.Instance;
        return gs != null && gs.mapStage != null && gs.mapStage.activeInHierarchy;
    }

    static string DescribeState()
    {
        if (IsInBattle()) return RoundManager.DebugRoundActive ? "전투 중 (테스트 라운드)" : "전투 중";
        if (IsOnMap()) return "맵";
        if (GameStateController.Instance == null) return "게임 상태 없음(메인 메뉴?)";
        return RoundManager.DebugRoundActive ? "노드 진행 중 (테스트 라운드)" : "노드 진행 중";
    }

    void EnsureEnemyList()
    {
        if (_enemies != null) return;
        _enemies = new List<EnemyData>();
#if UNITY_EDITOR
        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:EnemyData"))
        {
            var e = UnityEditor.AssetDatabase.LoadAssetAtPath<EnemyData>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
            if (e != null) _enemies.Add(e);
        }
#else
        // 개발 빌드: 씬/설정이 참조해 메모리에 올라온 적 데이터만 보인다
        _enemies.AddRange(Resources.FindObjectsOfTypeAll<EnemyData>());
#endif
        _enemies.Sort((a, b) =>
        {
            int c = a.enemyClass.CompareTo(b.enemyClass);
            if (c != 0) return c;
            c = a.element.CompareTo(b.element);
            return c != 0 ? c : string.CompareOrdinal(a.name, b.name);
        });
    }

    EnemyData FirstEnemyOfClass(EnemyClass cls)
    {
        EnsureEnemyList();
        foreach (var e in _enemies) if (e != null && e.enemyClass == cls) return e;
        return null;
    }

    static string EnemyLabel(EnemyData e)
    {
        if (e == null) return "(없음)";
        string name = string.IsNullOrEmpty(e.enemyName) ? e.name : e.enemyName;
        return $"[{e.enemyClass}/{e.element}] {name} ({e.name})";
    }
}
#endif
