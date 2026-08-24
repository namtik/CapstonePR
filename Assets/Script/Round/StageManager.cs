using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 3D 전투 스테이지 씬을 additive로 로드/표시/숨김 관리한다.
// 컨셉→씬 매핑을 지원하되, 매칭되는 컨셉이 없으면 fallback 스테이지를 사용한다(현재 스테이지 1개=항상 fallback).
// 로드된 스테이지는 상주시키고, 전투 진입 시에만 표시(BattleStageController.SetVisible)한다.
public class StageManager : MonoBehaviour
{
    public static StageManager Instance { get; private set; }

    [System.Serializable]
    public class StageEntry
    {
        [Tooltip("스테이지 컨셉 키(예: forest, cave). RoundManager가 이 키로 스테이지를 요청.")]
        public string concept;
        [Tooltip("해당 컨셉의 씬 이름(Build Settings에 등록되어 있어야 함).")]
        public string sceneName;
    }

    [Header("스테이지 씬")]
    [Tooltip("컨셉별 스테이지 매핑. 비어 있거나 매칭 없으면 fallback 사용.")]
    [SerializeField] private List<StageEntry> stages = new List<StageEntry>();
    [Tooltip("컨셉 매칭이 없을 때 사용할 기본 스테이지 씬 이름(Build Settings 등록 필수).")]
    [SerializeField] private string fallbackStageSceneName = "Stage_Forest_Battle";
    [Tooltip("시작 시 fallback 스테이지를 미리 additive 로드해 둘지 여부.")]
    [SerializeField] private bool preloadFallbackOnStart = true;

    [Header("카메라 전환")]
    [Tooltip("전투 밖에서 쓰는 기본(비전투) 카메라 — 보통 MainScene의 메인 카메라. 전투 중엔 자동으로 꺼서 스테이지 카메라(Base)와 충돌하지 않게 한다. URP는 Base 카메라 2개가 동시에 켜지면 서로 덮어써서 3D가 안 보인다.")]
    [SerializeField] private Camera nonCombatCamera;   // 비전투 기본 카메라(전투 중 off)

    [Header("디버그")]
    [Tooltip("스테이지 표시/숨김·로드 흐름을 콘솔에 출력.")]
    [SerializeField] private bool logVerbose = true;

    private string _loadedSceneName;   // 현재 additive 로드된 스테이지 씬 이름
    private bool _loading;             // 로드 진행 중 플래그
    private bool _desiredVisible;      // 원하는 표시 상태(로드 완료 시 반영 — 타이밍 안전)

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        // preload는 '숨김 상태'로 미리 로드만 해둔다(_desiredVisible=false 유지).
        if (preloadFallbackOnStart)
            StartCoroutine(LoadStageRoutine(fallbackStageSceneName));
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // 컨셉에 해당하는 씬 이름을 해석한다(매칭 없으면 fallback).
    string ResolveSceneName(string concept)
    {
        if (!string.IsNullOrEmpty(concept))
        {
            foreach (var s in stages)
                if (s != null && s.concept == concept && !string.IsNullOrEmpty(s.sceneName))
                    return s.sceneName;
        }
        return fallbackStageSceneName;
    }

    // 전투 진입 — 요청 컨셉(없으면 fallback) 스테이지를 로드/전환하고 표시한다.
    public void ShowStage(string concept = null)
    {
        string target = ResolveSceneName(concept);
        if (string.IsNullOrEmpty(target))
        {
            Debug.LogWarning("[StageManager] 표시할 스테이지 씬 이름이 비어 있습니다.");
            return;
        }

        _desiredVisible = true; // 로드가 진행 중이어도, 완료 시 이 값이 반영된다(race 방지)
        if (logVerbose) Debug.Log($"[StageManager] ShowStage('{target}') 요청. loaded={_loadedSceneName}, loading={_loading}");

        if (_loadedSceneName == target && !_loading)
            ApplyVisibility();               // 이미 로드됨 → 즉시 표시
        else
            StartCoroutine(LoadStageRoutine(target)); // 로드/전환 후 완료 시점에 ApplyVisibility
    }

    // 전투 이탈 — 현재 스테이지를 숨기고 비전투 카메라를 되살린다(언로드하지 않고 상주).
    public void HideStage()
    {
        _desiredVisible = false;
        if (logVerbose) Debug.Log("[StageManager] HideStage()");
        ApplyVisibility();
    }

    // 스테이지 씬을 additive 로드한다. 완료 시 현재 _desiredVisible을 반영한다.
    IEnumerator LoadStageRoutine(string sceneName)
    {
        // 이미 로딩 중이면, 진행 중인 로드가 끝날 때 ApplyVisibility가 최신 _desiredVisible을 반영하므로 중복 코루틴은 종료.
        if (_loading) yield break;
        if (_loadedSceneName == sceneName)
        {
            ApplyVisibility();
            yield break;
        }

        _loading = true;

        // 기존 스테이지 언로드(컨셉 전환)
        if (!string.IsNullOrEmpty(_loadedSceneName))
        {
            Scene old = SceneManager.GetSceneByName(_loadedSceneName);
            if (old.IsValid() && old.isLoaded)
            {
                var un = SceneManager.UnloadSceneAsync(old);
                while (un != null && !un.isDone) yield return null;
            }
            _loadedSceneName = null;
        }

        // 새 스테이지 로드(이미 로드돼 있지 않은 경우)
        Scene existing = SceneManager.GetSceneByName(sceneName);
        if (!existing.IsValid() || !existing.isLoaded)
        {
            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (op == null)
            {
                Debug.LogError($"[StageManager] 씬 로드 실패: '{sceneName}' (Build Settings 등록 확인).");
                _loading = false;
                yield break;
            }
            while (!op.isDone) yield return null;
        }

        _loadedSceneName = sceneName;
        _loading = false;
        if (logVerbose) Debug.Log($"[StageManager] 로드 완료: '{sceneName}'. 표시 반영 desiredVisible={_desiredVisible}");

        // 로드 직후 BattleStageController.Awake/OnEnable가 Current를 등록함. 최신 표시 의도를 반영.
        ApplyVisibility();
    }

    // 현재 _desiredVisible에 맞춰 스테이지 표시 + 비전투 카메라 반전 토글(Base 카메라 충돌 방지)
    void ApplyVisibility()
    {
        var stage = BattleStageController.Current;
        if (stage != null)
            stage.SetVisible(_desiredVisible);
        else if (logVerbose)
            Debug.LogWarning("[StageManager] BattleStageController.Current가 null — 스테이지 씬이 로드/활성 상태인지 확인.");

        if (nonCombatCamera != null)
            nonCombatCamera.enabled = !_desiredVisible; // 전투 표시 중엔 비전투 카메라 off, 이탈 시 on

        if (logVerbose)
            Debug.Log($"[StageManager] ApplyVisibility({_desiredVisible}) — stage={(stage != null)}, nonCombatCam.enabled={(nonCombatCamera != null ? (!_desiredVisible).ToString() : "N/A")}");
    }
}
