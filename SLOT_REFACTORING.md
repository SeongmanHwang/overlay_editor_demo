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
- `IsPlaced`는 슬롯이 실제로 캔버스에 배치되었는지(Width, Height > 0)를 나타냄
- 비활성화된 슬롯(Width = 0 또는 Height = 0)은 마킹 리딩에서 제외됨

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

### 4. OmrTemplate의 ScoringAreas 동기화 로직 변경

**파일**: `Models/OmrTemplate.cs`

**변경 사항**:
- `SyncQuestionsToScoringAreas()` 메서드에서 `IsPlaced`가 true인 슬롯만 `ScoringAreas`에 추가
- 비활성화된 슬롯은 마킹 리딩에서 제외

```csharp
private void SyncQuestionsToScoringAreas()
{
    _scoringAreas.Clear();
    foreach (var question in _questions.OrderBy(q => q.QuestionNumber))
    {
        foreach (var option in question.Options.OrderBy(o => o.OptionNumber))
        {
            if (option.IsPlaced) // Only add placed slots
            {
                _scoringAreas.Add(option);
            }
        }
    }
    OnPropertyChanged(nameof(ScoringAreas));
}
```

### 5. TemplateEditViewModel의 UX 변경

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

#### 5.2 OnCanvasClick 로직 변경
- 기존: 새로운 오버레이를 추가
- 변경: 빈 슬롯을 찾아서 배치 (Place Slot)

```csharp
private void OnCanvasClick(Point canvasPoint)
{
    if (SelectedOverlayType == OverlayType.ScoringArea)
    {
        // Find empty slot for current question
        var emptySlot = CurrentQuestion?.Options
            .FirstOrDefault(o => !o.IsPlaced);
        
        if (emptySlot != null)
        {
            // Place the slot
            emptySlot.X = canvasPoint.X;
            emptySlot.Y = canvasPoint.Y;
            emptySlot.Width = defaultWidth;
            emptySlot.Height = defaultHeight;
        }
        else
        {
            MessageBox.Show("이 문항의 모든 슬롯이 이미 사용 중입니다.");
        }
    }
    // ... other overlay types
}
```

#### 5.3 OnDeleteSelected 로직 변경
- 기존: 오버레이를 컬렉션에서 제거
- 변경: ScoringArea의 경우 좌표를 초기화하여 "unplace" (다른 타입은 여전히 삭제)

```csharp
private void OnDeleteSelected()
{
    if (SelectedOverlay?.OverlayType == OverlayType.ScoringArea)
    {
        // Unplace the slot instead of deleting
        SelectedOverlay.X = 0;
        SelectedOverlay.Y = 0;
        SelectedOverlay.Width = 0;
        SelectedOverlay.Height = 0;
    }
    else
    {
        // Delete other overlay types
        // ... deletion logic
    }
}
```

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
        if (!area.IsPlaced) continue; // Skip unplaced slots
        
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

### 8. UI 변경사항

**파일**: `Views/TemplateEditView.xaml`

**변경 사항**:
- "번호" 컬럼 재추가: `OptionNumber`를 표시 (FallbackValue: '-')
- 문항 선택 ComboBox: `QuestionNumbers` 속성에 바인딩

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
  └── Questions: ObservableCollection<Question>
      └── Options: ObservableCollection<RectangleOverlay>
          - 고정 12개 슬롯
          - OptionNumber (IdentityIndex) 보유
          - QuestionNumber 보유
          - IsPlaced로 활성/비활성 구분
  └── ScoringAreas: ReadOnlyObservableCollection<RectangleOverlay>
      - Questions에서 자동 동기화 (IsPlaced인 것만)
      - IdentityIndex 기반 마킹 리딩
```

## UX 변화

### 이전 UX
- 사용자가 오버레이를 추가/삭제할 수 있음
- 13번째 추가 시 오류 없이 추가되지만 마킹 리딩에서 문제 발생
- 중간 삭제 시 뒤의 오버레이가 당겨져서 매핑 오류 발생
- "번호"는 DisplayIndex처럼 보이지만 실제로는 IdentityIndex

### 새로운 UX
- 슬롯은 항상 12개로 고정
- 사용자는 "배치(Place)" 또는 "제거(Unplace)"만 가능
- 모든 슬롯이 사용 중이면 경고 메시지 표시
- "번호"는 명확한 IdentityIndex (OptionNumber)
- 실수로 인한 매핑 오류가 구조적으로 불가능

## 향후 개선 사항

### 1. 추가 모드(Add Mode) 제거 또는 개선

**현재 문제점**:
- 슬롯 구조에서는 "추가" 개념이 없음 (슬롯이 이미 존재)
- 모든 슬롯이 사용 중일 때 추가 모드가 켜져 있어도 아무 작업도 할 수 없음
- 사용자 혼란: "추가 모드가 필요한가?"

**제안**:
- ScoringArea의 경우 추가 모드를 제거하거나
- 추가 모드를 "배치 모드(Place Mode)"로 재명명
- 또는 추가 모드 없이 항상 배치 가능하도록 변경

### 2. 슬롯 상태 시각화

**제안**:
- DataGrid에서 비활성화된 슬롯(IsPlaced = false)을 회색으로 표시
- 또는 별도의 "활성 슬롯" / "비활성 슬롯" 필터 추가

### 3. 배치 가이드

**제안**:
- 문항별로 몇 개의 슬롯이 배치되었는지 표시 (예: "문항 1: 8/12 배치됨")
- 다음 배치할 슬롯 번호를 하이라이트

## 테스트 시나리오

### 시나리오 1: 정상적인 슬롯 배치
1. 템플릿 편집 모드 진입
2. 문항 1 선택
3. 캔버스 클릭하여 슬롯 배치 (12개)
4. 마킹 리딩 수행
5. **예상 결과**: 각 슬롯의 OptionNumber와 QuestionNumber가 정확히 매핑됨

### 시나리오 2: 모든 슬롯 사용 중
1. 문항 1의 12개 슬롯 모두 배치
2. 추가 모드 활성화
3. 캔버스 클릭
4. **예상 결과**: "이 문항의 모든 슬롯이 이미 사용 중입니다." 메시지 표시

### 시나리오 3: 슬롯 제거 후 재배치
1. 문항 1의 5번 슬롯 배치
2. 5번 슬롯 선택 후 삭제 (Unplace)
3. 다른 위치에 다시 배치
4. **예상 결과**: 5번 슬롯이 새로운 위치에 배치되고, OptionNumber는 여전히 5

### 시나리오 4: 백워드 호환성
1. 구형식 템플릿 파일 로드 (ScoringAreas만 있는 경우)
2. **예상 결과**: Questions 구조로 자동 변환되고, OptionNumber와 QuestionNumber가 올바르게 할당됨

## 결론

이번 리팩터링을 통해:

1. **사용자 실수 방지**: 구조적으로 잘못된 상태를 만들 수 없게 됨
2. **정신 모델 일치**: 사용자의 정신 모델(동그라미 좌표)과 프로그램의 정신 모델(슬롯 정체성)이 일치
3. **유지보수성 향상**: 상수 중앙화로 OMR 용지 구성 변경 시 코드 재사용 용이
4. **데이터 무결성**: IdentityIndex 기반 매핑으로 리스트 순서와 무관한 정확한 결과 보장

다만, 추가 모드(Add Mode)의 필요성에 대한 사용자 피드백을 반영하여 향후 개선이 필요합니다.
