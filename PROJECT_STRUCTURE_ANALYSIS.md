# SimpleOverlayEditor 프로젝트 구조 및 생애주기 분석

## 📁 디렉토리 구조 분석

### 전체 구조
```
overlay_editor/
├── App.xaml / App.xaml.cs              # 애플리케이션 진입점 및 글로벌 설정
├── SimpleOverlayEditor.csproj          # 프로젝트 설정
├── Directory.Build.props                # 빌드 속성 공통 설정
│
├── Models/                              # 데이터 모델 (도메인 모델)
│   ├── ApplicationMode.cs              # 애플리케이션 모드 열거형 (Home, TemplateEdit)
│   ├── Workspace.cs                    # 전체 워크스페이스 상태 (Singleton)
│   ├── ImageDocument.cs                # 이미지 문서 정보
│   ├── OmrTemplate.cs                  # OMR 템플릿 (타이밍 마크, 채점 영역)
│   ├── RectangleOverlay.cs             # 직사각형 오버레이 데이터
│   ├── OverlayType.cs                  # 오버레이 타입 열거형 (TimingMark, ScoringArea)
│   ├── AlignmentInfo.cs                # 이미지 정렬 정보
│   └── MarkingResult.cs                # 마킹 감지 결과
│
├── ViewModels/                          # MVVM 패턴 - ViewModel 계층
│   ├── NavigationViewModel.cs          # 네비게이션 관리 (모드 전환)
│   ├── HomeViewModel.cs                # 홈 화면 ViewModel
│   ├── TemplateEditViewModel.cs        # 템플릿 편집 ViewModel (주요 로직)
│   ├── TemplateViewModel.cs            # 템플릿 관리 ViewModel
│   ├── MarkingViewModel.cs             # 마킹 감지 ViewModel
│   ├── RelayCommand.cs                 # ICommand 구현체
│   └── MainViewModel.cs                # ⚠️ 사용되지 않음 (레거시)
│
├── Views/                               # MVVM 패턴 - View 계층 (UI)
│   ├── MainWindow.xaml/.cs             # 메인 윈도우 (View 컨테이너)
│   ├── HomeView.xaml/.cs               # 홈 화면 UI
│   └── TemplateEditView.xaml/.cs       # 템플릿 편집 화면 UI
│
├── Services/                            # 비즈니스 로직 서비스 계층
│   ├── StateStore.cs                   # 상태 저장/로드 (JSON 직렬화)
│   ├── PathService.cs                  # 경로 관리 (정적 클래스)
│   ├── Logger.cs                       # 로깅 서비스 (Singleton)
│   ├── ImageLoader.cs                  # 이미지 파일 로드
│   ├── ImageAlignmentService.cs        # 타이밍 마크 기반 이미지 정렬
│   ├── Renderer.cs                     # 오버레이 + 이미지 합성
│   └── MarkingDetector.cs              # 마킹 감지 서비스
│
└── Utils/                               # 유틸리티 클래스
    ├── CoordinateConverter.cs          # 화면 좌표 ↔ 픽셀 좌표 변환
    ├── ZoomHelper.cs                   # 줌/피트 계산 (Uniform 스케일)
    └── Converters.cs                   # XAML 데이터 바인딩 컨버터
```

### 계층별 역할

#### 1. **Models 계층**
- **역할**: 도메인 데이터 모델 정의
- **특징**: INotifyPropertyChanged 구현으로 UI 바인딩 지원
- **핵심 클래스**:
  - `Workspace`: 애플리케이션의 전체 상태 관리 (Singleton 패턴과 유사)
  - `OmrTemplate`: 모든 이미지에 공통 적용되는 템플릿
  - `ImageDocument`: 개별 이미지 정보 및 정렬 정보 포함

#### 2. **ViewModels 계층**
- **역할**: UI 로직 및 상태 관리
- **특징**: 
  - MVVM 패턴 구현
  - Command 패턴으로 사용자 입력 처리
  - PropertyChanged 이벤트로 UI 자동 업데이트
- **핵심 클래스**:
  - `NavigationViewModel`: 화면 간 전환 관리
  - `TemplateEditViewModel`: 템플릿 편집의 모든 비즈니스 로직 포함

#### 3. **Views 계층**
- **역할**: UI 정의 및 사용자 상호작용
- **특징**: 
  - XAML로 UI 정의
  - DataContext를 통해 ViewModel 바인딩
  - 코드비하인드 최소화

#### 4. **Services 계층**
- **역할**: 비즈니스 로직 및 외부 시스템과의 통신
- **특징**: 
  - 독립적인 서비스 클래스
  - 상태 비의존적 (Stateless)
  - 재사용 가능한 기능 제공

