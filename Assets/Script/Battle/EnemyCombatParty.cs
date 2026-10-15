using System.Collections.Generic;
using UnityEngine;

// 현재 전투에 스폰된 적 목록. 다중 전투의 타겟팅/게이지/전체 공격이 이 목록을 본다.
public static class EnemyCombatParty
{
    static readonly List<EnemyController> Members = new List<EnemyController>();
    static readonly List<EnemyOverlayHUD> ClonedHuds = new List<EnemyOverlayHUD>();
    static readonly List<StatusPanelUI> ClonedStatusPanels = new List<StatusPanelUI>();
    static readonly List<EnemyController> AliveBuffer = new List<EnemyController>();

    public static int SpawnedCount { get; private set; }
    public static bool IsMulti => SpawnedCount > 1;
    public static EnemyController CurrentTarget { get; set; }

    public static void BeginBattle(int spawnedCount)
    {
        ClearClones();
        Members.Clear();
        CurrentTarget = null;
        SpawnedCount = Mathf.Max(0, spawnedCount);
    }

    public static void Register(EnemyController enemy)
    {
        if (enemy == null || Members.Contains(enemy)) return;
        Members.Add(enemy);
    }

    public static void Unregister(EnemyController enemy)
    {
        if (enemy == null) return;
        Members.Remove(enemy);
        if (CurrentTarget == enemy) CurrentTarget = FirstAlive();
    }

    public static void TrackHudClone(EnemyOverlayHUD hud)
    {
        if (hud != null && !ClonedHuds.Contains(hud))
            ClonedHuds.Add(hud);
    }

    public static void TrackStatusClone(StatusPanelUI panel)
    {
        if (panel != null && !ClonedStatusPanels.Contains(panel))
            ClonedStatusPanels.Add(panel);
    }

    public static int AliveCount
    {
        get
        {
            int n = 0;
            for (int i = 0; i < Members.Count; i++)
                if (IsUsable(Members[i])) n++;
            return n;
        }
    }

    public static EnemyController FirstAlive()
    {
        for (int i = 0; i < Members.Count; i++)
            if (IsUsable(Members[i])) return Members[i];
        return null;
    }

    public static EnemyController PickRandomAlive()
    {
        CollectAlive(AliveBuffer);
        if (AliveBuffer.Count == 0) return null;
        return AliveBuffer[Random.Range(0, AliveBuffer.Count)];
    }

    public static void CollectAlive(List<EnemyController> dest)
    {
        if (dest == null) return;
        dest.Clear();
        for (int i = 0; i < Members.Count; i++)
            if (IsUsable(Members[i])) dest.Add(Members[i]);
    }

    // 포인터 아래의 적을 고른다. 겹치면 화면 중심이 더 가까운 쪽.
    public static EnemyController HitTest(Vector2 screenPos)
    {
        EnemyController best = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < Members.Count; i++)
        {
            EnemyController e = Members[i];
            if (!IsUsable(e) || !e.ContainsAimPoint(screenPos)) continue;
            if (!e.TryGetAimScreen(out Vector2 center, out _))
                center = screenPos;
            float d = Vector2.Distance(screenPos, center);
            if (d < bestDist)
            {
                best = e;
                bestDist = d;
            }
        }
        return best;
    }

    public static void DestroyAllMembers()
    {
        for (int i = Members.Count - 1; i >= 0; i--)
        {
            EnemyController e = Members[i];
            if (e != null) Object.Destroy(e.gameObject);
        }
        Members.Clear();
        CurrentTarget = null;
        SpawnedCount = 0;
        ClearClones();
    }

    public static void EndBattle()
    {
        Members.Clear();
        CurrentTarget = null;
        SpawnedCount = 0;
        ClearClones();
        if (EnemyOverlayHUD.Instance != null)
            EnemyOverlayHUD.Instance.Unbind();
    }

    static void ClearClones()
    {
        for (int i = 0; i < ClonedHuds.Count; i++)
            if (ClonedHuds[i] != null) Object.Destroy(ClonedHuds[i].gameObject);
        ClonedHuds.Clear();

        for (int i = 0; i < ClonedStatusPanels.Count; i++)
            if (ClonedStatusPanels[i] != null) Object.Destroy(ClonedStatusPanels[i].gameObject);
        ClonedStatusPanels.Clear();
    }

    static bool IsUsable(EnemyController e) => e != null && e.IsAlive;
}
