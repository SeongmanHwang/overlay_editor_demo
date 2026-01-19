using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SimpleOverlayEditor.Models;

namespace SimpleOverlayEditor.Services
{
    /// <summary>
    /// 회차별 데이터 사용 정보를 스캔하는 서비스입니다.
    /// </summary>
    public class DataUsageService
    {
        /// <summary>
        /// 프로그램 생애주기 순서에 따른 항목 정의
        /// </summary>
        private static readonly List<(string Name, string PathKey, DataUsageItem.ItemType Type)> ItemDefinitions = new()
        {
            ("템플릿", "template.json", DataUsageItem.ItemType.JsonFile),
            ("문항 배점", "scoring_rule.json", DataUsageItem.ItemType.JsonFile),
            ("면접위원 명렬", "interviewer_registry.json", DataUsageItem.ItemType.JsonFile),
            ("수험생 명렬", "student_registry.json", DataUsageItem.ItemType.JsonFile),
            ("스캔 이미지", "aligned_omr", DataUsageItem.ItemType.Folder),
            ("바코드 디버그", "barcode_debug", DataUsageItem.ItemType.Folder),
            ("오버레이 캐시", "overlay_cache", DataUsageItem.ItemType.Folder),
            ("로그", "logs", DataUsageItem.ItemType.Folder) // 전역
        };

        /// <summary>
        /// 회차별 데이터 사용 정보를 스캔합니다.
        /// </summary>
        public async Task<List<DataUsageItem>> ScanRoundDataUsageAsync(
            string roundName,
            CancellationToken cancellationToken,
            Action<int, int, string?>? progressCallback = null)
        {
            return await Task.Run(() =>
            {
                var items = new List<DataUsageItem>();
                var roundRoot = PathService.GetRoundRoot(roundName);
                var totalItems = ItemDefinitions.Count;
                var currentItem = 0;

                foreach (var (name, pathKey, type) in ItemDefinitions)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    progressCallback?.Invoke(currentItem, totalItems, $"스캔 중: {name}");

                    var item = new DataUsageItem
                    {
                        Name = name,
                        Type = type
                    };

                    if (type == DataUsageItem.ItemType.JsonFile)
                    {
                        // JSON 파일
                        var filePath = Path.Combine(roundRoot, pathKey);
                        item.Path = filePath;

                        if (File.Exists(filePath))
                        {
                            try
                            {
                                var fileInfo = new FileInfo(filePath);
                                item.LastModified = fileInfo.LastWriteTime;
                            }
                            catch
                            {
                                // 파일 접근 실패 시 무시
                            }
                        }
                    }
                    else
                    {
                        // 폴더
                        string folderPath;
                        if (pathKey == "logs")
                        {
                            // 로그는 전역
                            folderPath = PathService.LogsFolder;
                        }
                        else
                        {
                            folderPath = Path.Combine(roundRoot, pathKey);
                        }

                        item.Path = folderPath;
                        item.DisplayPath = folderPath;

                        if (Directory.Exists(folderPath))
                        {
                            try
                            {
                                // 폴더의 마지막 변경 시간은 폴더 내 파일 중 가장 최근 LastWriteTime 사용
                                var files = Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories);
                                var lastModified = files
                                    .Select(f =>
                                    {
                                        try
                                        {
                                            return new FileInfo(f).LastWriteTime;
                                        }
                                        catch
                                        {
                                            return DateTime.MinValue;
                                        }
                                    })
                                    .DefaultIfEmpty(DateTime.MinValue)
                                    .Max();

                                if (lastModified != DateTime.MinValue)
                                {
                                    item.LastModified = lastModified;
                                }
                                else
                                {
                                    // 파일이 없으면 폴더 자체의 LastWriteTime 사용
                                    var dirInfo = new DirectoryInfo(folderPath);
                                    item.LastModified = dirInfo.LastWriteTime;
                                }

                                // 폴더 크기 계산
                                try
                                {
                                    long totalSize = 0;
                                    foreach (var file in files)
                                    {
                                        cancellationToken.ThrowIfCancellationRequested();
                                        try
                                        {
                                            var fileInfo = new FileInfo(file);
                                            totalSize += fileInfo.Length;
                                        }
                                        catch
                                        {
                                            // 파일 접근 실패 시 무시
                                        }
                                    }
                                    item.Size = totalSize;
                                }
                                catch
                                {
                                    // 크기 계산 실패 시 무시
                                }
                            }
                            catch
                            {
                                // 폴더 접근 실패 시 무시
                            }
                        }
                    }

                    items.Add(item);
                    currentItem++;
                }

                progressCallback?.Invoke(totalItems, totalItems, "완료");

                return items;
            }, cancellationToken);
        }
    }
}
