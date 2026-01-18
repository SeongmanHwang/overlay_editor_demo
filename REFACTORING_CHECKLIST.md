# OmrConstants 변경 시 리팩토링 체크리스트

이 문서는 `Models/OmrConstants.cs`의 상수 값(`TimingMarksCount`, `BarcodeAreasCount`, `QuestionsCount`, `OptionsPerQuestion`)을 변경할 때 필요한 작업 목록입니다.

## 사전 준비

1. **현재 설정 백업**
   - `Models/OmrConstants.cs`의 현재 값 기록
   - 기존 템플릿 파일(`template.json`) 백업
   - 기존 세션 데이터(`session.json`) 백업

2. **의존성 확인**
   - 변경할 상수가 어떤 기능에 영향을 주는지 확인
   - 기존 데이터와의 호환성 검토

## 필수 작업

### 1. 모델 수정

#### 1.1 `Models/OmrSheetResult.cs`
- [ ] `Question1Marking` ~ `QuestionNMarking` 속성 추가/제거
- [ ] `QuestionsCount`가 변경된 경우, 모든 `QuestionNMarking` 속성 확인

#### 1.2 `Models/GradingResult.cs`
- [ ] `Question1Marking` ~ `QuestionNMarking` 속성 추가/제거
- [ ] `QuestionsCount`가 변경된 경우, 모든 `QuestionNMarking` 속성 확인

#### 1.3 `Models/OmrTemplate.cs`
- [ ] 생성자에서 슬롯 초기화 루프 확인
- [ ] `QuestionsCount` 변경 시 Questions 컬렉션 초기화 로직 확인
- [ ] `TimingMarksCount` 변경 시 TimingMarks 초기화 로직 확인
- [ ] `BarcodeAreasCount` 변경 시 BarcodeAreas 초기화 로직 확인

#### 1.4 `Models/Question.cs`
- [ ] 생성자에서 `OptionsPerQuestion` 사용 확인
- [ ] Options 초기화 루프 확인

### 2. Mapper 구현체 수정

#### 2.1 `Services/Mappers/QuestionResultMapper.cs`
- [ ] `OmrSheetResultMapper.SetQuestionMarking()` - switch문에 case 추가/제거
- [ ] `OmrSheetResultMapper.GetQuestionMarking()` - switch문에 case 추가/제거
- [ ] `GradingResultMapper.SetQuestionMarking()` - switch문에 case 추가/제거
- [ ] `GradingResultMapper.GetQuestionMarking()` - switch문에 case 추가/제거

**참고**: Mapper 패턴을 적용했으므로 이 파일만 수정하면 다른 코드는 자동으로 반영됩니다.

### 3. Strategy 구현체 수정

#### 3.1 `Services/Strategies/BarcodeProcessingStrategy.cs`
- [ ] `BarcodeAreasCount` 변경 시, `DefaultBarcodeProcessingStrategy.ApplyBarcodeResult()` 확인
- [ ] 새로운 바코드 의미가 필요한 경우, `OmrConstants.BarcodeSemantics` 딕셔너리 업데이트

### 4. 서비스 수정

#### 4.1 `Services/ScoringRuleStore.cs`
- [ ] `LoadScoringRuleFromXlsx()` - 엑셀 열/행 범위 확인
  - `OptionsPerQuestion` 변경 시 열 범위 수정
  - `QuestionsCount` 변경 시 행 범위 수정
- [ ] `ExportTemplate()` - 엑셀 출력 형식 확인
  - `OptionsPerQuestion` 변경 시 열 헤더 수정
  - `QuestionsCount` 변경 시 데이터 행 수정

#### 4.2 `Services/TemplateStore.cs`
- [ ] Import/Export 로직이 새로운 슬롯 개수를 올바르게 처리하는지 확인
- [ ] 기본 템플릿(`Assets/default_template.json`) 업데이트 필요 여부 확인

### 5. ViewModel 수정

