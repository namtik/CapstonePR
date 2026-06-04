# 덱 더미 관리 (CardDeckSystem) 명세

> 대상 파일: `Assets/Script/Battle/Deck/CardDeckSystem.cs`
> 카드 인스턴스 타입: `Assets/Script/Battle/Card/CardData.cs` 의 `CardInstance`

---

## 1. 개요 / 책임

전투 중 카드를 담는 **4개의 더미**와 **손패 한도**를 관리하는 시스템이다. (`CardDeckSystem.cs:8-11`)

- **뽑을 더미(`_drawPile`)** — 다음에 드로우될 카드 풀. 리스트의 맨 위(마지막 인덱스)가 다음에 뽑힐 카드. (`CardDeckSystem.cs:21`, `112-113`)
- **패(`_hand`)** — 플레이어가 현재 손에 들고 사용할 수 있는 카드. (`CardDeckSystem.cs:22`)
- **버린 더미(`_discardPile`)** — 사용/버려진 카드. 뽑을 더미가 비면 셔플되어 다시 뽑을 더미로 환원. (`CardDeckSystem.cs:23`, `156-163`)
- **소멸 더미(`_exilePile`)** — 소멸(EXHAUST) 키워드 카드와 파워 카드를 보관. 전투 중 다시 순환하지 않음. (`CardDeckSystem.cs:24`, `209-217`)

손패 한도(`HandLimit`)를 관리하여 드로우/추가 시 초과를 막는다. **카드 효과 자체의 처리는 이 시스템의 책임이 아니며 `CardEffectResolver`가 별도로 담당한다.** (`CardDeckSystem.cs:9-10`)

---

## 2. 위치 / 타입

| 항목 | 값 |
| --- | --- |
| 경로 | `Assets/Script/Battle/Deck/CardDeckSystem.cs` |
| namespace | `Battle.Deck` (`CardDeckSystem.cs:6`) |
| 타입 | 일반 C# 클래스 — **`MonoBehaviour`가 아님** (`CardDeckSystem.cs:12`, `public class CardDeckSystem`) |

`MonoBehaviour`를 상속하지 않으므로 `new CardDeckSystem()`으로 직접 인스턴스화하여 보유한다. 실제로 `NewBattleController`가 필드로 `new` 생성한다. (`NewBattleController.cs:84`)

---

## 3. 참조(의존)

| 의존 대상 | 용도 | 참조 위치 |
| --- | --- | --- |
| `Battle.Card.CardInstance` | 모든 더미·패가 담는 런타임 카드 단위 | `CardDeckSystem.cs:3`, `21-24` |
| `Battle.Card.CardData` | `CardInstance.data` 경유로 `displayName`, `BypassComboSlot` 접근 | `CardDeckSystem.cs:314`, `340` |
| `UnityEngine.Random` (별칭) | 셔플 / 무작위 삽입 위치 결정 | `CardDeckSystem.cs:4`, `257`, `376` |
| `UnityEngine.Mathf` | `HandLimit` 클램프 | `CardDeckSystem.cs:39` |
| `UnityEngine.Debug` | 더미 고갈 / 재셔플 진단 로그 | `CardDeckSystem.cs:100`, `162` |
| `System.Action` / `System.Action<T>` | 이벤트 델리게이트 | `CardDeckSystem.cs:46-50` |

> `CardInstance`는 `data`(정적 정의) 외에 `transient`(임시 카드), `cursed`(저주), `gaugeSinceDrawn`, `justDrawn`, `selfUseCount` 등의 런타임 상태를 가진다. (`CardData.cs:72-101`)

---

## 4. 피참조

| 참조자 | 접근 방식 | 위치 |
| --- | --- | --- |
| `NewBattleController` | `private readonly CardDeckSystem _deck = new CardDeckSystem();` 로 소유. `Deck` 프로퍼티로 노출. `OnCardDrawn`/`OnCardDiscarded` 구독, `StartBattle`/`EndBattle`/`Draw` 호출 | `NewBattleController.cs:84`, `142`, `166-174`, `277`, `320`, `331` |
| `CardEffectResolver` | `Ctx.deck` 경유로 더미·패 조작(`DrawPile`, `Draw`, `DiscardAllFromHand`, `ExileFromHand`, `ExileWholeDrawPile`, `MoveFromDrawPileToDiscard` 등) | `CardEffectResolver.cs:341`, `346`, `973`, `979`, `989-992`, `1023`, `1029` |
| `CardHandHUD` | `Bind(CardDeckSystem)` 로 받아 `OnPileChanged` 구독, `Hand`/`DrawPile`/`DiscardPile`/`DrawCount`/`DiscardCount` 표시. 상수 `MAX_HAND_LIMIT`/`HAND_LIMIT` 사용 | `CardHandHUD.cs:19`, `21`, `129`, `249-253`, `754`, `804-805` |
| `EnemyDisruptionAI` | `CardDeckSystem deck`를 인자로 받아 컨텍스트(손패 수 등) 구성 | `EnemyDisruptionAI.cs:107`, `200` |

