using System.Collections.Generic;
using UnityEngine;

public abstract class RoundData : ScriptableObject
{
    public string roundName; // 라운드 이름
    public NodeType roundType; // 라운드(노드) 타입

    // 라운드 타입별 진행 핸들러를 생성한다.
    public abstract IRoundHandler CreateHandler();
}


