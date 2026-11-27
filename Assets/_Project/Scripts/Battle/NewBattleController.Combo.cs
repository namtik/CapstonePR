using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using Battle.Card;
using Battle.Deck;
using Battle.Relic;
using Battle.UI;
using Sashindo.VFX;

namespace Battle
{
    public partial class NewBattleController
    {
        // [테스트] 인스펙터 보유 콤보(ownedComboIds)를 즉시 적용(각성 중에는 보류)
        [ContextMenu("[테스트] 보유 콤보 갱신 (인스펙터 리스트 적용)")]
        public void RefreshOwnedCombosRuntime()
        {
            if (!useComboDatabase)
            {
                Debug.LogWarning("[NewBattle] useComboDatabase가 OFF — 콤보 DB로 갱신하려면 ON으로 두세요.");
                return;
            }
            if (_awakenActive)
            {
                Debug.LogWarning("[NewBattle] 각성 중에는 콤보 갱신 보류 — 다음 각성부터 반영됩니다.");
                return;
            }
            ownedComboSkills = BuildOwnedComboDefs();
            _comboCooldown = new int[ownedComboSkills.Count];
            PushComboCooldownUi();
            Debug.Log($"[NewBattle] 보유 콤보 갱신 — {ownedComboSkills.Count}개 " +
                      (ownedComboIds != null && ownedComboIds.Count > 0
                          ? $"(ids: {string.Join(",", ownedComboIds)})" : "(전체)"));
        }

        // 보유 콤보 정의 구성 — 지정 ID(효과+커맨드)가 있으면 그걸로, 없으면 보유 콤보 없음
        // (콤보 보상 3택1에서 아무것도 선택하지 않으면 콤보 없이 시작)
        List<ComboSkillDef> BuildOwnedComboDefs()
        {
            List<ComboSkillDef> result = (ownedComboIds != null && ownedComboIds.Count > 0)
                ? ComboSkillDatabase.BuildCombosByIds(ownedComboIds)
                : new List<ComboSkillDef>();
            ApplyComboSlotOverrides(result);
            return result;
        }

        // 깨달음으로 저장한 슬롯 순서를 런타임 정의에 반영한다
        void ApplyComboSlotOverrides(List<ComboSkillDef> defs)
        {
            if (defs == null || _comboSlotOverrides.Count == 0) return;

            for (int i = 0; i < defs.Count; i++)
            {
                ComboSkillDef skill = defs[i];
                if (skill == null) continue;

                for (int j = 0; j < _comboSlotOverrides.Count; j++)
                {
                    if (_comboSlotOverrides[j].refComboId != skill.refComboId) continue;
                    skill.slot1 = _comboSlotOverrides[j].slot1;
                    skill.slot2 = _comboSlotOverrides[j].slot2;
                    skill.slot3 = _comboSlotOverrides[j].slot3;
                    break;
                }
            }
        }

        // 깨달음: 보유 콤보의 속성 순서를 런 동안 유지한다
        public void SetOwnedComboSlots(int refComboId, CardElement slot1, CardElement slot2, CardElement slot3)
        {
            UpsertComboSlotOverride(refComboId, slot1, slot2, slot3);

            if (ownedComboSkills != null)
            {
                for (int i = 0; i < ownedComboSkills.Count; i++)
                {
                    ComboSkillDef skill = ownedComboSkills[i];
                    if (skill == null || skill.refComboId != refComboId) continue;
                    skill.slot1 = slot1;
                    skill.slot2 = slot2;
                    skill.slot3 = slot3;
                }
            }

            if (handHud != null)
                PushComboCooldownUi();

            Debug.Log($"[NewBattle] 콤보 {refComboId} 슬롯 변경: {ComboSkillDef.ElementsLabel(slot1, slot2, slot3)}");
        }

        void UpsertComboSlotOverride(int refComboId, CardElement slot1, CardElement slot2, CardElement slot3)
        {
            var entry = new ComboSlotOverride
            {
                refComboId = refComboId,
                slot1 = slot1,
                slot2 = slot2,
                slot3 = slot3,
            };

            for (int i = 0; i < _comboSlotOverrides.Count; i++)
            {
                if (_comboSlotOverrides[i].refComboId != refComboId) continue;
                _comboSlotOverrides[i] = entry;
                return;
            }

            _comboSlotOverrides.Add(entry);
        }

        // 콤보 1개(효과 refComboId의 대표 커맨드) 추가 후 즉시 적용
        public void AddOwnedCombo(int refComboId)
        {
            if (ownedComboIds == null) ownedComboIds = new List<int>();
            int commandId = ComboSkillDatabase.GetRepresentativeCommandId(refComboId);
            if (ownedComboIds.Contains(commandId))
            {
                Debug.Log($"[NewBattle] 콤보 {refComboId} 이미 보유 중.");
                return;
            }
            ownedComboIds.Add(commandId);
            Battle.UI.CodexProgress.MarkCombo(refComboId);
            useComboDatabase = true;
            RefreshOwnedCombosRuntime();
            Debug.Log($"[NewBattle] 콤보 추가: 효과 {refComboId} → 커맨드 {commandId}");
        }