`NewBattleController`는 전투 컨텍스트에 `_ctx.deck = _deck;`로 주입하여 `CardEffectResolver`가 `Ctx.deck`로 접근하게 한다. (`NewBattleController.cs:254`)

---

## 5. 공개 API

### 상수 / 프로퍼티

| 시그니처 | 설명 | 비고 |
| --- | --- | --- |
| `const int HAND_LIMIT = 5` | 기본 손패 한도(PDF 사양) | `CardDeckSystem.cs:15` |
| `const int MAX_HAND_LIMIT = 10` | 손패 한도 절대 상한(UI 슬롯 사전 생성 기준) | `CardDeckSystem.cs:17` |
| `int HandLimit { get; private set; }` | 현재 손패 한도(런타임 변경 가능). 기본값 `HAND_LIMIT` | 외부 set 불가 (`CardDeckSystem.cs:19`) |
| `IReadOnlyList<CardInstance> Hand` | 패(읽기 전용 뷰) | `CardDeckSystem.cs:26` |
| `IReadOnlyList<CardInstance> DrawPile` | 뽑을 더미(읽기 전용 뷰) | `CardDeckSystem.cs:27` |
| `IReadOnlyList<CardInstance> DiscardPile` | 버린 더미(읽기 전용 뷰) | `CardDeckSystem.cs:28` |
| `IReadOnlyList<CardInstance> ExilePile` | 소멸 더미(읽기 전용 뷰) | `CardDeckSystem.cs:29` |
| `int HandCount` | 패 장수 (`_hand.Count`) | `CardDeckSystem.cs:31` |
| `int DrawCount` | 뽑을 더미 장수 | `CardDeckSystem.cs:32` |
| `int DiscardCount` | 버린 더미 장수 | `CardDeckSystem.cs:33` |
| `bool IsHandFull` | 패가 한도에 도달했는지(`_hand.Count >= HandLimit`) | `CardDeckSystem.cs:34` |

> `ExilePile`은 읽기 뷰만 노출되며 `ExileCount` 같은 별도 카운트 프로퍼티는 없다.

### 초기화 / 종료

| 시그니처 | 설명 | 비고 |
| --- | --- | --- |
| `void StartBattle(IEnumerable<CardInstance> startingDeck)` | 4더미·패 비우고 한도 초기화 후, 시작 덱을 뽑을 더미에 넣고 셔플 | `OnPileChanged` 발생 (`CardDeckSystem.cs:57-68`) |
| `void EndBattle()` | 패·뽑을·버린·소멸 더미 전부 비움(임시 카드 정리) | `OnPileChanged` 발생 (`CardDeckSystem.cs:71-78`) |

### 드로우

| 시그니처 | 설명 | 비고 |
| --- | --- | --- |
| `int Draw(int count)` | 패가 한도까지 차거나 더 못 뽑을 때까지 최대 `count`장 드로우. **실제 드로우된 수 반환** | 1장 이상 시 `OnPileChanged` 1회. 양 더미 고갈 시 경고 로그 (`CardDeckSystem.cs:85-105`) |
| `bool TryDrawOne()` | 1장 드로우. 뽑을 더미가 비면 먼저 재셔플 시도, 그래도 비면 `false` | `OnCardDrawn` 발생, `OnPileChanged`는 **발생 안 함** (`CardDeckSystem.cs:107-117`) |
| `bool MoveFromDrawPileToHand(CardInstance card)` | 지정 카드를 뽑을 더미→패로 이동(데이터 드리븐). 패가 가득이거나 카드 미존재 시 `false` | `OnCardDrawn` + `OnPileChanged` (`CardDeckSystem.cs:120-131`) |
| `bool AddToHand(CardInstance card)` | 카드를 패에 직접 추가. 한도 초과 시 `false` | `OnCardDrawn` + `OnPileChanged` (`CardDeckSystem.cs:134-142`) |
| `bool MoveFromDiscardToDrawPileTop(CardInstance card)` | 지정 카드를 버린 더미→뽑을 더미 맨 위로 | `OnPileChanged` (`CardDeckSystem.cs:145-154`) |

### 패 / 더미 조작

