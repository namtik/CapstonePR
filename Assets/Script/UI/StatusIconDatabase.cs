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

    public Sprite GetIcon(string key)
    {
        if (icons == null || string.IsNullOrEmpty(key)) return null;
        for (int i = 0; i < icons.Count; i++)
        {
            StatusIconInfo info = icons[i];
            if (info == null || info.iconSprite == null) continue;
            if (string.Equals(info.statusKey, key, System.StringComparison.OrdinalIgnoreCase))
                return info.iconSprite;
        }
        return null;
    }

    // 씬에 연결된 데이터베이스의 아이콘. burn은 Sprite/UI/Burn이다.
    public static Sprite LoadIcon(string key)
    {
        StatusIconDatabase[] all = Resources.FindObjectsOfTypeAll<StatusIconDatabase>();
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null) continue;
            Sprite sprite = all[i].GetIcon(key);
            if (sprite != null) return sprite;
        }
        StatusIconDatabase refs = BuildAssetRefs.Instance != null ? BuildAssetRefs.Instance.statusIconDatabase : null;
        if (refs != null) return refs.GetIcon(key);
#if UNITY_EDITOR
        StatusIconDatabase asset = UnityEditor.AssetDatabase.LoadAssetAtPath<StatusIconDatabase>(
            "Assets/Script/UI/StatusIconDatabase.asset");
        if (asset != null) return asset.GetIcon(key);
#endif
        return null;
    }
}