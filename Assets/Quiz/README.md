# GMTK Quiz System

게임 실행 시 `QuizGameBootstrap`이 `QuizSystem`, 반응형 Canvas, 새 Input System용
EventSystem을 자동 생성한다. 별도 프리팹이나 Scene 설정은 필요 없다. 퀴즈는 저주를
받은 인간 플레이어에게만 나타나며 일정 시간마다 자동으로 시작되지 않는다.

## 기본 동작

- 시작 조건: 저주 시스템이 `QuizSessionController.TriggerNow()`를 호출했을 때
- 형식: 산수, 수도 상식, 수열 퍼즐, 다른 모양 찾기
- 제한 시간: 문제별 6~10초
- 피드백: 정답/오답/시간 초과와 해설 표시

기본 문제는 `ProceduralQuizQuestionSource`가 자동 생성한다. 사람이 문제를 입력하지
않아도 동작하며 외부 네트워크나 API 키에 의존하지 않는다.

## 확장 지점

- 수동 문제를 섞으려면 `Create > GMTK > Quiz > Quiz Pool`로 `QuizPool`을 만든 뒤
  Scene에 배치한 `QuizSessionController`의 `Authored Pool`에 연결한다.
- 생성형 AI나 검증된 원격 JSON 공급자를 추가하려면 `IQuizQuestionSource`를 구현한다.
- 점수, 페널티, 연출은 `QuizSessionController.QuestionStarted`,
  `AnswerEvaluated`, `QuizClosed` 이벤트에 연결한다.
- 즉시 퀴즈를 시작하려면 `QuizSessionController.TriggerNow()`를 호출한다.

인터넷 페이지를 게임 클라이언트가 직접 크롤링하는 방식은 사이트별 구조 변경,
저작권, 오답 검증 문제 때문에 기본 구현에 포함하지 않는다. 원격 문제를 사용할
경우 서버에서 출처와 정답을 검증한 뒤 공통 `QuizQuestion` 형식으로 전달한다.
