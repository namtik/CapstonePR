using UnityEngine;

[CreateAssetMenu(menuName = "Round/EventRound")]
public class EventRoundData : RoundData
{
    public string eventDescription; // 이벤트 설명 문구
    // 이벤트 라운드 핸들러를 생성한다.
    public override IRoundHandler CreateHandler()
    {
        return new EventRoundHandler(this);
    }
}

