# 슬롯 구조 리팩터링 문서

## 배경 및 목적

### 문제점

기존 설계에서는 채점 영역(ScoringArea) 오버레이를 동적으로 추가/삭제할 수 있었습니다. 이로 인해 다음과 같은 문제가 발생했습니다:

1. **사용자 실수 가능성**: 사용자가 13번째 오버레이를 추가하거나, 중간 오버레이를 삭제하면 마킹 리딩 결과가 잘못 매핑될 수 있었습니다.
2. **정신 모델 불일치**: 
   - 사용자의 정신 모델: "동그라미 좌표"에 오버레이를 배치
   - 프로그램의 정신 모델: "리스트 순서"로 마킹 리딩 매핑
   - 두 세계가 어긋나면 **겉보기로는 정상인데 결과만 틀리는** 위험한 상황 발생
3. **숨은 규칙**: 사용자가 사실상 외워야 하는 규칙들:
   - 문항마다 항상 정확히 12개여야 함
   - 삭제하면 안 됨
   - 추가로 만들면 안 됨
   - 순서를 바꾸면 안 됨
   - "번호"는 단순한 줄 번호가 아니라 실제 의미를 가진 키

### 해결 방향

**"실수를 불가능하게 만드는 UX"**를 구현하기 위해 다음과 같은 설계를 채택했습니다:

1. **고정 슬롯 구조**: 12개 × 4문항 슬롯을 고정하고, 추가/삭제가 불가능하도록 구조화
2. **정체성 기반 매핑**: 각 슬롯이 (문항 번호, 선택지 번호) 정체성을 갖고, 마킹 리딩은 리스트 순서가 아니라 이 번호를 기준으로 매핑
3. **중앙화된 상수 관리**: OMR 용지 구성 변경 시 개발/빌드 단계에서 코드를 재사용하기 쉽도록 상수를 중앙화

## 주요 변경 사항

### 1. OmrConstants 클래스 도입

**파일**: `Models/OmrConstants.cs`

```csharp
public static class OmrConstants
{
    public static int TimingMarksCount => 5;
    public static int BarcodeAreasCount => 2;
    public static int QuestionsCount => 4;
    public static int OptionsPerQuestion => 12;
    public static int TotalScoringAreas => QuestionsCount * OptionsPerQuestion;
}
```

**목적**: 
- OMR 용지 구성 변경 시 한 곳에서만 수정하면 전체 코드에 반영
- 개발/빌드 단계에서 코드 재사용 용이

**사용 위치**:
- `Question.cs`: 초기 슬롯 생성
- `OmrTemplate.cs`: Questions 초기화 및 ScoringAreas 동기화
- `TemplateEditViewModel.cs`: QuestionNumbers 생성
- `MarkingDetector.cs`: 마킹 리딩 로직
- `MarkingAnalyzer.cs`: 결과 분석
- `ScoringRule.cs`: 배점 규칙
- `StateStore.cs`, `TemplateStore.cs`, `TemplateViewModel.cs`: 백워드 호환성

### 2. RectangleOverlay에 정체성 속성 추가

**파일**: `Models/RectangleOverlay.cs`

**추가된 속성**:
- `OptionNumber` (int?): 선택지 번호 (1-12, IdentityIndex)
- `QuestionNumber` (int?): 문항 번호 (1-4)
- `IsPlaced` (bool, 계산된 속성): 슬롯이 실제로 배치되었는지 여부

```csharp
public bool IsPlaced => OptionNumber.HasValue && Width > 0 && Height > 0;
```

**의미**:
- `OptionNumber`와 `QuestionNumber`는 **IdentityIndex**로, 리스트 순서와 무관하게 고정된 의미를 가짐
- `IsPlaced`는 슬롯이 실제로 유효한 ROI인지(Width, Height > 0)를 나타냄
- 현재 구현에서는 **추가/삭제/배치(Place) 개념을 UI에서 제거**했기 때문에, `IsPlaced`는 주로 “유효하지 않은 크기 ROI를 리딩 단계에서 안전하게 처리”하는 데 사용됨

