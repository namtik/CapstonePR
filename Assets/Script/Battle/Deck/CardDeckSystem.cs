using System.Collections.Generic;
using UnityEngine;
using Battle.Card;
using Random = UnityEngine.Random;

namespace Battle.Deck
{
    /// <summary>
    /// 뽑을 카드 더미 / 패 / 버린 카드 더미 / 소멸 더미를 관리한다.
    /// 카드 효과 자체는 CardEffectResolver가 별도로 다룬다.
    /// </summary>
    public class CardDeckSystem
    {
        /// <summary>기본 손패 한도(PDF 사양). 땅19(418) 등으로 동적 증가 가능.</summary>
        public const int HAND_LIMIT = 5;
        /// <summary>UI 슬롯 사전 생성용 절대 상한(Excel Extra: MaxHandLimit=10 기준).</summary>
        public const int MAX_HAND_LIMIT = 10;
        /// <summary>현재 손패 한도(런타임 변경 가능 — 땅19/418).</summary>
        public int HandLimit { get; private set; } = HAND_LIMIT;

        private readonly List<CardInstance> _drawPile = new List<CardInstance>();
        private readonly List<CardInstance> _hand = new List<CardInstance>();
        private readonly List<CardInstance> _discardPile = new List<CardInstance>();
        private readonly List<CardInstance> _exilePile = new List<CardInstance>();

        public IReadOnlyList<CardInstance> Hand => _hand;
        public IReadOnlyList<CardInstance> DrawPile => _drawPile;
        public IReadOnlyList<CardInstance> DiscardPile => _discardPile;
        public IReadOnlyList<CardInstance> ExilePile => _exilePile;

        public int HandCount => _hand.Count;
        public int DrawCount => _drawPile.Count;
        public int DiscardCount => _discardPile.Count;
        public bool IsHandFull => _hand.Count >= HandLimit;

        /// <summary>땅19(418) 등 — HandLimit를 delta만큼 증가(MAX_HAND_LIMIT 상한).</summary>
        public void IncreaseHandLimit(int delta)
        {
            HandLimit = Mathf.Clamp(HandLimit + delta, HAND_LIMIT, MAX_HAND_LIMIT);
            OnPileChanged?.Invoke();
        }

        /// <summary>전투 시작 시 HandLimit 초기화.</summary>
        public void ResetHandLimit() => HandLimit = HAND_LIMIT;

        public event System.Action OnPileChanged;
        /// <summary>패로 새로 들어온 카드. gaugeSinceDrawn 리셋 등에 사용.</summary>
        public event System.Action<CardInstance> OnCardDrawn;

        // ─────────────────────────────────────────────────────────────
        // 초기화
        // ─────────────────────────────────────────────────────────────

        /// <summary>전투 시작 — 시작 덱을 셔플해서 뽑을 더미로.</summary>
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

        /// <summary>전투 종료 — 임시 카드(파편 등) 제거 및 패/더미 초기화.</summary>
        public void EndBattle()
        {
            _hand.Clear();
            _drawPile.Clear();
            _discardPile.Clear();
            _exilePile.Clear();
            OnPileChanged?.Invoke();
        }

        // ─────────────────────────────────────────────────────────────
        // 드로우
        // ─────────────────────────────────────────────────────────────

        /// <summary>패가 한도까지 차거나 더 이상 뽑을 카드가 없을 때까지 드로우.</summary>
        public int Draw(int count)
        {
            int drawn = 0;
            for (int i = 0; i < count; i++)
            {
                if (_hand.Count >= HandLimit) break;
                if (!TryDrawOne()) break;
                drawn++;
            }
            if (drawn > 0) OnPileChanged?.Invoke();

            // 손패가 한도에 안 찼는데 못 뽑은 경우 — 더미 고갈 진단 로그
            if (drawn < count && _hand.Count < HandLimit
                && _drawPile.Count == 0 && _discardPile.Count == 0)
            {
                Debug.LogWarning($"[DeckSystem] 드로우 실패 — 뽑을·버린 더미가 모두 비어있음 " +
                                 $"(요청={count}, 실제={drawn}, 패={_hand.Count}/{HandLimit}, " +
                                 $"소멸 더미={_exilePile.Count})");
            }
            return drawn;
        }

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

        /// <summary>지정 카드를 뽑을 더미에서 패로 이동(데이터 드리븐 효과용).</summary>
        public bool MoveFromDrawPileToHand(CardInstance card)
        {
            if (card == null) return false;
            int idx = _drawPile.IndexOf(card);
            if (idx < 0) return false;
            _drawPile.RemoveAt(idx);
            _hand.Add(card);
            OnCardDrawn?.Invoke(card);
            OnPileChanged?.Invoke();
            return true;
        }

        /// <summary>카드를 패에 직접 추가 (한도 초과 시 false).</summary>
        public bool AddToHand(CardInstance card)
        {
            if (card == null) return false;
            if (_hand.Count >= HandLimit) return false;
            _hand.Add(card);
            OnCardDrawn?.Invoke(card);
            OnPileChanged?.Invoke();
            return true;
        }

