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
    /// <summary>
    /// 이미지의 ScoringArea ROI에서 마킹을 리딩하는 서비스입니다.
    /// </summary>
    public class MarkingDetector
    {
        /// <summary>
        /// 기본 마킹 리딩 임계값 (0-255, 이 값보다 어두우면 마킹으로 판단)
        /// </summary>
        public double DefaultThreshold { get; set; } = 220.0;

        /// <summary>
        /// 단일 이미지 문서의 모든 ScoringArea에서 마킹을 리딩합니다.
        /// </summary>
        /// <param name="document">이미지 문서</param>
        /// <param name="scoringAreas">채점 영역 목록</param>
        /// <param name="threshold">마킹 리딩 임계값 (기본값 사용 시 null)</param>
        /// <returns>각 ScoringArea에 대한 마킹 리딩 결과</returns>
        public List<MarkingResult> DetectMarkings(
            ImageDocument document, 
            IEnumerable<RectangleOverlay> scoringAreas, 
            double? threshold = null)
        {
            Logger.Instance.Info($"마킹 리딩 시작: {document.SourcePath}");

            if (!File.Exists(document.SourcePath))
            {
                Logger.Instance.Error($"이미지 파일을 찾을 수 없음: {document.SourcePath}");
                throw new FileNotFoundException($"이미지 파일을 찾을 수 없습니다: {document.SourcePath}");
            }

            var results = new List<MarkingResult>();
            var actualThreshold = threshold ?? DefaultThreshold;

            BitmapImage? bitmap = null;
            FormatConvertedBitmap? grayBitmap = null;
            
            try
            {
                // 정렬된 이미지 경로 사용 (정렬 실패 시 원본 사용)
                var imagePath = document.GetImagePathForUse();
                
                // 이미지 로드
                bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();

                // FormatConvertedBitmap으로 그레이스케일 변환
                grayBitmap = new FormatConvertedBitmap(bitmap, PixelFormats.Gray8, null, 0);
                grayBitmap.Freeze();

                // 슬롯 구조: OptionNumber와 QuestionNumber를 직접 사용
                foreach (var area in scoringAreas)
                {
                    // OptionNumber와 QuestionNumber가 설정되지 않은 경우 건너뛰기
                    if (!area.OptionNumber.HasValue || !area.QuestionNumber.HasValue)
                    {
                        Logger.Instance.Warning($"ScoringArea에 번호 정보가 없습니다. 건너뜁니다.");
                        continue;
                    }

                    try
                    {
                        var result = DetectMarkingInArea(grayBitmap, area, actualThreshold, area.OptionNumber.Value);
                        // 슬롯 구조: OptionNumber와 QuestionNumber 직접 사용
                        result.QuestionNumber = area.QuestionNumber.Value;
                        result.OptionNumber = area.OptionNumber.Value;
                        results.Add(result);
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Warning($"ScoringArea {area.QuestionNumber}-{area.OptionNumber} 마킹 리딩 실패: {ex.Message}");
                        // 실패한 영역도 결과에 추가 (IsMarked = false)
                        results.Add(new MarkingResult
                        {
                            ScoringAreaId = $"{area.QuestionNumber}-{area.OptionNumber}",
                            QuestionNumber = area.QuestionNumber.Value,
                            OptionNumber = area.OptionNumber.Value,
                            IsMarked = false,
                            AverageBrightness = 0,
                            Threshold = actualThreshold
                        });
                    }
                }

                var markedCount = results.Count(r => r.IsMarked);
                Logger.Instance.Info($"마킹 리딩 완료: 총 {results.Count}개 영역 중 {markedCount}개 마킹 리딩");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error($"마킹 리딩 중 오류 발생: {document.SourcePath}", ex);
                throw;
            }
            finally
            {
                // 메모리 최적화: 이미지 처리 후 즉시 참조 해제 (GC가 회수할 수 있도록)
                grayBitmap = null;
                bitmap = null;
            }

            return results;
        }

        /// <summary>
        /// 단일 ROI 영역에서 마킹을 리딩합니다.
        /// </summary>
        private MarkingResult DetectMarkingInArea(
            FormatConvertedBitmap grayBitmap, 
            RectangleOverlay area, 
            double threshold,
            int optionNumber)
        {
            // ROI 좌표를 정수로 변환 (픽셀 단위)
            int x = (int)Math.Round(area.X);
            int y = (int)Math.Round(area.Y);
            int width = (int)Math.Round(area.Width);
            int height = (int)Math.Round(area.Height);

            // 경계 체크
            if (x < 0) x = 0;
            if (y < 0) y = 0;
            if (x + width > grayBitmap.PixelWidth) width = grayBitmap.PixelWidth - x;
            if (y + height > grayBitmap.PixelHeight) height = grayBitmap.PixelHeight - y;

            if (width <= 0 || height <= 0)
            {
                Logger.Instance.Warning($"ScoringArea {area.QuestionNumber}-{area.OptionNumber}: 유효하지 않은 크기 ({width}x{height})");
                return new MarkingResult
                {
                    ScoringAreaId = $"{area.QuestionNumber}-{area.OptionNumber}",
                    QuestionNumber = area.QuestionNumber ?? 0,
                    OptionNumber = area.OptionNumber ?? 0,
                    IsMarked = false,
                    AverageBrightness = 255,
                    Threshold = threshold
                };
            }

            // ROI 영역의 픽셀 데이터 추출
            int stride = (width * grayBitmap.Format.BitsPerPixel + 7) / 8;
            byte[] pixels = new byte[stride * height];
            grayBitmap.CopyPixels(new System.Windows.Int32Rect(x, y, width, height), pixels, stride, 0);

            // 평균 밝기 계산
            long sum = 0;
            int pixelCount = width * height;

            for (int i = 0; i < pixels.Length; i++)
            {
                sum += pixels[i];
            }

            double averageBrightness = pixelCount > 0 ? (double)sum / pixelCount : 255.0;

            // 임계값보다 어두우면 마킹으로 판단
            bool isMarked = averageBrightness < threshold;

            Logger.Instance.Debug($"ScoringArea {area.QuestionNumber}-{area.OptionNumber}: 평균 밝기={averageBrightness:F2}, 임계값={threshold}, 마킹={isMarked}");

            // 슬롯 구조: OptionNumber와 QuestionNumber 직접 사용
            return new MarkingResult
            {
                ScoringAreaId = $"{area.QuestionNumber}-{area.OptionNumber}",
                QuestionNumber = area.QuestionNumber ?? 0,
                OptionNumber = area.OptionNumber ?? 0,
                IsMarked = isMarked,
                AverageBrightness = averageBrightness,
                Threshold = threshold
            };
        }

        /// <summary>
        /// 모든 문서에 대해 마킹을 리딩합니다 (병렬 처리).
        /// </summary>
        public Dictionary<string, List<MarkingResult>> DetectAllMarkings(
            IEnumerable<ImageDocument> documents,
            OmrTemplate template,
            double? threshold = null,
            Action<int, int, string>? progressCallback = null,
            CancellationToken cancellationToken = default)
        {
            var documentsList = documents.ToList();
            Logger.Instance.Info($"전체 문서 마킹 리딩 시작: {documentsList.Count}개 문서 (병렬 처리)");

            var allResults = new ConcurrentDictionary<string, List<MarkingResult>>();
            int completedCount = 0;
            var lockObject = new object();

            progressCallback?.Invoke(0, documentsList.Count, "마킹 리딩 시작");

            try
            {
                Parallel.ForEach(documentsList, new ParallelOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount,
                    CancellationToken = cancellationToken
                }, document =>
                {
                    if (cancellationToken.IsCancellationRequested) return;

                    try
                    {
                        var results = DetectMarkings(document, template.ScoringAreas, threshold);
                        allResults[document.ImageId] = results;

                        int current;
                        lock (lockObject)
                        {
                            completedCount++;
                            current = completedCount;
                        }

                        var fileName = Path.GetFileName(document.SourcePath);
                        progressCallback?.Invoke(current, documentsList.Count, $"마킹 리딩 중: {fileName}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Error($"문서 마킹 리딩 실패: {document.SourcePath}", ex);
                        // 실패한 문서는 빈 결과로 추가
                        allResults[document.ImageId] = new List<MarkingResult>();
                        
                        int current;
                        lock (lockObject)
                        {
                            completedCount++;
                            current = completedCount;
                        }
                        progressCallback?.Invoke(current, documentsList.Count, $"마킹 리딩 실패: {Path.GetFileName(document.SourcePath)}");
                    }
                });
            }
            catch (OperationCanceledException)
            {
                Logger.Instance.Info("마킹 리딩 작업이 취소되었습니다.");
                throw;
            }

            progressCallback?.Invoke(documentsList.Count, documentsList.Count, "마킹 리딩 완료");
            Logger.Instance.Info($"전체 문서 마킹 리딩 완료: {allResults.Count}개 문서 처리");
            
            return allResults.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
    }
}