#### 5. **Utils 계층**
- **역할**: 공통 유틸리티 함수
- **특징**: 정적 메서드 또는 단순 변환 클래스

---

## 🔄 프로그램 생애주기 (Application Lifecycle)

### Phase 1: 애플리케이션 시작 (Startup)

```
1. App.xaml.cs → OnStartup()
   ├─ Logger 인스턴스 초기화 (Singleton)
   ├─ 글로벌 예외 핸들러 등록
   │  ├─ DispatcherUnhandledException
   │  └─ AppDomain.UnhandledException
   └─ StartupUri에 의해 MainWindow.xaml 로드
```

**처리 흐름:**
```csharp
App.OnStartup()
  ↓
Logger.Instance 초기화 (최초 접근 시)
  ↓
예외 핸들러 등록
  ↓
MainWindow 생성자 호출
```

### Phase 2: 메인 윈도우 초기화

```
2. MainWindow 생성자
   ├─ InitializeComponent() [XAML UI 로드]
   ├─ StateStore 인스턴스 생성
   ├─ Workspace 로드 (StateStore.Load())
   │  ├─ state.json 파일 읽기
   │  ├─ JSON 역직렬화
   │  ├─ Workspace 객체 재구성
   │  └─ 정렬된 이미지 캐시 검증
   ├─ NavigationViewModel 생성
   ├─ PropertyChanged 이벤트 구독 (모드 변경 감지)
   ├─ MainNavigationViewModel 생성 (DataContext)
   └─ Navigation.NavigateTo(ApplicationMode.Home)
```

**처리 흐름:**
```csharp
MainWindow 생성자
  ↓
StateStore.Load()
  ├─ PathService.StateFilePath 확인
  ├─ 파일 없음 → 빈 Workspace 반환
  └─ 파일 있음 → JSON 파싱 및 Workspace 재구성
  ↓
NavigationViewModel 생성
  ↓
Navigation.PropertyChanged 구독 (모드 변경 감지)
  ↓
MainNavigationViewModel 생성 및 DataContext 설정
  ↓
Navigation.NavigateTo(ApplicationMode.Home)
```

### Phase 3: 홈 화면 표시

```
3. Navigation.NavigateTo(ApplicationMode.Home)
   ├─ CurrentMode = ApplicationMode.Home
   ├─ CurrentViewModel = null (임시)
   └─ PropertyChanged 이벤트 발생
      ↓
4. MainWindow.PropertyChanged 핸들러
   ├─ CurrentMode == Home && CurrentViewModel == null
   ├─ HomeViewModel 생성
   └─ Navigation.SetHomeViewModel(homeViewModel)
      ↓
5. MainWindow.xaml의 ContentControl
   ├─ DataTemplate 매칭 (HomeViewModel)
   └─ HomeView 표시
```

**처리 흐름:**
```csharp
Navigation.NavigateTo(Home)
  ↓
CurrentMode = Home
CurrentViewModel = null
  ↓
PropertyChanged 이벤트 발생
  ↓
MainWindow 이벤트 핸들러
  ├─ HomeViewModel 생성 (NavigationViewModel 주입)
  └─ Navigation.SetHomeViewModel()
  ↓
MainWindow.xaml ContentControl
  ├─ DataTemplate 매칭 (HomeViewModel → HomeView)
  └─ HomeView 렌더링
```

### Phase 4: 템플릿 편집 모드로 전환

```
6. 사용자가 "템플릿 편집" 버튼 클릭
   ├─ HomeViewModel.NavigateToTemplateEditCommand 실행
   └─ Navigation.NavigateTo(ApplicationMode.TemplateEdit)
      ↓
7. Navigation.NavigateTo(ApplicationMode.TemplateEdit)
   ├─ CurrentMode = ApplicationMode.TemplateEdit
   ├─ CurrentViewModel = null (임시)
   └─ PropertyChanged 이벤트 발생
      ↓
8. MainWindow.PropertyChanged 핸들러
   ├─ CurrentMode == TemplateEdit && CurrentViewModel == null
   ├─ TemplateEditViewModel 생성
   │  ├─ NavigationViewModel 주입
   │  ├─ Workspace 주입
   │  ├─ StateStore 주입
   │  ├─ ImageLoader, CoordinateConverter 생성
   │  ├─ TemplateViewModel 생성
   │  ├─ 템플릿 컬렉션 변경 이벤트 구독
   │  └─ Commands 초기화
   └─ Navigation.SetTemplateEditViewModel(templateEditViewModel)
      ↓
9. MainWindow.xaml의 ContentControl
   ├─ DataTemplate 매칭 (TemplateEditViewModel)
   └─ TemplateEditView 표시
      ↓
10. TemplateEditView.Loaded 이벤트
    ├─ ViewModel.PropertyChanged 구독
    ├─ 템플릿 컬렉션 변경 이벤트 구독
    └─ 초기 이미지 표시 (SelectedDocument가 있는 경우)
```

