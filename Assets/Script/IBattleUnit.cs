using System;
using UnityEngine;

// 전투에 참여하는 유닛(플레이어/적)이 구현하는 공통 인터페이스
public interface IBattleUnit
{
    // 상태이상 변경 이벤트 (타입, 수치)
    event Action<string, int> OnStatusChanged;
    // 피해를 입힌다 (피해량, 피해 타입)
    void TakeDamage(float damage, string damageType = "normal");
    // 상태이상을 누적 추가한다
    void AddStatus(string type, int amount);
    // 현재 상태이상 수치를 반환한다
    int GetStatus(string type);
    // 상태이상 수치를 설정한다
    void SetStatus(string type, int amount);
    // 방어막을 추가한다
    void AddGuard(float amount);
    // 공격 피해량을 반환한다
    float GetAttackDamage();
}

