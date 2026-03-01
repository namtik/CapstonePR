using UnityEngine;

//»ç°Ç
[CreateAssetMenu(menuName = "Round/EventRound")]
public class EventRoundData : RoundData
{
    public string eventDescription;
    public override IRoundHandler CreateHandler()
    {
        return new EventRoundHandler(this);
    }
}