#### 5.1 `ViewModels/GradingViewModel.cs`
- [ ] Mapper 사용으로 인해 자동 반영됨 (확인만)
- [ ] 배열 크기(`questionSums`, `questionCounts`)가 `QuestionsCount`와 일치하는지 확인

#### 5.2 `ViewModels/TemplateEditViewModel.cs`
- [ ] `QuestionNumbers` 프로퍼티가 올바른 범위를 반환하는지 확인
- [ ] `CurrentQuestionNumber` 유효성 검증 확인

### 6. View 수정 (필요 시)

#### 6.1 `Views/GradingView.xaml`
- [ ] DataGrid 컬럼 추가/제거
  - `QuestionsCount` 변경 시 "문항1" ~ "문항N" 컬럼 추가/제거
- [ ] 컬럼 바인딩 경로 확인

#### 6.2 `Views/ScoringRuleView.xaml`
- [ ] 점수 이름 입력 영역 컬럼 추가/제거
  - `OptionsPerQuestion` 변경 시 Grid.ColumnDefinitions 수정
  - `ScoreNames` 바인딩 인덱스 확인
- [ ] 문항별 배점 입력 영역 행 추가/제거
  - `QuestionsCount` 변경 시 행 수 확인

### 7. 기본 템플릿 수정

#### 7.1 `Assets/default_template.json`
- [ ] `TimingMarks` 배열 요소 개수 확인 (`TimingMarksCount`와 일치)
- [ ] `BarcodeAreas` 배열 요소 개수 확인 (`BarcodeAreasCount`와 일치)
- [ ] `Questions` 배열 요소 개수 확인 (`QuestionsCount`와 일치)
- [ ] 각 Question의 `Options` 배열 요소 개수 확인 (`OptionsPerQuestion`와 일치)
- [ ] 모든 슬롯에 `OptionNumber` 속성이 올바르게 설정되어 있는지 확인

### 8. 테스트

#### 8.1 단위 테스트
- [ ] `OmrConfigurationValidator` 테스트 실행
- [ ] Mapper 동작 테스트 (모든 문항 번호에 대해)
- [ ] Strategy 동작 테스트 (모든 바코드 인덱스에 대해)
- [ ] 템플릿 초기화 검증 테스트

#### 8.2 통합 테스트
- [ ] 템플릿 편집 모드 동작 확인
- [ ] 마킹 리딩 모드 동작 확인
- [ ] 채점 및 성적 처리 모드 동작 확인
- [ ] 배점 입력 모드 동작 확인

#### 8.3 수동 테스트
- [ ] 기존 템플릿 파일 로드 테스트
- [ ] 새 템플릿 생성 테스트
- [ ] 템플릿 내보내기/가져오기 테스트
- [ ] 전체 워크플로우 테스트 (템플릿 편집 → 마킹 리딩 → 채점)

## 검증

변경 후 다음 명령으로 검증을 수행하세요:

```csharp
// 애플리케이션 시작 시 자동으로 검증됩니다.
// MainWindow 생성자에서 OmrConfigurationValidator.ValidateConfiguration() 호출

// 또는 수동으로:
var result = OmrConfigurationValidator.ValidateConfiguration();
if (!result.IsValid)
{
    // 오류 메시지 출력 및 확인
    foreach (var error in result.Errors)
    {
        Console.WriteLine(error);
    }
}
```

## 롤백 계획

변경 사항에 문제가 발견되면:

1. `Models/OmrConstants.cs`를 이전 값으로 복원
2. Mapper 구현체의 switch문을 이전 상태로 복원
3. 기본 템플릿 파일 복원
4. 기존 템플릿 파일 복원

## 참고사항

- Mapper 패턴을 적용했으므로, 대부분의 변경은 Mapper 구현체만 수정하면 됩니다.
- Strategy 패턴을 적용했으므로, 바코드 의미론 변경은 `OmrConstants.BarcodeSemantics` 딕셔너리만 수정하면 됩니다.
- View 레벨의 변경은 완전 자동화되지 않았으므로, 수동 수정이 필요할 수 있습니다.
