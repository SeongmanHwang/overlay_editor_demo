# 빌드 가이드 - SimpleOverlayEditor

이 가이드는 터미널과 Cursor AI에 익숙하지 않은 분들을 위해 작성되었습니다. 단계별로 따라하시면 쉽게 빌드할 수 있습니다.

---

## 📋 사전 확인

### .NET SDK 설치 확인

프로젝트를 빌드하려면 .NET SDK가 설치되어 있어야 합니다.

**확인 방법**:
1. Cursor에서 터미널 열기: `Ctrl + `` (백틱 키) 또는 상단 메뉴에서 `Terminal` → `New Terminal`
2. 다음 명령어 입력:
   ```powershell
   dotnet --version
   ```
3. 버전이 표시되면 설치되어 있는 것입니다 (예: `10.0.101`)

**설치되어 있지 않다면**:
- https://dotnet.microsoft.com/download 에서 .NET 8.0 SDK 다운로드 및 설치

---

## 🚀 빌드 방법

### 방법 1: 간단한 빌드 (가장 쉬운 방법)

#### 단계별 설명

1. **터미널 열기**
   - Cursor에서 `Ctrl + `` (백틱) 키를 누르거나
   - 상단 메뉴: `Terminal` → `New Terminal`

2. **프로젝트 폴더로 이동**
   - 터미널에 다음 명령어 입력:
   ```powershell
   cd "C:\Users\Seongman Hwang\overlay_editor"
   ```
   - 또는 이미 프로젝트 폴더가 열려있다면 이 단계는 생략 가능

3. **빌드 실행**
   - 다음 명령어 입력:
   ```powershell
   dotnet build
   ```
   - 엔터 키를 누르면 빌드가 시작됩니다
   - 몇 초에서 몇 분 정도 걸릴 수 있습니다

4. **빌드 결과 확인**
   - 성공하면: `Build succeeded.` 메시지가 표시됩니다
   - 실패하면: 에러 메시지가 표시됩니다

#### 전체 명령어 (한 번에 복사해서 사용)

```powershell
cd "C:\Users\Seongman Hwang\overlay_editor"
dotnet build
```

---

### 방법 2: 빌드하고 바로 실행하기

빌드와 실행을 한 번에 하고 싶다면:

```powershell
cd "C:\Users\Seongman Hwang\overlay_editor"
dotnet run
```

이 명령어는:
1. 프로젝트를 빌드하고
2. 빌드가 성공하면 자동으로 프로그램을 실행합니다

**중지하는 방법**: 프로그램 창을 닫거나, 터미널에서 `Ctrl + C`를 누르세요.

---

### 방법 3: 릴리스 모드로 빌드하기 (배포용)

실행 파일을 만들고 싶다면:

```powershell
cd "C:\Users\Seongman Hwang\overlay_editor"
dotnet build -c Release
```

또는 실행 파일을 특정 폴더에 생성하려면:

```powershell
cd "C:\Users\Seongman Hwang\overlay_editor"
dotnet publish -c Release -o "C:\Users\Seongman Hwang\overlay_editor\publish"
```

**실행 파일 위치**:
- 빌드된 실행 파일은 `bin\Release\net8.0-windows\SimpleOverlayEditor.exe`에 생성됩니다
- 또는 `publish` 폴더에 모든 필요한 파일이 함께 생성됩니다

---

## 📁 빌드 결과물 위치

빌드가 성공하면 다음 폴더에 결과물이 생성됩니다:

### 디버그 모드 (기본)
```
C:\Users\Seongman Hwang\overlay_editor\bin\Debug\net8.0-windows\
```

### 릴리스 모드
```
C:\Users\Seongman Hwang\overlay_editor\bin\Release\net8.0-windows\
```

