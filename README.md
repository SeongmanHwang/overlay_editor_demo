# Simple Overlay Editor

이미지 위에 직사각형 오버레이를 그리고 저장하는 WPF 애플리케이션입니다.

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
│   ├── Workspace.cs               # 전체 세션 상태
│   ├── OmrTemplate.cs             # OMR 템플릿 (타이밍 마크, 채점 영역)
│   ├── OverlayType.cs             # 오버레이 타입 열거형
│   ├── MarkingResult.cs           # 마킹 감지 결과 모델
│   └── AlignmentInfo.cs           # 이미지 정렬 정보 모델
│
├── ViewModels/                    # 뷰모델 (MVVM)
│   ├── MainViewModel.cs           # 메인 뷰모델
│   ├── TemplateViewModel.cs       # 템플릿 관리 뷰모델
│   ├── MarkingViewModel.cs        # 마킹 감지 뷰모델
│   └── RelayCommand.cs            # 커맨드 패턴 구현
│
├── Views/                         # UI 뷰
│   └── MainWindow.xaml / .cs     # 메인 윈도우
│
├── Services/                      # 비즈니스 로직 서비스
│   ├── PathService.cs             # 경로 관리 (AppData, InputFolder)
│   ├── StateStore.cs              # state.json 저장/로드
│   ├── ImageLoader.cs             # 이미지 파일 로드
│   ├── ImageAlignmentService.cs   # 타이밍 마크 기반 이미지 정렬 서비스
│   ├── Renderer.cs                # 오버레이 + 이미지 합성 → output/
│   ├── MarkingDetector.cs         # 마킹 감지 서비스 (ROI 분석)
│   └── Logger.cs                  # 로깅 서비스 (파일 로그)
│
└── Utils/                         # 유틸리티
    ├── CoordinateConverter.cs     # 화면 좌표 ↔ 원본 픽셀 좌표 변환
    ├── ZoomHelper.cs              # 줌/피트 계산 (Uniform 스케일)
    └── Converters.cs              # XAML 데이터 바인딩 컨버터
