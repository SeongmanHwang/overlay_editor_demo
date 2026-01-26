# 안전한 Git 푸시 가이드

이 가이드는 실수로 main 브랜치에 푸시하는 것을 방지하고, 현재 작업 중인 브랜치에만 안전하게 푸시하는 방법을 설명합니다.

---

## 🎯 현재 상황

- **현재 브랜치**: `refactoring_marking_mode`
- **원격 브랜치**: `origin/refactoring_marking_mode` (이미 연결됨)
- **상태**: 모든 변경사항이 커밋되어 있음

---

## ✅ 현재 브랜치에 푸시하기 (가장 간단한 방법)

현재 브랜치(`refactoring_marking_mode`)에 푸시하는 가장 안전한 방법:

### 방법 1: 간단한 푸시 (권장)

```powershell
git push
```

**설명**:
- 현재 브랜치가 이미 원격과 연결되어 있으므로 (`-u` 옵션으로 이미 설정됨)
- `git push`만 입력하면 자동으로 현재 브랜치에 푸시됩니다
- **main 브랜치에 푸시되지 않습니다!**

### 방법 2: 명시적으로 브랜치 지정

```powershell
git push origin refactoring_marking_mode
```

**설명**:
- `origin`: 원격 저장소 이름
- `refactoring_marking_mode`: 푸시할 브랜치 이름
- 더 명확하지만 방법 1과 동일한 결과

---

## 🔒 main 브랜치 보호하기

### 방법 1: Git 설정으로 main 브랜치 보호 (권장)

main 브랜치에 직접 푸시하는 것을 방지하는 설정:

```powershell
# main 브랜치에 직접 푸시하는 것을 방지
git config branch.main.pushRemote no_push
```

또는 더 강력한 보호:

```powershell
# main 브랜치에 push를 시도하면 에러 발생
git config --global push.default simple
git config branch.main.pushRemote ""
```

### 방법 2: 푸시 전 확인 스크립트 만들기

더 안전하게 하려면, 푸시 전에 현재 브랜치를 확인하는 습관을 기르세요:

```powershell
# 1. 현재 브랜치 확인
git branch

# 2. main 브랜치가 아닌지 확인 후 푸시
git push
```

### 방법 3: Git Alias로 안전한 푸시 명령어 만들기

자주 사용하는 안전한 푸시 명령어를 만들어두면 편리합니다:

```powershell
# 안전한 푸시 명령어 등록
git config --global alias.safepush '!f() { branch=$(git branch --show-current); if [ "$branch" = "main" ]; then echo "❌ main 브랜치에는 직접 푸시할 수 없습니다!"; exit 1; else git push; fi; }; f'
```

사용 방법:
```powershell
git safepush
```

---

## 📝 일반적인 작업 흐름

### 1. 코드 수정 후 커밋하기

```powershell
# 1. 변경사항 확인
git status

# 2. 변경된 파일 스테이징
git add .

# 또는 특정 파일만
git add Models/ImageDocument.cs

# 3. 커밋
git commit -m "리팩토링: ImageDocument 개선"
```

### 2. 원격 저장소에 푸시하기

```powershell
# 현재 브랜치에 푸시 (안전)
git push
```

### 3. 전체 흐름 예시

```powershell
# 1. 현재 브랜치 확인
git branch

# 2. 변경사항 확인
git status

# 3. 파일 스테이징
git add .

# 4. 커밋
git commit -m "작업 내용 설명"

# 5. 푸시
git push
```

---

## ⚠️ 실수 방지 체크리스트

푸시하기 전에 항상 확인:

- [ ] `git branch`로 현재 브랜치 확인
- [ ] `refactoring_marking_mode` 브랜치인지 확인
- [ ] `main` 브랜치가 아닌지 확인
- [ ] `git status`로 커밋할 변경사항 확인
- [ ] 커밋 메시지가 적절한지 확인

---

## 🚨 실수로 main에 푸시하려고 할 때

### 상황 1: main 브랜치에 있는데 푸시하려고 할 때

```powershell
# 현재 브랜치 확인
git branch

# main 브랜치에 있다면
git checkout refactoring_marking_mode

# 그 다음 푸시
git push
```

### 상황 2: 이미 main에 푸시했다면

