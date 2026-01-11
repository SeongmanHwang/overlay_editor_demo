using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SimpleOverlayEditor.Models;

namespace SimpleOverlayEditor.Services
{
    public class Renderer
    {
        public void RenderAll(Session session, Workspace workspace)
        {
            try
            {
                PathService.EnsureDirectories();

                // output 폴더 삭제 후 재생성
                var outputFolder = PathService.OutputFolder;
                if (Directory.Exists(outputFolder))
                {
                    Directory.Delete(outputFolder, true);
                }
                Directory.CreateDirectory(outputFolder);

                foreach (var doc in session.Documents)
                {
                    RenderDocument(doc, session, workspace);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"렌더링 실패: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 단일 문서의 오버레이 이미지를 저장합니다.
        /// </summary>
        public void RenderSingleDocument(ImageDocument doc, Session session, Workspace workspace)
        {
            PathService.EnsureDirectories();
            RenderDocument(doc, session, workspace);
        }

        private void RenderDocument(ImageDocument doc, Session session, Workspace workspace)
        {
            if (!File.Exists(doc.SourcePath))
            {
                System.Diagnostics.Debug.WriteLine($"원본 이미지 파일을 찾을 수 없습니다: {doc.SourcePath}");
                return;
            }

            try
            {
                // 정렬된 이미지 경로 사용 (정렬 실패 시 원본 사용)
                var imagePath = doc.GetImagePathForUse();
                
                // 이미지 로드
                var originalImage = new BitmapImage();
                originalImage.BeginInit();
                originalImage.CacheOption = BitmapCacheOption.OnLoad;
                originalImage.UriSource = new Uri(imagePath, UriKind.Absolute);
                originalImage.EndInit();
                originalImage.Freeze();

                var template = workspace.Template;
                
                // DrawingVisual로 렌더링
                var drawingVisual = new DrawingVisual();
                using (var drawingContext = drawingVisual.RenderOpen())
                {
                    // 원본 이미지 그리기
                    drawingContext.DrawImage(originalImage, new Rect(0, 0, doc.ImageWidth, doc.ImageHeight));

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
                        
                        if (session.MarkingResults != null && 
                            session.MarkingResults.TryGetValue(doc.ImageId, out var results) &&
                            i < results.Count)
                        {
                            var result = results[i];
                            if (result.IsMarked)
                            {
                                // 마킹 리딩: 파란색 반투명 채우기 + 파란색 테두리
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
                        
                        if (session.BarcodeResults != null && 
                            session.BarcodeResults.TryGetValue(doc.ImageId, out var barcodeResults) &&
                            i < barcodeResults.Count)
                        {
                            var result = barcodeResults[i];
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
                        if (session.BarcodeResults != null && 
                            session.BarcodeResults.TryGetValue(doc.ImageId, out var barcodeResults2) &&
                            i < barcodeResults2.Count)
                        {
                            var result = barcodeResults2[i];
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
                var rtb = new RenderTargetBitmap(doc.ImageWidth, doc.ImageHeight, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(drawingVisual);

                // PNG로 저장
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));

                var fileName = Path.GetFileNameWithoutExtension(doc.SourcePath);
                var outputPath = Path.Combine(PathService.OutputFolder, $"{fileName}_overlay.png");

                using (var stream = File.Create(outputPath))
                {
                    encoder.Save(stream);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"문서 렌더링 실패: {doc.SourcePath}, 오류: {ex.Message}");
            }
        }
    }
}

