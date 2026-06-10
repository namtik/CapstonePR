using System.Collections.Generic;
using UnityEngine;
using Battle.Card;
using Random = UnityEngine.Random;

namespace Battle.Deck
{
    // 뽑을 더미/패/버린 더미/소멸 더미를 관리하는 시스템
    public class CardDeckSystem
    {
        public const int HAND_LIMIT = 5;      // 기본 손패 한도
        public const int MAX_HAND_LIMIT = 10; // 손패 절대 상한
        public int HandLimit { get; private set; } = HAND_LIMIT; // 현재 손패 한도

        private readonly List<CardInstance> _drawPile = new List<CardInstance>();    // 뽑을 더미
        private readonly List<CardInstance> _hand = new List<CardInstance>();        // 패
        private readonly List<CardInstance> _discardPile = new List<CardInstance>(); // 버린 더미
        private readonly List<CardInstance> _exilePile = new List<CardInstance>();   // 소멸 더미

        public IReadOnlyList<CardInstance> Hand => _hand;               // 패 읽기 전용 뷰
        public IReadOnlyList<CardInstance> DrawPile => _drawPile;       // 뽑을 더미 읽기 전용 뷰
        public IReadOnlyList<CardInstance> DiscardPile => _discardPile; // 버린 더미 읽기 전용 뷰
        public IReadOnlyList<CardInstance> ExilePile => _exilePile;     // 소멸 더미 읽기 전용 뷰

        public int HandCount => _hand.Count;                   // 패 장수
        public int DrawCount => _drawPile.Count;               // 뽑을 더미 장수
        public int DiscardCount => _discardPile.Count;         // 버린 더미 장수
        public bool IsHandFull => _hand.Count >= HandLimit;    // 패 가득참 여부

        // 손패 한도를 delta만큼 증가(상한 적용)
        public void IncreaseHandLimit(int delta)
        {
            HandLimit = Mathf.Clamp(HandLimit + delta, HAND_LIMIT, MAX_HAND_LIMIT);
            OnPileChanged?.Invoke();
        }

        // 손패 한도를 기본값으로 초기화
        public void ResetHandLimit() => HandLimit = HAND_LIMIT;

        public event System.Action OnPileChanged;                  // 더미 변경 알림
        public event System.Action<CardInstance> OnCardDrawn;      // 카드 드로우 알림
        public event System.Action<CardInstance> OnCardDiscarded;  // 카드 버려짐 알림
        public event System.Action<int> OnReshuffled;             // 리셔플 알림(되돌아간 카드 수)

        // 전투 시작 — 시작 덱을 셔플해 뽑을 더미로
        public void StartBattle(IEnumerable<CardInstance> startingDeck)
        {
            _drawPile.Clear();
            _hand.Clear();
            _discardPile.Clear();
            _exilePile.Clear();
            ResetHandLimit();

            foreach (var card in startingDeck) _drawPile.Add(card);
            Shuffle(_drawPile);
            OnPileChanged?.Invoke();
        }

        // 전투 종료 — 임시 카드 제거 및 패/더미 초기화
        public void EndBattle()
        {
            _hand.Clear();
            _drawPile.Clear();
            _discardPile.Clear();
            _exilePile.Clear();
            OnPileChanged?.Invoke();
        }

        // 패 한도까지 또는 카드 고갈까지 드로우
        public int Draw(int count)
        {
            int drawn = 0;
            for (int i = 0; i < count; i++)
            {
                if (_hand.Count >= HandLimit) break;
                if (!TryDrawOne()) break;
                drawn++;
            }
            if (drawn > 0)
            {
                OnPileChanged?.Invoke();
                SfxManager.Instance?.PlayDraw();
            }

            if (drawn < count && _hand.Count < HandLimit
                && _drawPile.Count == 0 && _discardPile.Count == 0)
            {
                Debug.LogWarning($"[DeckSystem] 드로우 실패 — 뽑을·버린 더미가 모두 비어있음 " +
                                 $"(요청={count}, 실제={drawn}, 패={_hand.Count}/{HandLimit}, " +
                                 $"소멸 더미={_exilePile.Count})");
            }
            return drawn;
        }

        // 카드 1장 드로우 시도(더미 비면 리셔플)
        public bool TryDrawOne()
        {
            if (_drawPile.Count == 0) ReshuffleDiscardIntoDraw();
            if (_drawPile.Count == 0) return false;

            var card = _drawPile[_drawPile.Count - 1];
            _drawPile.RemoveAt(_drawPile.Count - 1);
            _hand.Add(card);
            OnCardDrawn?.Invoke(card);
            return true;
        }