```powershell
# 1. 원격의 main 브랜치에서 마지막 커밋 제거 (주의!)
# 이 방법은 다른 사람과 협업 중이라면 사용하지 마세요!

# 2. 대신, main 브랜치를 이전 상태로 되돌리기
git checkout main
git reset --hard origin/main~1  # 마지막 커밋 제거
git push origin main --force    # 강제 푸시 (매우 주의!)

# 3. 작업 브랜치로 돌아가기
git checkout refactoring_marking_mode
```

**주의**: `--force` 옵션은 매우 위험합니다. 다른 사람과 협업 중이라면 사용하지 마세요!

---

## 💡 유용한 명령어들

### 현재 브랜치 확인
```powershell
git branch
# 또는
git branch --show-current
```

### 원격 브랜치와의 차이 확인
```powershell
git status
```

### 원격 브랜치 목록 보기
```powershell
git branch -r
```

### 로컬과 원격 브랜치 모두 보기
```powershell
git branch -a
```

### 원격 저장소 정보 확인
```powershell
git remote -v
```

---

## 🎯 실전 예시

### 예시 1: 코드 수정 후 푸시

```powershell
# 1. 파일 수정 (에디터에서)

# 2. 변경사항 확인
git status

# 3. 스테이징
git add ViewModels/MarkingViewModel.cs

# 4. 커밋
git commit -m "리팩토링: MarkingViewModel 로직 개선"

# 5. 현재 브랜치 확인 (안전 확인)
git branch

# 6. 푸시
git push
```

### 예시 2: 여러 파일 수정 후 푸시

```powershell
# 1. 모든 변경사항 스테이징
git add .

# 2. 커밋
git commit -m "리팩토링: 여러 서비스 레이어 개선"

# 3. 푸시
git push
```

### 예시 3: 원격과 동기화 후 푸시

```powershell
# 1. 원격의 최신 변경사항 가져오기
git fetch origin

# 2. 현재 브랜치에 병합 (필요한 경우)
git merge origin/refactoring_marking_mode

# 3. 푸시
git push
```

---

## 🔍 푸시 전 확인 명령어 (권장)

푸시하기 전에 이 명령어들을 실행해보세요:

```powershell
# 1. 현재 브랜치 확인
git branch

# 2. 변경사항 확인
git status

# 3. 커밋할 내용 확인
git diff --staged

# 4. 최근 커밋 확인
git log --oneline -5

# 5. 원격과의 차이 확인
git log origin/refactoring_marking_mode..HEAD

# 6. 모든 것이 괜찮으면 푸시
git push
```

---

## 📋 빠른 참조표

| 작업 | 명령어 |
|------|--------|
| 현재 브랜치 확인 | `git branch` |
| 변경사항 확인 | `git status` |
| 파일 스테이징 | `git add .` |
| 커밋 | `git commit -m "메시지"` |
| **안전한 푸시** | `git push` |
| 원격 브랜치 가져오기 | `git fetch origin` |
| 브랜치 전환 | `git checkout 브랜치이름` |

---

## 🎓 요약

### ✅ 안전하게 푸시하는 방법

1. **항상 현재 브랜치 확인**: `git branch`
2. **main 브랜치가 아닌지 확인**
3. **간단하게 푸시**: `git push`
4. **명시적으로 푸시**: `git push origin refactoring_marking_mode`

### ❌ 하지 말아야 할 것

- main 브랜치에서 직접 푸시하지 않기
- 확인 없이 `git push origin main` 실행하지 않기
- `--force` 옵션을 함부로 사용하지 않기

---

## 🆘 문제 해결

### 문제: "현재 브랜치가 원격과 연결되지 않았습니다"

**해결**:
```powershell
git push -u origin refactoring_marking_mode
```

### 문제: "원격에 이미 다른 커밋이 있습니다"

**해결**:
```powershell
# 1. 원격 변경사항 가져오기
git fetch origin

# 2. 병합
git merge origin/refactoring_marking_mode

# 3. 다시 푸시
git push
```

### 문제: "권한이 없습니다" 또는 "인증 실패"

**해결**:
- GitHub 인증 확인
- Personal Access Token 확인
- Git Credential Manager 확인

---

이제 안전하게 작업 브랜치에만 푸시할 수 있습니다! 🎉