**주요 파일들**:
- `SimpleOverlayEditor.exe` - 실행 파일
- `SimpleOverlayEditor.dll` - 메인 라이브러리
- `Assets\` 폴더 - 필요한 설정 파일들
- 기타 DLL 파일들 - 필요한 라이브러리들

---

## 🎯 자주 사용하는 빌드 명령어

### 기본 빌드
```powershell
dotnet build
```
- 디버그 모드로 빌드
- 가장 일반적인 빌드 방법

### 릴리스 빌드
```powershell
dotnet build -c Release
```
- 최적화된 버전으로 빌드
- 배포용으로 사용

### 빌드 후 실행
```powershell
dotnet run
```
- 빌드하고 바로 실행

### 깨끗한 빌드 (이전 빌드 파일 삭제 후 재빌드)
```powershell
dotnet clean
dotnet build
```
- 문제가 있을 때 시도해볼 수 있습니다

### 빌드 정보 자세히 보기
```powershell
dotnet build -v detailed
```
- 빌드 과정을 자세히 보고 싶을 때

---

## ⚠️ 문제 해결

### 문제 1: "dotnet을 찾을 수 없습니다" 오류

**원인**: .NET SDK가 설치되지 않았거나 PATH에 등록되지 않음

**해결 방법**:
1. https://dotnet.microsoft.com/download 에서 .NET 8.0 SDK 다운로드
2. 설치 후 터미널을 다시 시작
3. `dotnet --version`으로 확인

---

### 문제 2: 빌드 오류 발생

**일반적인 해결 순서**:

1. **의존성 복원**
   ```powershell
   dotnet restore
   ```

2. **깨끗한 빌드**
   ```powershell
   dotnet clean
   dotnet build
   ```

3. **에러 메시지 확인**
   - 터미널에 표시된 에러 메시지를 읽어보세요
   - 보통 어떤 파일의 몇 번째 줄에 문제가 있는지 알려줍니다

---

### 문제 3: "프로젝트를 로드할 수 없습니다" 오류

**해결 방법**:
```powershell
# 프로젝트 폴더로 이동 확인
cd "C:\Users\Seongman Hwang\overlay_editor"

# 현재 위치 확인
pwd

# 프로젝트 파일 확인
dir *.csproj
```

---

### 문제 4: 빌드는 성공했지만 실행이 안 됩니다

**확인 사항**:
1. 실행 파일이 생성되었는지 확인:
   ```powershell
   dir "bin\Debug\net8.0-windows\SimpleOverlayEditor.exe"
   ```

2. 직접 실행해보기:
   - Windows 탐색기에서 `bin\Debug\net8.0-windows\` 폴더로 이동
   - `SimpleOverlayEditor.exe` 파일을 더블클릭

3. 터미널에서 실행:
   ```powershell
   .\bin\Debug\net8.0-windows\SimpleOverlayEditor.exe
   ```

---

## 💡 유용한 팁

### 팁 1: 터미널 명령어 입력하기

- 명령어를 입력한 후 **Enter 키**를 눌러야 실행됩니다
- 명령어는 대소문자를 구분하지 않지만, 파일 경로는 구분할 수 있습니다
- 이전 명령어를 다시 사용하려면 **위쪽 화살표 키(↑)**를 누르세요

### 팁 2: 경로 입력하기

- 경로에 공백이 있으면 따옴표로 감싸야 합니다:
  ```powershell
  cd "C:\Users\Seongman Hwang\overlay_editor"
  ```

### 팁 3: 명령어 복사하기

- 이 문서의 명령어를 복사해서 터미널에 붙여넣을 수 있습니다
- 터미널에서: 마우스 오른쪽 클릭 또는 `Shift + Insert`로 붙여넣기

### 팁 4: 빌드 시간 단축하기

- 첫 빌드는 시간이 오래 걸립니다 (의존성 다운로드 등)
- 이후 빌드는 변경된 파일만 다시 빌드하므로 빠릅니다

---

## 📝 체크리스트

빌드 전:
- [ ] .NET SDK 설치 확인 (`dotnet --version`)
- [ ] 프로젝트 폴더로 이동 (`cd "C:\Users\Seongman Hwang\overlay_editor"`)

빌드:
- [ ] `dotnet build` 실행
- [ ] "Build succeeded" 메시지 확인

실행:
- [ ] `dotnet run` 또는 실행 파일 직접 실행
- [ ] 프로그램이 정상적으로 작동하는지 확인

---

## 🎓 다음 단계

빌드가 성공했다면:
1. 프로그램을 실행해서 테스트해보세요
2. 코드를 수정한 후 다시 빌드해보세요
3. Git으로 변경사항을 관리하세요 (GIT_WORKFLOW_GUIDE.md 참고)

---

## 📞 추가 도움이 필요하신가요?

빌드 중 문제가 발생하면:
1. 터미널에 표시된 에러 메시지를 확인하세요
2. 에러 메시지를 복사해서 Cursor AI에게 물어보세요
3. 이 가이드의 "문제 해결" 섹션을 참고하세요