        /// <summary>지정 카드를 버린 더미에서 뽑을 더미 맨 위로 이동.</summary>
        public bool MoveFromDiscardToDrawPileTop(CardInstance card)
        {
            if (card == null) return false;
            int idx = _discardPile.IndexOf(card);
            if (idx < 0) return false;
            _discardPile.RemoveAt(idx);
            _drawPile.Add(card); // 맨 위 = 다음 뽑힐 위치
            OnPileChanged?.Invoke();
            return true;
        }

        void ReshuffleDiscardIntoDraw()
        {
            if (_discardPile.Count == 0) return;
            _drawPile.AddRange(_discardPile);
            _discardPile.Clear();
            Shuffle(_drawPile);
            Debug.Log("[DeckSystem] 버린 더미 → 뽑을 더미로 셔플");
        }

        // ─────────────────────────────────────────────────────────────
        // 패/더미 조작
        // ─────────────────────────────────────────────────────────────

        public void DiscardFromHand(int handIndex)
        {
            if (handIndex < 0 || handIndex >= _hand.Count) return;
            var card = _hand[handIndex];
            _hand.RemoveAt(handIndex);
            _discardPile.Add(card);
            OnPileChanged?.Invoke();
        }

        /// <summary>카드 인스턴스를 패에서 버린 더미로 이동.</summary>
        public bool DiscardCardFromHand(CardInstance card)
        {
            if (_hand.Remove(card))
            {
                _discardPile.Add(card);
                OnPileChanged?.Invoke();
                return true;
            }
            return false;
        }

        public int DiscardAllFromHand()
        {
            int count = _hand.Count;
            for (int i = 0; i < _hand.Count; i++) _discardPile.Add(_hand[i]);
            _hand.Clear();
            if (count > 0) OnPileChanged?.Invoke();
            return count;
        }

        /// <summary>카드 효과 처리 직후 호출 — 파워는 별도 보관, 일반은 버린 더미로, 소멸 키워드는 소멸 더미로.</summary>
        public void MoveAfterUse(CardInstance card, bool exile, bool poweredField)
        {
            _hand.Remove(card);

            if (poweredField)
            {
                // 파워 카드는 별도 보관 (현재는 exile에 함께 보관 — 시각적으론 필드 위)
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

        public void ExileFromHand(CardInstance card)
        {
            if (_hand.Remove(card))
            {
                _exilePile.Add(card);
                OnPileChanged?.Invoke();
            }
        }

        /// <summary>패에서만 제거하여 어떤 더미에도 두지 않는다. 효과 처리 중 자기 자신 격리용.</summary>
        public bool PullFromHand(CardInstance card)
        {
            bool ok = _hand.Remove(card);
            if (ok) OnPileChanged?.Invoke();
            return ok;
        }

        /// <summary>외부 호출자가 임시로 꺼낸 카드를 다시 적절한 더미에 배치할 때 사용.</summary>
        public void PlaceCardAfterUse(CardInstance card, bool exile, bool poweredField)
        {
            if (poweredField || exile) _exilePile.Add(card);
            else _discardPile.Add(card);
            OnPileChanged?.Invoke();
        }

        public void RemoveFromExile(CardInstance card)
        {
            if (_exilePile.Remove(card)) OnPileChanged?.Invoke();
        }

        public void AddToDrawShuffled(CardInstance card)
        {
            int idx = Random.Range(0, _drawPile.Count + 1);
            _drawPile.Insert(idx, card);
            OnPileChanged?.Invoke();
        }

        public void AddToDiscard(CardInstance card)
        {
            _discardPile.Add(card);
            OnPileChanged?.Invoke();
        }

        /// <summary>버린 더미에서 displayName이 같은 카드 모두 제거.</summary>
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

        /// <summary>모든 더미(드로우/패/버린)에서 무속성 카드 추출. F 피버 발동/종료 시 사용.</summary>
        public List<CardInstance> ExtractAllNeutralCards()
        {
            var extracted = new List<CardInstance>();
            ExtractNeutral(_drawPile, extracted);
            ExtractNeutral(_hand, extracted);
            ExtractNeutral(_discardPile, extracted);
            if (extracted.Count > 0) OnPileChanged?.Invoke();
            return extracted;
        }

        static void ExtractNeutral(List<CardInstance> source, List<CardInstance> dest)
        {
            // 피버 중 콤보 슬롯에 들어가지 않는 카드(무속성 500 + 파편 501~503) 모두 격리.
            for (int i = source.Count - 1; i >= 0; i--)
            {
                if (source[i].data.BypassComboSlot)
                {
                    dest.Add(source[i]);
                    source.RemoveAt(i);
                }
            }
        }

        /// <summary>피버 종료 시 보관된 무속성 카드들을 버린 더미로 반환.</summary>
        public void ReturnNeutralCardsToDiscard(IEnumerable<CardInstance> cards)
        {
            foreach (var c in cards) _discardPile.Add(c);
            OnPileChanged?.Invoke();
        }

        /// <summary>패가 한도를 초과하면 초과분을 버린 더미로 이동.</summary>
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

        public void NotifyChanged() => OnPileChanged?.Invoke();

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
