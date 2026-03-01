using NUnit.Framework.Interfaces;
using System.Collections.Generic;
using UnityEngine;

public abstract class RoundData : ScriptableObject
{
    public string roundName;
    public NodeType roundType;

    public abstract IRoundHandler CreateHandler();
}