### 3. Question 클래스의 고정 슬롯 구조

**파일**: `Models/Question.cs`

**변경 사항**:
- 생성자에서 `OmrConstants.OptionsPerQuestion` 개수만큼 슬롯을 자동 생성
- 각 슬롯에 `OptionNumber` (1-12)를 자동 할당
- `QuestionNumber` 변경 시 모든 옵션의 `QuestionNumber`도 자동 업데이트

```csharp
public Question()
{
    for (int i = 1; i <= OmrConstants.OptionsPerQuestion; i++)
    {
        var slot = new RectangleOverlay
        {
            OptionNumber = i,
            QuestionNumber = null, // Will be set by parent OmrTemplate
            Width = 0,
            Height = 0,
            OverlayType = OverlayType.ScoringArea
        };
        _options.Add(slot);
    }
}
```

**효과**:
- 슬롯 개수가 항상 정확히 12개로 고정됨
- 추가/삭제가 구조적으로 불가능해짐

### 4. OmrTemplate의 고정 슬롯 구조 확장 (Timing/Barcode 포함) 및 ScoringAreas 동기화 정책 변경

**파일**: `Models/OmrTemplate.cs`

**변경 사항**:
- **TimingMarks는 5개, BarcodeAreas는 2개 고정 슬롯**으로 생성됨 (추가/삭제 불가)
- `ScoringAreas`는 `Questions.Options`의 **48개 슬롯을 항상 투영**하여 개수가 고정됨 (필터링 제거)
  - 목적: 리딩/렌더링/분석에서 “영역 수 부족/인덱스 흔들림”을 방지하고, 항상 동일한 구조를 보장

```csharp
private void SyncQuestionsToScoringAreas()
{
    _scoringAreas.Clear();
    foreach (var question in _questions.OrderBy(q => q.QuestionNumber))
    {
        foreach (var option in question.Options.OrderBy(o => o.OptionNumber))
        {
            // 고정 슬롯 구조: 항상 포함 (48개 고정)
            _scoringAreas.Add(option);
        }
    }
    OnPropertyChanged(nameof(ScoringAreas));
}
```

### 5. TemplateEditViewModel의 UX 변경 (전 타입 잠금)

**파일**: `ViewModels/TemplateEditViewModel.cs`

**주요 변경 사항**:

#### 5.1 QuestionNumbers 속성 추가
```csharp
public IEnumerable<int> QuestionNumbers
{
    get
    {
        for (int i = 1; i <= OmrConstants.QuestionsCount; i++)
        {
            yield return i;
        }
    }
}
```

#### 5.2 “추가/삭제” UX 제거 (전 타입 잠금)
- 기존(과거 단계): Place/Unplace 또는 Add/Delete 개념이 존재
- 현재 구현(최종): **추가/삭제(=unplace 포함) 기능 자체를 제거**하고, 사용자는 **이미 존재하는 슬롯 오버레이의 위치/크기만 수정**함
  - Timing/Barcode/Scoring 모두 동일 정책
  - 편집은 선택/다중선택 + 좌표/크기 수정(텍스트박스, 방향키) + 정렬 정도로 제한됨

### 6. MarkingDetector의 IdentityIndex 기반 매핑

**파일**: `Services/MarkingDetector.cs`

**변경 사항**:
- 기존: `areaIndex`를 기반으로 `QuestionNumber`와 `OptionNumber` 계산
- 변경: `RectangleOverlay`의 `QuestionNumber`와 `OptionNumber`를 직접 사용

