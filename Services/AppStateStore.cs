using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SimpleOverlayEditor.Models;

namespace SimpleOverlayEditor.Services
{
    /// <summary>
    /// 애플리케이션 전역 상태(app_state.json)를 관리하는 서비스입니다.
    /// 회차 목록과 마지막 선택 회차를 저장/로드합니다.
    /// </summary>
    public class AppStateStore
    {
        private const string AppStateFileName = "app_state.json";

        /// <summary>
        /// 애플리케이션 상태를 로드합니다.
        /// </summary>
        public AppState LoadAppState()
        {
            var appStatePath = PathService.AppStateFilePath;

            if (!File.Exists(appStatePath))
            {
                return new AppState
                {
                    Rounds = new List<RoundInfo>(),
                    LastSelectedRound = null
                };
            }

            try
            {
                var json = File.ReadAllText(appStatePath);
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var appState = new AppState
                {
                    Rounds = new List<RoundInfo>(),
                    LastSelectedRound = root.TryGetProperty("LastSelectedRound", out var lastSelected)
                        ? lastSelected.GetString()
                        : null
                };

                if (root.TryGetProperty("Rounds", out var roundsElement) && roundsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var roundElem in roundsElement.EnumerateArray())
                    {
                        var round = new RoundInfo
                        {
                            Name = roundElem.TryGetProperty("Name", out var name)
                                ? name.GetString() ?? string.Empty
                                : string.Empty,
                            CreatedAt = roundElem.TryGetProperty("CreatedAt", out var createdAt)
                                ? DateTime.Parse(createdAt.GetString() ?? DateTime.UtcNow.ToString("O"))
                                : DateTime.UtcNow,
                            LastAccessedAt = roundElem.TryGetProperty("LastAccessedAt", out var lastAccessed)
                                ? DateTime.Parse(lastAccessed.GetString() ?? DateTime.UtcNow.ToString("O"))
                                : DateTime.UtcNow
                        };

                        if (!string.IsNullOrEmpty(round.Name))
                        {
                            appState.Rounds.Add(round);
                        }
                    }
                }

                return appState;
            }
            catch (Exception ex)
            {
                Logger.Instance.Warning($"앱 상태 로드 실패: {ex.Message}");
                return new AppState
                {
                    Rounds = new List<RoundInfo>(),
                    LastSelectedRound = null
                };
            }
        }

        /// <summary>
        /// 애플리케이션 상태를 저장합니다.
        /// </summary>
        public void SaveAppState(AppState appState)
        {
            try
            {
                PathService.EnsureDirectories();

                var data = new
                {
                    Rounds = appState.Rounds.Select(r => new
                    {
                        Name = r.Name,
                        CreatedAt = r.CreatedAt.ToString("O"),
                        LastAccessedAt = r.LastAccessedAt.ToString("O")
                    }).ToList(),
                    LastSelectedRound = appState.LastSelectedRound
                };

                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });

