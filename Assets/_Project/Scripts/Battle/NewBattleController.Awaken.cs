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
        // 각성 지속 시간 카운트다운 및 종료 처리
        void TickAwakenTimer()
        {
            if (!_awakenActive) return;
            if (holdAwakenForever)
            {
                if (_awakenMaxTime > 0f)
                    _awakenTimeRemaining = _awakenMaxTime;
                return;
            }
            // 진입 연출이 화면을 다시 열 때까지 각성 시간을 보존한다.
            if (awakenEntryVfx != null && awakenEntryVfx.IsPlaying) return;
            // 손패가 부적으로 바뀌는 동안은 각성 시간이 흐르지 않음(여유 시간)
            if (pauseAwakenDuringTalismanBurn && handHud != null && handHud.IsTalismanTransitioning) return;
            _awakenTimeRemaining -= Time.deltaTime;
            if (_awakenTimeRemaining <= 0f)
            {
                _awakenTimeRemaining = 0f;
                EndAwaken();
                return;
            }
        }

        // 카드 사용 후 보류된 각성을 선택 모드 진입 대기 후 발동 시도
        IEnumerator DeferredPendingAwakenCheck()
        {
            yield return null;
            yield return null;
            TryActivatePendingAwaken();
        }

        // 각성 발동용 속성 입력 누적 — 임계 도달 시 발동 보류
        void AccumulateAwakenInput()
        {
            if (_awakenActive) return;

            _elementInputCount++;
            if (_elementInputCount >= EffectiveAwakenInput)
                _awakenPending = true;
        }

        // 보류된 각성을 실제로 발동(선택 진행 중이면 대기)
        void TryActivatePendingAwaken()
        {
            if (!_awakenPending || _awakenActive || !_inBattle) return;
            // 마지막 카드로 적이 모두 죽었으면 전투가 끝나므로 각성을 발동하지 않는다.
            if (EnemyCombatParty.AliveCount == 0)
            {
                _awakenPending = false;
                return;
            }
            if (HasPendingSelection) return; // 선택(버리기 연쇄 포함)이 모두 끝난 뒤 대기열이 다시 호출
            // 사용 중인 카드가 아직 더미 밖(중앙 연출 중) — 스냅샷에서 빠지지 않도록 배치 후 발동
            if (_cardPresenting)
            {
                if (isActiveAndEnabled) StartCoroutine(DeferredPendingAwakenCheck());
                return;
            }

            _awakenPending = false;
            ActivateAwaken();
        }

        // 각성 발동 — 지속시간 계산·스냅샷·무속성 격리·콤보 UI 활성화
        void ActivateAwaken()
        {
            bool awakenStarting = !_awakenActive;
            _awakenActive = true;
            if (awakenStarting) BattleLog.RecordAwaken(null);
            // 진입 때 패를 다시 채우며 생기는 카드·드로우 기록은 남기지 않는다.
            BattleLog.BeginSuppress();
            try
            {
                RunDeckState.Instance?.RecordAwakenActivation();
                _elementInputCount = 0;
                // 각성 시간은 기본 7초 고정(보유 콤보 수에 따른 추가 시간 없음)
                float awakenDuration = AWAKEN_DURATION_SECONDS;
                // 유물(각성 지속시간 증가류)이 있으면 합산
                if (RelicManager.Instance != null) awakenDuration += RelicManager.Instance.GetAwakenDurationBonus();
                _awakenTimeRemaining = awakenDuration;
                _awakenMaxTime = awakenDuration;

                // 각성 직전 패/뽑을 더미/버린 더미 스냅샷 — 각성 종료 시 그대로 복원
                _awakenSnapshotHand = new List<CardInstance>(_deck.Hand);
                _awakenSnapshotDraw = new List<CardInstance>(_deck.DrawPile);
                _awakenSnapshotDiscard = new List<CardInstance>(_deck.DiscardPile);

                // 무속성 카드 임시 격리 (각성 중 속성 카드만 순환)
                _awakenStoredNeutralCards = _deck.ExtractAllNeutralCards();

                // 격리된 카드 중 파편 수 기록
                _awakenFragmentExhausted = 0;
                foreach (var c in _awakenStoredNeutralCards)
                    if (c != null && c.Element == CardElement.Fragment) _awakenFragmentExhausted++;
                _comboSelfUseCount.Clear();

                // 각성 중엔 둔화·탈진 같은 무속성 카드를 뽑지 않는다(격리 후 새로 들어온 것도 포함)
                _deck.BlockNonComboDraws = true;
                _deck.MergeDiscardIntoDrawShuffled(); // 각성 진입 시 버린 더미까지 합쳐 섞는다(종료 시 스냅샷으로 원복)
                _deck.Draw(_deck.HandLimit);

                _comboInput.Clear();
                _comboCooldown = new int[ownedComboSkills != null ? ownedComboSkills.Count : 0];
                _queuedComboSkills.Clear();
                _awakenInputHistory.Clear();
                _awakenMarkerPositions.Clear();
                RemoveAwakenComboParticles(_awakenMarkerHandles);

                if (handHud != null)
                {
                    handHud.SetAwakenMode(true);
                    handHud.UpdateComboSlot(_comboInput);
                    PushComboCooldownUi();
                    handHud.UpdateAwakenInputHistory(_awakenInputHistory);
                }

                if (awakenTimerGauge != null) awakenTimerGauge.Hide();

                if (awakenStarting) PlayAwakenEntryVfx();
                _awakenScreenEffectInstance = SpawnAwakenScreenEffect();
                SfxManager.Instance?.PlayAwakenStart();

                Log($"[각성] 발동! 격리된 무속성={_awakenStoredNeutralCards.Count}, 지속 시간={awakenDuration:F1}s (기본 {AWAKEN_DURATION_SECONDS:F0}s, 콤보 매칭 시 +{COMBO_BONUS_SECONDS}s, 종료 시 일괄 발동)");
            }
            finally
            {
                BattleLog.EndSuppress();
            }
        }

        void PlayAwakenEntryVfx()
        {
            if (awakenEntryVfx == null)
            {
                foreach (var root in gameObject.scene.GetRootGameObjects())
                {
                    if (root.name == "Sashindo_AwakeningVFX" && root.TryGetComponent(out awakenEntryVfx))
                        break;
                }
            }

            if (awakenEntryVfx == null)
            {
                var preset = Resources.Load<AwakeningBattlePreset>("SashindoAwakening/BattlePreset");
                if (preset == null || preset.EntryPrefab == null) return;
                awakenEntryVfx = Instantiate(preset.EntryPrefab);
                ownsAwakenEntryVfx = true;
                if (awakenEntryVfx.gameObject.scene != gameObject.scene)
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(awakenEntryVfx.gameObject, gameObject.scene);
            }

            awakenEntryVfx.Play();
        }

        // 각성 화면 이펙트를 3D 스테이지 카메라 앞에 붙여 시야만 덮는다(UI 위가 아님).
        GameObject SpawnAwakenScreenEffect()
        {
            var prefab = awakenScreenEffectPrefab;
            if (prefab == null || prefab.name == "Screen wind")
            {
                var packagedFlicker = Resources.Load<GameObject>("VFX/AwakeningDurationFlicker");
                if (packagedFlicker != null) prefab = packagedFlicker;
            }

            if (prefab == null) return null;
            if (prefab.TryGetComponent<AwakeningDurationFlicker>(out _))
                return Instantiate(prefab);

            Camera cam = BattleStageController.Current != null
                ? BattleStageController.Current.StageCamera
                : Camera.main;
            if (cam == null) cam = Camera.main;
            if (cam == null) return null;

            GameObject go = Instantiate(prefab);
            go.name = "AwakenScreenWind";

            // StageCamera는 Stage3D/Enemy3D만 그리므로, 그 마스크에 맞춰 레이어를 올린다.
            int stage3d = LayerMask.NameToLayer("Stage3D");
            if (stage3d >= 0 && (cam.cullingMask & (1 << stage3d)) != 0)
                SetLayerRecursive(go.transform, stage3d);

            var hs = go.GetComponent<Hovl.HS_ScreenEffect>();
            if (hs == null) hs = go.GetComponentInChildren<Hovl.HS_ScreenEffect>(true);
            if (hs != null)
            {
                hs.sourceCamera = cam;
                hs.snapOnStart = true;
                hs.parentToCameraOnStart = true;
                // 기본 0.05는 StageCamera 근클립(0.3)보다 가까워 잘린다.
                hs.fallbackDistance = Mathf.Max(cam.nearClipPlane + 0.05f, 0.4f);
            }

            var systems = go.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] == null) continue;
                var main = systems[i].main;
                main.loop = true;
                var renderer = systems[i].GetComponent<ParticleSystemRenderer>();
                if (renderer != null) renderer.maxParticleSize = 2f;
            }

            return go;
        }

        // 각성 화면 이펙트를 정지하고 제거한다
        void StopAwakenScreenEffect()
        {
            if (awakenEntryVfx != null && awakenEntryVfx.IsPlaying)
                awakenEntryVfx.Stop();

            if (_awakenScreenEffectInstance == null) return;

            var systems = _awakenScreenEffectInstance.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] == null) continue;
                systems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            Destroy(_awakenScreenEffectInstance);
            _awakenScreenEffectInstance = null;
        }

        static void SetLayerRecursive(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++)
                SetLayerRecursive(t.GetChild(i), layer);
        }

        // 각성 종료 — 더미 복원·큐 콤보 일괄 발동·이펙트/UI 정리
        void EndAwaken()
        {
            _awakenActive = false;
            if (_deck != null) _deck.BlockNonComboDraws = false; // 각성 종료 — 일반 드로우로 복귀
            _awakenTimeRemaining = 0f;
            _awakenMaxTime = 0f;
            if (awakenTimerGauge != null) awakenTimerGauge.Hide();

            StopAwakenScreenEffect();

            // 스냅샷 이후 더미에 들어온 정규 카드(스냅샷 당시 사용 연출/선택 대기 중이었거나 소멸 더미에서 되돌아온 카드)는
            // 복원으로 덮어쓰면 패·묘지 어디에도 없게 되므로 버린 더미에 보존한다. 임시(transient) 카드는 기존대로 버린다.
            if (_awakenSnapshotHand != null && _awakenSnapshotDraw != null && _awakenSnapshotDiscard != null)
            {
                var known = new HashSet<CardInstance>(_awakenSnapshotHand);
                known.UnionWith(_awakenSnapshotDraw);
                known.UnionWith(_awakenSnapshotDiscard);
                var strays = new List<CardInstance>();
                void CollectStrays(IReadOnlyList<CardInstance> pile)
                {
                    for (int i = 0; i < pile.Count; i++)
                    {
                        CardInstance c = pile[i];
                        if (c == null || c.transient || known.Contains(c) || strays.Contains(c)) continue;
                        strays.Add(c);
                    }
                }
                CollectStrays(_deck.Hand);
                CollectStrays(_deck.DrawPile);
                CollectStrays(_deck.DiscardPile);
                if (strays.Count > 0)
                {
                    _awakenSnapshotDiscard.AddRange(strays);
                    Log($"[각성] 종료 — 스냅샷 밖 카드 {strays.Count}장을 버린 더미로 보존");
                }
            }

            // 각성 직전 상태로 패/뽑을 더미/버린 더미 복원 (격리 카드 포함)
            _deck.RestorePiles(_awakenSnapshotHand, _awakenSnapshotDraw, _awakenSnapshotDiscard);
            _awakenSnapshotHand = null;
            _awakenSnapshotDraw = null;
            _awakenSnapshotDiscard = null;
            _awakenStoredNeutralCards.Clear();

            if (_queuedComboSkills.Count > 0)
            {
                // Damage 콤보 수를 먼저 세어 이펙트 분산/로그에 사용
                int damageCount = 0;
                for (int i = 0; i < _queuedComboSkills.Count; i++)
                    if (_queuedComboSkills[i] != null && _queuedComboSkills[i].effect == ComboEffectType.Damage)
                        damageCount++;

                // DB 콤보 반복 공식용: 이번 각성 발동 콤보 총수 + refComboId별 발동 횟수
                _awakenComboCountThisEnd = _queuedComboSkills.Count;
                _comboSelfUseCount.Clear();
                foreach (var s in _queuedComboSkills)
                {
                    if (s == null || !s.fromDatabase) continue;
                    _comboSelfUseCount.TryGetValue(s.refComboId, out int c);
                    _comboSelfUseCount[s.refComboId] = c + 1;
                }

                if (awakenMarkerSequence)
                {
                    // 표식을 하나씩 터뜨리며 콤보를 순차 발동(큐/표식을 로컬로 넘겨 멤버는 즉시 비움)
                    Log($"[각성] 종료 — 표식 {_queuedComboSkills.Count}개 순차 폭발 (Damage {damageCount}건)");
                    var skills = new List<ComboSkillDef>(_queuedComboSkills);
                    var positions = new List<Vector2>(_awakenMarkerPositions);
                    var handles = new List<GameObject>(_awakenMarkerHandles);
                    _queuedComboSkills.Clear();
                    _awakenMarkerPositions.Clear();
                    _awakenMarkerHandles.Clear();

                    CancelAwakenComboBurst();
                    _awakenDetonatingParticles.AddRange(handles);
                    _awakenExplosionRoutine = StartCoroutine(ExplodeMarkersSequentially(skills, positions, handles, damageCount));
                }
                else
                {
                    // (기존) 종료 시 한 번에 일괄 발동
                    Log($"[각성] 종료 — 콤보 {_queuedComboSkills.Count}건 일괄 발동 (Damage {damageCount}건)");

                    int damageIndex = 0;
                    for (int i = 0; i < _queuedComboSkills.Count; i++)
                    {
                        var skill = _queuedComboSkills[i];
                        if (skill == null) continue;
                        ActivateComboSkill(skill);

                        if (skill.effect == ComboEffectType.Damage)
                        {
                            if (effectOverlay != null && !string.IsNullOrEmpty(awakenDamageEffectName))
                            {
                                Vector2 offset = ComputeAwakenDamageOffset(damageIndex, damageCount);
                                effectOverlay.PlayByNameAtOffset(awakenDamageEffectName, offset);
                            }
                            damageIndex++;
                        }
                    }

                    // Damage 콤보가 1건 이상이면 임팩트 강조용 셰이크 (콤보 수 비례, 상한 있음)
                    if (damageCount > 0 && damageOverlay != null && awakenShakeIntensityPerHit > 0f)
                    {
                        float intensity = Mathf.Min(damageCount * awakenShakeIntensityPerHit, awakenShakeIntensityMax);
                        damageOverlay.TriggerShake(intensity);
                    }

                    ApplyAwakenComboGaugeRecovery(_queuedComboSkills.Count);
                    _queuedComboSkills.Clear();
                }
            }
            else
            {
                Log("[각성] 종료 — 발동된 콤보 없음");
            }

            _comboInput.Clear();
            _comboCooldown = System.Array.Empty<int>();
            _awakenInputHistory.Clear();

            if (handHud != null)
            {
                handHud.SetAwakenMode(false);
                handHud.UpdateComboSlot(_comboInput);
                PushComboCooldownUi();
                handHud.UpdateAwakenInputHistory(_awakenInputHistory);
            }
        }

        // 각성 중 콤보 성공 시 몬스터 주위에 표식 1개 생성(콤보 큐와 1:1 정렬)
        void SpawnAwakenComboMarker()
        {
            if (!awakenMarkerSequence) return;
            int index = _awakenMarkerHandles.Count;
            float radius = awakenMarkerRadius + index * awakenMarkerRadiusGrowth;
            if (awakenMarkerMaxRadius > 0f) radius = Mathf.Min(radius, awakenMarkerMaxRadius);

            var handle = Sashindo.VFX.AwakeningComboParticle.Spawn(transform, _enemy, index, radius,
                awakenMarkerVerticalSquash, awakenMarkerCenterOffset, awakenComboParticleScale,
                handHud != null ? handHud.AwakenMarkerLayer : null);
            _awakenMarkerPositions.Add(ComputeAwakenMarkerPosition(index));
            _awakenMarkerHandles.Add(handle);
        }

        void ClearAwakenComboParticles()
        {
            CancelAwakenComboBurst();
            _awakenMarkerPositions.Clear();
            RemoveAwakenComboParticles(_awakenMarkerHandles);
        }

        void CancelAwakenComboBurst()
        {
            if (_awakenExplosionRoutine != null)
            {
                StopCoroutine(_awakenExplosionRoutine);
                _awakenExplosionRoutine = null;
            }
            RemoveAwakenComboParticles(_awakenDetonatingParticles);
        }

        void RemoveAwakenComboParticles(List<GameObject> handles)
        {
            foreach (var handle in handles)
                if (handle != null) Destroy(handle);
            handles.Clear();
        }

        // idx번째 표식의 배치 위치 — 몬스터 중심 기준 황금각 분산(겹침 최소화)
        Vector2 ComputeAwakenMarkerPosition(int idx)
        {
            Vector2 center = (effectOverlay != null ? effectOverlay.EnemyAnchoredPos : new Vector2(0f, 300f))
                             + awakenMarkerCenterOffset;
            const float goldenAngle = 137.50776f * Mathf.Deg2Rad;
            float angle = idx * goldenAngle;
            float radius = awakenMarkerRadius + idx * awakenMarkerRadiusGrowth;
            // 최대 반경으로 범위 제한(0 이하면 무제한)
            if (awakenMarkerMaxRadius > 0f) radius = Mathf.Min(radius, awakenMarkerMaxRadius);
            return center + new Vector2(Mathf.Cos(angle) * radius,
                                       Mathf.Sin(angle) * radius * awakenMarkerVerticalSquash);
        }

        // 각성 종료 후 표식을 하나씩 터뜨리며 콤보를 순차 발동
        IEnumerator ExplodeMarkersSequentially(List<ComboSkillDef> skills, List<Vector2> positions,
                                               List<GameObject> handles, int damageCount)
        {
            var wait = awakenExplosionInterval > 0f ? new WaitForSeconds(awakenExplosionInterval) : null;

            for (int i = 0; i < skills.Count; i++)
            {
                if (!_inBattle) break; // 전투가 끝났으면 잔여 폭발 중단

                var skill = skills[i];
                Vector2 pos = i < positions.Count
                    ? positions[i]
                    : (effectOverlay != null ? effectOverlay.EnemyAnchoredPos : Vector2.zero);

                if (i < handles.Count && handles[i] != null)
                {
                    var handle = handles[i];
                    if (effectOverlay != null && handle.TryGetComponent<Sashindo.VFX.AwakeningComboParticle>(out var marker)
                        && marker.TryGetBurstScreenPosition(out var screen)
                        && effectOverlay.TryAnchoredFromScreen(screen, out var at))
                        pos = at;

                    handles[i] = null;
                    Destroy(handle);
                }

                if (skill == null) continue;
                if (effectOverlay != null && !string.IsNullOrEmpty(awakenDamageEffectName))
                    effectOverlay.PlayByNameAtPosition(awakenDamageEffectName, pos);

                // 콤보 효과 적용 — 데미지면 한 발씩 셰이크
                ActivateComboSkill(skill);
                if (skill.effect == ComboEffectType.Damage && damageOverlay != null && awakenShakeIntensityPerHit > 0f)
                    damageOverlay.TriggerShake(Mathf.Min(awakenShakeIntensityPerHit, awakenShakeIntensityMax));

                if (i < skills.Count - 1 && wait != null) yield return wait;
            }

            RemoveAwakenComboParticles(_awakenDetonatingParticles);
            ApplyAwakenComboGaugeRecovery(skills.Count);
            _awakenExplosionRoutine = null;
        }

        // 각성 종료 시 유물 효과 디스패치 (이무기의 여의주: 성공 콤보 수만큼 게이지 회복 등)
        void ApplyAwakenComboGaugeRecovery(int comboCount)
        {
            RelicManager.Instance?.NotifyAwakenEnded(this, comboCount);
        }

        // 각성 게이지 텍스트/게이지 갱신 및 HP바 앵커 재시도
        void UpdateAwakenText()
        {
            if (handHud == null) return;

            var activeCombatHpRect = ResolveActiveCombatHpBarRect();
            if (activeCombatHpRect != null)
                handHud.SetAwakenGaugeAnchor(activeCombatHpRect);

            int awakenMax = EffectiveAwakenInput;
            handHud.SetAwakenText($"{_elementInputCount}/{awakenMax}");
            handHud.UpdateAwakenGauge(
                _elementInputCount, awakenMax,
                _awakenActive, _awakenTimeRemaining, _awakenMaxTime);
        }

        // 각성 종료 시 데미지 콤보 N건을 좌우/상하로 분산할 오프셋 계산
        Vector2 ComputeAwakenDamageOffset(int index, int total)
        {
            if (total <= 1) return Vector2.zero;
            float lane = index - (total - 1) * 0.5f;
            float x = lane * awakenDamageEffectSpread.x;
            float y = ((index % 2 == 0) ? -1f : 1f) * awakenDamageEffectSpread.y;
            return new Vector2(x, y);
        }

        // 각성 중엔 패를 항상 한도까지 채운다. 각성 중 버리기를 당하면 줄어든 장수가 각성 끝까지 유지되던 문제 방지.
        // (각성 중 드로우는 콤보 슬롯 카드만 — BlockNonComboDraws. 버린 카드도 섞어 다시 뽑힐 수 있게 한다)
        void RefillAwakenHand()
        {
            if (!_awakenActive || _deck == null) return;
            if (_deck.HandCount >= _deck.HandLimit) return;
            _deck.MergeDiscardIntoDrawShuffled();
            _deck.Draw(_deck.HandLimit);
            handHud?.Refresh();
        }
    }
}