```csharp
public List<MarkingResult> DetectMarkings(/* ... */)
{
    var results = new List<MarkingResult>();
    
    foreach (var area in scoringAreas)
    {
        // 고정 슬롯 구조: 항상 48개를 순회하되,
        // 유효하지 않은 크기(Width/Height <= 0)는 내부에서 안전하게 IsMarked=false 처리됨
        var marking = DetectMarkingInArea(/* ... */, area.OptionNumber.Value);
        
        marking.QuestionNumber = area.QuestionNumber.Value;
        marking.OptionNumber = area.OptionNumber.Value;
        
        results.Add(marking);
    }
    
    return results;
}
```

**효과**:
- 리스트 순서와 무관하게 정확한 매핑 보장
- UI에서 재정렬이 일어나도 의미가 유지됨

### 7. TemplateStore 및 StateStore의 백워드 호환성

**파일**: `Services/TemplateStore.cs`, `Services/StateStore.cs`

**변경 사항**:
- 저장 시: `OptionNumber`와 `QuestionNumber`를 JSON에 포함
- 로드 시: 
  - 새로운 형식(Questions 구조) 우선 로드
  - 구형식(ScoringAreas만 있는 경우)은 `OmrConstants`를 사용하여 Questions 구조로 변환
  - 변환 시 각 오버레이에 `OptionNumber`와 `QuestionNumber` 할당
  - **고정 슬롯 정책**: 로드/가져오기 과정에서 컬렉션에 `Add()`로 슬롯 개수를 늘리지 않고, 항상 기존 슬롯에 매핑하여 좌표/크기만 업데이트

추가로, 새 PC 첫 실행을 위해:
- `Assets/default_template.json`을 앱에 번들하고, AppData에 `template.json`이 없으면 자동으로 설치/로드합니다.

### 8. UI 변경사항

**파일**: `Views/TemplateEditView.xaml`

**변경 사항**:
- "번호" 컬럼 재추가: `OptionNumber`를 표시 (FallbackValue: '-')
- 문항 선택 ComboBox: `QuestionNumbers` 속성에 바인딩
- 템플릿 편집 화면에서 **추가 모드/삭제 버튼 제거**(전 타입 잠금)
- 캔버스에 슬롯 라벨 표시:
  - Timing/Barcode: 숫자(OptionNumber)만
  - Scoring: 숫자(OptionNumber)만, 그리고 **드롭다운에서 선택된 문항만 라벨 표시**(UI 일관성)

```xml
<DataGridTextColumn Header="번호" 
                    Binding="{Binding OptionNumber, FallbackValue='-'}" 
                    Width="50" 
                    IsReadOnly="True"/>

<ComboBox SelectedItem="{Binding CurrentQuestionNumber, Mode=TwoWay, FallbackValue={x:Null}}"
          Margin="3,0"
          VerticalAlignment="Center"
          MinWidth="100"
          FontSize="11"
          Visibility="{Binding IsQuestionNumberVisible, Converter={StaticResource BoolToVisibilityConverter}, FallbackValue=Collapsed}">
    <ComboBox.ItemsSource>
        <Binding Path="QuestionNumbers"/>
    </ComboBox.ItemsSource>
</ComboBox>
```

## 데이터 모델 변화

### 이전 구조
```
OmrTemplate
  └── ScoringAreas: ObservableCollection<RectangleOverlay>
      - 동적 추가/삭제 가능
      - 리스트 순서로 마킹 리딩 매핑
      - areaIndex 기반 계산
```

### 새로운 구조
```
OmrTemplate
  └── TimingMarks: ObservableCollection<RectangleOverlay>
      - 고정 {TimingMarksCount}개 슬롯 (OptionNumber=1..N)
  └── BarcodeAreas: ObservableCollection<RectangleOverlay>
      - 고정 {BarcodeAreasCount}개 슬롯 (OptionNumber=1..N)
  └── Questions: ObservableCollection<Question>
      └── Options: ObservableCollection<RectangleOverlay>
          - 고정 12개 슬롯
          - OptionNumber (IdentityIndex) 보유
          - QuestionNumber 보유
  └── ScoringAreas: ReadOnlyObservableCollection<RectangleOverlay>
      - Questions에서 자동 동기화 (항상 48개)
      - IdentityIndex 기반 마킹 리딩
```