**처리 흐름:**
```csharp
사용자 액션: "템플릿 편집" 버튼 클릭
  ↓
Navigation.NavigateTo(TemplateEdit)
  ↓
PropertyChanged 이벤트 발생
  ↓
MainWindow 이벤트 핸들러
  ├─ TemplateEditViewModel 생성
  │  ├─ 서비스 인스턴스 생성
  │  ├─ TemplateViewModel 생성
  │  └─ 이벤트 구독
  └─ Navigation.SetTemplateEditViewModel()
  ↓
TemplateEditView 렌더링
  ↓
TemplateEditView.Loaded
  ├─ ViewModel 이벤트 구독
  └─ 초기 상태 UI 업데이트
```

### Phase 5: 템플릿 편집 작업 (Runtime)

#### 5.1 샘플 이미지 로드
```
11. 사용자가 "이미지 로드" 버튼 클릭
    ├─ TemplateEditViewModel.OnLoadSampleImage()
    ├─ OpenFileDialog 표시
    ├─ 선택된 이미지 로드
    ├─ ImageDocument 생성
    ├─ Workspace.Documents.Clear()
    ├─ Workspace.Documents.Add(document)
    └─ SelectedDocument = document
       ↓
12. SelectedDocument 변경 감지
    ├─ PropertyChanged 이벤트 발생
    ├─ TemplateEditView.PropertyChanged 핸들러
    ├─ UpdateImageDisplay() 호출
    └─ DrawOverlays() 호출
```

#### 5.2 오버레이 추가
```
13. 사용자가 "사각형 추가 모드" 토글
    ├─ IsAddMode = true
    └─ PropertyChanged 이벤트 발생
       ↓
14. 사용자가 캔버스 클릭
    ├─ TemplateEditView.OnCanvasClick()
    ├─ 화면 좌표 → 픽셀 좌표 변환 (CoordinateConverter)
    ├─ RectangleOverlay 생성
    ├─ GetCurrentOverlayCollection() 호출
    ├─ 컬렉션에 오버레이 추가
    ├─ SelectedOverlay = overlay
    └─ PropertyChanged 이벤트 발생
       ↓
15. 컬렉션 변경 감지
    ├─ Template.TimingMarks/ScoringAreas.CollectionChanged
    ├─ TemplateEditViewModel.PropertyChanged 발생
    └─ TemplateEditView.DrawOverlays() 호출
```

#### 5.3 템플릿 저장
```
16. 사용자가 "기본 템플릿 저장" 버튼 클릭
    ├─ TemplateViewModel.OnSaveDefaultTemplate()
    ├─ StateStore.SaveDefaultTemplate()
    ├─ JSON 직렬화
    └─ default_template.json 파일에 저장
```

### Phase 6: 애플리케이션 종료 (Shutdown)

```
17. 사용자가 창 닫기 (X 버튼 클릭)
    └─ MainWindow.OnClosed() 호출
       ↓
18. OnClosed()
    ├─ StateStore.Save(_workspace)
    │  ├─ Workspace → JSON 직렬화
    │  ├─ state.json 파일에 저장
    │  └─ 정렬 정보, 템플릿, 문서 목록 모두 저장
    └─ Logger.Instance.Info("상태 저장 완료")
       ↓
19. Application 종료
```

**처리 흐름:**
```csharp
창 닫기 (X 버튼)
  ↓
MainWindow.OnClosed()
  ├─ StateStore.Save(Workspace)
  │  ├─ JSON 직렬화
  │  └─ state.json 저장
  └─ 예외 처리 (저장 실패 시 로깅)
  ↓
Application 종료
```

---

## 📊 데이터 흐름 (Data Flow)

### 상태 저장 및 복원 흐름

```
[애플리케이션 시작]
  ↓
StateStore.Load()
  ├─ state.json 읽기
  ├─ Workspace 객체 재구성
  ├─ ImageDocument 객체들 재구성
  ├─ AlignmentInfo 검증 (캐시 파일 존재 확인)
  └─ SelectedDocumentId로 선택된 문서 복원
  ↓
[작업 수행]
  ├─ 템플릿 편집
  ├─ 이미지 로드
  └─ 오버레이 추가/수정/삭제
  ↓
[애플리케이션 종료]
  ↓
StateStore.Save()
  ├─ Workspace → JSON
  ├─ 모든 상태 정보 포함
  └─ state.json 저장
```

### Workspace 데이터 구조

