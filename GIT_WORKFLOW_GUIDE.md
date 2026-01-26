# Git 작업 가이드 - 리팩토링을 위한 안전한 워크플로우

## 📋 현재 Git 상태

- **현재 브랜치**: `main`
- **원격 저장소**: `origin` (https://github.com/SeongmanHwang/overlay_editor_demo.git)
- **상태**: origin/main보다 1 커밋 앞서 있음
- **수정된 파일**: 8개 파일이 수정되었지만 아직 커밋되지 않음

---

## 🎯 리팩토링을 위한 안전한 작업 흐름

### 1단계: 현재 작업을 안전하게 저장하기

리팩토링을 시작하기 전에, 현재 수정된 파일들을 먼저 커밋하거나 저장해야 합니다.

#### 옵션 A: 현재 변경사항을 커밋하기 (권장)

```powershell
# 1. 현재 디렉토리로 이동
cd "C:\Users\Seongman Hwang\overlay_editor"

# 2. 변경된 파일들을 스테이징 영역에 추가
git add .

# 3. 커밋 메시지와 함께 커밋
git commit -m "작업 중인 변경사항 저장"
```

#### 옵션 B: 현재 변경사항을 임시 저장하기 (나중에 다시 적용)

```powershell
# 변경사항을 임시 저장
git stash save "리팩토링 전 작업 중인 내용"

# 나중에 다시 적용하려면
git stash pop
```

---

### 2단계: 리팩토링용 새 브랜치 생성하기

현재 `main` 브랜치를 기준점으로 삼고, 새로운 브랜치에서 작업합니다.

```powershell
# 1. main 브랜치로 이동 (이미 main에 있다면 생략 가능)
git checkout main

# 2. 최신 상태로 업데이트 (원격 저장소와 동기화)
git pull origin main

# 3. 리팩토링용 새 브랜치 생성 및 이동
git checkout -b refactoring
```

**설명**:
- `git checkout -b refactoring`: `refactoring`이라는 이름의 새 브랜치를 만들고 바로 이동합니다
- 브랜치 이름은 원하는 대로 변경 가능합니다 (예: `refactoring-2024`, `refactor-architecture` 등)

---

### 3단계: 리팩토링 작업하기

이제 `refactoring` 브랜치에서 자유롭게 작업하세요. 파일을 수정하고, 테스트하고, 커밋할 수 있습니다.

#### 작업 중 커밋하기

```powershell
# 1. 변경된 파일 확인
git status

# 2. 특정 파일만 스테이징하기
git add Models/ImageDocument.cs
git add Services/SessionStore.cs

# 또는 모든 변경사항 스테이징하기
git add .

# 3. 커밋하기
git commit -m "리팩토링: ImageDocument와 SessionStore 개선"
```

**커밋 메시지 작성 팁**:
- 간결하고 명확하게 작성
- 무엇을 했는지 설명 (예: "리팩토링: 서비스 레이어 분리", "리팩토링: ViewModel 로직 정리")
- 여러 파일을 한 번에 커밋하거나, 논리적으로 그룹화해서 커밋

---

### 4단계: 문제 발생 시 안전하게 되돌리기

리팩토링 중 문제가 발생했다면, 언제든지 `main` 브랜치로 돌아갈 수 있습니다.

#### 방법 1: 브랜치를 버리고 main으로 돌아가기 (모든 변경사항 삭제)

```powershell
# 1. main 브랜치로 이동
git checkout main

# 2. refactoring 브랜치 삭제 (모든 변경사항이 사라집니다)
git branch -D refactoring
```

**주의**: 이 방법은 `refactoring` 브랜치의 모든 작업을 완전히 삭제합니다!

#### 방법 2: 브랜치는 유지하고 main으로만 돌아가기

```powershell
# main 브랜치로 이동 (refactoring 브랜치는 그대로 유지)
git checkout main
```

나중에 다시 `refactoring` 브랜치로 돌아가려면:
```powershell
git checkout refactoring
```

#### 방법 3: 특정 커밋으로 되돌리기

```powershell
# 1. 커밋 히스토리 확인
git log --oneline

# 2. 되돌리고 싶은 커밋의 해시 복사 (예: 93bf874)

# 3. 해당 커밋으로 되돌리기 (모든 변경사항 삭제)
git reset --hard 93bf874
```

---

### 5단계: 리팩토링이 성공적으로 완료된 경우

리팩토링이 잘 완료되었다면, `main` 브랜치에 병합합니다.

```powershell
# 1. main 브랜치로 이동
git checkout main

# 2. refactoring 브랜치의 변경사항을 main에 병합
git merge refactoring

# 3. (선택사항) 병합 후 refactoring 브랜치 삭제
git branch -d refactoring
```

---

## 📚 기본 Git 명령어 가이드

### 현재 상태 확인하기

```powershell
# 현재 브랜치와 변경사항 확인
git status

# 커밋 히스토리 보기 (간단히)
git log --oneline

# 커밋 히스토리 보기 (자세히)
git log

# 브랜치 목록 보기
git branch
```

### 파일 스테이징하기

```powershell
# 특정 파일만 추가
git add Models/ImageDocument.cs

# 특정 폴더의 모든 파일 추가
git add Models/

# 모든 변경사항 추가
git add .

# 변경된 파일만 추가 (새 파일 제외)
git add -u
```

### 커밋하기

```powershell
# 간단한 메시지와 함께 커밋
git commit -m "커밋 메시지"

# 여러 줄 메시지와 함께 커밋
git commit -m "제목" -m "상세 설명"
```

### 브랜치 관리하기

```powershell
# 브랜치 목록 보기
git branch

# 새 브랜치 생성 (이동하지 않음)
git branch 브랜치이름

# 브랜치 생성하고 이동
git checkout -b 브랜치이름

# 브랜치 이동
git checkout 브랜치이름

# 브랜치 삭제 (병합된 브랜치만)
git branch -d 브랜치이름

# 브랜치 강제 삭제 (병합되지 않은 브랜치도)
git branch -D 브랜치이름
```

### 원격 저장소와 동기화하기

```powershell
# 원격 저장소의 변경사항 가져오기 (병합하지 않음)
git fetch origin

# 원격 저장소의 변경사항 가져와서 병합
git pull origin main

# 로컬 변경사항을 원격 저장소에 업로드
git push origin main

# 새 브랜치를 원격에 푸시
git push -u origin refactoring
```

---

## 🔄 실전 예시: 리팩토링 워크플로우

### 시나리오: 대대적인 리팩토링 시작하기

```powershell
# 1. 현재 작업 디렉토리로 이동
cd "C:\Users\Seongman Hwang\overlay_editor"

# 2. 현재 상태 확인
git status

# 3. 현재 변경사항 커밋 (또는 stash)
git add .
git commit -m "리팩토링 전 작업 저장"

# 4. main 브랜치 최신화
git pull origin main

# 5. 리팩토링 브랜치 생성 및 이동
git checkout -b refactoring

# 이제 리팩토링 작업 시작!
```

### 시나리오: 문제 발생 시 되돌리기

```powershell
# 1. 현재 브랜치 확인
git branch

# 2. main 브랜치로 돌아가기
git checkout main

# 3. refactoring 브랜치 삭제 (원한다면)
git branch -D refactoring

# 이제 main 브랜치의 깨끗한 상태로 돌아왔습니다!
```

### 시나리오: 작업 중간에 커밋하기

```powershell
# 1. 변경사항 확인
git status

# 2. 관련된 파일들 스테이징
git add Models/
git add Services/

# 3. 커밋
git commit -m "리팩토링: Models와 Services 레이어 개선"

# 4. 계속 작업...
```

---

## ⚠️ 주의사항

1. **항상 현재 상태를 확인하세요**: `git status`로 현재 브랜치와 변경사항을 확인
2. **커밋 전에 테스트하세요**: 리팩토링 후 코드가 제대로 작동하는지 확인
3. **작은 단위로 커밋하세요**: 큰 변경사항을 여러 개의 작은 커밋으로 나누면 되돌리기 쉽습니다
4. **브랜치 삭제 전에 확인하세요**: `-D` 옵션은 강제 삭제이므로 신중하게 사용

---

## 🆘 도움이 필요할 때

### 실수로 잘못된 파일을 스테이징했을 때

```powershell
# 스테이징 취소 (파일은 그대로 유지)
git reset HEAD 파일명

# 모든 스테이징 취소
git reset HEAD
```

### 커밋 메시지를 잘못 입력했을 때

```powershell
# 마지막 커밋 메시지 수정
git commit --amend -m "올바른 메시지"
```

### 변경사항을 완전히 버리고 싶을 때

```powershell
# 특정 파일의 변경사항 취소
git restore 파일명

# 모든 변경사항 취소 (주의!)
git restore .
```

---

## 📝 체크리스트

리팩토링 시작 전:
- [ ] 현재 변경사항 커밋 또는 stash
- [ ] main 브랜치 최신화 (`git pull origin main`)
- [ ] 새 브랜치 생성 (`git checkout -b refactoring`)

리팩토링 중:
- [ ] 작은 단위로 커밋
- [ ] 각 커밋 후 테스트
- [ ] 의미 있는 커밋 메시지 작성

문제 발생 시:
- [ ] `git checkout main`으로 안전한 브랜치로 이동
- [ ] 필요시 `git branch -D refactoring`으로 브랜치 삭제

성공적으로 완료 시:
- [ ] `git checkout main`으로 이동
- [ ] `git merge refactoring`으로 병합
- [ ] `git push origin main`으로 원격 저장소에 업로드
