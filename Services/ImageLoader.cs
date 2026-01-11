using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SimpleOverlayEditor.Models;

namespace SimpleOverlayEditor.Services
{
    public class ImageLoader
    {
        private static readonly string[] SupportedExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff" };

        public List<ImageDocument> LoadImagesFromFolder(string folderPath, Action<int, int, string>? progressCallback = null, CancellationToken cancellationToken = default)
        {
            Logger.Instance.Info($"LoadImagesFromFolder 시작: {folderPath} (병렬 처리)");
            
            if (!Directory.Exists(folderPath))
            {
                Logger.Instance.Error($"폴더를 찾을 수 없음: {folderPath}");
                throw new DirectoryNotFoundException($"폴더를 찾을 수 없습니다: {folderPath}");
            }

            var documents = new ConcurrentBag<ImageDocument>();

            try
            {
                Logger.Instance.Debug("폴더에서 이미지 파일 검색 중");
                var imageFiles = Directory.GetFiles(folderPath)
                    .Where(file => SupportedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                    .ToList();
                
                Logger.Instance.Info($"발견된 이미지 파일 수: {imageFiles.Count}");

                int successCount = 0;
                int failCount = 0;
                int completedCount = 0;
                var lockObject = new object();
                
                progressCallback?.Invoke(0, imageFiles.Count, "이미지 파일 검색 완료");
                
                try
                {
                    Parallel.ForEach(imageFiles, new ParallelOptions
                    {
                        MaxDegreeOfParallelism = Environment.ProcessorCount,
                        CancellationToken = cancellationToken
                    }, filePath =>
                    {
                        if (cancellationToken.IsCancellationRequested) return;

                        try
                        {
                            var document = LoadImageDocument(filePath);
                            if (document != null)
                            {
                                documents.Add(document);
                                int current;
                                lock (lockObject)
                                {
                                    successCount++;
                                    completedCount++;
                                    current = completedCount;
                                }
                                
                                var fileName = Path.GetFileName(filePath);
                                progressCallback?.Invoke(current, imageFiles.Count, $"이미지 로드 중: {fileName}");
                            }
                        }
                        catch (Exception ex)
                        {
                            lock (lockObject)
                            {
                                failCount++;
                                completedCount++;
                            }
                            Logger.Instance.Warning($"이미지 로드 실패: {filePath}, 오류: {ex.Message}");
                            
                            int current;
                            lock (lockObject)
                            {
                                current = completedCount;
                            }
                            progressCallback?.Invoke(current, imageFiles.Count, $"이미지 로드 실패: {Path.GetFileName(filePath)}");
                        }
                    });
                }
                catch (OperationCanceledException)
                {
                    Logger.Instance.Info("이미지 로드 작업이 취소되었습니다.");
                    throw;
                }
                
                progressCallback?.Invoke(imageFiles.Count, imageFiles.Count, "이미지 로드 완료");
                Logger.Instance.Info($"이미지 로드 완료. 성공: {successCount}, 실패: {failCount}, 총: {documents.Count}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.Instance.Error($"폴더 접근 권한 없음: {folderPath}", ex);
                throw new UnauthorizedAccessException($"폴더 접근 권한이 없습니다: {folderPath}");
            }

            return documents.ToList();
        }

        public ImageDocument? LoadImageDocument(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Logger.Instance.Warning($"파일이 존재하지 않음: {filePath}");
                return null;
            }

            try
            {
                // 메모리 최적화: BitmapDecoder를 사용하여 헤더만 읽기 (전체 이미지 로드하지 않음)
                using (var stream = File.OpenRead(filePath))
                {
                    var decoder = BitmapDecoder.Create(
                        stream,
                        BitmapCreateOptions.DelayCreation,  // 지연 생성 (헤더만 읽음)
                        BitmapCacheOption.None);            // 캐시하지 않음
                    
                    var frame = decoder.Frames[0];
                    var document = new ImageDocument
                    {
                        SourcePath = filePath,
                        ImageWidth = frame.PixelWidth,   // 헤더에서 크기만 읽음
                        ImageHeight = frame.PixelHeight  // 픽셀 데이터는 로드하지 않음
                    };

                    // stream이 닫히면 decoder도 자동으로 해제됨
                    Logger.Instance.Debug($"ImageDocument 생성 완료: {filePath} ({document.ImageWidth}x{document.ImageHeight})");
                    return document;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error($"이미지 파일 로드 실패: {filePath}", ex);
                throw new InvalidOperationException($"이미지 파일을 로드할 수 없습니다: {filePath}", ex);
            }
        }
    }
}