        // 지정 카드를 뽑을 더미에서 패로 이동
        public bool MoveFromDrawPileToHand(CardInstance card)
        {
            if (card == null) return false;
            if (_hand.Count >= HandLimit) return false;
            int idx = _drawPile.IndexOf(card);
            if (idx < 0) return false;
            _drawPile.RemoveAt(idx);
            _hand.Add(card);
            OnCardDrawn?.Invoke(card);
            OnPileChanged?.Invoke();
            return true;
        }

        // 카드를 패에 직접 추가(한도 초과 시 실패)
        public bool AddToHand(CardInstance card)
        {
            if (card == null) return false;
            if (_hand.Count >= HandLimit) return false;
            _hand.Add(card);
            OnCardDrawn?.Invoke(card);
            OnPileChanged?.Invoke();
            return true;
        }

        // 지정 카드를 버린 더미에서 뽑을 더미 맨 위로 이동
        public bool MoveFromDiscardToDrawPileTop(CardInstance card)
        {
            if (card == null) return false;
            int idx = _discardPile.IndexOf(card);
            if (idx < 0) return false;
            _discardPile.RemoveAt(idx);
            _drawPile.Add(card);
            OnPileChanged?.Invoke();
            return true;
        }

        // 버린 더미를 뽑을 더미로 합치고 셔플
        void ReshuffleDiscardIntoDraw()
        {
            if (_discardPile.Count == 0) return;
            int moved = _discardPile.Count;
            _drawPile.AddRange(_discardPile);
            _discardPile.Clear();
            Shuffle(_drawPile);
            Debug.Log("[DeckSystem] 버린 더미 → 뽑을 더미로 셔플");
            OnReshuffled?.Invoke(moved);
        }

        // 인덱스로 패에서 카드를 버린 더미로 이동
        public void DiscardFromHand(int handIndex)
        {
            if (handIndex < 0 || handIndex >= _hand.Count) return;
            var card = _hand[handIndex];
            _hand.RemoveAt(handIndex);
            _discardPile.Add(card);
            OnPileChanged?.Invoke();
            OnCardDiscarded?.Invoke(card);
        }

        // 카드 인스턴스를 패에서 버린 더미로 이동
        public bool DiscardCardFromHand(CardInstance card)
        {
            if (_hand.Remove(card))
            {
                _discardPile.Add(card);
                OnPileChanged?.Invoke();
                OnCardDiscarded?.Invoke(card);
                return true;
            }
            return false;
        }

        // 패 전체를 버린 더미로 이동
        public int DiscardAllFromHand()
        {
            int count = _hand.Count;
            var discarded = count > 0 ? new List<CardInstance>(_hand) : null;
            for (int i = 0; i < _hand.Count; i++) _discardPile.Add(_hand[i]);
            _hand.Clear();
            if (count > 0) OnPileChanged?.Invoke();
            if (discarded != null)
                for (int i = 0; i < discarded.Count; i++) OnCardDiscarded?.Invoke(discarded[i]);
            return count;
        }

        // 사용 직후 카드를 유형에 따라 적절한 더미로 이동
        public void MoveAfterUse(CardInstance card, bool exile, bool poweredField)
        {
            _hand.Remove(card);

            if (poweredField)
            {
                _exilePile.Add(card);
            }
            else if (exile)
            {
                _exilePile.Add(card);
            }
            else
            {
                _discardPile.Add(card);
            }
            OnPileChanged?.Invoke();
        }

        // 패에서 카드를 소멸 더미로 이동
        public void ExileFromHand(CardInstance card)
        {
            if (_hand.Remove(card))
            {
                _exilePile.Add(card);
                OnPileChanged?.Invoke();
            }
        }

        // 패에서만 제거(어떤 더미에도 두지 않음)
        public bool PullFromHand(CardInstance card)
        {
            bool ok = _hand.Remove(card);
            if (ok) OnPileChanged?.Invoke();
            return ok;
        }

        // 임시로 꺼낸 카드를 적절한 더미에 재배치
        public void PlaceCardAfterUse(CardInstance card, bool exile, bool poweredField)
        {
            if (poweredField || exile) _exilePile.Add(card);
            else _discardPile.Add(card);
            OnPileChanged?.Invoke();
        }

