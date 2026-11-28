using System;
using System.Collections;
using UnityEngine;

// 획득물이 오른쪽 위 인벤토리 버튼까지 곡선을 그리며 작아지는 연출(유물 상자 획득 연출에서 분리한 공용 코드).
// 유물 상자(RelicStageController)와 이벤트 카드 획득(EventStageController)이 같이 쓴다.
public static class InventoryFlyFx
{
    public const float Duration = 0.58f;

    // rt를 화면 좌표 from -> to로 날린다. 크기는 startScale -> startScale*0.22, 끝(88% 이후)에서 setAlpha로 흐려진다.
    public static IEnumerator Fly(RectTransform rt, Vector2 fromScreen, Vector2 toScreen,
        Action<float> setAlpha = null, float startScale = 1f, bool keepOnTop = true, float duration = Duration)
    {
        if (rt == null) yield break;
        Camera cam = CanvasCamera(rt);

        Vector2 from = fromScreen;
        Vector2 to = toScreen;
        Vector2 delta = to - from;
        Vector2 side = delta.sqrMagnitude > 1f ? new Vector2(-delta.y, delta.x).normalized : Vector2.up;
        if (side.y < 0f) side = -side;
        Vector2 control = Vector2.Lerp(from, to, 0.42f) + side * Mathf.Clamp(delta.magnitude * 0.16f, 50f, 180f);

        Place(rt, from, cam);
        float t = 0f;
        while (t < duration)
        {
            if (rt == null) yield break;
            t += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(t / duration);
            float e = u * u * (3f - 2f * u);
            Place(rt, QuadBezier(from, control, to, e), cam);
            rt.localScale = Vector3.one * (startScale * Mathf.Lerp(1f, 0.22f, e));
            if (u > 0.88f)
                setAlpha?.Invoke(Mathf.Lerp(1f, 0.15f, (u - 0.88f) / 0.12f));
            if (keepOnTop) rt.SetAsLastSibling();
            yield return null;
        }
    }

    // 화면 좌표에 rt를 놓는다(오버레이·카메라 캔버스 모두)
    public static void Place(RectTransform rt, Vector2 screen, Camera cam)
    {
        var parent = rt.parent as RectTransform;
        if (parent != null && RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, screen, cam, out Vector3 world))
            rt.position = world;
        else
            rt.position = new Vector3(screen.x, screen.y, 0f);
    }

    public static Vector2 QuadBezier(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    public static Camera CanvasCamera(Transform t)
    {
        Canvas canvas = t != null ? t.GetComponentInParent<Canvas>() : null;
        if (canvas == null) return null;
        canvas = canvas.rootCanvas;
        return canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
    }

    // rt 중심의 화면 좌표
    public static Vector2 ScreenCenter(RectTransform rt)
    {
        if (rt == null)
            return new Vector2(Screen.width * 0.5f, Screen.height * 0.45f);
        return RectTransformUtility.WorldToScreenPoint(CanvasCamera(rt), rt.TransformPoint(rt.rect.center));
    }

    // rt의 화면상 크기(픽셀)
    public static Vector2 ScreenSize(RectTransform rt)
    {
        if (rt == null)
            return new Vector2(160f, 160f);
        Camera cam = CanvasCamera(rt);
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        Vector2 bl = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
        Vector2 tr = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);
        return new Vector2(Mathf.Abs(tr.x - bl.x), Mathf.Abs(tr.y - bl.y));
    }
}
