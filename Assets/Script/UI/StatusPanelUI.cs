using System.Collections.Generic;
using UnityEngine;

public class StatusPanelUI : MonoBehaviour
{
    [Header("UI 아이콘")]
    [SerializeField] private StatusIconUI iconPrefab;   // 아이콘+숫자 프리팹
    [SerializeField] private StatusIconDatabase iconDatabase;   // 아이콘 SO 데이터베이스

    // 현재 화면에 표시 중인 아이콘들을 저장하는 딕셔너리
    private Dictionary<string, StatusIconUI> activeIcons = new Dictionary<string, StatusIconUI>();

    // 데이터베이스 기반으로 키(string)에서 이미지(Sprite)를 빠르게 찾는 맵
    private Dictionary<string, Sprite> dataMap = new Dictionary<string, Sprite>();

    // 이 패널이 상태를 표시할 대상(Player 또는 EnemyController)
    private IBattleUnit targetUnit;

    private Transform followTarget;   // 적 피격 흔들림을 따라갈 추적 대상
    private Vector3 followWorldOffset;   // 추적 대상 대비 초기 월드 오프셋
    private bool following;   // 추적 활성화 여부
    private bool followOffsetCaptured;   // 오프셋 캡처 완료 여부

    // 데이터베이스의 아이콘 목록을 키-스프라이트 맵으로 변환한다
    void Awake()
    {
        // SO 데이터베이스의 리스트를 딕셔너리로 변환
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
            Debug.LogError("StatusIconDatabase�� �гο� ������� �ʾҽ��ϴ�!");
        }
    }

    // 이 패널이 상태를 표시할 대상을 지정하고 이벤트를 다시 연결한다
    public void SetTarget(IBattleUnit unit)
    {
        // 기존 타겟이 있었다면 이벤트 구독 해제
        if (targetUnit != null)
        {
            targetUnit.OnStatusChanged -= UpdateStatusUI;
        }

        targetUnit = unit;
        ClearAllIcons(); // 화면 초기화

        if (targetUnit != null)
        {
            // 새 타겟의 상태이상 변경 이벤트 구독
            targetUnit.OnStatusChanged += UpdateStatusUI;

            // 시작 시 0이 아닌 상태이상이 있다면 여기서 초기화
        }
    }

    /// <summary>
    /// 이 패널이 따라갈 대상(적의 흔들림 RectTransform)을 지정. 적이 피격으로 흔들릴 때
    /// 상태이상 아이콘도 같은 오프셋으로 함께 흔들리게 한다. null이면 추적 해제.
    /// </summary>
    public void SetFollowTarget(Transform target)
    {
        followTarget = target;
        following = target != null;
        followOffsetCaptured = false; // 레이아웃 정착 후(첫 LateUpdate) 오프셋 캡처
    }

    void LateUpdate()
    {
        // 1) 타겟(적)이 파괴됐는데 아이콘이 남아 있으면 정리 — 사망 시 상태이상이 화면에 남는 문제 방지.
        if (targetUnit != null && (targetUnit as UnityEngine.Object) == null)
        {
            targetUnit = null;
            following = false;
            followTarget = null;
            ClearAllIcons();
            return;
        }

        // 2) 적 피격 흔들림 추적 — 적의 현재 위치 + 초기 오프셋으로 패널을 함께 이동(흔들림 동기화).
        if (following && followTarget != null)
        {
            if (!followOffsetCaptured)
            {
                // 적 기본 위치(아직 흔들리기 전) 기준으로 패널과의 월드 오프셋을 1회 캡처
                followWorldOffset = transform.position - followTarget.position;
                followOffsetCaptured = true;
            }
            transform.position = followTarget.position + followWorldOffset;
        }
    }

    // 타겟의 상태이상이 바뀔 때 자동으로 호출되어 아이콘을 갱신한다
    public void UpdateStatusUI(string key, int count)
    {
        // 데이터베이스에 등록되지 않은 상태이상 키는 무시
        if (!dataMap.ContainsKey(key)) return;

        // 이미 화면에 표시된 아이콘이 있다면
        if (activeIcons.ContainsKey(key))
        {
            if (count > 0)
            {
                activeIcons[key].UpdateCount(count); // 숫자만 갱신
            }
            else
            {
                // 수치가 0 이하가 되면 아이콘 제거
                Destroy(activeIcons[key].gameObject);
                activeIcons.Remove(key);
            }
        }
        // 화면에 아이콘이 없는데 수치가 0보다 커졌다면
        else if (count > 0)
        {
            // 새 아이콘을 생성해 화면(패널 자식)에 등록
            StatusIconUI newIcon = Instantiate(iconPrefab, transform);
            newIcon.SetStatus(dataMap[key], count, key);
            activeIcons.Add(key, newIcon);
        }
    }

    // 패널의 모든 아이콘을 제거한다
    private void ClearAllIcons()
    {
        foreach (var icon in activeIcons.Values)
        {
            if (icon != null) Destroy(icon.gameObject);
        }
        activeIcons.Clear();
    }
}
