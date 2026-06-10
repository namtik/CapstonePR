using UnityEngine;

public interface IRoundHandler
{
    // 라운드 진입 시 호출된다.
    void OnEnterRound(RoundManager rm);
    // 라운드 종료 시 호출된다.
    void OnExitRound(RoundManager rm);
}

public class CombatRoundHandler : IRoundHandler
{
    private CombatRoundData data; // 일반 전투 라운드 데이터
    // 일반 전투 라운드 데이터를 받아 핸들러를 초기화한다.
    public CombatRoundHandler(CombatRoundData data)
    {
        this.data = data;
    }
    // 일반 전투를 시작한다.
    public void OnEnterRound(RoundManager rm)
    {
        Debug.Log("Entering Combat Round: " + data.roundName);
        rm.StartCombat(data);
    }
    // 전투 종료 후 스킬 보상을 표시한다.
    public void OnExitRound(RoundManager rm)
    {
        Debug.Log("Exiting Combat Round: " + data.roundName);
        rm.ShowSkillReward();

    }
}

public class EliteRoundHandler : IRoundHandler
{
    private EliteRoundData data; // 정예 전투 라운드 데이터
    // 정예 전투 라운드 데이터를 받아 핸들러를 초기화한다.
    public EliteRoundHandler(EliteRoundData data)
    {
        this.data = data;
    }
    // 정예 전투를 시작한다.
    public void OnEnterRound(RoundManager rm)
    {
        Debug.Log("Entering Elite Round: " + data.roundName);
        rm.StartCombat(data);
    }
    // 전투 종료 후 스킬 보상을 표시한다.
    public void OnExitRound(RoundManager rm)
    {
        Debug.Log("Exiting Elite Round: " + data.roundName);
        rm.ShowSkillReward();
    }
}

public class BossRoundHandler : IRoundHandler
{
    private BossRoundData data; // 보스 라운드 데이터
    // 보스 라운드 데이터를 받아 핸들러를 초기화한다.
    public BossRoundHandler(BossRoundData data)
    {
        this.data = data;
    }
    // 보스 전투를 시작한다.
    public void OnEnterRound(RoundManager rm)
    {
        Debug.Log("Entering Boss Round: " + data.roundName);
        rm.StartBoss(data);
    }
    // 전투 종료 후 스킬 보상을 표시한다.
    public void OnExitRound(RoundManager rm)
    {
        Debug.Log("Exiting Boss Round: " + data.roundName);
        rm.ShowSkillReward();
    }
}

public class ShopRoundHandler : IRoundHandler
{
    private ShopRoundData data; // 상점 라운드 데이터
    // 상점 라운드 데이터를 받아 핸들러를 초기화한다.
    public ShopRoundHandler(ShopRoundData data)
    {
        this.data = data;
    }
    // 상점 라운드 진입을 로그로 남긴다.
    public void OnEnterRound(RoundManager rm)
    {
        Debug.Log("Entering Shop Round: " + data.roundName);
    }
    // 상점 라운드 종료를 로그로 남긴다.
    public void OnExitRound(RoundManager rm)
    {
        Debug.Log("Exiting Shop Round: " + data.roundName);
    }
}

public class EventRoundHandler : IRoundHandler
{
    private EventRoundData data; // 이벤트 라운드 데이터
    // 이벤트 라운드 데이터를 받아 핸들러를 초기화한다.
    public EventRoundHandler(EventRoundData data)
    {
        this.data = data;
    }
    // 이벤트 스테이지를 연다.
    public void OnEnterRound(RoundManager rm)
    {
        Debug.Log("Entering Event Round: " + data.roundName);
        rm.OpenEvent(data);
    }
    // 이벤트 라운드 종료를 로그로 남긴다.
    public void OnExitRound(RoundManager rm)
    {
        Debug.Log("Exiting Event Round: " + data.roundName);
    }
}

public class RestRoundHandler : IRoundHandler
{
    private RestRoundData data; // 휴식 라운드 데이터
    // 휴식 라운드 데이터를 받아 핸들러를 초기화한다.
    public RestRoundHandler(RestRoundData data)
    {
        this.data = data;
    }
    // 휴식 스테이지를 연다.
    public void OnEnterRound(RoundManager rm)
    {
        Debug.Log("Entering Rest Round: " + data.roundName);
        rm.OpenRest(data);
    }
    // 휴식 라운드 종료를 로그로 남긴다.
    public void OnExitRound(RoundManager rm)
    {
        Debug.Log("Exiting Rest Round: " + data.roundName);
    }
}

public class RelicRoundHandler : IRoundHandler
{
    private RelicRoundData data; // 유물 라운드 데이터
    // 유물 라운드 데이터를 받아 핸들러를 초기화한다.
    public RelicRoundHandler(RelicRoundData data)
    {
        this.data = data;
    }

    // 유물 스테이지를 연다.
    public void OnEnterRound(RoundManager rm)
    {
        Debug.Log("Entering Relic Round: " + data.roundName);
        rm.OpenRelic(data);
    }

    // 유물 라운드 종료를 로그로 남긴다.
    public void OnExitRound(RoundManager rm)
    {
        Debug.Log("Exiting Relic Round: " + data.roundName);
    }
}
