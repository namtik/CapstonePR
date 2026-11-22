using UnityEngine;

// 게임 전체에서 쓰는 마우스 커서. 화살 끝이 클릭 지점이다.
public static class GameCursor
{
    const string ResourcePath = "UI/GameCursor";
    static readonly Vector2 Hotspot = new Vector2(2f, 2f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Apply()
    {
        Texture2D tex = Resources.Load<Texture2D>(ResourcePath);
        if (tex == null)
        {
            Debug.LogWarning("[GameCursor] UI/GameCursor 텍스처를 찾지 못했습니다.");
            return;
        }

        Cursor.SetCursor(tex, Hotspot, CursorMode.ForceSoftware);
    }
}
