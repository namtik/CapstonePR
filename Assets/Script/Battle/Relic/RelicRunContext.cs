using UnityEngine;

namespace Battle.Relic
{
    // IRelicRunContext 기본 구현 — 유물 획득 시(OnAcquired) 런 레벨 효과를 실제로 적용.
    // RelicManager가 AddRelic 시점에 생성해 전달한다.
    public class RelicRunContext : IRelicRunContext
    {
        // 최대 체력 영구 증가 (현재 체력도 같이 증가 — SampleScene 단일 씬에서 플레이어 지속)
        public void AddMaxHp(int amount)
        {
            if (amount == 0) return;
            var player = Player.Resolve(true);
            if (player == null) return;
            int oldMax = player.maxHp;
            player.maxHp += amount;
            // currentHp가 아직 0(미초기화)이거나 만피였으면 최대치까지 채운다 — 0+amount만 더하면 10/110 버그
            if (player.currentHp <= 0 || player.currentHp >= oldMax)
                player.currentHp = player.maxHp;
            else
                player.currentHp += amount;
            Player.SyncAllInstancesFrom(player);
            Debug.Log($"[유물] 최대 체력 +{amount} (현재 {player.currentHp}/{player.maxHp})");
        }

        // 현재 체력을 최대치까지 전체 회복
        public void HealPlayerFull()
        {
            var player = Player.Resolve(true);
            if (player == null) return;
            player.Heal(player.maxHp);
            Debug.Log("[유물] 전체 체력 회복");
        }

        // 보유 덱의 모든 카드를 랜덤한 카드로 변환
        public void RandomizeAllOwnedCards()
        {
            if (Battle.RunDeckState.Instance == null) return;
            Battle.RunDeckState.Instance.RandomizeAllCards();
        }

        // 기본 카드 효과 2배 (런 지속 — 다음 전투부터 적용)
        public void EnableBasicCardEffectDouble()
        {
            if (Battle.RunDeckState.Instance == null) return;
            Battle.RunDeckState.Instance.EnableDoubleBasicCardEffects();
        }
    }
}
