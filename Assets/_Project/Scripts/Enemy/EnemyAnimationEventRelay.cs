using UnityEngine;

// Animation Event는 Animator가 붙은 오브젝트의 컴포넌트만 호출한다.
// 3D 몬스터는 Animator가 자식 모델에, EnemyView3D가 루트에 있으므로 이 중계기가 이벤트를 부모 뷰로 넘긴다.
// EnemyView3D.Awake에서 자동으로 붙으므로 직접 붙일 필요 없음(에디터에서 미리 붙여둬도 무방).
public class EnemyAnimationEventRelay : MonoBehaviour
{
    private EnemyView3D _view; // 전달 대상 뷰

    public void Bind(EnemyView3D view) { _view = view; }

    void Awake()
    {
        if (_view == null) _view = GetComponentInParent<EnemyView3D>();
    }

    // 공격 클립의 Animation Event 함수 이름: OnAttackImpact
    public void OnAttackImpact()
    {
        if (_view != null) _view.OnAttackImpact();
    }

    // Animation Event 함수 이름: PauseMotion (Float 칸에 정지 시간(초) 입력)
    public void PauseMotion(float seconds)
    {
        if (_view != null) _view.PauseMotion(seconds);
    }
}
