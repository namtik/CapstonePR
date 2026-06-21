namespace Battle.Relic
{
    // 유물 획득 시(OnAcquired) 호출할 런 레벨 동작 묶음.
    // 4단계(비전투형 유물)에서 구현부를 연결한다 — 현재는 계약만 정의.
    public interface IRelicRunContext
    {
        // 최대 체력 영구 증가 (10000)
        void AddMaxHp(int amount);
        // 현재 체력을 최대치까지 전체 회복 (10012)
        void HealPlayerFull();
        // 현재 보유 덱의 모든 카드를 랜덤한 카드로 변환 (망각의 붓 10017)
        void RandomizeAllOwnedCards();
        // 기본 카드의 효과 수치를 2배로 (10004 — 5단계)
        void EnableBasicCardEffectDouble();
    }
}
