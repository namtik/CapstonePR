using UnityEngine;

public abstract class StatusEffectBase
{
    // caster = 시전자(플레이어), target = 맞는 애(적)
    public abstract void Execute(IBattleUnit caster, IBattleUnit target, int amount);
}


// 적(Target)에게 쌓이는 디버프 효과들

public class BurnEffect : StatusEffectBase
{
    public override void Execute(IBattleUnit caster, IBattleUnit target, int amount)
    {
        target.AddStatus("burn", amount);
        Debug.Log($"[효과 발동] 대상에게 화상 {amount} 부여!");
    }
}

public class WetEffect : StatusEffectBase
{
    public override void Execute(IBattleUnit caster, IBattleUnit target, int amount)
    {
        target.AddStatus("wet", amount);
        Debug.Log($"[효과 발동] 대상에게 젖음 {amount} 부여!");
    }
}

public class FreezeEffect : StatusEffectBase
{
    public override void Execute(IBattleUnit caster, IBattleUnit target, int amount)
    {
        int wetAmount = target.GetStatus("wet");
        if (wetAmount > 0)
        {
            target.SetStatus("wet", 0);
            target.AddStatus("freeze", wetAmount);
            Debug.Log($"[효과 발동] 대상의 젖음({wetAmount})이 빙결로 전환됨!");
        }
    }
}

public class DetonateEffect : StatusEffectBase
{
    public override void Execute(IBattleUnit caster, IBattleUnit target, int amount)
    {
        int burnCount = target.GetStatus("burn");
        if (burnCount > 0)
        {
            target.SetStatus("burn", 0);
            int extraDamage = burnCount * 2; // 화상 중첩당 추가 데미지
            target.TakeDamage(extraDamage, "detonate");
            Debug.Log($"[효과 발동] 화상 폭발! 대상에게 추가 데미지 {extraDamage}!");
        }
    }
}


// 플레이어(Caster)에게 쌓이는 버프/자원 효과들
public class LauncherEffect : StatusEffectBase
{
    public override void Execute(IBattleUnit caster, IBattleUnit target, int amount)
    {
        caster.AddStatus("launcher", amount);
        Debug.Log($"[효과 발동] 시전자가 사출기 {amount} 획득!");
    }
}

public class FortifyEffect : StatusEffectBase
{
    public override void Execute(IBattleUnit caster, IBattleUnit target, int amount)
    {
        caster.AddStatus("fortify", amount);
        Debug.Log($"[효과 발동] 시전자가 흙 {amount} 획득!");
    }
}

public class ChargeEffect : StatusEffectBase
{
    Player player;
    public override void Execute(IBattleUnit caster, IBattleUnit target, int amount)
    {
        int launcherCount = caster.GetStatus("launcher");
        if (launcherCount > 0)
        {
            caster.SetStatus("launcher", 0);
            float extraDamage = launcherCount * (player.attackDamage/10); // 사출기 당 추가 데미지
            target.TakeDamage(extraDamage, "Charge");
            Debug.Log($"[효과 발동] 화상 폭발! 대상에게 추가 데미지 {extraDamage}!");
        }
    }
}

public class SpendEffect : StatusEffectBase
{
    public override void Execute(IBattleUnit caster, IBattleUnit target, int amount)
    {
        int earthCount = caster.GetStatus("fortify");
        if (earthCount > 0)
        {
            caster.SetStatus("fortify", 0);
            int guardGain = earthCount * 2; // 흙 1당 방어도 2
            caster.AddGuard(guardGain);
            Debug.Log($"[효과 발동] 흙 {earthCount}개를 소비하여 시전자가 방어도 {guardGain} 획득!");
        }
    }
}