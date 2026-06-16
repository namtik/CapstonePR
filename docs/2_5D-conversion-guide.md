# 2.5D 전환 가이드 (배경·연출만 3D)

> 목표: 카드·HUD·전투 로직은 **그대로** 두고, 전투 화면 **뒤에 진짜 3D 씬**을 넣어 입체감을 준다 (하스스톤 보드 방식).
> 범위(사용자 선택): **배경·연출만 3D**, **빠른 비주얼 임팩트** 우선. 전투 씬만 먼저, Shop/Map/Rest는 그대로.
> 작성: 멀티에이전트 조사(씬·적·UI·VFX) + URP 렌더링 검증 2건 종합. 2026-06-16.

---

## 0. 결론 먼저 — 추천 아키텍처: **RenderTexture 백드롭**

3D 월드를 화면에 직접 합성하지 말고, **별도 3D 카메라가 RenderTexture에 렌더 → 그 텍스처를 전투 캔버스 맨 뒤 RawImage로 표시**한다. 카드/HUD/적은 기존 UI 그대로 그 위에 얹힌다.

왜 이 방식인가 (검증된 이유):

1. **기존 Main Camera(Orthographic)를 안 건드린다.** 이 카메라는 전투 HUD·SettingCanvas·맵 캔버스 **3개의 Screen Space - Camera 캔버스**(planeDistance 100)의 렌더 카메라다. 카메라를 perspective로 바꾸면 이 UI 3개가 전부 왜곡/재배치된다 → 절대 금지.
2. **URP 카메라 스택의 함정을 원천 회피.** URP에선 빌트인의 "Depth + Depth Only clear"로 두 카메라를 겹치는 게 동작하지 않는다. 정식 방법은 Camera Stack(Base+Overlay)인데, **하나의 스택에 Orthographic UI + Perspective 월드를 섞으면 projection이 깨진다(`_ProjectionParams` 버그)**. RenderTexture는 이 문제 자체가 없다 — 월드 카메라는 독립적으로 RT에만 렌더.
3. **배경 교체 로직과 자연스럽게 연결.** 지금 `CombatStageController`가 적/난이도별로 배경 스프라이트를 바꾼다. 이걸 "3D 씬/스카이박스/조명 프리셋 교체"로 확장하면 끝.
4. **전투에서만 켜기 쉽다.** 3D 씬 루트 + 월드 카메라를 CombatStage 활성/비활성에 묶으면 됨.

> Camera Stack(두 카메라 직접 합성) 방식은 **적까지 진짜 3D 모델로 만들어 UI와 깊이 상호작용시키고 싶을 때**만 의미가 있다(§4). 배경만이면 RenderTexture가 더 단순·안전·빠르다.

---

## 1. 현재 구조 (조사로 확인된 실제 값)

| 항목 | 실제 상태 | 의미 |
|---|---|---|
| 카메라 | **단 1개** `Main Camera` (fileID 330585545), **Orthographic** size 5, Post-Processing **ON**, Clear=Solid(투명 a:0) | 3D엔 perspective 필요 → **새 카메라 추가**. 기존 건 UI 전용으로 유지 |
| 전투 HUD 캔버스 | `Canvas` (19061628), **Screen Space - Camera**, planeDistance 100, CombatStage의 자식 | 3D가 이 캔버스 **뒤**로 가야 함 |
| 전투 배경 | 평면 UI Image `background` (1642830527), `CombatStageController.backgroundImage`가 스프라이트 교체(normal/elite/boss + 적별) | **이 레이어를 3D로 대체.** 단 삭제 금지(컨트롤러 null-ref) → 투명/비활성 |
| 적 | UGUI `Image` (자식 "Image"), 루트엔 로직만. **Animator 없음** — 코루틴 sprite 교체 + anchoredPosition 흔들기 + color 플래시 | 배경만이면 **적은 UI 그대로 둔다** (3D 배경 위 선명한 2D = 하스스톤 느낌) |
| 적 머리 위 UI | HP바·게이지·의도텍스트·데미지숫자 = 적 RectTransform의 **자식**(자동으로 따라다님). 상태아이콘만 `StatusPanelUI` 팔로워 | 적을 UI로 두면 **손댈 것 없음** |
| 플레이어 | `menubar.prefab` 상단 고정 HUD(HP바·하트). 화면상 바디 없음 | **변경 불필요** |
| VFX | 전부 캔버스 바인딩(Coffee **UIParticle**). 카드 VFX는 **고정 좌표**(적 (0,300)/플레이어 (0,100)) | 배경만이면 그대로 작동. §4에서만 재앵커 |
| 패키지 | `com.unity.cloud.gltfast 6.16` 설치됨 (Draco/KTX는 **미설치**) | glb 3D 모델 임포트 가능 |
| 렌더러 | `PC_Renderer.asset`, `Mobile_Renderer.asset` (URP 17.3) | **Forward/Forward+ 인지 확인**(Deferred면 카메라 스택 불가, §5) |

