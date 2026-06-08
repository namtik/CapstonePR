using System.Collections.Generic;
using UnityEngine;

public class StatusPanelUI : MonoBehaviour
{
    [Header("UI ����")]
    [SerializeField] private StatusIconUI iconPrefab; // ������+���� ������
    [SerializeField] private StatusIconDatabase iconDatabase; // ������ SO �����ͺ��̽� 1��

    // ���� ȭ�鿡 ������ �����ܵ��� �����ϴ� ��ųʸ�
    private Dictionary<string, StatusIconUI> activeIcons = new Dictionary<string, StatusIconUI>();

    // �����ͺ��̽��� ������� Ű��(string)�� �̹���(Sprite)�� ������ ã�� ���� ��
    private Dictionary<string, Sprite> dataMap = new Dictionary<string, Sprite>();

    // �� �г��� ���¸� ������ ���� (Player �Ǵ� EnemyController)
    private IBattleUnit targetUnit;

    // 적 피격 흔들림을 함께 따라가기 위한 추적 대상(적의 shakeTarget)과 초기 월드 오프셋
    private Transform followTarget;
    private Vector3 followWorldOffset;
    private bool following;
    private bool followOffsetCaptured;

    void Awake()
    {
        // SO �����ͺ��̽� �ȿ� �ִ� ����Ʈ�� ��ųʸ��� ��ȯ
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

    // ���� ���� �� � ������ UI�� ����� �������ִ� �Լ�
    public void SetTarget(IBattleUnit unit)
    {
        // ���� Ÿ���� �־��ٸ� ����(����) ����
        if (targetUnit != null)
        {
            targetUnit.OnStatusChanged -= UpdateStatusUI;
        }

        targetUnit = unit;
        ClearAllIcons(); // ȭ�� �ʱ�ȭ

        if (targetUnit != null)
        {
            // ���ο� Ÿ���� �����̻� ���� �̺�Ʈ ���� (���� ���� ������ UpdateStatusUI ����)
            targetUnit.OnStatusChanged += UpdateStatusUI;

            // �������ڸ��� �⺻ ��ġ�� 0�� �ƴ� �����̻��� �ִٸ� ���⼭ �ʱ�ȭ
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

    // Ÿ���� �����̻��� ���� ������ �ڵ����� ȣ��Ǵ� �Լ�
    public void UpdateStatusUI(string key, int count)
    {
        // �����ͺ��̽��� ��ϵ��� ���� �����̻� Ű�� ����
        if (!dataMap.ContainsKey(key)) return;

        // �̹� ȭ�鿡 ����� �������� �ִٸ�?
        if (activeIcons.ContainsKey(key))
        {
            if (count > 0)
            {
                activeIcons[key].UpdateCount(count); // ���ڸ� ����
            }
            else
            {
                // ��ġ�� 0 ���ϰ� �Ǹ� ������ ����
                Destroy(activeIcons[key].gameObject);
                activeIcons.Remove(key);
            }
        }
        // ȭ�鿡 �������� ���µ�, ��ġ�� 0���� Ŀ���ٸ�?
        else if (count > 0)
        {
            // �� ������ �������� �����ؼ� ȭ��(�г� ����)�� ���
            StatusIconUI newIcon = Instantiate(iconPrefab, transform);
            newIcon.SetStatus(dataMap[key], count, key);
            activeIcons.Add(key, newIcon);
        }
    }

    // �г� ���� ��� ������ �����
    private void ClearAllIcons()
    {
        foreach (var icon in activeIcons.Values)
        {
            if (icon != null) Destroy(icon.gameObject);
        }
        activeIcons.Clear();
    }
}