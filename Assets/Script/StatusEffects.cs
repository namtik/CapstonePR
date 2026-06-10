using UnityEngine;

// 모든 상태이상 효과의 공통 기반 클래스
public abstract class StatusEffectBase
{
    // 효과를 실행한다(caster=시전자, target=대상, amount=수치)
    public abstract void Execute(IBattleUnit caster, IBattleUnit target, int amount);
}



// 대상에게 화상을 부여하는 효과
public class BurnEffect : StatusEffectBase
{
    // 대상에게 화상 수치를 부여한다
    public override void Execute(IBattleUnit caster, IBattleUnit target, int amount)
    {
        target.AddStatus("burn", amount);
        Debug.Log($"[효과 발동] 대상에게 화상 {amount} 부여!");
    }
}

// 대상에게 젖음을 부여하는 효과
public class WetEffect : StatusEffectBase
{
    // 대상에게 젖음 수치를 부여한다
    public override void Execute(IBattleUnit caster, IBattleUnit target, int amount)
    {
        target.AddStatus("wet", amount);
        Debug.Log($"[효과 발동] 대상에게 젖음 {amount} 부여!");
    }
}

// 젖음을 소모해 빙결로 전환하는 효과
public class FreezeEffect : StatusEffectBase
{
    // 젖음 5당 빙결 1로 변환하고 나머지를 남긴다
    public override void Execute(IBattleUnit caster, IBattleUnit target, int amount)
    {
        int wetAmount = target.GetStatus("wet");
        if (wetAmount >= 5)
        {
            int remainder = wetAmount % 5;
            int freezeGain = wetAmount / 5;

            target.SetStatus("wet", remainder);
            target.AddStatus("freeze", freezeGain);
            Debug.Log($"[효과 발동] 대상의 젖음이 빙결 {freezeGain}로 전환됨! (남은 젖음: {remainder})");
        }
    }
}

// 화상 스택을 터뜨려 추가 피해를 주는 효과
public class DetonateEffect : StatusEffectBase
{
    // 화상 스택을 소모하고 스택당 2의 추가 피해를 준다
    public override void Execute(IBattleUnit caster, IBattleUnit target, int amount)
    {
        int burnCount = target.GetStatus("burn");
        if (burnCount > 0)
        {
            target.SetStatus("burn", 0);
            int extraDamage = burnCount * 2;
            target.TakeDamage(extraDamage, "detonate");
            Debug.Log($"[효과 발동] 화상 폭발! 대상에게 추가 데미지 {extraDamage}!");
        }
    }
}


// 시전자에게 사출기 스택을 부여하는 효과
public class LauncherEffect : StatusEffectBase
{
    // 시전자에게 사출기 수치를 부여한다
    public override void Execute(IBattleUnit caster, IBattleUnit target, int amount)
    {
        caster.AddStatus("launcher", amount);
        Debug.Log($"[효과 발동] 시전자가 사출기 {amount} 획득!");
    }
}

// 시전자에게 흙 스택을 부여하는 효과
public class FortifyEffect : StatusEffectBase
{
    // 시전자에게 흙 수치를 부여한다
    public override void Execute(IBattleUnit caster, IBattleUnit target, int amount)
    {
        caster.AddStatus("fortify", amount);
        Debug.Log($"[효과 발동] 시전자가 흙 {amount} 획득!");
    }
}

// 사출기 스택을 소모해 추가 피해를 주는 효과
public class ChargeEffect : StatusEffectBase
{
    // 사출기 스택을 소모하고 스택당 공격력 1/10의 추가 피해를 준다
    public override void Execute(IBattleUnit caster, IBattleUnit target, int amount)
    {
        int launcherCount = caster.GetStatus("launcher");
        if (launcherCount > 0)
        {
            caster.SetStatus("launcher", 0);
            float extraDamage = launcherCount * (caster.GetAttackDamage() / 10f);
            target.TakeDamage(extraDamage, "Charge");
            Debug.Log($"[효과 발동] 화상 폭발! 대상에게 추가 데미지 {extraDamage}!");
        }
    }
}

// 흙 스택을 소모해 방어도를 얻는 효과
public class SpendEffect : StatusEffectBase
{
    // 흙 스택을 소모하고 스택당 방어도 2를 얻는다
    public override void Execute(IBattleUnit caster, IBattleUnit target, int amount)
    {
        int earthCount = caster.GetStatus("fortify");
        if (earthCount > 0)
        {
            caster.SetStatus("fortify", 0);
            int guardGain = earthCount * 2;
            caster.AddGuard(guardGain);
            Debug.Log($"[효과 발동] 흙 {earthCount}개를 소비하여 시전자가 방어도 {guardGain} 획득!");
        }
    }
}