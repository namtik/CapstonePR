# 시스템별 명세서 (개발자 레퍼런스)

각 문서는 "이 시스템이 **무엇을 참조하고(의존), 무엇이 이걸 쓰고(피참조), 어떤 공개 메서드가 동작하는지**"를 담는다.
상위 개요는 루트 `시스템_명세서.md` 참조.

## 문서 목록
| 시스템 | 문서 | 핵심 파일 |
|---|---|---|
| 전투 코어 | [new-battle-controller.md](new-battle-controller.md) | NewBattleController.cs |
| 덱 더미 관리 | [card-deck-system.md](card-deck-system.md) | Deck/CardDeckSystem.cs |
| 카드 효과 엔진 | [card-effect-resolver.md](card-effect-resolver.md) | Deck/CardEffectResolver.cs |
| 콤보 시스템 | [combo-system.md](combo-system.md) | ComboSkillDef, Deck/ComboSkill*·ComboEffectResolver |
| 카드 데이터 | [card-data.md](card-data.md) | Card/CardDatabase·CardData·CardEffectData·CardEnums |
| 런 상태 & 부트스트랩 | [run-state-bootstrap.md](run-state-bootstrap.md) | Run/RunDeckState, NewBattleSystemBootstrap |
| 유물 | [relic-system.md](relic-system.md) | Relic/RelicManager·RelicDef, UI/RelicHUD |
| 적 AI (FCM-RBFN) | [enemy-ai.md](enemy-ai.md) | AI/EnemyDisruptionAI·EnemyAiModel·DisruptionLogger |
| ↳ 적 AI 계산·코드 동작 | [enemy-ai-pipeline.md](enemy-ai-pipeline.md) | 6단계 수식+코드 (FCM/RBFN/추론/온라인학습) |
| 전투 UI | [battle-ui.md](battle-ui.md) | UI/CardHandHUD·NewCardView·CardRewardUI·오버레이 |
| 전투 연동(라운드/적) | [battle-integration.md](battle-integration.md) | Round/Roundmanager, Enemy/EnemyStat·EnemyController |

## 문서 템플릿 (각 문서 공통 섹션)
1. **개요 / 책임** — 한 줄 + 담당 범위
2. **위치 / 타입** — 파일 경로, `namespace`, MonoBehaviour/static, 싱글톤 여부
3. **참조(의존)** — 이 시스템이 호출/사용하는 다른 시스템·클래스 (무엇을 위해)
4. **피참조** — 이 시스템을 호출하는 곳 (누가, 어떤 메서드를)
5. **공개 API** — `시그니처 | 설명 | 호출 시점/주의` 표
6. **내부 동작 / 데이터 흐름** — 핵심 로직, 주요 상태 필드
7. **직렬화 필드 / 데이터 / 이벤트** — Inspector 필드, 이벤트, 외부 데이터
8. **주의 / 엣지케이스**

## 전체 의존 관계 (요약)
```
GameStateController ─ Roundmanager ─┬─ NewBattleController ─┬─ CardDeckSystem
   (씬/스테이지)        (라운드 구동)   │   (전투 코어)         ├─ CardEffectResolver ─ CardDatabase
                                      │                       ├─ ComboSkillDatabase ─ ComboEffectResolver
                                      │                       ├─ RunDeckState (런 덱/프로파일/영구공격력)
                                      │                       ├─ EnemyDisruptionAI ─ EnemyAiModel (적 AI)
                                      │                       └─ CardHandHUD ─ NewCardView (UI)
                                      ├─ CardRewardUI (보상)   
                                      └─ EnemyController ─ EnemyStat (게이지/공격/방해 hook)
NewBattleSystemBootstrap → (NewBattleController·RelicManager·RelicHUD·RunDeckState 생성/보장)
RelicManager → 전투 곳곳(각성 종료·콤보 보너스 등)에서 효과 조회
```
```
참조 방향: A → B = "A가 B를 호출/사용". 화살표 역방향 = 피참조.
```
