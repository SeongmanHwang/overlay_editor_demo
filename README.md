# Simple Overlay Editor

OMR(Optical Mark Recognition) 시트의 오버레이를 편집하고 마킹을 리딩하는 WPF 애플리케이션입니다.

## 목차

- [프로젝트 개요](#프로젝트-개요)
- [프로젝트 메트릭](#프로젝트-메트릭)
- [로드맵 / TODO](#로드맵--todo)
- [프로젝트 구조](#프로젝트-구조)
- [애플리케이션 모드 및 아키텍처](#애플리케이션-모드-및-아키텍처)
- [아키텍처 패턴 상세](#아키텍처-패턴-상세)
- [성능 최적화 기법](#성능-최적화-기법)
- [애플리케이션 생애주기](#애플리케이션-생애주기)
- [기술 스택](#기술-스택)
- [빌드 및 실행](#빌드-및-실행)
- [사용 방법](#사용-방법)
- [저장 위치](#저장-위치)
- [로그 파일](#로그-파일)
- [주의사항](#주의사항)

## 프로젝트 메트릭

이 섹션은 프로젝트 규모(파일 수/라인 수)를 **날짜와 함께 자동 기록**합니다.

- **갱신 방법**:
  - PowerShell:
    - `powershell -ExecutionPolicy Bypass -File tools\\metrics\\project-metrics.ps1 -Root . -WriteMarkdownReport docs\\metrics\\latest.md -UpdateReadme README.md`
- **집계에서 제외되는 항목**: `bin/`, `obj/`, `.git/`, `tools/metrics/`, `docs/metrics/`

<!-- METRICS:START -->
#### Project metrics (2026-01-25)

- Root: "overlay_editor"
- Files (excluding bin/obj/.git/tools/metrics/docs/metrics): **102**
- Code-like files (.cs, .xaml, .csproj, .json, .md, .xml): **99**
- Total lines (code-like): **24498**

**By extension (lines)**

| Ext | Files | Lines |
|---:|---:|---:|
| .cs | 81 | 18529 |
| .xaml | 12 | 3509 |
| .md | 2 | 1287 |
| .json | 3 | 1128 |
| .csproj | 1 | 45 |
| .xml | 0 | 0 |

**Top folders by lines**

| Folder | Lines |
|---|---:|
| ViewModels | 6643 |
| Services | 5884 |
| Views | 5267 |
| Models | 1946 |
| . | 1929 |
| Assets | 1128 |
| Utils | 850 |
| Tests | 264 |
| Services\Mappers | 236 |
| Utils\Commands | 172 |
| Services\Validators | 93 |
| Services\Strategies | 86 |
<!-- METRICS:END -->

## 프로젝트 개요

이 애플리케이션은 OMR 시트의 템플릿을 제작하고, 스캔된 이미지에서 마킹을 자동으로 리딩하며, 바코드를 읽고 채점까지 수행하는 통합 도구입니다. 주요 기능은 다음과 같습니다:

- **템플릿 편집**: 타이밍 마크, 채점 영역, 바코드 영역을 시각적으로 편집
- **이미지 정렬**: 타이밍 마크를 기반으로 스캔 이미지 자동 정렬
- **마킹 리딩**: 채점 영역에서 마킹 여부 자동 판단
- **바코드 디코딩**: 바코드 영역에서 자동으로 바코드 텍스트 추출
- **채점 및 성적 처리**: 자동 채점 및 석차 계산
- **명렬 관리**: 수험생/면접위원 명렬 관리
- **상태 관리**: 작업 상태를 자동 저장하여 재실행 시 복구

### 주요 개선사항 (최근 리팩터링)

**OMR Ingest 파이프라인 개선**:
- **누적 가능한 ingest 파이프라인**: 폴더 로드를 여러 번 수행해도 결과가 누적되어 추가 가능
- **정렬 실패 문서 처리 개선**: 정렬 실패 시 원본 이미지로 fallback하지 않고 명시적으로 처리 제외하여 데이터 품질 보장
- **바코드 디코딩 최적화**: 폴더 로드 시점에만 바코드를 디코딩하여 중복 처리 방지 및 성능 향상
- **중복 허용 정책**: 동일 CombinedId를 가진 문서도 모두 유지하여 실제 중복 상황을 정확히 파악 가능
- **파일명 기반 스킵**: 동일 파일명의 이미지는 자동으로 스킵하여 중복 로드 방지
- **즉시 UI 피드백**: 폴더 로드 직후 바코드 결과를 기반으로 수험번호/면접번호가 즉시 하단뷰에 표시
- **미리딩 전용 리딩**: 아직 마킹 리딩이 되지 않은 문서만 선별하여 빠르게 리딩 가능
- **상세한 ingest 상태 추적**: 정렬 실패, 바코드 실패, ID 누락 등 원인별로 상태를 추적하고 요약 표시

## 로드맵 / TODO

아래 항목들은 기능 확장 및 운영 편의성을 위한 우선 과제입니다.

- **OMR 리딩을 부분적으로 수행하여 누적 추가 가능하도록 개선**
  - 폴더 전체 리딩뿐 아니라 **일부만 리딩 후 결과를 세션에 누적**
  - 특히 **스캐너에서 바로 스캔되는 이미지를 지속적으로 수집/리딩**할 수 있는 입력 방식 지원


## 프로젝트 구조

```
overlay_editor/
├── SimpleOverlayEditor.csproj    # 프로젝트 파일
├── Directory.Build.props          # 빌드 속성 설정
├── App.xaml / App.xaml.cs         # 애플리케이션 진입점
│
├── Models/                        # 데이터 모델
│   ├── RectangleOverlay.cs        # 직사각형 오버레이 데이터
│   ├── ImageDocument.cs           # 이미지 문서 (이미지 정보)
│   ├── Workspace.cs               # 프로그램 전반 상태 (템플릿, 입력 폴더 경로)
│   ├── Session.cs                 # 이미지 로드 및 리딩 작업 세션 (문서 목록, 마킹/바코드 결과, ingest 상태)
│   ├── OmrTemplate.cs             # OMR 템플릿 (타이밍 마크, 채점 영역, 바코드 영역, 문항)
│   ├── Question.cs                # 문항 모델 (4개 문항, 각 12개 선택지)
│   ├── OverlayType.cs             # 오버레이 타입 열거형 (TimingMark, ScoringArea, BarcodeArea)
│   ├── MarkingResult.cs           # 마킹 리딩 결과 모델 (문항/선택지 번호 포함)
│   ├── BarcodeResult.cs           # 바코드 디코딩 결과 모델
│   ├── AlignmentInfo.cs           # 이미지 정렬 정보 모델
│   ├── OmrSheetResult.cs          # OMR 시트 결과 (문항별 마킹 결과, 바코드 결과)
│   ├── GradingResult.cs           # 채점 결과 모델
│   ├── ScoringRule.cs             # 배점 규칙 모델
│   ├── StudentRegistry.cs         # 수험생 명렬 모델
│   ├── InterviewerRegistry.cs     # 면접위원 명렬 모델
│   ├── ApplicationMode.cs         # 애플리케이션 모드 열거형
│   ├── OmrConstants.cs            # OMR 구조 상수 (중앙화된 상수 관리)
│   ├── IngestDocState.cs          # ingest 문서 상태 모델 (정렬/바코드/ID 상태 추적)
│   ├── IngestFailureReason.cs     # ingest 실패 원인 열거형
│   ├── LoadFailureItem.cs         # 로드 실패 항목 모델
│   ├── DataUsageItem.cs           # 데이터 사용 항목 모델
│   └── RoundInfo.cs               # 회차 정보 모델
│
├── ViewModels/                    # 뷰모델 (MVVM)
│   ├── NavigationViewModel.cs     # 네비게이션 관리 뷰모델
│   ├── HomeViewModel.cs           # 홈 화면 뷰모델
│   ├── TemplateEditViewModel.cs   # 템플릿 편집 뷰모델
│   ├── TemplateViewModel.cs       # 템플릿 관리 뷰모델
│   ├── MarkingViewModel.cs        # 마킹 리딩 뷰모델 (partial class)
│   │   ├── MarkingViewModel.Session.cs      # 세션 관리
│   │   ├── MarkingViewModel.Commands.cs     # 커맨드 처리
│   │   ├── MarkingViewModel.Export.cs       # Excel 내보내기
│   │   ├── MarkingViewModel.Filters.cs     # 필터링
│   │   ├── MarkingViewModel.LoadFailure.cs  # 로드 실패 처리
│   │   └── MarkingViewModel.Rendering.cs   # 렌더링
│   ├── GradingViewModel.cs        # 채점 및 성적 처리 뷰모델
│   ├── RegistryViewModel.cs       # 명렬 관리 뷰모델
│   ├── ScoringRuleViewModel.cs    # 정답 및 배점 관리 뷰모델
│   ├── ManualVerificationViewModel.cs # 수기 검산 뷰모델
│   ├── SingleStudentVerificationViewModel.cs # 단일 학생 검산 뷰모델 (Grading 서브모드)
│   ├── OmrVerificationCore.cs      # 검산 공통 코어 (Manual/SingleStudent 공용)
│   ├── OverlaySelectionViewModel.cs # 오버레이 선택 뷰모델
│   ├── QuestionVerificationRow.cs   # 문항 검증 행 모델
│   └── RelayCommand.cs            # 커맨드 패턴 구현
│
├── Views/                         # UI 뷰
│   ├── MainWindow.xaml / .cs      # 메인 윈도우
│   ├── HomeView.xaml / .cs        # 홈 화면
│   ├── TemplateEditView.xaml / .cs # 템플릿 편집 화면
│   ├── MarkingView.xaml / .cs     # 마킹 리딩 화면
│   ├── GradingView.xaml / .cs     # 채점 및 성적 처리 화면
│   ├── RegistryView.xaml / .cs    # 명렬 관리 화면
│   ├── ScoringRuleView.xaml / .cs # 정답 및 배점 화면
│   ├── ManualVerificationView.xaml / .cs # 수기 검산 화면
│   ├── SingleStudentVerificationView.xaml / .cs # 단일 학생 검산 화면
│   └── ProgressWindow.xaml / .cs  # 진행률 표시 윈도우
│
├── Services/                      # 비즈니스 로직 서비스
│   ├── PathService.cs             # 경로 관리 (AppData, InputFolder)
│   ├── StateStore.cs              # state.json 저장/로드 (프로그램 상태)
│   ├── SessionStore.cs            # session.json 저장/로드 (이미지 로드 및 리딩 세션)
│   ├── TemplateStore.cs           # 템플릿 저장/로드 (template.json)
│   ├── RegistryStore.cs           # 명렬 저장/로드
│   ├── ScoringRuleStore.cs        # 배점 규칙 저장/로드
│   ├── ImageLoader.cs             # 이미지 파일 로드
│   ├── ImageAlignmentService.cs   # 타이밍 마크 기반 이미지 정렬 서비스
│   ├── Renderer.cs                # 오버레이 + 이미지 합성 → output/
│   ├── MarkingDetector.cs         # 마킹 리딩 서비스 (ROI 분석)
│   ├── MarkingAnalyzer.cs        # 마킹 결과 분석 서비스 (OmrSheetResult 생성)
│   ├── BarcodeReaderService.cs    # 바코드 디코딩 서비스 (ZXing.Net 사용)
│   ├── Logger.cs                  # 로깅 서비스 (파일 로그)
│   ├── AppStateStore.cs           # 앱 상태 저장/로드 (회차 관리)
│   ├── DataUsageService.cs        # 데이터 사용 서비스
│   ├── DuplicateDetector.cs       # 중복 검출 서비스
│   ├── GradingCalculator.cs       # 채점 계산 서비스
│   ├── OmrAnalysisCache.cs        # OMR 분석 캐시
│   ├── ProgressRunner.cs          # 진행률 표시 러너
│   ├── Mappers/                   # Mapper 패턴 구현
│   │   └── QuestionResultMapper.cs # 문항 결과 매핑 (Template Method Pattern)
│   ├── Strategies/                # Strategy 패턴 구현
│   │   └── BarcodeProcessingStrategy.cs # 바코드 처리 전략
│   └── Validators/                # 검증 서비스
│       └── OmrConfigurationValidator.cs # OMR 설정 검증
│
└── Utils/                         # 유틸리티
    ├── Commands/                  # Undo/Redo 지원 커맨드
    │   ├── IUndoableCommand.cs    # Undo 가능한 커맨드 인터페이스
    │   ├── AddOverlayCommand.cs   # 오버레이 추가 커맨드
    │   ├── DeleteOverlayCommand.cs # 오버레이 삭제 커맨드
    │   ├── AlignLeftCommand.cs    # 왼쪽 정렬 커맨드
    │   └── AlignTopCommand.cs     # 위 정렬 커맨드
    ├── UndoManager.cs             # Undo/Redo 관리자
    ├── CoordinateConverter.cs     # 화면 좌표 ↔ 원본 픽셀 좌표 변환
    ├── ZoomHelper.cs              # 줌/피트 계산 (Uniform 스케일)
    ├── Converters.cs              # XAML 데이터 바인딩 컨버터
    ├── DataGridMultiSortHelper.cs # DataGrid 다중 정렬 헬퍼
    ├── OmrFilterUtils.cs          # OMR 필터 유틸리티
    └── UiThread.cs                # UI 스레드 유틸리티
```

## 애플리케이션 모드 및 아키텍처

이 애플리케이션은 8가지 모드를 지원하며, 각 모드는 독립적인 ViewModel과 View를 가진 MVVM 아키텍처로 구성되어 있습니다.

### 1. Home 모드
**목적**: 애플리케이션 진입점 및 모드 선택 화면

**구현 위치**: `HomeViewModel`, `HomeView`

**아키텍처 패턴**:
- **MVVM 패턴**: View와 ViewModel 완전 분리
- **Navigation 패턴**: `NavigationViewModel`을 통한 모드 전환

**특징**:
- 단순한 라우팅 역할만 수행
- 각 모드로 이동하는 버튼 제공

---

### 2. TemplateEdit 모드
**목적**: OMR 템플릿 편집 (타이밍 마크, 채점 영역, 바코드 영역)

**구현 위치**: `TemplateEditViewModel`, `TemplateEditView`

**아키텍처 패턴**:
- **고정 슬롯 구조 패턴**: 슬롯 추가/삭제 불가, 위치/크기만 편집
- **중앙화된 상수 관리**: `OmrConstants` 클래스를 통한 구조 상수 관리
- **Command 패턴**: Undo/Redo 지원 (`IUndoableCommand`, `UndoManager`)

**최적화 방법**:
- **고정 슬롯 구조**: 4문항 × 12선택지 = 48개 고정 슬롯으로 실수 방지
- **ID 기반 매핑**: `OptionNumber`, `QuestionNumber`를 IdentityIndex로 사용하여 리스트 순서와 무관한 정확한 매핑 보장
- **자동 동기화**: `ScoringAreas`는 `Questions.Options`에서 자동 동기화 (ReadOnlyObservableCollection)

**데이터 모델**:
- `OmrTemplate.Questions`: 구조화된 문항 데이터 (4개 문항, 각 12개 선택지)
- `OmrTemplate.ScoringAreas`: Questions에서 자동 동기화된 읽기 전용 컬렉션
- `RectangleOverlay.OptionNumber/QuestionNumber`: IdentityIndex로 사용

**주요 기능**:
- 고정 슬롯의 위치/크기 편집 (추가/삭제 불가)
- 문항별 채점 영역 관리
- 템플릿 내보내기/가져오기
- 기본 템플릿 자동 설치 (`Assets/default_template.json`)

---

### 3. Marking 모드
**목적**: 이미지 로드, 정렬, 마킹 리딩, 바코드 디코딩

**구현 위치**: `MarkingViewModel`, `MarkingView`

**아키텍처 패턴**:
- **Service 패턴**: `MarkingDetector`, `BarcodeReaderService`, `ImageAlignmentService` 등 서비스 분리
- **Strategy 패턴**: `BarcodeProcessingStrategy`를 통한 바코드 의미 처리
- **Observer 패턴**: `ICollectionView`를 통한 View 레벨 정렬/필터링
- **Ingest 파이프라인 패턴**: 폴더 로드 → 정렬 → 바코드 디코딩 → 상태 추적의 단계별 처리

**최적화 방법**:
1. **병렬 처리** (`Parallel.ForEach`):
   - 이미지 로드: CPU 코어 수만큼 병렬 처리
   - 전체 마킹 리딩: 여러 이미지 동시 처리
   - 폴더 로드 시 정렬 및 바코드 디코딩: 여러 이미지 동시 처리
   - 스레드 안전 컬렉션 사용: `ConcurrentBag`, `ConcurrentDictionary`
   - 진행률 업데이트: `lock`으로 동기화

2. **이미지 정렬 캐싱**:
   - 정렬된 이미지를 `aligned_cache/` 폴더에 저장
   - 재실행 시 캐시 재사용으로 정렬 재계산 방지
   - `AlignmentInfo`에 캐시 경로 저장

3. **바코드 디코딩 최적화**:
   - **폴더 로드 시점에만 수행**: 리딩 단계에서 중복 디코딩 방지
   - 정렬 성공한 문서만 대상으로 처리하여 불필요한 작업 제거
   - 결과를 `Session.BarcodeResults`에 저장하여 재사용

4. **비동기 처리** (`async/await`):
   - UI 스레드 블로킹 방지
   - `Task.Run`으로 CPU 집약적 작업 백그라운드 실행
   - `CancellationToken`으로 작업 취소 지원

5. **메모리 최적화**:
   - `BitmapDecoder`의 `DelayCreation` 옵션으로 이미지 메타데이터만 읽기
   - 처리 완료 후 명시적 참조 해제로 GC 촉진

6. **View 레벨 정렬** (`ICollectionView.SortDescriptions`):
   - 데이터 컬렉션은 정렬하지 않고 View에서만 정렬
   - MVVM 패턴 준수, 데이터와 View 분리

7. **Ingest 상태 추적**:
   - 각 문서의 정렬/바코드/ID 상태를 `IngestDocState`로 추적
   - 원인별 집계(정렬 실패, 바코드 실패, ID 누락 등)를 즉시 확인 가능

**주요 기능**:
- 폴더에서 이미지 로드 (병렬 처리)
- 타이밍 마크 기반 자동 정렬
- **폴더 로드 시 자동 바코드 디코딩**: 정렬 성공한 문서에 대해 자동으로 바코드 디코딩 수행
- 단일/전체 이미지 마킹 리딩 (병렬 처리)
- **미리딩만 전체 리딩**: 아직 마킹 리딩이 되지 않은 문서만 선별하여 빠르게 리딩
- OMR 결과 요약 및 Excel 내보내기
- 오류 필터링 (중복, 오류만 표시 등)
- **파일명 기반 중복 스킵**: 동일 파일명의 이미지는 자동으로 스킵
- **즉시 UI 피드백**: 폴더 로드 직후 바코드 결과를 기반으로 수험번호/면접번호가 하단뷰에 즉시 표시

---

### 4. Registry 모드
**목적**: 수험생/면접위원 명렬 관리

**구현 위치**: `RegistryViewModel`, `RegistryView`

**아키텍처 패턴**:
- **Service 패턴**: `RegistryStore`를 통한 명렬 저장/로드
- **Excel 연동**: `ClosedXML` 라이브러리 사용

**최적화 방법**:
- Excel 파일 직렬화/역직렬화 최적화
- 양식 템플릿 자동 생성

**주요 기능**:
- 수험생 명부 로드/저장/내보내기 (Excel)
- 면접위원 명부 로드/저장/내보내기 (Excel)
- 양식 템플릿 자동 생성

---

### 5. Grading 모드
**목적**: 채점 및 성적 처리 (면접위원별 점수 평균, 석차 계산)

**구현 위치**: `GradingViewModel`, `GradingView`

**아키텍처 패턴**:
1. **Mapper 패턴** (Template Method Pattern):
   - `IQuestionResultMapper<T>` 인터페이스로 문항 결과 매핑 추상화
   - `OmrSheetResultMapper`: `OmrSheetResult`용 Mapper
   - `GradingResultMapper`: `GradingResult`용 Mapper
   - 문항 수 변경 시 Mapper 구현체만 수정하면 자동 반영

2. **Service 패턴**:
   - `MarkingAnalyzer`: 마킹 결과 분석
   - `RegistryStore`: 명렬 관리
   - `ScoringRuleStore`: 배점 규칙 관리

**최적화 방법**:
1. **Mapper 패턴을 통한 유지보수성**:
   - `OmrConstants.QuestionsCount` 변경 시 Mapper만 수정
   - 반복문으로 문항 순회 시 `GetAllQuestionNumbers()` 사용

2. **View 레벨 정렬** (`ICollectionView.SortDescriptions`):
   - 중복/오류 먼저 표시, 그 다음 전형명/석차/접수번호 순
   - 사용자 정렬 변경 시 기본 정렬 비활성화

3. **데이터 집계 최적화**:
   - `Dictionary`를 통한 수험번호별 그룹화
   - 배열을 통한 문항별 점수 합산/평균 계산

**주요 기능**:
- 면접위원별 점수 평균 계산 (3명 면접위원 기준)
- 석차 계산 (전형명별 그룹화, 동점 처리)
- 수험번호 불일치 검사 (명렬 vs 채점 결과)
- Excel 내보내기 (UI 정렬 순서 유지)
- 중복/오류 검사 및 표시

---

### 6. ScoringRule 모드
**목적**: 정답 및 배점 관리 (문항별 선택지 점수 설정)

**구현 위치**: `ScoringRuleViewModel`, `ScoringRuleView`

**아키텍처 패턴**:
- **Service 패턴**: `ScoringRuleStore`를 통한 배점 규칙 저장/로드
- **자동 저장 패턴**: PropertyChanged 이벤트 기반 자동 저장

**최적화 방법**:
- Excel 파일 직렬화/역직렬화
- PropertyChanged 이벤트 기반 자동 저장으로 사용자 편의성 향상

**주요 기능**:
- 문항별 선택지 점수 설정 (4문항 × 12선택지)
- 점수 이름 설정 (선택지 1~12의 점수 이름)
- Excel 양식 내보내기/가져오기
- 자동 저장 (변경 시 즉시 저장)

---

### 7. ManualVerification 모드
**목적**: 수기 검산 (표본 수험번호를 추출하여 프로그램 결과를 사람이 직접 확인)

**구현 위치**: `ManualVerificationViewModel`, `ManualVerificationView`

**아키텍처 패턴**:
- **MVVM 패턴**: 기본적인 View-ViewModel 구조

**특징**:
- 진입 시 자동 실행을 하지 않고, 사용자가 상단의 **"샘플 새로고침"** 버튼을 눌러 샘플을 추출/로딩
- 현재는 표본(샘플) 기반의 검산 UI이며, 향후 검산 기능 확장 예정

---

### 8. SingleStudentVerification 모드
**목적**: 성적 처리(Grading)에서 특정 수험번호를 선택(더블클릭)하여 진입하는 **단일 학생 전용 검산 화면**

**구현 위치**: `SingleStudentVerificationViewModel`, `SingleStudentVerificationView`

**특징**:
- 네비게이션 파라미터로 전달된 `StudentId`를 기준으로 해당 학생의 결과만 로드하여 표시
- 상단의 "다시 로드"는 전체 데이터 재로딩 후 동일 학생을 다시 탐색(진단/복구용)

---

## 아키텍처 패턴 상세

### 1. MVVM (Model-View-ViewModel) 패턴
**목적**: UI와 비즈니스 로직 분리

**구현 방식**:
- **View**: XAML 파일, UI 정의만 담당
- **ViewModel**: 비즈니스 로직 및 상태 관리, `INotifyPropertyChanged` 구현
- **Model**: 데이터 모델, `INotifyPropertyChanged` 구현

**데이터 바인딩**:
- 양방향 바인딩: 사용자 입력 ↔ ViewModel 속성
- 단방향 바인딩: ViewModel 속성 → UI 표시
- 컬렉션 바인딩: `ObservableCollection`을 통한 동적 UI 업데이트

**예시**:
```csharp
// ViewModel
public ObservableCollection<OmrSheetResult> SheetResults { get; set; }

// View (XAML)
<DataGrid ItemsSource="{Binding SheetResults}" />
```

---

### 2. Mapper 패턴 (Template Method Pattern)
**목적**: 문항 수 변경 시 코드 재사용성 향상

**구현 위치**: `Services/Mappers/QuestionResultMapper.cs`

**인터페이스**:
```csharp
public interface IQuestionResultMapper<T>
{
    void SetQuestionMarking(T target, int questionNumber, int? marking);
    int? GetQuestionMarking(T source, int questionNumber);
    IEnumerable<int> GetAllQuestionNumbers();
}
```

**장점**:
- `OmrConstants.QuestionsCount` 변경 시 Mapper 구현체만 수정
- 반복문으로 문항 순회 시 `GetAllQuestionNumbers()` 사용
- 타입 안전성 보장

**사용 예시**:
```csharp
// GradingViewModel에서 사용
for (int q = 1; q <= OmrConstants.QuestionsCount; q++)
{
    var marking = _sheetMapper.GetQuestionMarking(sheet, q);
    if (marking.HasValue)
    {
        var score = scoringRule.GetScore(q, marking.Value);
        questionSums[q - 1] += score;
    }
}
```

---

### 3. Strategy 패턴
**목적**: 바코드 의미 처리 로직 분리 및 확장성

**구현 위치**: `Services/Strategies/BarcodeProcessingStrategy.cs`

**인터페이스**:
```csharp
public interface IBarcodeProcessingStrategy
{
    void ApplyBarcodeResult(OmrSheetResult result, BarcodeResult barcodeResult, int barcodeIndex);
    string? GetBarcodeSemantic(int barcodeIndex);
}
```

**기본 구현**: `DefaultBarcodeProcessingStrategy`
- `OmrConstants.BarcodeSemantics` 딕셔너리를 통한 바코드 의미 매핑
- 바코드 인덱스에 따라 `StudentId`, `InterviewId` 등으로 자동 매핑

**장점**:
- 바코드 의미 변경 시 `OmrConstants.BarcodeSemantics`만 수정
- 새로운 바코드 처리 전략 추가 시 인터페이스 구현만 하면 됨

---

### 4. Command 패턴
**목적**: UI 액션 처리 및 Undo/Redo 지원

**구현 위치**: `ViewModels/RelayCommand.cs`, `Utils/Commands/`

**RelayCommand**:
- `ICommand` 인터페이스 구현
- `Action`/`Func<bool>` 델리게이트를 통한 명령 실행/가능 여부 판단

**UndoableCommand**:
- `IUndoableCommand` 인터페이스 구현
- `Execute()`, `Undo()` 메서드 제공
- `UndoManager`를 통한 Undo/Redo 스택 관리

**사용 예시**:
```csharp
public ICommand NavigateToHomeCommand { get; }
    = new RelayCommand(() => _navigation.NavigateTo(ApplicationMode.Home));
```

---

### 5. Service 패턴
**목적**: 비즈니스 로직을 서비스 클래스로 분리

**주요 서비스**:
- `ImageLoader`: 이미지 파일 로드 (병렬 처리)
- `ImageAlignmentService`: 이미지 정렬 (캐싱)
- `MarkingDetector`: 마킹 리딩 (ROI 분석)
- `BarcodeReaderService`: 바코드 디코딩 (ZXing.Net)
- `MarkingAnalyzer`: 마킹 결과 분석 (`OmrSheetResult` 생성)
- `StateStore`, `SessionStore`, `TemplateStore`: 영속성 관리
- `Renderer`: 이미지 렌더링

**의존성 주입**:
- 생성자 주입 방식 사용
- 테스트 및 확장 용이

---

### 6. 고정 슬롯 구조 패턴
**목적**: 사용자 실수 방지 및 데이터 무결성 보장

**구현 위치**: `Models/OmrTemplate.cs`, `Models/Question.cs`

**특징**:
- 4문항 × 12선택지 = 48개 고정 슬롯 (추가/삭제 불가)
- `OptionNumber`, `QuestionNumber`를 IdentityIndex로 사용
- 리스트 순서와 무관한 정확한 매핑 보장

**데이터 흐름**:
```
Questions (구조화된 데이터)
  └── Options (12개 고정 슬롯, OptionNumber 보유)
       ↓
ScoringAreas (자동 동기화, ReadOnlyObservableCollection)
```

---

## 성능 최적화 기법

### 1. 병렬 처리
**사용 위치**: `ImageLoader`, `MarkingViewModel`

**기법**:
- `Parallel.ForEach`: CPU 코어 수만큼 병렬 처리
- `ConcurrentBag`, `ConcurrentDictionary`: 스레드 안전 컬렉션
- `lock`: 진행률 업데이트 동기화

**효과**:
- 이미지 로드 시간 대폭 단축 (CPU 코어 수만큼)
- 전체 마킹 리딩 시간 단축

**예시**:
```csharp
Parallel.ForEach(imageFiles, new ParallelOptions
{
    MaxDegreeOfParallelism = Environment.ProcessorCount,
    CancellationToken = cancellationToken
}, filePath => { /* 이미지 로드 */ });
```

---

### 2. 이미지 정렬 캐싱
**사용 위치**: `ImageAlignmentService`, `ImageDocument`

**기법**:
- 정렬된 이미지를 `aligned_cache/` 폴더에 저장
- `AlignmentInfo`에 캐시 경로 저장
- 재실행 시 캐시 재사용

**효과**:
- 정렬 재계산 방지로 이미지 로드 시간 단축
- 디스크 I/O 최소화

---

### 3. 메모리 최적화
**사용 위치**: `ImageLoader`

**기법**:
- `BitmapDecoder`의 `DelayCreation` 옵션으로 이미지 메타데이터만 읽기
- 처리 완료 후 명시적 참조 해제 (`null` 할당)

**효과**:
- 이미지 메타데이터만 읽을 때 메모리 사용량 대폭 감소 (24MB → 1KB)
- GC 가비지 수집 촉진

---

### 4. View 레벨 정렬 (ICollectionView)
**사용 위치**: `MarkingViewModel`, `GradingViewModel`

**기법**:
- 데이터 컬렉션은 정렬하지 않고 `ICollectionView.SortDescriptions`를 통한 View 레벨 정렬
- `ICollectionView.Filter`를 통한 View 레벨 필터링

**효과**:
- MVVM 패턴 준수 (데이터와 View 분리)
- 정렬/필터 변경 시 컬렉션 재생성 불필요

**예시**:
```csharp
var view = CollectionViewSource.GetDefaultView(SheetResults);
view.SortDescriptions.Add(new SortDescription("IsDuplicate", ListSortDirection.Descending));
view.SortDescriptions.Add(new SortDescription("HasErrors", ListSortDirection.Descending));
```

#### 4.1 Marking/Grading 기본 정렬 정책 (유지보수/교육용)

이 섹션은 **마킹 리딩(Marking)**과 **성적 처리(Grading)** 화면에서 DataGrid/ICollectionView 정렬이 어떻게 동작하는지(기본 정렬, 사용자 정렬, 데이터 갱신 시 유지 여부)를 정리합니다.

##### Marking 모드 (마킹 리딩)

- **기본 정렬(초기 1회만)**:
  - 조건: `FilteredSheetResults.SortDescriptions.Count == 0`일 때만 설정 (이미 정렬이 있으면 덮어쓰지 않음)
  - 키 순서:
    - `IsDuplicate` DESC (중복 우선)
    - `IsSimpleError` DESC (단순 오류 우선)
    - `StudentId` ASC
    - `CombinedId` ASC
    - `ImageFileName` ASC
  - 구현 위치: `ViewModels/MarkingViewModel.cs`의 `ApplyInitialSort()`

- **사용자 정렬(열 헤더 클릭)**:
  - 클릭한 열을 **1순위 정렬 키로 올리고**, 기존 정렬 키는 **차순위로 유지**
  - UI 정렬 아이콘은 **1순위만 표시**
  - 구현 위치: `Views/MarkingView.xaml.cs`의 `OmrDataGrid_Sorting`

- **데이터 갱신 시 정렬 유지**:
  - 원칙: 리딩 결과 갱신이 잦으므로 사용자 정렬을 최대한 보존
  - 구현: `SheetResults` 컬렉션 인스턴스를 유지(Clear/Add)하여 `ICollectionView` 인스턴스가 재사용되도록 함
  - 결과: 초기 기본 정렬은 1회만 적용되고, 이후 사용자 정렬은 데이터 갱신 후에도 유지되기 쉬움

##### Grading 모드 (성적 처리)

- **기본 정렬(사용자 정렬 전까지 적용 가능)**:
  - 조건: 사용자가 정렬을 바꾼 적이 없을 때(`_userHasSorted == false`) 기본 정렬을 적용
  - 키 순서:
    - `IsDuplicate` DESC
    - `IsSimpleError` DESC
    - `ExamType` ASC
    - `Rank` ASC
    - `RegistrationNumber` ASC
  - 구현 위치: `ViewModels/GradingViewModel.cs`의 `UpdateFilteredResults()`

- **사용자 정렬(열 헤더 클릭)**:
  - Marking과 동일한 멀티키 정렬 UX(클릭한 열 1순위 + 나머지 유지, 아이콘 1순위만)
  - 추가 정책: 사용자가 헤더 정렬을 수행하면 `_userHasSorted = true`로 전환하여 이후 기본 정렬 강제 적용을 중단
  - 구현 위치:
    - `Views/GradingView.xaml.cs`의 `GradingDataGrid_Sorting()` → `ViewModel.MarkUserHasSorted()`
    - `ViewModels/GradingViewModel.cs`의 `MarkUserHasSorted()` / `_userHasSorted`

- **데이터 갱신 시 정렬 유지**:
  - `GradingResults`는 새 `ObservableCollection<GradingResult>`로 교체(assign)되는 경로가 있어,
    교체 시점에 `_userHasSorted = false`로 초기화되고 기본 정렬이 다시 적용될 수 있음
  - 구현 위치: `ViewModels/GradingViewModel.cs`의 `GradingResults` setter (`_userHasSorted = false; UpdateFilteredResults();`)

##### 왜 정책이 다른가?

- **Marking**: 결과 갱신이 잦고 사용자가 특정 정렬(오류만 먼저 보기 등)을 고정해두고 작업하는 경우가 많아, “초기 기본 정렬 1회 + 이후 사용자 정렬 보존”을 우선합니다.
- **Grading**: 기본 우선순위(중복/오류 우선)가 명확하고, 사용자가 정렬을 시작하기 전까지는 일관된 기본 보기(전형/석차/접수번호)를 제공하되, 사용자가 정렬을 시작하면 그 이후엔 사용자 의도를 최우선으로 둡니다.

---

### 5. 로그 버퍼링
**사용 위치**: `Logger`

**기법**:
- `StringBuilder`를 통한 로그 버퍼링
- 최소 로그 레벨 설정 (`MinLogLevel`)

**효과**:
- 디스크 I/O 최소화
- 성능 최적화를 위한 불필요한 Debug 로그 제거

---

### 6. 비동기 처리
**사용 위치**: `MarkingViewModel`, `ImageLoader`

**기법**:
- `async/await` 패턴
- `Task.Run`으로 CPU 집약적 작업 백그라운드 실행
- `CancellationToken`으로 작업 취소 지원

**효과**:
- UI 스레드 블로킹 방지
- 사용자 인터랙션 유지

---

### 7. Ingest 파이프라인 최적화
**사용 위치**: `MarkingViewModel`, `ImageLoader`, `BarcodeReaderService`

**기법**:
- **바코드 디코딩 시점 최적화**: 폴더 로드 시점에만 바코드 디코딩 수행, 리딩 단계에서는 캐시된 결과만 사용
- **정렬 실패 문서 명시적 처리**: 정렬 실패 시 원본 fallback 제거하여 불완전한 데이터 처리 방지
- **파일명 기반 중복 스킵**: 동일 파일명 체크로 불필요한 재처리 방지
- **상태 추적 및 집계**: `IngestDocState`를 통한 문서별 상태 추적 및 원인별 집계

**효과**:
- 바코드 디코딩 중복 처리 방지로 성능 향상
- 데이터 품질 보장 (정렬 실패 문서는 처리 제외)
- 사용자 피드백 개선 (즉시 상태 확인 가능)
- 중복 로드 방지로 처리 시간 단축

**예시**:
```csharp
// 폴더 로드 시점에 바코드 디코딩
if (doc.AlignmentInfo?.Success == true)
{
    var results = _barcodeReaderService.DecodeBarcodes(doc, barcodeAreas);
    _session.BarcodeResults[doc.ImageId] = results;
}

// 리딩 단계에서는 캐시된 결과만 사용
if (!_session.BarcodeResults.TryGetValue(document.ImageId, out var cachedResults))
{
    // 바코드 결과 없음으로 스킵
    return;
}
```

---

## 애플리케이션 생애주기

### 1. 시작 (Startup)

```
App.xaml.cs OnStartup()
  ↓
  - Logger 초기화 및 로그 파일 경로 설정
  - 처리되지 않은 예외 핸들러 등록
  ↓
MainWindow.xaml.cs 생성자
  ↓
  - 회차(AppState) 로드/선택 및 `PathService.CurrentRound` 설정
  - `StateStore` 생성 및 Workspace 로드
    - 작업 상황(state.json: 입력 폴더, 선택 문서 ID)
    - 템플릿(template.json: 없으면 기본 템플릿 자동 설치/로드)
  - NavigationViewModel 생성
  - 초기 모드: Home
  - HomeViewModel 생성 및 설정
  ↓
MainWindow 표시
```

### 2. 모드 전환 (Navigation)

애플리케이션은 8가지 모드를 지원합니다:
- **Home**: 모드 선택 화면
- **TemplateEdit**: 템플릿 편집 화면
- **Marking**: 마킹 리딩 화면
- **Registry**: 명렬 관리 화면
- **Grading**: 채점 및 성적 처리 화면
- **ScoringRule**: 정답 및 배점 화면
- **ManualVerification**: 수기 검산 화면
- **SingleStudentVerification**: 성적처리에서 특정 수험번호를 선택하여 진입하는 단일 학생 검산 화면

모드 전환 흐름:

```
사용자 모드 선택
  ↓
NavigationViewModel.NavigateTo(mode)
  ↓
  - CurrentMode 변경
  - CurrentViewModel을 null로 설정 (지연 생성)
  ↓
MainNavigationViewModel(Navigation.PropertyChanged)에서 변경 감지
  ↓
  - 현재 모드에 맞는 ViewModel 생성
    - Home → HomeViewModel
    - TemplateEdit → TemplateEditViewModel
    - Marking → MarkingViewModel
    - Registry → RegistryViewModel
    - Grading → GradingViewModel
    - ScoringRule → ScoringRuleViewModel
    - ManualVerification → ManualVerificationViewModel
    - SingleStudentVerification → SingleStudentVerificationViewModel
  ↓
NavigationViewModel.SetXXXViewModel(viewModel)
  ↓
  - CurrentViewModel 설정
  ↓
MainWindow.ContentControl이 DataTemplate을 통해 자동으로 View 선택
  ↓
해당 View 표시
```

### 3. 이미지 로드 및 정렬 (Ingest 파이프라인)

```
사용자 "폴더 로드" 버튼 클릭
  ↓
MarkingViewModel.OnLoadFolder()
  ↓
  - FolderBrowserDialog 표시
  - ImageLoader.LoadImagesFromFolder() (병렬 처리)
    - 폴더에서 이미지 파일 검색
    - Parallel.ForEach로 병렬 로드
    - ImageDocument 객체 생성
    - 파일명 중복 체크 (기존 세션과 비교하여 스킵)
  ↓
각 이미지에 대해 병렬 처리 (Parallel.ForEach)
  ↓
  - 정렬 적용: MarkingViewModel.ApplyAlignmentToDocument()
    - ImageAlignmentService.AlignImage()
      - 타이밍 마크 감지
      - 변환 행렬 계산
      - 정렬된 이미지 생성
    - 정렬된 이미지를 캐시 폴더에 저장
    - AlignmentInfo를 ImageDocument에 저장
    - IngestDocState에 정렬 상태 기록
  ↓
  - 바코드 디코딩 (정렬 성공한 문서만 대상)
    - BarcodeReaderService.DecodeBarcodes()
    - 결과를 Session.BarcodeResults[imageId]에 저장
    - IngestDocState에 바코드 상태 기록
    - CombinedId 계산 및 상태 기록
  ↓
Session.Documents에 추가
Session.BarcodeResults에 바코드 결과 저장
Session.IngestStateByImageId에 상태 저장
  ↓
UpdateSheetResults() 호출
  - 바코드 결과를 기반으로 임시 OmrSheetResult 생성
  - 하단뷰에 즉시 표시 (수험번호/면접번호)
  - ReadyForReadingCount 계산
  ↓
로드 완료 메시지 표시 (원인별 집계)
```

### 4. 템플릿 편집

```
TemplateEdit 모드 진입
  ↓
TemplateEditViewModel 초기화
  - Workspace.Template 바인딩
  ↓
사용자 오버레이 편집 (위치/크기만 수정)
  ↓
  - 오버레이 타입 선택 (TimingMark / ScoringArea / BarcodeArea)
  - ScoringArea일 때: 문항 선택 (1-4)
  - 캔버스 클릭 → 선택된 문항의 슬롯 위치/크기 수정
  - 오버레이 선택 → 속성 편집
  ↓
Workspace.Template.Questions 업데이트
  ↓
Workspace.Template.ScoringAreas 자동 동기화 (Questions에서)
  ↓
PropertyChanged 이벤트로 UI 자동 업데이트
```

### 5. 마킹 리딩

```
Marking 모드 진입
  ↓
MarkingViewModel 초기화
  - Session.Documents 바인딩
  - Workspace.Template.ScoringAreas 바인딩
  ↓
사용자 "마킹 리딩", "전체 문서 리딩", 또는 "미리딩만 전체 리딩" 버튼 클릭
  ↓
MarkingViewModel.DetectMarkings() / DetectAllMarkings() / DetectUnreadMarkings()
  ↓
바코드 결과 확인 (Session.BarcodeResults에서 캐시된 결과 사용)
  - 바코드 결과 없으면 리딩 스킵 및 경고 표시
  ↓
MarkingDetector.DetectMarkings() (단일) 또는 Parallel.ForEach (전체)
  ↓
  - GetImagePathForUse()로 정렬된 이미지 경로 확인
    - 정렬 실패 문서(null 반환)는 리딩 스킵
  - 정렬된 이미지 로드 (캐시)
  - 그레이스케일 변환
  - 각 ScoringArea ROI 추출
  - 평균 밝기 계산
  - 임계값 비교하여 마킹 판단
  - OptionNumber, QuestionNumber를 IdentityIndex로 사용하여 결과 생성
  ↓
MarkingResult 리스트 반환
  ↓
Session.MarkingResults[imageId]에 저장
  ↓
MarkingAnalyzer.AnalyzeAllSheets() (OmrSheetResult 생성)
  - 바코드 결과는 Session.BarcodeResults에서 사용
  ↓
UpdateSheetResults() 호출
  ↓
UI에 결과 표시 (ICollectionView를 통한 정렬/필터링)
```

### 5. 마킹 결과 내보내기

```
Marking 모드에서 OMR 결과 확인
  ↓
사용자 "Excel 내보내기" 버튼 클릭
  ↓
MarkingViewModel.OnExportToXlsx()
  ↓
  - SaveFileDialog 표시 (기본 파일명: OMR_Results_YYYYMMDD_HHMMSS.xlsx)
  ↓
  - ClosedXML을 사용하여 Excel 워크북 생성
  - 헤더 행 작성 (파일명, 수험번호, 시각, 실, 순, 면접번호, 결합ID, 문항1-4, 오류, 오류 메시지)
  - 필터링된 SheetResults 데이터를 행으로 추가
  - 헤더 서식 적용 (굵게, 배경색)
  - 자동 열 너비 조정
  ↓
Excel 파일 저장
  ↓
완료 메시지 표시
```

### 6. 채점 처리

```
Grading 모드 진입
  ↓
GradingViewModel 초기화
  ↓
LoadGradingDataAsync() / GradingCalculator.GetAllAsync()
  ↓
  - OmrAnalysisCache를 통해 데이터 로드/캐시
    - Session
    - 전체 OMR 결과(OmrSheetResult)
    - 수험생 명렬(StudentRegistry)
    - 배점(ScoringRule)
  - GradingCalculator에서 성적처리 결과 계산 및 내부 캐시 유지
  ↓
수험번호별로 그룹화
  ↓
면접위원별 점수 평균 계산 (Mapper 패턴 사용)
  ↓
석차 계산 (전형명별 그룹화)
  ↓
수험번호 불일치 검사
  ↓
GradingResults 컬렉션 생성 (ICollectionView로 정렬/필터링)
  ↓
UI 표시
```

### 7. 회차 관리

```
애플리케이션 시작
  ↓
AppStateStore.LoadAppState()
  ↓
  - app_state.json에서 회차 목록 및 마지막 선택 회차 로드
  - 폴더가 없는 회차 자동 정리
  ↓
PathService.CurrentRound 설정
  ↓
모든 데이터 저장/로드 시 회차별 경로 사용
  - state.json, session.json, template.json → Rounds/{회차명}/
  - output, aligned_cache, barcode_debug → Rounds/{회차명}/
```

### 8. 저장

```
사용자 "저장" 버튼 클릭 또는 자동 저장
  ↓
StateStore.SaveWorkspaceState(Workspace)
  - Workspace를 JSON으로 직렬화
  - state.json에 저장 (입력 폴더 경로, 선택된 문서 ID)
  ↓
SessionStore.Save(Session)
  - Session을 JSON으로 직렬화
  - session.json에 저장 (문서 목록, 마킹/바코드 결과)
  ↓
TemplateStore.SaveTemplate(OmrTemplate) (템플릿 편집 시)
  - OmrTemplate을 JSON으로 직렬화
  - template.json에 저장
```

### 9. 종료 (Shutdown)

```
사용자 창 닫기
  ↓
MainWindow.OnClosed()
  ↓
  - StateStore.SaveWorkspaceState(Workspace)
    - 현재 상태를 state.json에 저장
  ↓
  - Logger.Instance.Info("애플리케이션 종료")
  ↓
애플리케이션 종료
```

## 기술 스택

### 프레임워크 및 런타임
- **.NET 8.0**: 최신 .NET 런타임
- **WPF (Windows Presentation Foundation)**: 데스크톱 UI 프레임워크
- **Windows Forms**: 폴더 선택 대화상자용 (System.Windows.Forms)

### 아키텍처 패턴
- **MVVM (Model-View-ViewModel)**: UI와 비즈니스 로직 분리
- **Service 패턴**: 비즈니스 로직을 서비스 클래스로 분리
- **Mapper 패턴** (Template Method Pattern): 문항 결과 매핑 추상화
- **Strategy 패턴**: 바코드 처리 전략 분리
- **Command 패턴**: UI 액션 처리 및 Undo/Redo 지원
- **고정 슬롯 구조 패턴**: 사용자 실수 방지 및 데이터 무결성 보장

### 라이브러리
- **System.Text.Json 9.0.0**: 상태 저장 및 직렬화
- **ZXing.Net 0.15.0**: 바코드 디코딩 라이브러리
  - 지원 포맷: CODE_128, CODE_39, EAN_13, EAN_8, CODABAR, ITF
  - 이미지 전처리 및 바코드 패턴 인식
- **ClosedXML 0.102.2**: Excel 파일 생성/읽기 (마킹 결과 내보내기, 명렬 관리)

### 비동기 및 병렬 처리 기술
- **async/await**: 비동기 프로그래밍 패턴
  - UI 스레드 블로킹 방지
  - 백그라운드 작업 처리
- **Task.Run**: 백그라운드 스레드에서 CPU 집약적 작업 실행
- **Parallel.ForEach**: 데이터 병렬 처리 (TPL)
  - CPU 코어 수만큼 병렬 처리
  - 이미지 로드, 마킹 리딩, 정렬 작업 병렬화
- **CancellationToken**: 작업 취소 지원
  - 협조적 취소 패턴
  - 사용자 요청 시 작업 중단
- **ConcurrentDictionary**: 스레드 안전 딕셔너리
- **ConcurrentBag**: 스레드 안전 컬렉션
- **lock**: 스레드 동기화 (진행률 업데이트 등)

### 스레드 및 동기화
- **Dispatcher.Invoke**: UI 스레드로 작업 마샬링
  - 백그라운드 스레드에서 UI 업데이트
  - 스레드 안전한 UI 접근
- **Dispatcher.CheckAccess**: 현재 스레드가 UI 스레드인지 확인

### 메모리 최적화
- **BitmapDecoder**: 이미지 메타데이터만 읽기 (헤더만 로드)
  - DelayCreation 옵션으로 픽셀 데이터 로드 지연
  - 메모리 사용량 대폭 절감 (24MB → 1KB)
- **using 문**: 리소스 자동 해제
- **명시적 참조 해제**: 처리 후 즉시 null 할당으로 GC 가비지 수집 촉진

### 주요 기술
- **데이터 바인딩**: 양방향/단방향 바인딩을 통한 UI 업데이트
- **ObservableCollection**: 컬렉션 변경 시 자동 UI 업데이트
- **INotifyPropertyChanged**: 속성 변경 알림
- **ICollectionView**: View 레벨 정렬/필터링
- **이미지 처리**: WPF BitmapSource API
  - BitmapImage: 이미지 로드
  - FormatConvertedBitmap: 그레이스케일 변환
  - RenderTargetBitmap: 이미지 렌더링
  - DrawingVisual: 이미지 합성
- **파일 시스템**: AppData 폴더 기반 데이터 저장
- **로깅**: 파일 기반 로깅 시스템 (날짜별 로그 파일)
  - 로그 레벨 관리 (Debug, Info, Warning, Error)
  - 성능 최적화를 위한 최소 로그 레벨 설정

## 빌드 및 실행

```bash
# 빌드
dotnet build
```

빌드 후 다음 방법으로 실행할 수 있습니다:

1. **더블클릭 실행** (권장):
   - `bin/Debug/net8.0-windows/SimpleOverlayEditor.exe` 파일을 더블클릭하여 실행
   
2. **명령줄 실행**:
   ```bash
   dotnet run
   ```

3. **Visual Studio**:
   - 프로젝트를 열고 F5로 실행

## 사용 방법

### 1. 템플릿 편집
- **TemplateEdit 모드**로 이동
- **타이밍 마크 추가** (정렬용): 오버레이 타입을 "TimingMark"로 선택 후 위치 클릭
- **채점 영역 추가** (리딩용): 오버레이 타입을 "ScoringArea"로 선택, 문항 선택 후 선택지 위치 클릭
- **바코드 영역 추가**: 오버레이 타입을 "BarcodeArea"로 선택 후 위치 클릭

### 2. 이미지 로드 및 마킹 리딩
- **Marking 모드**로 이동
- **폴더 로드**: 이미지가 있는 폴더 선택
  - 병렬 처리로 자동 로드 및 정렬
  - **정렬 성공한 문서에 대해 자동으로 바코드 디코딩 수행**
  - **폴더 로드 직후 하단뷰에 수험번호/면접번호가 즉시 표시** (바코드 결과 기반)
  - **동일 파일명의 이미지는 자동으로 스킵** (중복 로드 방지)
  - 로드 완료 시 원인별 집계 표시 (정렬 실패, 바코드 실패, ID 누락 등)
- **마킹 리딩**: 
  - "마킹 리딩" (단일) 또는 "전체 문서 리딩" (병렬 처리) 버튼 클릭
  - **"미리딩만 전체 리딩"**: 아직 마킹 리딩이 되지 않은 문서만 선별하여 빠르게 리딩
- **결과 확인**: 오른쪽 패널에서 마킹/바코드 결과 확인, 하단 패널에서 OMR 결과 요약 확인
- **Excel 내보내기**: 하단 패널에서 "Excel 내보내기" 버튼 클릭하여 현재 필터링된 결과를 Excel 파일로 저장

### 3. 명렬 관리
- **Registry 모드**로 이동
- **명렬 로드**: Excel 파일에서 수험생/면접위원 명렬 로드
- **명렬 저장**: 변경 사항 저장

### 4. 배점 설정
- **ScoringRule 모드**로 이동
- **배점 입력**: 문항별 선택지 점수 입력 (자동 저장)

### 5. 마킹 결과 내보내기
- **Marking 모드**에서 OMR 결과 확인 후
- **Excel 내보내기**: 현재 필터링된 결과를 Excel(.xlsx) 파일로 내보내기
  - 파일명, 수험번호, 시각, 실, 순, 면접번호, 결합ID, 문항별 마킹, 오류 정보 포함

### 6. 채점 처리
- **Grading 모드**로 이동
- **채점 결과 확인**: 면접위원별 점수 평균, 석차, 수험번호 불일치 검사 결과 확인
- **Excel 내보내기**: 채점 결과를 Excel 파일로 내보내기

## 저장 위치

애플리케이션은 **회차별 데이터 분리 시스템**을 사용합니다. 회차가 선택된 경우 모든 데이터는 회차별 폴더에 저장되며, 회차가 없으면 전역 폴더에 저장됩니다.

### 회차별 저장 구조 (회차 선택 시)

- **회차 루트 폴더**: `%AppData%/SimpleOverlayEditor/Rounds/{회차명}/`
  - **상태 파일 (state.json)**: `Rounds/{회차명}/state.json`
    - 프로그램 전반 상태: 입력 폴더 경로, 선택된 문서 ID
  - **세션 파일 (session.json)**: `Rounds/{회차명}/session.json`
    - 이미지 로드 및 리딩 작업 세션: 문서 목록, 정렬 정보, 마킹 결과, 바코드 결과
  - **템플릿 파일 (template.json)**: `Rounds/{회차명}/template.json`
    - OMR 템플릿 정보: 타이밍 마크, 채점 영역, 바코드 영역
  - **출력 이미지**: `Rounds/{회차명}/output/`
    - 오버레이가 합성된 이미지 파일 (재생성 가능한 캐시)
    - 용량 상한: 512MB (초과 시 오래된 파일부터 자동 삭제)
    - 보관 기간: 7일 이상 된 파일 자동 삭제
  - **정렬된 이미지 캐시**: `Rounds/{회차명}/aligned_cache/`
    - 정렬된 이미지 캐시 파일
    - 용량 상한: 5GB (초과 시 오래된 파일부터 자동 삭제)
    - 보관 기간: 14일 이상 된 파일 자동 삭제
  - **바코드 디버그**: `Rounds/{회차명}/barcode_debug/`
    - 바코드 디코딩 디버그용 크롭 이미지 (DEBUG 빌드에서만 생성)
    - 용량 상한: 512MB
    - 보관 기간: 3일 이상 된 파일 자동 삭제

### 전역 저장 위치 (회차 미선택 시)

- **상태 파일 (state.json)**: `%AppData%/SimpleOverlayEditor/state.json`
- **세션 파일 (session.json)**: `%AppData%/SimpleOverlayEditor/session.json`
- **템플릿 파일 (template.json)**: `%AppData%/SimpleOverlayEditor/template.json`
- **출력 이미지**: `%AppData%/SimpleOverlayEditor/output/` (용량 상한: 512MB)
- **정렬된 이미지 캐시**: `%AppData%/SimpleOverlayEditor/aligned_cache/` (용량 상한: 5GB)
- **바코드 디버그**: `%AppData%/SimpleOverlayEditor/barcode_debug/` (용량 상한: 512MB)

### 공통 저장 위치 (회차와 무관)

- **로그 파일**: `%AppData%/SimpleOverlayEditor/logs/overlay_editor_YYYYMMDD.log`
- **명렬 파일**: `%AppData%/SimpleOverlayEditor/student_registry.json`, `interviewer_registry.json`
- **배점 규칙 파일**: `%AppData%/SimpleOverlayEditor/scoring_rule.json`
- **앱 상태 파일**: `%AppData%/SimpleOverlayEditor/app_state.json` (회차 목록 및 마지막 선택 회차)

### 캐시 정리 정책

애플리케이션 시작 시 자동으로 캐시 폴더를 정리합니다:
- **output**: 7일 이상 된 파일 삭제, 512MB 초과 시 오래된 파일부터 삭제
- **aligned_cache**: 14일 이상 된 파일 삭제, 5GB 초과 시 오래된 파일부터 삭제 (현재 세션에서 참조하는 파일은 보호)
- **barcode_debug**: 3일 이상 된 파일 삭제, 512MB 초과 시 오래된 파일부터 삭제

**참고**: output 폴더의 이미지는 UI 표시용이 아니라 내보내기/검수용 캐시입니다. UI에서는 문서 선택 시 메모리에서 즉석 렌더링하여 표시합니다.

## 로그 파일

애플리케이션은 모든 주요 작업과 오류를 로그 파일에 기록합니다.

- **로그 위치**: `%AppData%/SimpleOverlayEditor/logs/overlay_editor_YYYYMMDD.log`
- **로그 레벨**: Debug, Info, Warning, Error
- **로그 내용**: 
  - 애플리케이션 시작/종료
  - Workspace 로드/저장
  - 폴더 로드 및 이미지 로드
  - 이미지 정렬 성공/실패 (신뢰도, 변환 정보)
  - 마킹 리딩 결과
  - 예외 및 오류 정보 (스택 트레이스 포함)

## 주의사항

- **정렬 기능**:
  - 타이밍 마크가 설정되어 있어야 정렬이 수행됩니다
  - 정렬은 이미지 로드 시 자동으로 수행되며, 정렬된 이미지는 캐시에 저장됩니다
  - **정렬 실패 시 해당 문서는 처리 대상에서 제외됩니다** (원본 이미지로 fallback하지 않음)
  - 정렬 실패 문서는 로드 완료 메시지에서 "정렬 실패" 개수로 확인 가능

- **바코드 디코딩**:
  - **폴더 로드 시점에 자동으로 수행됩니다** (정렬 성공한 문서만 대상)
  - 리딩 단계에서는 이미 디코딩된 결과를 사용하므로 추가 디코딩이 발생하지 않습니다
  - 바코드 영역이 정확하게 지정되어 있어야 합니다 (바코드 주변 여백 포함 권장)
  - 바코드 디코딩 실패 시 해당 문서는 마킹 리딩 대상에서 제외됩니다

- **중복 처리**:
  - **동일 파일명의 이미지는 자동으로 스킵**되어 중복 로드가 방지됩니다
  - **동일 CombinedId를 가진 문서는 모두 유지**되며, 중복으로 표시됩니다
  - 실제 중복 상황을 정확히 파악하기 위해 모든 문서를 유지하는 정책을 따릅니다

- **템플릿 좌표**:
  - 오버레이 좌표는 템플릿 기준 이미지 픽셀 기준으로 저장됩니다
  - 정렬된 이미지에서는 템플릿 좌표가 그대로 적용되어 정확한 위치를 보장합니다

- **고정 슬롯 구조**:
  - 채점 영역은 4문항 × 12선택지 = 48개 고정 슬롯입니다
  - 추가/삭제가 불가능하며, 위치/크기만 편집할 수 있습니다

- **마킹 리딩**:
  - 정렬된 이미지에서 채점 영역의 평균 밝기를 분석하여 판단합니다
  - 임계값보다 어두우면 마킹으로 판단 (기본값: 220)
  - 이미지 품질과 조명 조건에 따라 임계값 조정이 필요할 수 있습니다
  - **"미리딩만 전체 리딩" 기능**을 사용하면 아직 리딩되지 않은 문서만 빠르게 처리할 수 있습니다

- **채점 처리**:
  - 면접위원 3명 기준으로 점수 평균을 계산합니다
  - 석차는 전형명별로 그룹화하여 계산됩니다