| 시그니처 | 설명 | 비고 |
| --- | --- | --- |
| `void DiscardFromHand(int handIndex)` | 패의 인덱스 카드를 버린 더미로. 범위 밖이면 무동작 | `OnPileChanged` + `OnCardDiscarded` (`CardDeckSystem.cs:169-177`) |
| `bool DiscardCardFromHand(CardInstance card)` | 인스턴스 지정으로 패→버린 더미. 성공 시 `true` | `OnPileChanged` + `OnCardDiscarded` (`CardDeckSystem.cs:180-190`) |
| `int DiscardAllFromHand()` | 패 전체를 버린 더미로. **버린 장수 반환** | 1장 이상 시 `OnPileChanged` 1회 + 카드별 `OnCardDiscarded` (`CardDeckSystem.cs:192-202`) |
| `void MoveAfterUse(CardInstance card, bool exile, bool poweredField)` | 효과 처리 **직후** 호출. 분기 배치(아래 §6 참조) | `OnPileChanged` (`CardDeckSystem.cs:205-223`) |
| `void ExileFromHand(CardInstance card)` | 패→소멸 더미. 패에 없으면 무동작 | `OnPileChanged`(성공 시) (`CardDeckSystem.cs:225-232`) |
| `bool PullFromHand(CardInstance card)` | 패에서만 제거, **어떤 더미에도 안 둠**(효과 중 자기 격리용) | `OnPileChanged`(성공 시) (`CardDeckSystem.cs:235-240`) |
| `void PlaceCardAfterUse(CardInstance card, bool exile, bool poweredField)` | 외부가 임시로 꺼낸 카드를 다시 더미에 배치. 파워/소멸→소멸, 그 외→버린 더미 | `OnPileChanged` (`CardDeckSystem.cs:243-248`) |
| `void RemoveFromExile(CardInstance card)` | 소멸 더미에서 제거 | `OnPileChanged`(성공 시) (`CardDeckSystem.cs:250-253`) |
| `void AddToDrawShuffled(CardInstance card)` | 뽑을 더미의 **무작위 위치**에 삽입 | `OnPileChanged` (`CardDeckSystem.cs:255-260`) |
| `void AddToDiscard(CardInstance card)` | 버린 더미에 추가 | `OnPileChanged` (`CardDeckSystem.cs:262-266`) |
| `void RestorePiles(List<CardInstance> hand, List<CardInstance> draw, List<CardInstance> discard)` | 패·뽑을·버린 더미를 스냅샷으로 교체(각성 종료 시 복원). `null` 인자는 빈 더미. **소멸 더미는 건드리지 않음** | `OnPileChanged` (`CardDeckSystem.cs:272-281`) |
| `int ExileWholeDrawPile()` | 뽑을 더미 전체→소멸 더미. 소멸 장수 반환 | 1장 이상 시 `OnPileChanged` (`CardDeckSystem.cs:284-289`) |
| `int ExileWholeDiscardPile()` | 버린 더미 전체→소멸 더미. 소멸 장수 반환 | 1장 이상 시 `OnPileChanged` (`CardDeckSystem.cs:292-297`) |
| `bool MoveFromDrawPileToDiscard(CardInstance card)` | 뽑을 더미의 특정 카드→버린 더미. 성공 시 `true` | `OnPileChanged` (`CardDeckSystem.cs:300-306`) |
| `int RemoveFromDiscardByName(string displayName)` | 버린 더미에서 `displayName` 일치 카드 전부 제거. 제거 장수 반환 | 1장 이상 시 `OnPileChanged` (`CardDeckSystem.cs:309-322`) |
| `List<CardInstance> ExtractAllNeutralCards()` | 드로우·패·버린 더미에서 `BypassComboSlot` 카드(무속성+파편) 추출하여 반환(각성/피버용) | 1장 이상 시 `OnPileChanged`. **소멸 더미는 미포함** (`CardDeckSystem.cs:325-333`) |
| `void ReturnNeutralCardsToDiscard(IEnumerable<CardInstance> cards)` | 보관했던 무속성 카드들을 버린 더미로 반환 | `OnPileChanged` (`CardDeckSystem.cs:349-353`) |
| `int TrimHandOverflowToDiscard()` | 패가 한도를 초과하면 초과분(맨 뒤부터)을 버린 더미로. 정리 장수 반환 | 1장 이상 시 `OnPileChanged`. `OnCardDiscarded`는 **발생 안 함** (`CardDeckSystem.cs:356-368`) |

### 손패 한도

