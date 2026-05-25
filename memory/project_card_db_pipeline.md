---
name: project-card-db-pipeline
description: 카드 데이터는 Card_DB.xlsx → Tools/ConvertCardDB.py → Assets/Resources/CardDB/*.json 파이프라인으로 관리. 기획자가 Excel만 수정하면 게임에 반영되도록 설계.
metadata:
  type: project
---

카드 DB는 기획자가 Excel만 교체해도 반영되는 워크플로우.

**Why:** 팀 프로젝트에서 기획자가 코드 수정 없이 데이터를 갱신하기 위함. 사용자가 명시적으로 요청한 설계.

**How to apply:**
- 카드 메타데이터(이름/효과 텍스트/게이지/태그) 변경은 [Card_DB.xlsx](C:\Users\dusdn\Downloads\Card_DB.xlsx)에서. `python Tools/ConvertCardDB.py` 한 줄로 [Assets/Resources/CardDB/Cards.json](Assets/Resources/CardDB/Cards.json)과 [CardEffects.json](Assets/Resources/CardDB/CardEffects.json) 재생성됨.
- 코드 안에 카드 정의를 다시 하드코딩하지 말 것 — `CardDatabase.cs`는 JSON 로더로만 동작.
- ID 체계: FIRE 100~121, WATER 200~221, WIND 300~321, EARTH 400~421, NEUTRAL 500 (방해 더미), FRAGMENT 501~503.
- `CardDatabase.FRAGMENT_1/2/3` = 501/502/503, `NEUTRAL_FILLER` = 500.
- 효과 엔진은 **완전 데이터 드리븐**. When/If/Formula/HitFormula 인터프리터가 [CardEffectResolver.cs](Assets/Script/Battle/Deck/CardEffectResolver.cs)에 구현되어 있어 새 카드는 Excel만 채우면 동작. 미구현 동사/공식이 필요하면 resolver에 추가.
- Excel을 통한 갱신 경로 두 가지: (1) Unity 메뉴 `Tools > Card DB > Excel → JSON 변환` (권장, [Assets/Editor/CardDBImporter.cs](Assets/Editor/CardDBImporter.cs)), (2) `python Tools/ConvertCardDB.py` (CLI).

관련: [[user-namtik]]
