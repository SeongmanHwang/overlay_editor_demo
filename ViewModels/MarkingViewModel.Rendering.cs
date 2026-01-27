using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SimpleOverlayEditor.Models;
using SimpleOverlayEditor.Services;
using SimpleOverlayEditor.Utils;

namespace SimpleOverlayEditor.ViewModels
{
    public partial class MarkingViewModel
    {
        /// <summary>
        /// 이미지에 정렬을 적용하고 캐시에 저장합니다.
        /// </summary>
        private void ApplyAlignmentToDocument(ImageDocument document)
        {
            BitmapImage? bitmap = null;
            AlignmentResult? result = null;
            
            try
            {
                // 타이밍 마크가 없으면 정렬 생략
                if (_workspace.Template.TimingMarks.Count == 0)
                {
                    Logger.Instance.Debug($"타이밍 마크가 없어 정렬 생략: {document.SourcePath}");

                    document.AlignmentInfo = new AlignmentInfo { Success = false, Confidence = 0.0 };
                    _session.AlignmentFailedImageIds.Add(document.ImageId);

                    return;
                }

                Logger.Instance.Debug($"이미지 정렬 적용 시작: {document.SourcePath}");

                // 원본 이미지 로드
                bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(document.SourcePath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();

                // 정렬 적용
                result = _alignmentService.AlignImage(bitmap, _workspace.Template);

                // 정렬 정보 저장
                document.AlignmentInfo = new AlignmentInfo
                {
                    Success = result.Success,
                    Confidence = result.Confidence
                };

                if (result.Success && result.Transform != null)
                {
                    document.AlignmentInfo.Rotation = result.Transform.Rotation;
                    document.AlignmentInfo.ScaleX = result.Transform.ScaleX;
                    document.AlignmentInfo.ScaleY = result.Transform.ScaleY;
                    document.AlignmentInfo.TranslationX = result.Transform.TranslationX;
                    document.AlignmentInfo.TranslationY = result.Transform.TranslationY;

                    // 정렬된 이미지 크기 저장 (저장 전에)
                    var alignedImageWidth = result.AlignedImage.PixelWidth;
                    var alignedImageHeight = result.AlignedImage.PixelHeight;

                    // 정렬된 이미지를 캐시에 저장
                    var alignedImagePath = SaveAlignedImageToCache(document, result.AlignedImage);
                    document.AlignmentInfo.AlignedImagePath = alignedImagePath;

                    // 정렬된 이미지 크기로 ImageWidth/Height 업데이트
                    document.ImageWidth = alignedImageWidth;
                    document.ImageHeight = alignedImageHeight;

                    Logger.Instance.Info(
                        $"이미지 정렬 성공: {document.SourcePath}, " +
                        $"신뢰도={result.Confidence:F2}, " +
                        $"정렬된 이미지={alignedImagePath}");
                        _session.AlignmentFailedImageIds.Remove(document.ImageId);
                }
                else
                {
                    Logger.Instance.Info(
                        $"이미지 정렬 실패 또는 생략: {document.SourcePath}, " +
                        $"신뢰도={result.Confidence:F2}");
                    _session.AlignmentFailedImageIds.Add(document.ImageId);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error($"이미지 정렬 중 오류: {document.SourcePath}", ex);
                // 오류 발생 시 정렬 정보를 실패로 설정
                document.AlignmentInfo = new AlignmentInfo { Success = false, Confidence = 0.0 };
                _session.AlignmentFailedImageIds.Add(document.ImageId);
            }
            finally
            {
                // 메모리 최적화: 이미지 처리 후 즉시 참조 해제 (GC가 회수할 수 있도록)
                if (result != null && result.AlignedImage != null)
                {
                    // 정렬된 이미지는 디스크에 저장되었으므로 메모리에서 해제 가능
                    result = null;
                }
                bitmap = null;
            }
        }

        /// <summary>
        /// 정렬된 이미지를 캐시 폴더에 저장합니다.
        /// </summary>
        private string SaveAlignedImageToCache(ImageDocument document, BitmapSource alignedImage)
        {
            try
            {
                PathService.EnsureDirectories();

                // 캐시 폴더가 없으면 생성 (회차 생성/선택 타이밍과 무관하게 저장이 항상 가능해야 함)
                Directory.CreateDirectory(PathService.AlignmentCacheFolder);

                // 캐시 파일명 생성 (원본 파일명 + ImageId 해시)
                var originalFileName = Path.GetFileNameWithoutExtension(document.SourcePath);
                var cacheFileName = $"{originalFileName}_{document.ImageId.Substring(0, 8)}_aligned.png";
                var cachePath = Path.Combine(PathService.AlignmentCacheFolder, cacheFileName);

                // PNG 인코더로 저장
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(alignedImage));

                using (var stream = File.Create(cachePath))
                {
                    encoder.Save(stream);
                }

                Logger.Instance.Debug($"정렬된 이미지 캐시 저장: {cachePath}");
                return cachePath;
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("정렬된 이미지 캐시 저장 실패", ex);
                throw;
            }
        }

        /// <summary>
        /// 이미지 표시 영역을 업데이트합니다 (줌 레벨 적용).
        /// </summary>
        public void UpdateImageDisplayRect(Size availableSize)
        {
            if (SelectedDocument != null)
            {
                // 기본 표시 크기 계산 (줌 없이)
                var baseRect = ZoomHelper.CalculateImageDisplayRect(
                    SelectedDocument.ImageWidth,
                    SelectedDocument.ImageHeight,
                    availableSize,
                    ZoomHelper.ImageAlignment.TopLeft);

                // 줌 레벨을 적용하여 실제 표시 크기 계산
                var newRect = new Rect(
                    baseRect.X * ZoomLevel,
                    baseRect.Y * ZoomLevel,
                    baseRect.Width * ZoomLevel,
                    baseRect.Height * ZoomLevel);

                const double epsilon = 0.001;
                if (Math.Abs(CurrentImageDisplayRect.X - newRect.X) > epsilon ||
                    Math.Abs(CurrentImageDisplayRect.Y - newRect.Y) > epsilon ||
                    Math.Abs(CurrentImageDisplayRect.Width - newRect.Width) > epsilon ||
                    Math.Abs(CurrentImageDisplayRect.Height - newRect.Height) > epsilon)
                {
                    CurrentImageDisplayRect = newRect;
                }
            }
        }

        /// <summary>
        /// 선택된 문서의 오버레이 이미지를 생성하여 DisplayImage를 업데이트합니다.
        /// </summary>
        private void UpdateDisplayImage()
        {
            if (SelectedDocument == null)
            {
                DisplayImage = null;
                return;
            }

            try
            {
                Logger.Instance.Debug($"오버레이 이미지 생성 시작: {SelectedDocument.SourcePath}");

                // 정렬된 이미지 경로 사용 (정렬 실패 시 처리 중단)
                var imagePath = SelectedDocument.GetImagePathForUse();
                
                if (string.IsNullOrWhiteSpace(imagePath))
                {
                    Logger.Instance.Warning($"정렬된 이미지 경로가 없어 오버레이 이미지를 표시할 수 없습니다: {SelectedDocument.SourcePath}");
                    DisplayImage = null;
                    return;
                }

                if (!File.Exists(imagePath))
                {
                    Logger.Instance.Warning($"정렬된 이미지 파일을 찾을 수 없음: {imagePath}");
                    DisplayImage = null;
                    return;
                }

                // 이미지 로드
                var originalImage = new BitmapImage();
                originalImage.BeginInit();
                originalImage.CacheOption = BitmapCacheOption.OnLoad;
                originalImage.UriSource = new Uri(imagePath, UriKind.Absolute);
                originalImage.EndInit();
                originalImage.Freeze();

                var template = _workspace.Template;
                
                // DrawingVisual로 렌더링
                var drawingVisual = new DrawingVisual();
                using (var drawingContext = drawingVisual.RenderOpen())
                {
                    // 원본 이미지 그리기
                    drawingContext.DrawImage(originalImage, new Rect(0, 0, SelectedDocument.ImageWidth, SelectedDocument.ImageHeight));

                    // 템플릿의 타이밍 마크 그리기 (녹색)
                    var timingMarkPen = new Pen(Brushes.Green, 2.0);
                    foreach (var overlay in template.TimingMarks)
                    {
                        var rect = new Rect(overlay.X, overlay.Y, overlay.Width, overlay.Height);
                        drawingContext.DrawRectangle(null, timingMarkPen, rect);
                    }
                    
                    // 템플릿의 채점 영역 그리기
                    // 마킹 리딩 결과가 있으면 결과에 따라 색상 변경
                    var scoringAreas = template.ScoringAreas.ToList();
                    for (int i = 0; i < scoringAreas.Count; i++)
                    {
                        var overlay = scoringAreas[i];
                        var rect = new Rect(overlay.X, overlay.Y, overlay.Width, overlay.Height);
                        
                        // 마킹 리딩 결과 확인
                        Brush? fillBrush = null;
                        Pen? pen = null;
                        
                        if (CurrentMarkingResults != null && i < CurrentMarkingResults.Count)
                        {
                            var result = CurrentMarkingResults[i];
                            if (result.IsMarked)
                            {
                                // 마킹 리딩: 파란색 반투명 채우기 + 빨간색 테두리
                                fillBrush = new SolidColorBrush(Color.FromArgb(128, 0, 0, 255));
                                pen = new Pen(Brushes.Blue, 2.0);
                            }
                            else
                            {
                                // 미마킹: 빨간색 반투명 채우기 + 빨간색 테두리
                                fillBrush = new SolidColorBrush(Color.FromArgb(128, 255, 0, 0));
                                pen = new Pen(Brushes.Red, 2.0);
                            }
                        }
                        else
                        {
                            // 마킹 리딩 결과 없음: 빨간색 테두리만
                            pen = new Pen(Brushes.Red, 2.0);
                        }
                        
                        drawingContext.DrawRectangle(fillBrush, pen, rect);
                    }

                    // 템플릿의 바코드 영역 그리기
                    var barcodeAreas = template.BarcodeAreas.ToList();
                    for (int i = 0; i < barcodeAreas.Count; i++)
                    {
                        var overlay = barcodeAreas[i];
                        var rect = new Rect(overlay.X, overlay.Y, overlay.Width, overlay.Height);
                        
                        // 바코드 디코딩 결과 확인
                        Brush? fillBrush = null;
                        Pen? pen = null;
                        
                        if (CurrentBarcodeResults != null && i < CurrentBarcodeResults.Count)
                        {
                            var result = CurrentBarcodeResults[i];
                            if (result.Success)
                            {
                                // 바코드 디코딩 성공: 주황색 반투명 채우기 + 주황색 테두리
                                fillBrush = new SolidColorBrush(Color.FromArgb(128, 255, 165, 0));
                                pen = new Pen(Brushes.Orange, 2.0);
                            }
                            else
                            {
                                // 바코드 디코딩 실패: 회색 반투명 채우기 + 회색 테두리
                                fillBrush = new SolidColorBrush(Color.FromArgb(128, 128, 128, 128));
                                pen = new Pen(Brushes.Gray, 2.0);
                            }
                        }
                        else
                        {
                            // 바코드 디코딩 결과 없음: 주황색 테두리만
                            pen = new Pen(Brushes.Orange, 2.0);
                        }
                        
                        drawingContext.DrawRectangle(fillBrush, pen, rect);

                        // 바코드 디코딩 성공 시 텍스트 표시
                        if (CurrentBarcodeResults != null && i < CurrentBarcodeResults.Count)
                        {
                            var result = CurrentBarcodeResults[i];
                            if (result.Success && !string.IsNullOrEmpty(result.DecodedText))
                            {
                                var text = result.DecodedText;
                                var formattedText = new FormattedText(
                                    text,
                                    System.Globalization.CultureInfo.CurrentCulture,
                                    FlowDirection.LeftToRight,
                                    new Typeface("Arial"),
                                    12,
                                    Brushes.White,
                                    96.0);

                                // 텍스트 배경 (검은색 반투명)
                                var textRect = new Rect(
                                    overlay.X,
                                    overlay.Y - formattedText.Height - 2,
                                    Math.Max(formattedText.Width + 4, overlay.Width),
                                    formattedText.Height + 2);
                                drawingContext.DrawRectangle(
                                    new SolidColorBrush(Color.FromArgb(200, 0, 0, 0)),
                                    null,
                                    textRect);

                                // 텍스트 그리기
                                drawingContext.DrawText(
                                    formattedText,
                                    new Point(overlay.X + 2, overlay.Y - formattedText.Height));
                            }
                        }
                    }
                }

                // RenderTargetBitmap으로 변환
                var rtb = new RenderTargetBitmap(SelectedDocument.ImageWidth, SelectedDocument.ImageHeight, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(drawingVisual);
                rtb.Freeze();

                DisplayImage = rtb;
                Logger.Instance.Debug($"오버레이 이미지 생성 완료: {SelectedDocument.SourcePath}");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error($"오버레이 이미지 생성 실패: {SelectedDocument.SourcePath}", ex);
                DisplayImage = null;
            }
        }
    }
}