**건드리면 안 되는 것**: `IBattleUnit`, `Player.cs`, `EnemyStat`/`EnemyController` public API, `NewBattleController`(적을 `FindFirstObjectByType<EnemyController>()`로만 참조), `RoundManager.SpawnEnemy`의 시그니처. 시각 변경은 카메라/씬/배경 레이어에만 가둔다.

---

## 2. 마일스톤 1 — 3D 배경만 (★ 사용자 선택 범위, 이것만으로 "3D네" 달성)

> 코드 변경 거의 0. 순수 에디터 작업. 게임 로직/UI/전투 전혀 안 건드림.

### 2-1. 3D 레이어 만들기
1. `Edit ▸ Project Settings ▸ Tags and Layers`에서 새 레이어 **`World3D`** 추가.

### 2-2. 3D 씬(무대) 구성
1. Hierarchy에 빈 오브젝트 **`World3DRoot`** 생성 → CombatStage 아래로 넣기(전투 때만 활성화되게).
2. 그 아래에 3D 무대 배치:
   - 바닥: `Plane` 또는 지형, 벽/프롭 몇 개.
   - **Directional Light 1개 필수** (URP Lit 모델은 광원 없으면 까맣게 나옴).
   - 원하면 Skybox 머티리얼.
   - 3D 에셋은 `gltfast`로 무료 CC0 glb(Quaternius/Kenney/Sketchfab) 임포트하거나 Unity 기본 도형으로 시작.
3. `World3DRoot`와 모든 자식의 **Layer를 `World3D`로** 설정.

### 2-3. 월드 카메라 + RenderTexture
1. **RenderTexture 에셋 생성**: Project에서 `Create ▸ Rendering ▸ Render Texture`, 이름 `RT_World3D`. 크기 1920×1080(또는 화면 비율), Depth Buffer = At least 24 bit.
2. **새 카메라 `WorldCamera`** 생성(`World3DRoot` 아래):
   - Projection = **Perspective**, FOV 35~55, Near 0.3 / Far 1000.
   - **Culling Mask = `World3D`만** (다른 레이어 다 끔).
   - **Output ▸ Target Texture = `RT_World3D`**.
   - Background = Skybox 또는 Solid Color.
   - Post Processing은 여기서 켜도 됨(UI엔 영향 없음 — §5).
   - 무대를 잘 잡도록 위치/각도 조정(약간 위에서 내려다보면 깊이감↑).
3. **기존 Main Camera의 Culling Mask에서 `World3D` 레이어 제거** (3D가 UI 카메라에 중복 렌더되지 않게).

### 2-4. 전투 캔버스에 3D 띄우기
1. 전투 HUD 캔버스(`Canvas` 19061628) 아래 **맨 앞(첫 번째 자식)** 에 `RawImage` 생성, 이름 `World3DView`.
   - UGUI는 **계층 순서가 그리기 순서** → 첫 자식 = 맨 뒤에 깔림.
   - RectTransform을 캔버스 전체로 stretch(앵커 0~1, offset 0).
   - **Texture = `RT_World3D`** 지정.
   - ⚠️ RawImage 비율과 WorldCamera aspect를 맞춰야 안 늘어남(1920×1080 RT + 16:9면 OK. 다른 비율 대응은 RT를 화면 크기로 갱신하거나 RawImage `uvRect` 조정).