```

## 주요 기능

1. **이미지 로드**
   - 폴더에서 이미지 파일들을 로드하여 목록으로 표시
   - 지원 형식: JPG, JPEG, PNG, BMP, GIF, TIFF
   - 이미지 로드 시 자동으로 정렬 처리 수행

2. **OMR 템플릿 관리 - 정렬 기능 - 오버레이 편집**
   - **타이밍 마크**: 상단에 위치한 이미지 정렬용 오버레이
     - 타이밍 마크를 기반으로 스캔 이미지의 위치/각도/크기 자동 보정
     - 정렬 신뢰도 검증 (최소 70% 이상)
     - 변환 범위 제한 (회전 ±5도, 스케일 90~110%)
     - 정렬 실패 시 원본 이미지 사용 (투명한 fallback)
   - **채점 영역**: 우측에 위치한 마킹 감지용 오버레이
   - **직사각형 오버레이 추가**: 클릭 위치에 기본 크기의 직사각형 추가
   - **오버레이 편집**: 선택한 오버레이의 X, Y, Width, Height 직접 편집
   - **오버레이 삭제**: 선택한 오버레이 또는 전체 삭제
   - 기본 템플릿 저장/로드 기능

3. **마킹 감지 (리딩)**
   - 채점 영역(ScoringArea)에서 마킹 여부 자동 감지
   - 정렬된 이미지에서 정확한 위치의 픽셀 읽기
   - 그레이스케일 변환 후 평균 밝기 분석
   - 임계값 기반 마킹 판단 (기본값: 180)
   - 단일 이미지 또는 전체 이미지 일괄 감지
   - 감지 결과를 표로 표시 (영역별 마킹 여부, 평균 밝기)

4. **상태 저장**
   - AppData에 상태를 JSON으로 저장하여 재실행 시 복구
   - 템플릿 정보, 정렬 정보, 문서 목록 저장

5. **결과 이미지 생성**
   - 정렬된 이미지에 오버레이를 적용하여 output 폴더에 저장
   - 타이밍 마크(녹색), 채점 영역(빨간색) 표시

## 이미지 표시 규칙

- **정렬된 이미지 사용**: 모든 이미지는 타이밍 마크 기반으로 정렬된 후 표시/처리됩니다
  - 이미지 로드 시 자동 정렬 적용
  - 정렬된 이미지는 캐시 폴더에 저장되어 재사용
  - 정렬 실패 시 원본 이미지 사용 (투명한 fallback)
- **Uniform 스케일**: 이미지는 항상 가로/세로 동일 비율로만 확대/축소됩니다 (왜곡 없음)
- **Fit 모드**: 이미지가 창 안에 들어가도록 자동 축소
- **최소 줌 제한**: 40% 이하로 축소되지 않음
- **화면 정렬**: 이미지는 Canvas의 **왼쪽 위**에 배치됩니다
- **가로 A4 지원**: 가로가 긴 A4 사이즈 이미지도 잘리지 않도록 처리

## 저장 위치

- **상태 파일**: `%AppData%/SimpleOverlayEditor/state.json`
- **출력 이미지**: `%AppData%/SimpleOverlayEditor/output/`
- **정렬된 이미지 캐시**: `%AppData%/SimpleOverlayEditor/aligned_cache/`
- **로그 파일**: `%AppData%/SimpleOverlayEditor/logs/overlay_editor_YYYYMMDD.log`
- **기본 입력 폴더**: `%Documents%/OverlayEditorInput/`

## 로그 파일

애플리케이션은 모든 주요 작업과 오류를 로그 파일에 기록합니다.

- **로그 위치**: `%AppData%/SimpleOverlayEditor/logs/overlay_editor_YYYYMMDD.log`
  - 예: `C:\Users\사용자명\AppData\Roaming\SimpleOverlayEditor\logs\overlay_editor_20260102.log`
- **로그 레벨**: Debug, Info, Warning, Error
- **로그 내용**: 
  - 애플리케이션 시작/종료
  - Workspace 로드/저장
  - 폴더 로드 및 이미지 로드
  - 이미지 정렬 성공/실패 (신뢰도, 변환 정보)
  - SelectedDocument 변경
  - Documents 컬렉션 변경
  - 마킹 감지 결과
  - 예외 및 오류 정보 (스택 트레이스 포함)

### 로그 파일 확인 방법

1. **Windows 탐색기에서**:
   - `Win + R` → `%AppData%` 입력 → Enter
   - `SimpleOverlayEditor\logs\` 폴더로 이동
   - 날짜별 로그 파일 확인 (예: `overlay_editor_20260102.log`)

2. **명령 프롬프트에서**:
   ```cmd
   notepad %AppData%\SimpleOverlayEditor\logs\overlay_editor_YYYYMMDD.log
   ```

3. **PowerShell에서**:
   ```powershell
   Get-Content "$env:APPDATA\SimpleOverlayEditor\logs\overlay_editor_YYYYMMDD.log" -Tail 50
   ```

문제 발생 시 로그 파일의 마지막 부분을 확인하세요. 오류 메시지와 스택 트레이스가 기록되어 있습니다.

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

1. **이미지 로드**
   - "폴더 로드" 버튼을 클릭하여 이미지가 있는 폴더를 선택
   - 이미지 로드 시 타이밍 마크 기반 정렬이 자동으로 수행됩니다
   - 정렬 성공 여부는 로그 파일에서 확인할 수 있습니다

2. **OMR 템플릿 설정**
   - **타이밍 마크 추가** (정렬용):
     - 오버레이 타입을 "TimingMark"로 선택
     - "사각형 추가 모드" 토글 버튼을 활성화
     - 기본 크기(기본값: 30×30 픽셀)를 상단 도구바에서 조정 가능
     - 이미지 위의 타이밍 마크 위치를 클릭하여 사각형 추가
     - 최소 2개 이상의 타이밍 마크를 추가하는 것이 권장됩니다
   
   - **채점 영역 추가** (리딩용):
     - 오버레이 타입을 "ScoringArea"로 선택
     - "사각형 추가 모드" 토글 버튼을 활성화
     - 각 채점 영역 위치를 클릭하여 사각형 추가

3. **오버레이 편집**
   - 오버레이 목록에서 사각형을 선택
   - 오른쪽 패널에서 X, Y, Width, Height 값을 직접 수정
   - 선택한 오버레이 삭제 또는 전체 삭제 가능

4. **기본 템플릿 저장** (선택사항)
   - 템플릿 설정 완료 후 "기본 템플릿 저장" 버튼 클릭
   - 다음 실행 시 자동으로 템플릿이 로드됩니다

5. **마킹 감지 (리딩)**
   - 채점 영역(ScoringArea)이 설정되어 있어야 합니다
   - 임계값을 조정 (기본값: 180, 0-255 범위)
   - "마킹 감지" 버튼: 현재 선택된 이미지에 대해 감지
   - "전체 감지" 버튼: 로드된 모든 이미지에 대해 일괄 감지
   - 오른쪽 패널의 "마킹 감지 결과"에서 각 영역별 마킹 여부와 평균 밝기 확인

6. **저장**
   - "저장" 버튼을 클릭하여 상태와 결과 이미지 저장
   - 상태 파일: 템플릿 정보, 정렬 정보, 문서 목록이 저장됩니다
   - 출력 이미지: 정렬된 이미지에 오버레이가 적용된 이미지가 output 폴더에 저장됩니다

## 기술 스택

- .NET 8.0
- WPF (Windows Presentation Foundation)
- Windows Forms (폴더 선택 대화상자)
- MVVM 패턴
- System.Text.Json 9.0.0 (상태 저장)

## 주의사항

- **정렬 기능**:
  - 타이밍 마크가 설정되어 있어야 정렬이 수행됩니다
  - 정렬은 이미지 로드 시 자동으로 수행되며, 정렬된 이미지는 캐시에 저장됩니다
  - 정렬 신뢰도가 70% 미만이거나 변환 범위가 초과되면 원본 이미지를 사용합니다
  - 정렬 실패 시 로그 파일에 기록되며, 원본 이미지로 자동 fallback 됩니다

- **템플릿 좌표**:
  - 오버레이 좌표는 템플릿 기준 이미지 픽셀 기준으로 저장됩니다
  - 정렬된 이미지에서는 템플릿 좌표가 그대로 적용되어 정확한 위치를 보장합니다

- **화면 표시**:
  - 정렬된 이미지가 화면에 표시되며, Uniform 스케일로만 처리되어 왜곡이 발생하지 않습니다
  - 이미지는 Canvas의 왼쪽 위에 정렬됩니다

- **저장**:
  - 저장 시 output 폴더가 삭제 후 재생성됩니다
  - 정렬 정보는 상태 파일에 저장되며, 재실행 시 정렬된 이미지 캐시를 재사용합니다

- **마킹 감지**:
  - 정렬된 이미지에서 채점 영역의 평균 밝기를 분석하여 판단합니다
  - 임계값보다 어두우면 마킹으로 판단 (기본값: 180)
  - 이미지 품질과 조명 조건에 따라 임계값 조정이 필요할 수 있습니다