        // 런 재시작 시 보유 콤보를 비운다(맵 진입 3택1용)
        public void ResetOwnedCombosForNewRun()
        {
            if (ownedComboIds == null)
                ownedComboIds = new List<int>();
            else
                ownedComboIds.Clear();

            ownedComboSkills?.Clear();
            _comboSlotOverrides.Clear();
            _comboCooldown = System.Array.Empty<int>();
            if (handHud != null)
                PushComboCooldownUi();

            Debug.Log("[NewBattle] 런 재시작 — 보유 콤보 초기화");
        }

        // [테스트] debugAddComboRefId 콤보 1개 추가
        [ContextMenu("[테스트] 콤보 1개 추가 (debugAddComboRefId)")]
        void DebugAddOneCombo() => AddOwnedCombo(debugAddComboRefId);

        // 콤보 슬롯에 속성 추가(윈도우 3장 유지) + 히스토리 누적
        void AddToComboSlot(CardElement element)
        {
            _comboInput.Add(element);
            if (_comboInput.Count > 3) _comboInput.RemoveAt(0);
            _awakenInputHistory.Add(element);
        }

        // 각성 중 카드를 1장 쓸 때마다 콤보 재사용 대기를 1씩 줄인다
        void TickComboCooldownByCardUse()
        {
            for (int i = 0; i < _comboCooldown.Length; i++)
                if (_comboCooldown[i] > 0) _comboCooldown[i]--;
        }

        // 남은 카드 사용 횟수로 콤보 스킬 목록을 갱신한다
        void PushComboCooldownUi()
        {
            if (handHud == null) return;
            handHud.UpdateComboSkillList(ownedComboSkills, (int[])_comboCooldown.Clone());
        }

        // 콤보 매칭 — 매칭 시 큐에 적재·쿨다운 부여, 매칭 여부 반환(시간 보너스용)
        bool TryActivateCombo()
        {
            if (_comboInput.Count < 3) return false;
            if (ownedComboSkills == null) return false;

            for (int i = 0; i < ownedComboSkills.Count; i++)
            {
                if (i < _comboCooldown.Length && _comboCooldown[i] > 0) continue;
                var skill = ownedComboSkills[i];
                if (skill == null) continue;
                if (skill.Matches(_comboInput))
                {
                    _queuedComboSkills.Add(skill);
                    Battle.UI.BattleLog.NoteAwakenCombo(skill);
                    if (i < _comboCooldown.Length)
                    {
                        _comboCooldown[i] = COMBO_REUSE_CARD_USES;
                        PushComboCooldownUi();              // 쿨타임 표시를 바로 반영
                        handHud?.PulseComboSkillItem(i);    // 쿨타임이 걸린 항목을 한 번 호버
                    }
                    Log($"[콤보 큐] {skill.displayName} ({skill.ComboString()}) — 각성 종료 시 발동 (대기 {_queuedComboSkills.Count}건, 재사용까지 카드 {COMBO_REUSE_CARD_USES}장)");
                    return true;
                }
            }
            return false;
        }

        // 콤보 스킬 1건 발동 — DB 콤보는 다중 효과, 레거시는 단일 효과
        void ActivateComboSkill(ComboSkillDef skill)
        {
            if (skill.fromDatabase)
            {
                Log($"[콤보 스킬] {skill.displayName} ({skill.ComboString()}, ref={skill.refComboId}) 발동 — 효과 {(skill.dbEffects != null ? skill.dbEffects.Count : 0)}건");
                FillComboContext(skill);
                _comboResolver.Resolve(skill, _comboCtx);
                return;
            }

            Log($"[콤보 스킬] {skill.displayName} ({skill.ComboString()}) 발동 — {skill.effect} {skill.amount}");

            switch (skill.effect)
            {
                case ComboEffectType.Damage:
                    if (_enemy != null) _enemy.TakeDamage(skill.amount);
                    // 데미지 이펙트는 EndAwaken이 분산+셰이크와 함께 처리하므로 여기선 띄우지 않음
                    break;
                case ComboEffectType.Burn:
                    if (_enemy != null) _enemy.AddStatus("burn", skill.amount);
                    break;
                case ComboEffectType.Guard:
                    if (_player != null) _player.AddGuard(skill.amount);
                    break;
                case ComboEffectType.Heal:
                    if (_player != null) _player.Heal(skill.amount);
                    break;
                case ComboEffectType.Draw:
                    _deck.Draw(skill.amount);
                    break;
            }
        }

        // DB 콤보 효과 실행 직전 런타임 참조/카운터를 채움
        void FillComboContext(ComboSkillDef skill)
        {
            _comboCtx.enemy = _enemy;
            _comboCtx.enemyStat = _enemyStat;
            _comboCtx.player = _player;
            _comboCtx.deck = _deck;

            _comboCtx.usedFireCardCount = _usedFireCardCount;
            _comboCtx.usedFragmentCardCount = _ctx.usedFragmentCardCount;
            _comboCtx.playerChain = _ctx.chainCount;
            _comboCtx.awakenComboCount = _awakenComboCountThisEnd;
            _comboCtx.awakenFragmentExhausted = _awakenFragmentExhausted;
            _comboSelfUseCount.TryGetValue(skill.refComboId, out int selfUse);
            _comboCtx.selfRepeatCount = selfUse;

            // GAIN_STATUS CHAIN → 연쇄 누적 (SyncChainStatus가 UI 반영)
            _comboCtx.onGainChain = n => { _ctx.chainCount += n; };
        }
    }
}