2. 기존 평면 배경 `background`(1642830527)을 **투명(alpha 0)으로** 만들거나 GameObject 비활성화.
   - **삭제 금지**: `CombatStageController.backgroundImage`가 참조 → null-ref 위험.

### 2-5. 전투에서만 켜기
- `World3DRoot`(카메라 포함)가 CombatStage 자식이면 CombatStage on/off에 자동으로 따라감. 별도 코드 불필요.
- 확실히 하려면 전투 진입/이탈에서 `WorldCamera.enabled`와 `World3DRoot.SetActive()` 토글. **단 Main Camera는 절대 끄지 말 것**(다른 스테이지 SS-Camera 캔버스를 렌더 중).

### 2-6. (선택) 적/난이도별 3D 씬 교체
- `CombatStageController`에 `normal/elite/boss`별 **3D 환경 프리셋**(프롭 묶음 또는 스카이박스/조명) 참조를 추가하고, 기존 스프라이트 교체와 같은 타이밍에 3D 씬을 스왑. 데이터 흐름(`EnemyData`)은 그대로 재사용.

✅ **여기까지가 "배경·연출만 3D" 완성.** 카드/적/HUD는 한 줄도 안 바뀌고, 뒤에 입체 무대가 생긴다.

---

## 3. 폴리시 (가성비 큰 연출, 마일스톤 1 위에 바로)

- **URP Post-processing**: 씬에 Global `Volume` + Profile 추가 → Bloom + Vignette + 약한 Depth of Field. **WorldCamera에서만** Post Processing 체크(§5 주의: Main Camera에 켜면 SS-Camera UI까지 블룸 먹음).
- **카메라 흔들림**: 지금 피격 흔들림은 UI 오버레이만(`PlayerDamageOverlay.TriggerShake`). 3D 배경은 가만히 있어 따로 놂 → `WorldCamera`에 가벼운 셰이크를 같은 타이밍에 추가하면 타격감 통일.
- 배경 파티클(먼지·광원), 약한 환경 애니메이션(흔들리는 불씨 등).

---

## 4. 더 나아가려면 — 적/플레이어를 입체로 (선택, 나중)

배경만으로 부족하고 **적 자체**를 3D로 두고 싶을 때. 두 갈래:

### 4a. 빌보드(가장 쌈, 기존 2D 아트 재활용)
- 3D 씬 안에 Quad를 두고 기존 적 스프라이트를 텍스처로(머티리얼 = **URP/Unlit, Surface=Transparent**, 필요시 Alpha Clip). `BillboardToCamera`(§6)로 카메라 정면 유지.
- RT 방식이면 이 Quad가 RT 안에 찍히고, 적 머리 위 UI는 여전히 UI라서 **RT 안 좌표→RawImage 사각형**으로 매핑해야 함(조금 번거로움).

### 4b. 진짜 3D 모델(richer, 작업 큼) — 이때는 **Camera Stack로 전환** 권장
적을 화면에 직접 렌더(RT 안이 아니라)해서 HP바 팔로워 수학을 단순화. 검증된 **URP 정석 구성**:

- **WorldCamera = Base**(Perspective, Skybox/Solid clear, Culling=World3D).
- **기존 Main Camera = 그 스택의 Overlay**로 추가… 하지만 ⚠️ **Ortho UI + Perspective World를 한 스택에 섞으면 projection 깨짐.** 그래서 UI 카메라는 ortho를 유지해야 하므로, 현실적으로는 **RT 방식을 유지하되 적만 RT 안 3D 모델로** 두거나, UI를 Screen Space - Camera 대신 손볼 각오를 해야 함. → **배경만 범위에선 4b까지 가지 말 것.**

