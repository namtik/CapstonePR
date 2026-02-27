using UnityEngine;

public class Roundmanager : MonoBehaviour
{
    private IRoundHandler currentRoundHandler;

    public void StartRound(RoundData roundData)
    {
        currentRoundHandler = roundData.CreateHandler();
        currentRoundHandler.OnEnterRound(this);
    }

    public void EndRound()
    {

            currentRoundHandler.OnExitRound(this);
    }

}