| 시그니처 | 설명 | 비고 |
| --- | --- | --- |
| `void IncreaseHandLimit(int delta)` | `HandLimit`을 `delta`만큼 증감하되 `[HAND_LIMIT, MAX_HAND_LIMIT]`로 클램프 | `OnPileChanged` (`CardDeckSystem.cs:37-41`) |
| `void ResetHandLimit()` | `HandLimit`을 `HAND_LIMIT`으로 초기화 | 이벤트 없음. `StartBattle` 내부에서도 호출 (`CardDeckSystem.cs:44`, `63`) |

### 알림

| 시그니처 | 설명 | 비고 |
| --- | --- | --- |
| `void NotifyChanged()` | `OnPileChanged`를 수동 발생(외부에서 강제 UI 갱신용) | `CardDeckSystem.cs:370` |

> **참고 — 시그니처 정확성:** 명세 요청 목록 중 일부는 실제 메서드명이 다르다. 코드 실제 이름 기준으로 위 표를 작성했다.
> - `MoveFromDrawPileToHand` 는 존재하나, 버린 더미 환원용은 `MoveFromDiscardToDrawPileTop`(맨 위로) 이다.
> - `ExileWholeDrawPile` / `ExileWholeDiscardPile` 두 개가 별도로 존재한다.
> - `Card/CardData.cs` 가 아니라 **`Battle/Card/CardData.cs`** 가 실제 경로이다.

---

## 6. 내부 동작 / 데이터 흐름

### 셔플

`Shuffle<T>(List<T>)` — Fisher–Yates 방식. 뒤에서 앞으로 순회하며 `Random.Range(0, i+1)`로 뽑은 인덱스와 교환. (`CardDeckSystem.cs:372-379`) `StartBattle`과 재셔플 시 사용.

### 뽑을 위치 규칙

뽑을 더미는 **리스트의 마지막 인덱스가 다음에 뽑힐 카드(맨 위)**이다. `TryDrawOne`은 `_drawPile[Count-1]`을 꺼낸다. (`CardDeckSystem.cs:112-113`) 따라서 `MoveFromDiscardToDrawPileTop`은 `Add`(맨 뒤=맨 위)로 다음 드로우를 보장한다. (`CardDeckSystem.cs:151`)

### 더미 고갈 시 재셔플 — `ReshuffleDiscardIntoDraw()`

`TryDrawOne`에서 뽑을 더미가 비었을 때 자동 호출(`private`). (`CardDeckSystem.cs:109`, `156-163`)

1. 버린 더미가 비었으면 즉시 반환(아무 일 없음).
2. 버린 더미 전체를 뽑을 더미로 옮기고 버린 더미를 비운다.
3. 뽑을 더미를 셔플하고 진단 로그 출력.

이후에도 뽑을 더미가 비어 있으면(= 버린 더미도 비었던 경우) `TryDrawOne`은 `false`를 반환한다. (`CardDeckSystem.cs:110`)

### 사용 후 배치 — `MoveAfterUse(card, exile, poweredField)`

효과 처리 직후 호출되며 패에서 카드를 제거한 뒤 3분기로 배치한다. (`CardDeckSystem.cs:205-223`)

| 조건(우선순위순) | 배치 대상 |
| --- | --- |
| `poweredField == true` (파워 카드) | **소멸 더미**(`_exilePile`). 주석상 "별도 보관"이나 현재 구현은 소멸 더미와 공유, 시각적으로는 필드 위 (`CardDeckSystem.cs:209-213`) |
| `exile == true` (소멸 키워드) | 소멸 더미 (`CardDeckSystem.cs:214-217`) |
| 그 외(일반 카드) | 버린 더미 (`CardDeckSystem.cs:218-221`) |

`PlaceCardAfterUse`도 같은 배치 규칙이나, **패에서 제거하는 단계가 없다**(이미 외부가 꺼낸 카드 전용). (`CardDeckSystem.cs:243-248`)

### 무속성/파편 격리 — `ExtractNeutral` (private static)

`ExtractAllNeutralCards`가 드로우·패·버린 더미를 뒤에서부터 순회하며 `data.BypassComboSlot`(= `comboSlot == false`)인 카드를 추출 리스트로 옮긴다(원본에서 제거). (`CardDeckSystem.cs:335-346`) 각성/피버 중 콤보 슬롯에 들어가지 않는 카드(무속성 + 파편)를 임시 격리하는 용도이며, 종료 시 `ReturnNeutralCardsToDiscard`로 버린 더미에 되돌린다.

---

## 7. 이벤트