데이터 연결(모델로 가도 로직 안 깨지게):
- `EnemyData`에 `GameObject modelPrefab` 필드 추가(기존 `enemySprite` 옆).
- `EnemyView.SetSprite` 등 **메서드 시그니처 유지**한 채 내부에서 모델 스폰/숨김 분기. `OnAttackMotionLastFrame`은 Animator 이벤트로 발화 → `EnemyController` 무수정.
- 흔들림/플래시는 3D용으로 재구현(world 오프셋 / 머티리얼 emission). 기존 픽셀 흔들림은 `usesModel` 플래그로 분기.
- 머리 위 UI(HP바 등)는 **UI에 그대로 두고** `UIFollow3DTarget`(§6)으로 적 3D 위치를 화면 투영해 따라가게.
- 카드/피격 VFX 재앵커: `CardEffectOverlay`의 고정 좌표(적 (0,300))를 적 3D 위치의 `WorldToScreenPoint` 투영으로 교체. 단일 헬퍼로 통일.

---

## 5. URP 함정 모음 (검증으로 확인된 지뢰)

1. **기존 카메라를 perspective로 바꾸지 마라.** SS-Camera 캔버스 3개(planeDistance 100)가 전부 왜곡됨. 3D는 **새 카메라**로.
2. **레거시 "Depth + Depth Only clear"는 URP에서 안 통함.** URP 멀티카메라 합성은 Camera Stack(Base+Overlay)뿐. 그리고 **한 스택에 ortho+perspective 혼합 금지.** → 그래서 우리는 RenderTexture로 회피.
3. **카메라 스택은 Forward/Forward+ 렌더러에서만** Overlay 카메라 지원. **Deferred면 불가.** `PC_Renderer.asset`/`Mobile_Renderer.asset`의 Rendering Path 확인.
4. **Post-processing 범위**: Screen Space - **Overlay** UI는 영향 없음(나중에 합성). 하지만 이 프로젝트의 **Screen Space - Camera 캔버스 3개는 Main Camera가 렌더**하므로, Main Camera에 Bloom/Vignette 켜면 **그 UI까지 효과 먹음.** → Post FX는 **WorldCamera에만**.
5. **`WorldToScreenPoint` 카메라 뒤 가드**: 대상이 카메라 뒤면 z<0 + x/y 반전된 쓰레기값 반환 → HP바가 화면 반대편으로 순간이동. **`if (sp.z <= 0) 숨김`** 필수. (UI-follows-3D 1순위 버그)
6. **`ScreenPointToLocalPointInRectangle`의 카메라 인자**: **Overlay 캔버스 = `null`**, **Screen Space - Camera 캔버스 = 그 캔버스의 렌더 카메라**. 반대로 넣으면 위치/스케일 어긋남(2순위 버그).
7. **URP는 Volume에 Fog 오버라이드가 없다.** 안개는 `Window ▸ Rendering ▸ Lighting ▸ Environment`의 RenderSettings fog 또는 커스텀 렌더 피처. Volume Profile에서 Fog 찾지 말 것.
8. **gltfast 빌드 셰이더 변종**: 에디터에선 멀쩡한데 **빌드에서 모델이 핑크**로 나오는 함정. URP 셰이더 변종을 `Always Included Shaders`나 ShaderVariantCollection에 넣어야 함. 에디터에서 prefab으로 임포트해 그대로 쓰면 회피 가능. Draco/KTX2 압축 glb는 `com.unity.cloud.draco`/`ktx` 패키지 없으면 로드 실패(현재 미설치).
9. **조명**: 현재 씬엔 3D 라이팅이 없음. Lit glb는 Directional Light 없으면 까맣게 나옴 → 광원 추가 or Unlit 머티리얼.
10. **빌보드 정렬**: `LookAt(카메라 위치)`는 화면 가장자리에서 비뚤어짐. **카메라 forward에 정렬**(Y축 잠금)이 안정적. URP/Unlit Transparent 머티리얼 사용.

---

## 6. 헬퍼 스크립트 (마일스톤 2에서 사용, 지금은 참고)