        // 소멸 더미에서 카드 제거
        public void RemoveFromExile(CardInstance card)
        {
            if (_exilePile.Remove(card)) OnPileChanged?.Invoke();
        }

        // 카드를 뽑을 더미에 무작위 위치로 삽입
        public void AddToDrawShuffled(CardInstance card)
        {
            int idx = Random.Range(0, _drawPile.Count + 1);
            _drawPile.Insert(idx, card);
            OnPileChanged?.Invoke();
        }

        // 카드를 버린 더미에 추가
        public void AddToDiscard(CardInstance card)
        {
            _discardPile.Add(card);
            OnPileChanged?.Invoke();
        }

        // 패/뽑을 더미/버린 더미를 스냅샷 내용으로 교체(상태 복원)
        public void RestorePiles(List<CardInstance> hand, List<CardInstance> draw, List<CardInstance> discard)
        {
            _hand.Clear();
            if (hand != null) _hand.AddRange(hand);
            _drawPile.Clear();
            if (draw != null) _drawPile.AddRange(draw);
            _discardPile.Clear();
            if (discard != null) _discardPile.AddRange(discard);
            OnPileChanged?.Invoke();
        }

        // 뽑을 더미 전체를 소멸 더미로 이동(소멸 수 반환)
        public int ExileWholeDrawPile()
        {
            int n = _drawPile.Count;
            if (n > 0) { _exilePile.AddRange(_drawPile); _drawPile.Clear(); OnPileChanged?.Invoke(); }
            return n;
        }

        // 버린 더미 전체를 소멸 더미로 이동(소멸 수 반환)
        public int ExileWholeDiscardPile()
        {
            int n = _discardPile.Count;
            if (n > 0) { _exilePile.AddRange(_discardPile); _discardPile.Clear(); OnPileChanged?.Invoke(); }
            return n;
        }

        // 뽑을 더미의 특정 카드를 버린 더미로 이동
        public bool MoveFromDrawPileToDiscard(CardInstance card)
        {
            if (card == null || !_drawPile.Remove(card)) return false;
            _discardPile.Add(card);
            OnPileChanged?.Invoke();
            return true;
        }

        // 버린 더미에서 같은 이름 카드 모두 제거(제거 수 반환)
        public int RemoveFromDiscardByName(string displayName)
        {
            int removed = 0;
            for (int i = _discardPile.Count - 1; i >= 0; i--)
            {
                if (_discardPile[i].data.displayName == displayName)
                {
                    _discardPile.RemoveAt(i);
                    removed++;
                }
            }
            if (removed > 0) OnPileChanged?.Invoke();
            return removed;
        }

        // 모든 더미에서 콤보 슬롯 미입력 카드 추출(각성 처리용)
        public List<CardInstance> ExtractAllNeutralCards()
        {
            var extracted = new List<CardInstance>();
            ExtractNeutral(_drawPile, extracted);
            ExtractNeutral(_hand, extracted);
            ExtractNeutral(_discardPile, extracted);
            if (extracted.Count > 0) OnPileChanged?.Invoke();
            return extracted;
        }

        // 소스 리스트에서 콤보 슬롯 미입력 카드를 격리
        static void ExtractNeutral(List<CardInstance> source, List<CardInstance> dest)
        {
            for (int i = source.Count - 1; i >= 0; i--)
            {
                if (source[i].data.BypassComboSlot)
                {
                    dest.Add(source[i]);
                    source.RemoveAt(i);
                }
            }
        }

        // 보관된 무속성 카드들을 버린 더미로 반환
        public void ReturnNeutralCardsToDiscard(IEnumerable<CardInstance> cards)
        {
            foreach (var c in cards) _discardPile.Add(c);
            OnPileChanged?.Invoke();
        }

        // 패 한도 초과분을 버린 더미로 이동(이동 수 반환)
        public int TrimHandOverflowToDiscard()
        {
            int trimmed = 0;
            while (_hand.Count > HandLimit)
            {
                var card = _hand[_hand.Count - 1];
                _hand.RemoveAt(_hand.Count - 1);
                _discardPile.Add(card);
                trimmed++;
            }
            if (trimmed > 0) OnPileChanged?.Invoke();
            return trimmed;
        }

        // 더미 변경 이벤트를 외부에서 강제 발행
        public void NotifyChanged() => OnPileChanged?.Invoke();

        // 리스트를 Fisher-Yates 방식으로 셔플
        static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