| 이벤트 | 시그니처 | 발생 시점 |
| --- | --- | --- |
| `OnPileChanged` | `event System.Action` | 더미/패 구성이 바뀌는 거의 모든 조작 후. UI(`CardHandHUD`)가 구독해 갱신. **`TryDrawOne` 단독 호출과 `TrimHandOverflowToDiscard`의 카드별 알림에서는 발생 안 함**(단, `Draw`/Trim은 묶음으로 1회 발생). 수동 발생은 `NotifyChanged`. (`CardDeckSystem.cs:46`, `370`) |
| `OnCardDrawn` | `event System.Action<CardInstance>` | 카드가 패로 **새로 들어올 때**: `TryDrawOne`, `MoveFromDrawPileToHand`, `AddToHand`. 새 카드의 `gaugeSinceDrawn`/`justDrawn` 리셋 등에 사용. (`CardDeckSystem.cs:48`, `115`, `128`, `139`) |
| `OnCardDiscarded` | `event System.Action<CardInstance>` | 패에서 버린 더미로 **'버려질' 때(사용 아님)**: `DiscardFromHand`, `DiscardCardFromHand`, `DiscardAllFromHand`. `ON_SELF_DISCARDED` 트리거용. **`MoveAfterUse`(사용 경로)·`TrimHandOverflowToDiscard`(한도 정리)에서는 발생 안 함.** (`CardDeckSystem.cs:50`, `176`, `186`, `200`) |

> 구독/해제는 `NewBattleController`(`OnCardDrawn`/`OnCardDiscarded`, `NewBattleController.cs:166-174`)와 `CardHandHUD`(`OnPileChanged`, `CardHandHUD.cs:251-253`, `259`)가 담당한다.

---

## 8. 주의 / 엣지케이스

- **패 한도 초과 방지**: `Draw`, `MoveFromDrawPileToHand`, `AddToHand`는 모두 `_hand.Count >= HandLimit` 검사로 한도 초과를 막는다(드로우는 break, 이동/추가는 `false` 반환). 다만 `RestorePiles`는 스냅샷을 그대로 복원하므로 한도 검사를 하지 않는다(복원 후 한도 초과가 발생할 수 있음). (`CardDeckSystem.cs:90`, `123`, `137`, `272-281`)
- **한도 정리 시 알림 차이**: `TrimHandOverflowToDiscard`로 버려지는 카드는 `OnCardDiscarded`를 발생시키지 **않으므로** `ON_SELF_DISCARDED` 류 효과가 트리거되지 않는다. (`CardDeckSystem.cs:356-368`)
- **파워 카드 보관 위치**: 파워 카드(`poweredField`)는 현재 **소멸 더미(`_exilePile`)에 함께 보관**된다(코드 주석상 시각적으로는 필드 위). 별도 전용 더미는 없으므로 소멸 더미를 순회/소거하는 로직은 파워 카드까지 함께 영향받을 수 있음에 유의. (`CardDeckSystem.cs:209-213`)
- **재셔플은 버린 더미만 환원**: 뽑을 더미가 비면 버린 더미만 셔플되어 돌아온다. **소멸 더미는 영구히 순환에서 제외**된다. 뽑을·버린 더미가 동시에 비면 더 이상 드로우 불가이며 `Draw`가 진단 경고 로그를 남긴다. (`CardDeckSystem.cs:97-103`, `156-163`)
- **`PullFromHand`는 어떤 더미에도 두지 않음**: 효과 처리 중 자기 자신을 임시 격리할 때 쓰며, 이후 `PlaceCardAfterUse`/`AddToDiscard` 등으로 **반드시 재배치**하지 않으면 카드가 모든 더미에서 사라진다(호출자 책임). (`CardDeckSystem.cs:235-240`)
- **읽기 전용 뷰**: `Hand`/`DrawPile`/`DiscardPile`/`ExilePile`은 `IReadOnlyList`로 노출되어 외부에서 직접 추가/삭제할 수 없다. 변경은 반드시 공개 메서드를 거쳐야 `OnPileChanged`가 보장된다. (`CardDeckSystem.cs:26-29`)
- **`ResetHandLimit`은 이벤트를 발생시키지 않음**: 단독 호출 시 UI 갱신이 일어나지 않으므로(증가는 `IncreaseHandLimit`가 발생) 필요 시 `NotifyChanged`를 별도 호출해야 한다. (`CardDeckSystem.cs:44`)
- **`EndBattle`은 소멸 더미까지 전부 비운다**: 전투 한정 임시 카드(파편 등) 정리 목적. 런 단위로 카드를 유지하려면 외부(예: `RunDeckState`)에서 별도 보존해야 한다. (`CardDeckSystem.cs:71-78`)
