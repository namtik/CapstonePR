using System.Collections.Generic;
using UnityEngine;

// 상태이상 키와 아이콘 이미지를 묶은 정보
[System.Serializable]
public class StatusIconInfo
{
    public string statusKey;   // 상태이상 키(예: "burn", "wet")
    public Sprite iconSprite;  // 표시할 아이콘 이미지
}

// 상태이상 아이콘 정보를 모아두는 ScriptableObject 데이터베이스
[CreateAssetMenu(fileName = "StatusIconDatabase", menuName = "Status UI/Icon Database")]
public class StatusIconDatabase : ScriptableObject
{
    public List<StatusIconInfo> icons = new List<StatusIconInfo>();   // 상태이상 아이콘 목록
}