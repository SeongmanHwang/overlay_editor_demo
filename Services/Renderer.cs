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
        public void RenderAll(Workspace workspace)
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

                foreach (var doc in workspace.Documents)
                {
                    RenderDocument(doc, workspace);
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
        public void RenderSingleDocument(ImageDocument doc, Workspace workspace)
        {
            PathService.EnsureDirectories();
            RenderDocument(doc, workspace);
        }

        private void RenderDocument(ImageDocument doc, Workspace workspace)
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
                    // 마킹 감지 결과가 있으면 결과에 따라 색상 변경
                    var scoringAreas = template.ScoringAreas.ToList();
                    for (int i = 0; i < scoringAreas.Count; i++)
                    {
                        var overlay = scoringAreas[i];
                        var rect = new Rect(overlay.X, overlay.Y, overlay.Width, overlay.Height);
                        
                        // 마킹 감지 결과 확인
                        Brush? fillBrush = null;
                        Pen? pen = null;
                        
                        if (workspace.MarkingResults != null && 
                            workspace.MarkingResults.TryGetValue(doc.ImageId, out var results) &&
                            i < results.Count)
                        {
                            var result = results[i];
                            if (result.IsMarked)
                            {
                                // 마킹 감지: 파란색 반투명 채우기 + 파란색 테두리
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
                            // 마킹 감지 결과 없음: 빨간색 테두리만
                            pen = new Pen(Brushes.Red, 2.0);
                        }
                        
                        drawingContext.DrawRectangle(fillBrush, pen, rect);
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

