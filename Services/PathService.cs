using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SimpleOverlayEditor.Services
{
    public static class PathService
    {
        public static string AppDataFolder =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "SimpleOverlayEditor");

        public static string StateFilePath =>
            Path.Combine(AppDataFolder, "state.json");

        public static string SessionFilePath =>
            Path.Combine(AppDataFolder, "session.json");

        public static string TemplateFilePath =>
            Path.Combine(AppDataFolder, "template.json");

        public static string OutputFolder =>
            Path.Combine(AppDataFolder, "output");

        public static string AlignmentCacheFolder =>
            Path.Combine(AppDataFolder, "aligned_cache");

        public static string BarcodeDebugFolder =>
            Path.Combine(AppDataFolder, "barcode_debug");

        public static string LogsFolder =>
            Path.Combine(AppDataFolder, "logs");

        public static void EnsureDirectories()
        {
            Directory.CreateDirectory(AppDataFolder);
            Directory.CreateDirectory(OutputFolder);
            Directory.CreateDirectory(AlignmentCacheFolder);
            // barcode_debug는 DEBUG 빌드에서만 생성되지만, 존재해도 무방합니다.
            Directory.CreateDirectory(BarcodeDebugFolder);
        }

        /// <summary>
        /// AppData 하위 캐시/출력 폴더를 정리합니다.
        /// - 오래된 파일 삭제 (maxAgeDays)
        /// - 총 용량 상한 (maxTotalBytes) 초과 시 오래된 파일부터 삭제
        /// 주의: aligned_cache/output는 "재생성 가능한 캐시" 성격입니다.
        /// </summary>
        public static void CleanupAppData()
        {
            EnsureDirectories();

            // 현재 세션이 참조하는 aligned_cache 파일은 보존 (정리로 인해 Marking 모드에서 대량 재정렬이 발생하는 것을 방지)
            var protectedAlignedCachePaths = GetSessionReferencedAlignedCachePaths();

            // aligned_cache가 폭증하기 쉬워서 가장 엄격하게 관리합니다.
            CleanupFolder(
                folderPath: AlignmentCacheFolder,
                maxAgeDays: 14,
                maxTotalBytes: 5L * 1024 * 1024 * 1024, // 5GB
                searchPattern: "*.png",
                protectedFilePaths: protectedAlignedCachePaths);

            // output은 사용자에게 직접 노출되는 "내보내기" 용도가 아니라 내부 생성물에 가깝습니다.
            CleanupFolder(
                folderPath: OutputFolder,
                maxAgeDays: 7,
                maxTotalBytes: 1L * 1024 * 1024 * 1024, // 1GB
                searchPattern: "*.png",
                protectedFilePaths: null);

            // 디버그 스냅샷은 너무 빨리 쌓이므로 더 짧게 유지합니다.
            CleanupFolder(
                folderPath: BarcodeDebugFolder,
                maxAgeDays: 3,
                maxTotalBytes: 512L * 1024 * 1024, // 512MB
                searchPattern: "*.png",
                protectedFilePaths: null);
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

        private static ISet<string> GetSessionReferencedAlignedCachePaths()
        {
            try
            {
                if (!File.Exists(SessionFilePath))
                {
                    return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                }

                var json = File.ReadAllText(SessionFilePath);
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

                    // aligned_cache 폴더 내부 파일만 보호
                    var full = Path.GetFullPath(p);
                    if (full.StartsWith(AlignmentCacheFolder, StringComparison.OrdinalIgnoreCase))
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
}