## UX 변화

### 이전 UX
- 사용자가 오버레이를 추가/삭제할 수 있음
- 13번째 추가 시 오류 없이 추가되지만 마킹 리딩에서 문제 발생
- 중간 삭제 시 뒤의 오버레이가 당겨져서 매핑 오류 발생
- "번호"는 DisplayIndex처럼 보이지만 실제로는 IdentityIndex

### 새로운 UX
- Timing/Barcode/Scoring 모두 **고정 슬롯**이며, 사용자는 **추가/삭제를 할 수 없음**
- 사용자는 오직 **기존 슬롯의 위치/크기만 수정**
- "번호"는 명확한 IdentityIndex (OptionNumber)로 UI/리딩/분석이 일치
- 첫 실행 시 **기본 템플릿이 자동으로 설치/로드**되어 바로 편집 가능

## 향후 개선 사항 (선택)

### 1. 기본 템플릿 튜닝 워크플로우
- `Assets/default_template.json`은 샘플 용지/해상도에 맞게 지속적으로 보정이 필요할 수 있음
- “샘플 이미지 로드 → 좌표 조정 → 내보내기 → 기본 템플릿 교체” 방식의 운영 가이드 문서화 권장

## 테스트 시나리오

### 시나리오 1: 첫 실행 기본 템플릿 자동 설치
1. 새 PC(또는 AppData 초기화)에서 앱 실행
2. 템플릿 편집 모드 진입
3. **예상 결과**: `template.json`이 없으면 번들된 `Assets/default_template.json` 기반으로 자동 생성/저장되고, 모든 슬롯이 보이며 편집 가능

### 시나리오 2: 추가/삭제 불가 확인(전 타입 잠금)
1. 템플릿 편집 모드 진입
2. **예상 결과**: 추가 모드/삭제 버튼이 없고, 캔버스 클릭으로 새 오버레이가 생성되지 않음 (오직 기존 슬롯의 이동/크기 변경만 가능)

### 시나리오 3: 라벨 표시 정책(Scoring은 선택 문항만)
1. 템플릿 편집 모드에서 ScoringArea 선택
2. 문항 드롭다운을 1로 선택
3. **예상 결과**: 문항 1의 선택지(1~12) 라벨만 보이고, 나머지 문항의 라벨은 숨김

### 시나리오 4: 마킹 리딩 개수/정합성
1. 템플릿 편집 모드에서 48개 Scoring 슬롯이 모두 유효한 크기/좌표를 갖도록 조정
2. 마킹 리딩 수행
3. **예상 결과**: `ScoringAreas.Count == 48`이고, 결과 분석에서 “영역 수 부족” 오류가 발생하지 않음

### 시나리오 4: 백워드 호환성
1. 구형식 템플릿 파일 로드 (ScoringAreas만 있는 경우)
2. **예상 결과**: Questions 구조로 자동 변환되고, OptionNumber와 QuestionNumber가 올바르게 할당됨

## 결론

이번 리팩터링을 통해:

1. **사용자 실수 방지**: 구조적으로 잘못된 상태를 만들 수 없게 됨
2. **정신 모델 일치**: 사용자의 정신 모델(동그라미 좌표)과 프로그램의 정신 모델(슬롯 정체성)이 일치
3. **유지보수성 향상**: 상수 중앙화로 OMR 용지 구성 변경 시 코드 재사용 용이
4. **데이터 무결성**: IdentityIndex 기반 매핑으로 리스트 순서와 무관한 정확한 결과 보장

또한 현재 구현은 “전 타입 잠금” 정책에 맞춰 **추가/삭제 경로를 제거**하고, **기본 템플릿 자동 설치**를 통해 새 환경에서도 즉시 사용 가능하도록 정리되었습니다.