                File.WriteAllText(PathService.AppStateFilePath, json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"앱 상태 저장 실패: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Rounds 폴더를 스캔해서 존재하는 회차 폴더를 모두 인식합니다.
        /// app_state.json에 없지만 폴더가 존재하면 목록에 추가합니다.
        /// </summary>
        public List<RoundInfo> DiscoverRounds()
        {
            var discoveredRounds = new List<RoundInfo>();
            var roundsFolder = PathService.RoundsFolder;

            if (!Directory.Exists(roundsFolder))
            {
                return discoveredRounds;
            }

            try
            {
                var existingAppState = LoadAppState();
                var existingRoundNames = new HashSet<string>(existingAppState.Rounds.Select(r => r.Name), StringComparer.OrdinalIgnoreCase);
                var existingSafeNames = new HashSet<string>(existingAppState.Rounds.Select(r => PathService.SanitizeRoundName(r.Name)), StringComparer.OrdinalIgnoreCase);

                // Rounds 폴더의 모든 하위 폴더를 스캔
                foreach (var folderPath in Directory.EnumerateDirectories(roundsFolder))
                {
                    var folderName = Path.GetFileName(folderPath);
                    
                    // "기존 폴더"는 제외 (마이그레이션 기능 제거로 더 이상 사용하지 않음)
                    if (folderName.Equals("기존 폴더", StringComparison.OrdinalIgnoreCase) ||
                        PathService.SanitizeRoundName("기존 폴더").Equals(folderName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    
                    // 폴더명이 이미 알려진 SafeRoundName과 일치하는지 확인
                    var matchingRound = existingAppState.Rounds.FirstOrDefault(r => 
                        PathService.SanitizeRoundName(r.Name).Equals(folderName, StringComparison.OrdinalIgnoreCase));

                    if (matchingRound != null)
                    {
                        // 이미 app_state.json에 있는 회차
                        discoveredRounds.Add(matchingRound);
                    }
                    else
                    {
                        // app_state.json에 없지만 폴더가 존재하는 경우 (재설치 시나리오)
                        // 폴더명을 역으로 회차 이름으로 추정 (안전하지 않지만 최선의 추정)
                        var estimatedName = folderName;
                        var createdAt = Directory.GetCreationTimeUtc(folderPath);
                        var lastAccessed = Directory.GetLastWriteTimeUtc(folderPath);

                        discoveredRounds.Add(new RoundInfo
                        {
                            Name = estimatedName,
                            CreatedAt = createdAt,
                            LastAccessedAt = lastAccessed
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Warning($"회차 자동 발견 실패: {ex.Message}");
            }

            return discoveredRounds;
        }

        /// <summary>
        /// 새 회차를 생성합니다. 중복 처리를 포함합니다.
        /// </summary>
        public RoundInfo CreateRound(string roundName)
        {
            if (string.IsNullOrWhiteSpace(roundName))
            {
                throw new ArgumentException("회차 이름은 비어있을 수 없습니다.", nameof(roundName));
            }

            var appState = LoadAppState();
            var existingSafeNames = new HashSet<string>(
                appState.Rounds.Select(r => PathService.SanitizeRoundName(r.Name)),
                StringComparer.OrdinalIgnoreCase);

            // DiscoverRounds로 폴더에서도 확인
            var discoveredRounds = DiscoverRounds();
            foreach (var discovered in discoveredRounds)
            {
                var safeName = PathService.SanitizeRoundName(discovered.Name);
                existingSafeNames.Add(safeName);
            }

            // 중복 처리: 사용 가능한 SafeRoundName 찾기
            var safeRoundName = PathService.FindAvailableRoundName(roundName, existingSafeNames);

            // 실제로 사용할 회차 이름 결정 (SafeRoundName이 원본과 다르면 원본에 접미사 추가)
            var finalRoundName = roundName;
            if (!PathService.SanitizeRoundName(roundName).Equals(safeRoundName, StringComparison.OrdinalIgnoreCase))
            {
                // 원본 이름에 접미사를 추가해야 함
                var baseSafeName = PathService.SanitizeRoundName(roundName);
                var suffix = 2;
                while (true)
                {
                    var candidate = $"{roundName}_{suffix}";
                    if (PathService.SanitizeRoundName(candidate).Equals(safeRoundName, StringComparison.OrdinalIgnoreCase))
                    {
                        finalRoundName = candidate;
                        break;
                    }
                    suffix++;
                    if (suffix > 1000) // 무한 루프 방지
                    {
                        finalRoundName = roundName;
                        break;
                    }
                }
            }

            var round = new RoundInfo
            {
                Name = finalRoundName,
                CreatedAt = DateTime.UtcNow,
                LastAccessedAt = DateTime.UtcNow
            };

            // 회차 폴더 생성
            var roundRoot = PathService.GetRoundRoot(finalRoundName);
            Directory.CreateDirectory(roundRoot);
            PathService.EnsureRoundDirectories(finalRoundName);

            // 기본값 복사 (템플릿, 문항 배점, 면접위원 명렬)
            InitializeRoundWithDefaults(finalRoundName);

            // app_state.json에 추가
            appState.Rounds.Add(round);
            SaveAppState(appState);

            Logger.Instance.Info($"회차 생성: {finalRoundName} (SafeName: {safeRoundName})");

            return round;
        }

        /// <summary>
        /// 새 회차에 기본값(템플릿, 문항 배점, 면접위원 명렬)을 복사합니다.
        /// </summary>
        private void InitializeRoundWithDefaults(string roundName)
        {
            var oldRound = PathService.CurrentRound;
            try
            {
                // 임시로 새 회차를 CurrentRound로 설정
                PathService.CurrentRound = roundName;

                // 기본 템플릿 복사
                var templateStore = new TemplateStore();
                var template = templateStore.Load();
                if (template != null)
                {
                    // Load() 내부에서 이미 저장하므로 중복 저장 불필요
                    // 하지만 안전을 위해 한 번 더 저장 (같은 내용이므로 문제 없음)
                    try
                    {
                        templateStore.Save(template);
                        Logger.Instance.Info($"회차 '{roundName}'에 기본 템플릿 복사 완료");
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Warning($"회차 '{roundName}' 기본 템플릿 저장 실패: {ex.Message}");
                    }
                }
                else
                {
                    Logger.Instance.Warning($"회차 '{roundName}' 기본 템플릿 로드 실패");
                }

                // 기본 문항 배점 복사
                var scoringRuleStore = new ScoringRuleStore();
                var scoringRule = scoringRuleStore.LoadScoringRule();
                scoringRuleStore.SaveScoringRule(scoringRule);
                Logger.Instance.Info($"회차 '{roundName}'에 기본 문항 배점 복사 완료");

                // 기본 면접위원 명렬 복사
                var registryStore = new RegistryStore();
                var interviewerRegistry = registryStore.LoadInterviewerRegistry();
                registryStore.SaveInterviewerRegistry(interviewerRegistry);
                Logger.Instance.Info($"회차 '{roundName}'에 기본 면접위원 명렬 복사 완료");
            }
            catch (Exception ex)
            {
                Logger.Instance.Warning($"회차 '{roundName}' 기본값 초기화 실패: {ex.Message}");
                // 기본값 초기화 실패해도 회차 생성은 계속 진행
            }
            finally
            {
                // 원래 회차로 복원
                PathService.CurrentRound = oldRound;
            }
        }

        /// <summary>
        /// 회차 이름을 변경합니다. 폴더명 변경과 app_state.json 업데이트를 수행합니다.
        /// </summary>
        public void RenameRound(string oldName, string newName)
        {
            if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName))
            {
                throw new ArgumentException("회차 이름은 비어있을 수 없습니다.");
            }

            if (oldName.Equals(newName, StringComparison.OrdinalIgnoreCase))
            {
                return; // 변경 없음
            }

            var appState = LoadAppState();
            var oldRound = appState.Rounds.FirstOrDefault(r => r.Name.Equals(oldName, StringComparison.OrdinalIgnoreCase));
            if (oldRound == null)
            {
                throw new InvalidOperationException($"회차를 찾을 수 없습니다: {oldName}");
            }

            // 기존 SafeRoundName 제외하고 중복 확인
            var existingSafeNames = new HashSet<string>(
                appState.Rounds.Where(r => !r.Name.Equals(oldName, StringComparison.OrdinalIgnoreCase))
                    .Select(r => PathService.SanitizeRoundName(r.Name)),
                StringComparer.OrdinalIgnoreCase);

            // DiscoverRounds로 폴더에서도 확인
            var discoveredRounds = DiscoverRounds();
            foreach (var discovered in discoveredRounds)
            {
                if (!discovered.Name.Equals(oldName, StringComparison.OrdinalIgnoreCase))
                {
                    var safeName = PathService.SanitizeRoundName(discovered.Name);
                    existingSafeNames.Add(safeName);
                }
            }

            // 중복 처리: 사용 가능한 SafeRoundName 찾기
            var safeRoundName = PathService.FindAvailableRoundName(newName, existingSafeNames);

            // 실제로 사용할 회차 이름 결정
            var finalRoundName = newName;
            if (!PathService.SanitizeRoundName(newName).Equals(safeRoundName, StringComparison.OrdinalIgnoreCase))
            {
                var baseSafeName = PathService.SanitizeRoundName(newName);
                var suffix = 2;
                while (true)
                {
                    var candidate = $"{newName}_{suffix}";
                    if (PathService.SanitizeRoundName(candidate).Equals(safeRoundName, StringComparison.OrdinalIgnoreCase))
                    {
                        finalRoundName = candidate;
                        break;
                    }
                    suffix++;
                    if (suffix > 1000)
                    {
                        finalRoundName = newName;
                        break;
                    }
                }
            }

            var oldRoundRoot = PathService.GetRoundRoot(oldName);
            var newRoundRoot = PathService.GetRoundRoot(finalRoundName);

            // 폴더명 변경
            if (Directory.Exists(oldRoundRoot))
            {
                if (Directory.Exists(newRoundRoot))
                {
                    throw new InvalidOperationException($"대상 회차 폴더가 이미 존재합니다: {newRoundRoot}");
                }

                Directory.Move(oldRoundRoot, newRoundRoot);
                Logger.Instance.Info($"회차 폴더 이름 변경: {oldRoundRoot} → {newRoundRoot}");
            }

            // app_state.json 업데이트
            oldRound.Name = finalRoundName;
            oldRound.LastAccessedAt = DateTime.UtcNow;

            // LastSelectedRound도 업데이트
            if (appState.LastSelectedRound != null && appState.LastSelectedRound.Equals(oldName, StringComparison.OrdinalIgnoreCase))
            {
                appState.LastSelectedRound = finalRoundName;
            }

            SaveAppState(appState);

            // PathService.CurrentRound도 업데이트
            if (PathService.CurrentRound != null && PathService.CurrentRound.Equals(oldName, StringComparison.OrdinalIgnoreCase))
            {
                PathService.CurrentRound = finalRoundName;
            }

            Logger.Instance.Info($"회차 이름 변경: {oldName} → {finalRoundName}");
        }

        /// <summary>
        /// 회차 접근 시각을 업데이트합니다.
        /// </summary>
        public void UpdateRoundAccessTime(string roundName)
        {
            var appState = LoadAppState();
            var round = appState.Rounds.FirstOrDefault(r => r.Name.Equals(roundName, StringComparison.OrdinalIgnoreCase));
            if (round != null)
            {
                round.LastAccessedAt = DateTime.UtcNow;
                appState.LastSelectedRound = roundName;
                SaveAppState(appState);
            }
        }
    }

    /// <summary>
    /// 애플리케이션 전역 상태를 나타냅니다.
    /// </summary>
    public class AppState
    {
        public List<RoundInfo> Rounds { get; set; } = new List<RoundInfo>();
        public string? LastSelectedRound { get; set; }
    }
}
