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
        // 물15(214) — 플레이어에게 적용된 모든 저주 해제
        public void ClearAllCurses()
        {
            bool hadCurse = _curses.Count > 0;
            _curses.Clear();
            handHud?.Refresh();
            if (hadCurse) Log("[저주] 해제됨");
        }

        // 외부(NewCardView)가 카드 속성이 저주되었는지 확인용
        public bool IsElementCursed(CardElement element)
        {
            // 저주는 전투 범위 상태 — 전투 밖(보상창·상점·이벤트 등)의 카드는 저주 표시를 하지 않는다
            if (!_inBattle) return false;
            for (int i = 0; i < _curses.Count; i++)
                if (_curses[i].element == element && _curses[i].Active)
                    return true;
            return false;
        }

        // 해당 속성 저주가 풀리기까지 남은 카드 사용 횟수(저주 없으면 0) — 카드 툴팁 표시용
        public int GetCurseRemainingUses(CardElement element)
        {
            if (!_inBattle) return 0;
            int max = 0;
            for (int i = 0; i < _curses.Count; i++)
                if (_curses[i].element == element && _curses[i].remainingUses > max)
                    max = _curses[i].remainingUses;
            return max;
        }

        // 적(심해거신·백족승)이 붙잡고 있어 사용 횟수로 풀리지 않는 저주인지 — 카드 툴팁 표시용
        public bool IsCurseHeldByEnemy(CardElement element)
        {
            if (!_inBattle) return false;
            for (int i = 0; i < _curses.Count; i++)
                if (_curses[i].element == element && _curses[i].held) return true;
            return false;
        }

        // 특이사항(심해거신·백족승): 붙잡고 있던 저주를 다른 속성으로 옮긴다.
        // 이전 속성은 붙잡기만 풀고(방해행동 등으로 따로 남은 횟수가 있으면 그만큼은 유지), 새 속성을 붙잡는다.
        public void MoveHeldCurse(CardElement? from, CardElement to, string sourceName = null)
        {
            if (from.HasValue && from.Value != to)
            {
                for (int i = _curses.Count - 1; i >= 0; i--)
                {
                    if (_curses[i].element != from.Value || !_curses[i].held) continue;
                    _curses[i].held = false;
                    if (_curses[i].remainingUses <= 0) _curses.RemoveAt(i);
                }
            }

            ActiveCurse target = null;
            for (int i = 0; i < _curses.Count; i++)
                if (_curses[i].element == to) { target = _curses[i]; break; }
            if (target == null)
            {
                target = new ActiveCurse { element = to, remainingUses = 0 };
                _curses.Add(target);
            }
            target.held = true;
            if (!string.IsNullOrWhiteSpace(sourceName)) target.sourceName = sourceName;

            handHud?.Refresh();
            RequestCurseNudge(to); // 저주 속성 손패 카드 한 번 떠오르기(카메라·UI 복귀 후)
            Log($"[특이사항] 저주 이동 — {(from.HasValue ? ElementName(from.Value) : "없음")} -> {ElementName(to)}");
        }

        // 저주된 속성 카드 사용 시 플레이어 피해 적용. 저주마다 카드 사용 1회씩 소모.
        void ApplyCurseOnCardUse(CardInstance card)
        {
            if (_curses.Count == 0) return;

            for (int i = _curses.Count - 1; i >= 0; i--)
            {
                ActiveCurse curse = _curses[i];
                if (card.Element == curse.element && _player != null)
                {
                    _player.TakeDamage(CURSE_PLAYER_DAMAGE, "normal", curse.sourceName);
                    Log($"[저주] {curse.element} 카드 사용 — 플레이어 {CURSE_PLAYER_DAMAGE} 피해");
                    if (_track != null && _track.action == 0) _track.curseHits++;
                }

                if (curse.remainingUses > 0) curse.remainingUses--;
                if (!curse.Active) // 적이 붙잡고 있는 저주는 사용 횟수로 풀리지 않는다
                {
                    Log($"[저주] {curse.element} 저주 종료");
                    _curses.RemoveAt(i);
                    if (_track != null && _track.action == 0) ResolveTrack(CurseReward(_track.curseHits));
                }
            }
            handHud?.Refresh();
        }

        // 속성을 한글 이름으로 변환
        static string ElementName(CardElement e) => e switch
        {
            CardElement.Fire => "불", CardElement.Water => "물",
            CardElement.Wind => "바람", CardElement.Earth => "땅", _ => "?"
        };

        // 적 특이사항(EnemySpecialAbility) 플레이어측 효과 API

        // 특이사항: 지정 속성에 저주 부여(uses회 카드 사용 동안 지속)
        public void ApplyElementCurse(CardElement element, int uses, string sourceName = null)
        {
            uses = Mathf.Max(1, uses);
            for (int i = 0; i < _curses.Count; i++)
            {
                if (_curses[i].element != element) continue;
                _curses[i].remainingUses += uses;
                if (!string.IsNullOrWhiteSpace(sourceName))
                    _curses[i].sourceName = sourceName;
                handHud?.Refresh();
                RequestCurseNudge(element); // 저주 속성 손패 카드 한 번 떠오르기(카메라·UI 복귀 후)
                Log($"[특이사항] {ElementName(element)} 속성 저주 중첩 — {_curses[i].remainingUses}회 지속");
                return;
            }

            _curses.Add(new ActiveCurse { element = element, remainingUses = uses, sourceName = sourceName });
            handHud?.Refresh();
            RequestCurseNudge(element); // 저주 속성 손패 카드 한 번 떠오르기(카메라·UI 복귀 후)
            Log($"[특이사항] {ElementName(element)} 속성 저주 — {uses}회 지속");
        }

        IEnumerator FlushCurseNudgesWhenHudVisible()
        {
            yield return null; // 같은 프레임에 걸린 숨김 상태가 반영되도록 한 프레임 대기
            float deadline = Time.unscaledTime + 15f; // 안전 상한
            while (_inBattle && Time.unscaledTime < deadline &&
                   (_attackStartRoutine != null || _attackResolving ||
                    !EnemyActionFocusCamera.IsSettled || !EnemyActionHudFader.IsFullyVisible))
                yield return null;

            _curseNudgeCo = null;
            if (_inBattle && handHud != null)
                foreach (var el in _pendingCurseNudges)
                    if (IsElementCursed(el)) handHud.NudgeHandCardsOfElement(el); // 그사이 풀린 저주는 생략
            _pendingCurseNudges.Clear();
        }

        // 전투 시작/종료 시 대기 중인 떠오르기 취소
        void CancelCurseNudges()
        {
            if (_curseNudgeCo != null) { StopCoroutine(_curseNudgeCo); _curseNudgeCo = null; }
            _pendingCurseNudges.Clear();
        }

        // 특이사항: 무작위 속성에 저주 부여
        public void CurseRandomElement(int uses, string sourceName = null)
        {
            CardElement[] elements = { CardElement.Fire, CardElement.Water, CardElement.Wind, CardElement.Earth };
            ApplyElementCurse(elements[Random.Range(0, elements.Length)], uses, sourceName);
        }
    }
}