### BillboardToCamera.cs
```csharp
using UnityEngine;

/// 3D 공간의 Quad/스프라이트가 항상 카메라를 향하게(2D 아트 재활용).
[DisallowMultipleComponent]
public class BillboardToCamera : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;   // 3D 월드 카메라(WorldCamera)
    [SerializeField] private bool lockY = true;     // 위아래로 안 눕게 Y축 고정

    private void Awake() { if (targetCamera == null) targetCamera = Camera.main; }

    private void LateUpdate()   // 카메라 이동 후 실행되도록 Late
    {
        if (targetCamera == null) return;
        // LookAt(position)이 아니라 카메라 평면에 정렬 → 화면 가장자리에서도 안 비뚤어짐
        Vector3 fwd = targetCamera.transform.forward;
        if (lockY) fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-6f) return;
        transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
    }
}
```

### UIFollow3DTarget.cs
```csharp
using UnityEngine;

/// 3D 월드 오브젝트(적/플레이어)를 화면 투영해서, UI 요소(HP바 등)가 따라다니게.
/// ※ 적을 카메라가 "화면에 직접" 렌더할 때(4b/Camera Stack) 정확. RenderTexture 안에
///   찍히는 경우는 RT→RawImage 사각형 매핑이 따로 필요.
[DisallowMultipleComponent]
public class UIFollow3DTarget : MonoBehaviour
{
    [SerializeField] private RectTransform uiElement;   // 따라다닐 UI(없으면 자기 자신)
    [SerializeField] private Transform worldTarget;     // 3D 적/플레이어
    [SerializeField] private Vector3 worldOffset = Vector3.up * 2f; // 머리 위 오프셋
    [SerializeField] private Camera worldCamera;        // 3D를 렌더하는 카메라

    private RectTransform _canvasRect;
    private Camera _uiCam;   // Overlay면 null, Screen Space - Camera면 그 카메라

    private void Awake()
    {
        if (uiElement == null) uiElement = transform as RectTransform;
        var canvas = uiElement.GetComponentInParent<Canvas>();
        _canvasRect = canvas.transform as RectTransform;
        _uiCam = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (worldCamera == null) worldCamera = Camera.main;
    }

    private void LateUpdate()
    {
        if (worldTarget == null || worldCamera == null) return;

        Vector3 sp = worldCamera.WorldToScreenPoint(worldTarget.position + worldOffset);
        if (sp.z <= 0f) {                       // ★ 카메라 뒤 가드(필수)
            if (uiElement.gameObject.activeSelf) uiElement.gameObject.SetActive(false);
            return;
        }
        if (!uiElement.gameObject.activeSelf) uiElement.gameObject.SetActive(true);

        // ★ Overlay=null / SS-Camera=그 카메라 (반대로 넣으면 어긋남)
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, sp, _uiCam, out Vector2 local))
            uiElement.anchoredPosition = local;
    }
}
```

> 코드베이스에 이미 같은 관용구(`WorldToScreenPoint` → `ScreenPointToLocalPointInRectangle`)가 `RelicStageController.cs`(763–783)와 worktree `Projectile.cs`에 있으니 일관성 참고.

---

## 7. 작업 순서 체크리스트

- [ ] **마일스톤 1**: `World3D` 레이어 → 3D 무대 + 광원 → `RT_World3D` → `WorldCamera`(perspective, Culling=World3D, Target=RT) → Main Camera Culling에서 World3D 제거 → 전투 캔버스 첫 자식 `World3DView`(RawImage, RT) → 평면 `background` 투명화 → 전투에서만 토글. **검증: 플레이 → 카드/HUD 위, 뒤에 3D 무대.**
- [ ] **폴리시**: Volume(Bloom/Vignette/DoF) on WorldCamera only + 카메라 셰이크.
- [ ] (선택) **2-6**: CombatStageController에 3D 씬 프리셋 스왑.
- [ ] (선택, 나중) **마일스톤 2**: 적 빌보드/모델 + `UIFollow3DTarget` + VFX 재앵커.
- [ ] **빌드 전**: gltfast 셰이더 변종 포함 확인(핑크 방지), 렌더러 Forward 확인.
