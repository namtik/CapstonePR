using UnityEngine;

public interface IRoundHandler
{
    void OnEnterRound(RoundManager rm);
    void OnExitRound(RoundManager rm);
}

public class CombatRoundHandler : IRoundHandler
{
    private CombatRoundData data;
    public CombatRoundHandler(CombatRoundData data)
    {
        this.data = data;
    }
    public void OnEnterRound(RoundManager rm)
    {
        Debug.Log("Entering Combat Round: " + data.roundName);
        rm.StartCombat(data);
    }
    public void OnExitRound(RoundManager rm)
    {
        Debug.Log("Exiting Combat Round: " + data.roundName);
        rm.ShowSkillReward();

    }
}

public class EliteRoundHandler : IRoundHandler
{
    private EliteRoundData data;
    public EliteRoundHandler(EliteRoundData data)
    {
        this.data = data;
    }
    public void OnEnterRound(RoundManager rm)
    {
        Debug.Log("Entering Elite Round: " + data.roundName);
        rm.StartCombat(data);
    }
    public void OnExitRound(RoundManager rm)
    {
        Debug.Log("Exiting Elite Round: " + data.roundName);
        rm.ShowSkillReward();
    }
}

public class BossRoundHandler : IRoundHandler
{
    private BossRoundData data;
    public BossRoundHandler(BossRoundData data)
    {
        this.data = data;
    }
    public void OnEnterRound(RoundManager rm)
    {
        Debug.Log("Entering Boss Round: " + data.roundName);
        rm.StartBoss(data);
    }
    public void OnExitRound(RoundManager rm)
    {
        Debug.Log("Exiting Boss Round: " + data.roundName);
        rm.ShowSkillReward();
    }
}

public class ShopRoundHandler : IRoundHandler
{
    private ShopRoundData data;
    public ShopRoundHandler(ShopRoundData data)
    {
        this.data = data;
    }
    public void OnEnterRound(RoundManager rm)
    {
        Debug.Log("Entering Shop Round: " + data.roundName);
    }
    public void OnExitRound(RoundManager rm)
    {
        Debug.Log("Exiting Shop Round: " + data.roundName);
    }
}

public class EventRoundHandler : IRoundHandler
{
    private EventRoundData data;
    public EventRoundHandler(EventRoundData data)
    {
        this.data = data;
    }
    public void OnEnterRound(RoundManager rm)
    {
        Debug.Log("Entering Event Round: " + data.roundName);
        rm.OpenEvent(data);
    }
    public void OnExitRound(RoundManager rm)
    {
        Debug.Log("Exiting Event Round: " + data.roundName);
    }
}

public class RestRoundHandler : IRoundHandler
{
    private RestRoundData data;
    public RestRoundHandler(RestRoundData data)
    {
        this.data = data;
    }
    public void OnEnterRound(RoundManager rm)
    {
        Debug.Log("Entering Rest Round: " + data.roundName);
        rm.OpenRest(data);
    }
    public void OnExitRound(RoundManager rm)
    {
        Debug.Log("Exiting Rest Round: " + data.roundName);
    }
}

public class RelicRoundHandler : IRoundHandler
{
    private RelicRoundData data;
    public RelicRoundHandler(RelicRoundData data)
    {
        this.data = data;
    }

    public void OnEnterRound(RoundManager rm)
    {
        Debug.Log("Entering Relic Round: " + data.roundName);
        rm.OpenRelic(data);
    }

    public void OnExitRound(RoundManager rm)
    {
        Debug.Log("Exiting Relic Round: " + data.roundName);
    }
}
