using System;

namespace Battle.Relic
{
    // 유물이 전투 중 호출할 수 있는 동작 묶음. NewBattleController가 구현한다.
    // 유물은 전투 내부 구현을 모른 채 이 인터페이스를 통해서만 전투에 영향을 준다.
    public interface IRelicBattleContext
    {
        // 적에게 빙결 부여 (서리화 10008)
        void ApplyFrostToEnemy(int amount);
        // 플레이어 방어도 획득 (10002)
        void AddPlayerGuard(int amount);
        // 뽑을 더미에 무작위 파편 카드를 섞어 넣음 (10007)
        void ShuffleFragmentsIntoDrawPile(int count);
        // 카드 드로우 (10003, 10006)
        void DrawCards(int count);
        // 각성 남은시간/최대시간을 함께 증가 (비급서 10014)
        void AddAwakenTimeSeconds(float seconds);
        // 각성 종료 후 성공 콤보 수만큼 각성 게이지 회복 (이무기의 여의주)
        void RecoverAwakenGauge(int successCombos);
        // 연쇄 스택 증가 (신선도복 10016)
        void AddChain(int amount);
        // 무작위 적에게 즉시 피해 (피 묻은 가시 10019)
        void DealDamageToRandomEnemy(int amount);
        // 즉시 각성 상태로 진입 (황룡옥적 10010)
        void ForceActivateAwaken();
        // 이번 전투 첫 사용 카드의 효과를 한 번 더 발동하도록 예약 (복사경 10015)
        void EnableFirstCardRecast();
        // 좌상단 유물 아이콘 펄스 연출 후 onShown 호출 (HUD 없으면 즉시 onShown)
        void PulseRelic(RelicSO relic, string label, Action onShown);
    }
}
