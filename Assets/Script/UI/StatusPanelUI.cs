using System.Collections.Generic;
using UnityEngine;

public class StatusPanelUI : MonoBehaviour
{
    [Header("UI 설정")]
    [SerializeField] private StatusIconUI iconPrefab; // 아이콘+숫자 프리팹
    [SerializeField] private StatusIconDatabase iconDatabase; // 만들어둔 SO 데이터베이스 1개

    // 현재 화면에 생성된 아이콘들을 관리하는 딕셔너리
    private Dictionary<string, StatusIconUI> activeIcons = new Dictionary<string, StatusIconUI>();

    // 데이터베이스를 기반으로 키값(string)과 이미지(Sprite)를 빠르게 찾기 위한 맵
    private Dictionary<string, Sprite> dataMap = new Dictionary<string, Sprite>();

    // 이 패널이 상태를 추적할 유닛 (Player 또는 EnemyController)
    private IBattleUnit targetUnit;

    void Awake()
    {
        // SO 데이터베이스 안에 있는 리스트를 딕셔너리로 변환
        if (iconDatabase != null)
        {
            foreach (var info in iconDatabase.icons)
            {
                if (!dataMap.ContainsKey(info.statusKey))
                {
                    dataMap.Add(info.statusKey, info.iconSprite);
                }
            }
        }
        else
        {
            Debug.LogError("StatusIconDatabase가 패널에 연결되지 않았습니다!");
        }
    }

    // 전투 시작 시 어떤 유닛의 UI를 띄울지 연결해주는 함수
    public void SetTarget(IBattleUnit unit)
    {
        // 기존 타겟이 있었다면 연결(구독) 해제
        if (targetUnit != null)
        {
            targetUnit.OnStatusChanged -= UpdateStatusUI;
        }

        targetUnit = unit;
        ClearAllIcons(); // 화면 초기화

        if (targetUnit != null)
        {
            // 새로운 타겟의 상태이상 변경 이벤트 구독 (값이 변할 때마다 UpdateStatusUI 실행)
            targetUnit.OnStatusChanged += UpdateStatusUI;

            // 시작하자마자 기본 수치가 0이 아닌 상태이상이 있다면 여기서 초기화
        }
    }

    // 타겟의 상태이상이 변할 때마다 자동으로 호출되는 함수
    public void UpdateStatusUI(string key, int count)
    {
        // 데이터베이스에 등록되지 않은 상태이상 키면 무시
        if (!dataMap.ContainsKey(key)) return;

        // 이미 화면에 띄워진 아이콘이 있다면?
        if (activeIcons.ContainsKey(key))
        {
            if (count > 0)
            {
                activeIcons[key].UpdateCount(count); // 숫자만 갱신
            }
            else
            {
                // 수치가 0 이하가 되면 아이콘 삭제
                Destroy(activeIcons[key].gameObject);
                activeIcons.Remove(key);
            }
        }
        // 화면에 아이콘이 없는데, 수치가 0보다 커졌다면?
        else if (count > 0)
        {
            // 새 아이콘 프리팹을 생성해서 화면(패널 내부)에 띄움
            StatusIconUI newIcon = Instantiate(iconPrefab, transform);
            newIcon.SetStatus(dataMap[key], count, key);
            activeIcons.Add(key, newIcon);
        }
    }

    // 패널 안의 모든 아이콘 지우기
    private void ClearAllIcons()
    {
        foreach (var icon in activeIcons.Values)
        {
            if (icon != null) Destroy(icon.gameObject);
        }
        activeIcons.Clear();
    }
}