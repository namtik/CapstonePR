using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class StatusIconInfo
{
    public string statusKey;   // 예: "burn", "wet"
    public Sprite iconSprite;  // 아이콘 이미지
}

[CreateAssetMenu(fileName = "StatusIconDatabase", menuName = "Status UI/Icon Database")]
public class StatusIconDatabase : ScriptableObject
{
    public List<StatusIconInfo> icons = new List<StatusIconInfo>();
}