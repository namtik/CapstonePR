using NUnit.Framework.Interfaces;
using System.Collections.Generic;
using UnityEngine;

public abstract class RoundData : ScriptableObject
{
    public string roundName;
    public NodeType roundType;

    public abstract IRoundHandler CreateHandler();
}

// 일반 전투
[CreateAssetMenu(menuName = "Round/CombatRound")]
public class CombatRoundData : RoundData
{
    public List<EnemyData> enemies;

    public override IRoundHandler CreateHandler()
    {
        return new CombatRoundHandler(this);
    }
}

// 정예
[CreateAssetMenu(menuName = "Round/EliteRound")]
public class EliteRoundData : RoundData
{
    public List<EnemyData> enemies;

    public override IRoundHandler CreateHandler()
    {
        return new EliteRoundHandler(this);
    }
}

// 보스
[CreateAssetMenu(menuName = "Round/BossRound")]
public class BossRoundData : RoundData
{
    public EnemyData bossEnemy;
    public List<EnemyData> minionEnemies; // 보스는 부하도 있을 수 있음

    public override IRoundHandler CreateHandler()
    {
        return new BossRoundHandler(this);
    }
}

// 상점
[CreateAssetMenu(menuName = "Round/ShopRound")]
public class ShopRoundData : RoundData
{
    //public List<CardData> CardPool;
    //public int itemCount = 3;

    public override IRoundHandler CreateHandler()
    {
        return new ShopRoundHandler(this);
    }
}

//사건
[CreateAssetMenu(menuName = "Round/EventRound")]
public class EventRoundData : RoundData
{
    public string eventDescription;
    public override IRoundHandler CreateHandler()
    {
        return new EventRoundHandler(this);
    }
}

// 휴식
[CreateAssetMenu(menuName = "Round/RestRound")]
public class RestRoundData : RoundData
{
    public float healPercent = 0.3f; // 최대 회복량

    public override IRoundHandler CreateHandler()
    {
        return new RestRoundHandler(this);
    }
}