```csharp
Workspace
├─ InputFolderPath: string
├─ SelectedDocumentId: string?
├─ Template: OmrTemplate
│  ├─ ReferenceWidth: int
│  ├─ ReferenceHeight: int
│  ├─ TimingMarks: ObservableCollection<RectangleOverlay>
│  └─ ScoringAreas: ObservableCollection<RectangleOverlay>
└─ Documents: ObservableCollection<ImageDocument>
   └─ ImageDocument
      ├─ ImageId: string
      ├─ SourcePath: string
      ├─ ImageWidth: int
      ├─ ImageHeight: int
      └─ AlignmentInfo: AlignmentInfo?
         ├─ Success: bool
         ├─ Confidence: double
         ├─ Rotation, ScaleX, ScaleY, TranslationX, TranslationY: double
         └─ AlignedImagePath: string?
```

---

## 🔗 컴포넌트 간 의존성

### 의존성 그래프

```
App
 └─ MainWindow
    ├─ StateStore
    ├─ NavigationViewModel
    └─ MainNavigationViewModel
       ├─ NavigationViewModel
       └─ Workspace
          └─ OmrTemplate
             ├─ TimingMarks
             └─ ScoringAreas

TemplateEditViewModel
 ├─ NavigationViewModel
 ├─ Workspace
 ├─ StateStore
 ├─ ImageLoader
 ├─ CoordinateConverter
 └─ TemplateViewModel
    └─ StateStore

TemplateEditView
 └─ TemplateEditViewModel
    └─ (위의 모든 의존성)

HomeViewModel
 └─ NavigationViewModel
```

### 서비스 의존성

```
Logger (Singleton)
 └─ PathService

StateStore
 └─ PathService

ImageAlignmentService
 └─ (독립적)

Renderer
 └─ PathService

MarkingDetector
 └─ (독립적)

ImageLoader
 └─ (독립적)
```

---

## 🎯 핵심 패턴 및 설계

### 1. **MVVM 패턴**
- **Model**: Models 계층의 데이터 클래스
- **View**: Views 계층의 XAML 및 코드비하인드
- **ViewModel**: ViewModels 계층의 로직 및 상태 관리

### 2. **Singleton 패턴**
- `Logger`: 단일 인스턴스로 로깅 관리

### 3. **Command 패턴**
- `RelayCommand`: ICommand 구현으로 UI 액션 처리

### 4. **Observer 패턴**
- `INotifyPropertyChanged`: 속성 변경 시 UI 자동 업데이트
- `CollectionChanged`: 컬렉션 변경 감지

### 5. **Service 패턴**
- 비즈니스 로직을 서비스 클래스로 분리
- 의존성 주입 방식 (생성자 주입)

---

## 📝 주요 파일 경로 (런타임)

### 사용자 데이터 경로
```
%AppData%/SimpleOverlayEditor/
├── state.json                    # 워크스페이스 상태
├── default_template.json         # 기본 템플릿
├── output/                       # 렌더링된 결과 이미지
├── aligned_cache/                # 정렬된 이미지 캐시
└── logs/                         # 로그 파일
    └── overlay_editor_YYYYMMDD.log
```

### 기본 입력 폴더
```
%Documents%/OverlayEditorInput/
```

---

## ⚠️ 주의사항 및 특이사항

### 1. **MainViewModel.cs 미사용**
- 프로젝트에 존재하지만 실제로 사용되지 않음
- `TemplateEditViewModel`이 해당 역할을 수행

### 2. **지연 초기화 (Lazy Initialization)**
- ViewModel은 모드 전환 시에만 생성됨
- 메모리 효율성 향상

### 3. **상태 복원**
- 애플리케이션 재시작 시 이전 상태 자동 복원
- 정렬된 이미지 캐시 검증 (파일이 없으면 정렬 정보 무시)

### 4. **이벤트 구독 관리**
- 여러 레벨에서 이벤트 구독 발생
- 메모리 누수 방지를 위한 적절한 구독 해제 필요

---

## 🔄 전체 생애주기 요약

```
[시작]
  ↓
App.OnStartup()
  ↓
MainWindow 생성
  ↓
Workspace 로드 (StateStore.Load)
  ↓
NavigationViewModel 생성
  ↓
홈 화면 표시 (HomeViewModel)
  ↓
[사용자 작업]
  ├─ 템플릿 편집 모드 전환
  ├─ 이미지 로드
  ├─ 오버레이 편집
  └─ 템플릿 저장
  ↓
[종료]
  ↓
MainWindow.OnClosed()
  ↓
Workspace 저장 (StateStore.Save)
  ↓
[종료 완료]
```

---

**생성일**: 2026-01-09
**분석 대상**: SimpleOverlayEditor 프로젝트 전체 구조

