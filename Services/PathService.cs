using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SimpleOverlayEditor.Services
{
    public static class PathService
    {
        private static string? _currentRound;

        /// <summary>

        /// 현재 선택된 회차 이름 (null이면 회차 시스템 미사용)
        /// </summary>
        public static string? CurrentRound
        {
            get => _currentRound;
            set
            {

                if (string.Equals(_currentRound, value, StringComparison.Ordinal))
                {
                    return;
                }

                var previous = _currentRound;

                _currentRound = value;
                Logger.Instance.Debug($"PathService.CurrentRound 변경: {value ?? "(null)"}");
            
                CurrentRoundChanged?.Invoke(null, new RoundChangedEventArgs(previous, value));
            }
        }


        /// <summary>
        /// CurrentRound 변경 이벤트입니다.
        /// </summary>
        public static event EventHandler<RoundChangedEventArgs>? CurrentRoundChanged;

        public static string AppDataFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "SimpleOverlayEditor");

        /// <summary>
        /// 전역 설정 파일 경로 (회차 목록 + 마지막 선택 회차)
        /// </summary>
        public static string AppStateFilePath =>
            Path.Combine(AppDataFolder, "app_state.json");

        /// <summary>
        /// 회차별 데이터가 저장되는 루트 폴더
        /// </summary>
        public static string RoundsFolder =>
            Path.Combine(AppDataFolder, "Rounds");

        /// <summary>
        /// 현재 회차의 루트 경로를 반환합니다.
        /// </summary>
        public static string GetRoundRoot(string roundName)
        {
            var safeName = SanitizeRoundName(roundName);
            return Path.Combine(RoundsFolder, safeName);
        }

        /// <summary>
        /// 회차 이름을 안전한 폴더명으로 변환합니다.
        /// Windows 파일명으로 사용 불가능한 문자를 제거/치환합니다.
        /// </summary>
        public static string SanitizeRoundName(string roundName)
        {
            if (string.IsNullOrWhiteSpace(roundName))
            {
                return "Round";
            }

            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = roundName;

            foreach (var c in invalidChars)
            {
                sanitized = sanitized.Replace(c, '_');
            }

            // 추가로 위험한 문자들도 치환
            sanitized = sanitized.Replace(':', '_')
                                 .Replace('\\', '_')
                                 .Replace('/', '_')
                                 .Replace('*', '_')
                                 .Replace('?', '_')
                                 .Replace('"', '_')
                                 .Replace('<', '_')
                                 .Replace('>', '_')
                                 .Replace('|', '_');

            // 앞뒤 공백 및 점 제거 (Windows 제약)
            sanitized = sanitized.Trim().TrimEnd('.');

            if (string.IsNullOrWhiteSpace(sanitized))
            {
                return "Round";
            }

            return sanitized;
        }

        /// <summary>
        /// 중복 처리를 위한 사용 가능한 회차 이름을 찾습니다 (연쇄 대응).
        /// baseName의 SafeRoundName이 existingSafeNames에 없을 때까지 _2, _3, ... 접미사를 추가합니다.
        /// </summary>
        public static string FindAvailableRoundName(string baseName, ISet<string> existingSafeNames)
        {
            var baseSafeName = SanitizeRoundName(baseName);
            var candidate = baseSafeName;
            int suffix = 2;

            while (existingSafeNames.Contains(candidate))
            {
                candidate = $"{baseSafeName}_{suffix}";
                suffix++;
                
                // 무한 루프 방지
                if (suffix > 10000)
                {
                    candidate = $"{baseSafeName}_{Guid.NewGuid():N}";
                    break;
                }
            }

            return candidate;
        }

        /// <summary>
        /// 현재 회차 기준 상태 파일 경로를 반환합니다.
        /// </summary>
        public static string StateFilePath
        {
            get
            {
                if (CurrentRound != null)
                {
                    return Path.Combine(GetRoundRoot(CurrentRound), "state.json");
                }
                return Path.Combine(AppDataFolder, "state.json");
            }
        }

        /// <summary>
        /// 현재 회차 기준 세션 파일 경로를 반환합니다.
        /// </summary>
        public static string SessionFilePath
        {
            get
            {
                if (CurrentRound != null)
                {
                    return Path.Combine(GetRoundRoot(CurrentRound), "session.json");
                }
                return Path.Combine(AppDataFolder, "session.json");
            }
        }

        /// <summary>
        /// 현재 회차 기준 템플릿 파일 경로를 반환합니다.
        /// </summary>
        public static string TemplateFilePath
        {
            get
            {
                if (CurrentRound != null)
                {
                    return Path.Combine(GetRoundRoot(CurrentRound), "template.json");
                }
                return Path.Combine(AppDataFolder, "template.json");
            }
        }

        /// <summary>
        /// 현재 회차 기준 출력 폴더 경로를 반환합니다.
        /// </summary>
        public static string OutputFolder
        {
            get
            {
                if (CurrentRound == null)
                {
                    throw new InvalidOperationException("CurrentRound가 설정되지 않았습니다. (Rounds 전용 모드)");
                }
                return Path.Combine(GetRoundRoot(CurrentRound), "overlay_cache");
            }
        }

        /// <summary>
        /// 현재 회차 기준 정렬 캐시 폴더 경로를 반환합니다.
        /// </summary>
        public static string AlignmentCacheFolder
        {
            get
            {
                if (CurrentRound == null)
                {
                    throw new InvalidOperationException("CurrentRound가 설정되지 않았습니다. (Rounds 전용 모드)");
                }
                return Path.Combine(GetRoundRoot(CurrentRound), "aligned_omr");
            }
        }

        /// <summary>
        /// 현재 회차 기준 바코드 디버그 폴더 경로를 반환합니다.
        /// </summary>
        public static string BarcodeDebugFolder
        {
            get
            {
                if (CurrentRound == null)
                {
                    throw new InvalidOperationException("CurrentRound가 설정되지 않았습니다. (Rounds 전용 모드)");
                }
                return Path.Combine(GetRoundRoot(CurrentRound), "barcode_debug");
            }
        }

        /// <summary>
        /// 로그 폴더는 전역으로 유지 (회차별 분리하지 않음)
        /// </summary>
        public static string LogsFolder =>
            Path.Combine(AppDataFolder, "logs");

        public static void EnsureDirectories()
        {
            Directory.CreateDirectory(AppDataFolder);
            // 로그 폴더는 전역으로 생성
            Directory.CreateDirectory(LogsFolder);
        }

        /// <summary>
        /// 특정 회차의 필요한 디렉토리를 생성합니다.
        /// </summary>
        public static void EnsureRoundDirectories(string roundName)
        {
            var roundRoot = GetRoundRoot(roundName);
            Directory.CreateDirectory(roundRoot);
            Directory.CreateDirectory(Path.Combine(roundRoot, "overlay_cache"));
            Directory.CreateDirectory(Path.Combine(roundRoot, "aligned_omr"));
            Directory.CreateDirectory(Path.Combine(roundRoot, "barcode_debug"));
        }

        /// <summary>
        /// AppData 하위 캐시/출력 폴더를 정리합니다.
        /// - 오래된 파일 삭제 (maxAgeDays)
        /// - 총 용량 상한 (maxTotalBytes) 초과 시 오래된 파일부터 삭제
        /// 주의: aligned_omr/overlay_cache는 "재생성 가능한 캐시" 성격입니다.
        /// </summary>
        public static void CleanupAppData()
        {
            EnsureDirectories();

            // Rounds 전용 모드:
            // 앱 시작 시점엔 CurrentRound가 아직 null일 수 있으므로,
            // 특정 회차에 종속된 속성(OutputFolder/AlignmentCacheFolder/BarcodeDebugFolder)을 사용하지 않습니다.
            // 대신 Rounds 하위의 모든 회차 폴더를 순회하며 캐시를 정리합니다.
            if (!Directory.Exists(RoundsFolder))
            {
                return;
            }

            foreach (var roundRoot in Directory.EnumerateDirectories(RoundsFolder))
            {
                var alignedOmrFolder = Path.Combine(roundRoot, "aligned_omr");
                var overlayCacheFolder = Path.Combine(roundRoot, "overlay_cache");
                var barcodeDebugFolder = Path.Combine(roundRoot, "barcode_debug");
                var sessionFilePath = Path.Combine(roundRoot, "session.json");

                // 현재 세션이 참조하는 aligned_omr 파일은 보존 (정리로 인해 Marking 모드에서 대량 재정렬이 발생하는 것을 방지)
                var protectedAlignedCachePaths = GetSessionReferencedAlignedCachePaths(
                    sessionFilePath: sessionFilePath,
                    alignmentCacheFolder: alignedOmrFolder);

                // aligned_omr가 폭증하기 쉬워서 가장 엄격하게 관리합니다.
                CleanupFolder(
                    folderPath: alignedOmrFolder,
                    maxAgeDays: 14,
                    maxTotalBytes: 5L * 1024 * 1024 * 1024, // 5GB
                    searchPattern: "*.png",
                    protectedFilePaths: protectedAlignedCachePaths);

                // overlay_cache은 사용자에게 직접 노출되는 "내보내기" 용도가 아니라 내부 생성물에 가깝습니다.
                CleanupFolder(
                    folderPath: overlayCacheFolder,
                    maxAgeDays: 7,
                    maxTotalBytes: 512L * 1024 * 1024, // 512MB
                    searchPattern: "*.png",
                    protectedFilePaths: null);

                // 디버그 스냅샷은 너무 빨리 쌓이므로 더 짧게 유지합니다.
                CleanupFolder(
                    folderPath: barcodeDebugFolder,
                    maxAgeDays: 3,
                    maxTotalBytes: 512L * 1024 * 1024, // 512MB
                    searchPattern: "*.png",
                    protectedFilePaths: null);
            }
        }

        private static void CleanupFolder(
            string folderPath,
            int maxAgeDays,
            long maxTotalBytes,
            string searchPattern,
            ISet<string>? protectedFilePaths)
        {
            try
            {
                if (!Directory.Exists(folderPath))
                {
                    return;
                }

                var cutoffUtc = DateTime.UtcNow.AddDays(-maxAgeDays);
                var files = Directory.EnumerateFiles(folderPath, searchPattern, SearchOption.TopDirectoryOnly)
                    .Select(p => new FileInfo(p))
                    .OrderBy(fi => fi.LastWriteTimeUtc)
                    .ToList();

                // 1) 오래된 파일 우선 삭제
                foreach (var fi in files.Where(f => f.LastWriteTimeUtc < cutoffUtc).ToList())
                {
                    if (protectedFilePaths != null && protectedFilePaths.Contains(fi.FullName))
                    {
                        continue;
                    }
                    TryDeleteFile(fi);
                }

                // 2) 용량 상한 초과 시 오래된 파일부터 삭제
                files = Directory.EnumerateFiles(folderPath, searchPattern, SearchOption.TopDirectoryOnly)
                    .Select(p => new FileInfo(p))
                    .OrderBy(fi => fi.LastWriteTimeUtc)
                    .ToList();

                long totalBytes = files.Sum(f => f.Length);
                if (totalBytes <= maxTotalBytes)
                {
                    return;
                }

                foreach (var fi in files)
                {
                    if (totalBytes <= maxTotalBytes)
                    {
                        break;
                    }

                    if (protectedFilePaths != null && protectedFilePaths.Contains(fi.FullName))
                    {
                        continue;
                    }

                    var len = fi.Length;
                    if (TryDeleteFile(fi))
                    {
                        totalBytes -= len;
                    }
                }
            }
            catch (Exception ex)
            {
                // 정리 실패는 치명적이지 않음: 로깅 후 계속 진행
                Logger.Instance.Warning($"AppData 폴더 정리 실패: {folderPath}, 오류: {ex.Message}");
            }
        }

        private static ISet<string> GetSessionReferencedAlignedCachePaths(string sessionFilePath, string alignmentCacheFolder)
        {
            try
            {
                if (!File.Exists(sessionFilePath))
                {
                    return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }

                var json = File.ReadAllText(sessionFilePath);
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("Documents", out var documentsElement) ||
                    documentsElement.ValueKind != JsonValueKind.Array)
                {
                    return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }

                var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var d in documentsElement.EnumerateArray())
                {
                    if (!d.TryGetProperty("AlignmentInfo", out var alignmentInfo) ||
                        alignmentInfo.ValueKind == JsonValueKind.Null ||
                        alignmentInfo.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    if (!alignmentInfo.TryGetProperty("AlignedImagePath", out var pathProp) ||
                        pathProp.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    var p = pathProp.GetString();
                    if (string.IsNullOrWhiteSpace(p))
                    {
                        continue;
                    }

                    // aligned_omr 폴더 내부 파일만 보호
                    var full = Path.GetFullPath(p);
                    if (full.StartsWith(alignmentCacheFolder, StringComparison.OrdinalIgnoreCase))
                    {
                        set.Add(full);
                    }
                }

                return set;
            }
            catch (Exception ex)
            {
                Logger.Instance.Warning($"세션 기반 캐시 보호 목록 생성 실패: {ex.Message}");
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static bool TryDeleteFile(FileInfo fi)
        {
            try
            {
                if (fi.Exists)
                {
                    fi.Delete();
                }
                return true;
            }
            catch
            {
                // 잠김/권한/경합 등은 무시
                return false;
            }
        }

        public static string DefaultInputFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                         "OverlayEditorInput");
    }

    public sealed class RoundChangedEventArgs : EventArgs
    {
        public RoundChangedEventArgs(string? previousRound, string? currentRound)
        {
            PreviousRound = previousRound;
            CurrentRound = currentRound;
        }

        public string? PreviousRound { get; }
        public string? CurrentRound { get; }
    }
}

