using UnityEngine;

public interface IRoundHandler
{
    void OnEnterRound(Roundmanager rm);
    void OnExitRound(Roundmanager rm);
}

public class CombatRoundHandler : IRoundHandler
{
    private CombatRoundData Data;
    public CombatRoundHandler(CombatRoundData data)
    {
        Data = data;
    }
    public void OnEnterRound(Roundmanager rm)
    {
        Debug.Log("Entering Combat Round: " + Data.roundName);
    }
    public void OnExitRound(Roundmanager rm)
    {
        Debug.Log("Exiting Combat Round: " + Data.roundName);

    }
}

public class EliteRoundHandler : IRoundHandler
{
    private EliteRoundData Data;
    public EliteRoundHandler(EliteRoundData data)
    {
        Data = data;
    }
    public void OnEnterRound(Roundmanager rm)
    {
        Debug.Log("Entering Elite Round: " + Data.roundName);
    }
    public void OnExitRound(Roundmanager rm)
    {
        Debug.Log("Exiting Elite Round: " + Data.roundName);
    }
}

public class BossRoundHandler : IRoundHandler
{
    private BossRoundData Data;
    public BossRoundHandler(BossRoundData data)
    {
        Data = data;
    }
    public void OnEnterRound(Roundmanager rm)
    {
        Debug.Log("Entering Boss Round: " + Data.roundName);
    }
    public void OnExitRound(Roundmanager rm)
    {
        Debug.Log("Exiting Boss Round: " + Data.roundName);
    }
}

public class ShopRoundHandler : IRoundHandler
{
    private ShopRoundData Data;
    public ShopRoundHandler(ShopRoundData data)
    {
        Data = data;
    }
    public void OnEnterRound(Roundmanager rm)
    {
        Debug.Log("Entering Shop Round: " + Data.roundName);
    }
    public void OnExitRound(Roundmanager rm)
    {
        Debug.Log("Exiting Shop Round: " + Data.roundName);
    }
}

public class EventRoundHandler : IRoundHandler
{
    private EventRoundData Data;
    public EventRoundHandler(EventRoundData data)
    {
        Data = data;
    }
    public void OnEnterRound(Roundmanager rm)
    {
        Debug.Log("Entering Event Round: " + Data.roundName);
    }
    public void OnExitRound(Roundmanager rm)
    {
        Debug.Log("Exiting Event Round: " + Data.roundName);
    }
}

public class RestRoundHandler : IRoundHandler
{
    private RestRoundData Data;
    public RestRoundHandler(RestRoundData data)
    {
        Data = data;
    }
    public void OnEnterRound(Roundmanager rm)
    {
        Debug.Log("Entering Rest Round: " + Data.roundName);
    }
    public void OnExitRound(Roundmanager rm)
    {
        Debug.Log("Exiting Rest Round: " + Data.roundName);
    }
}