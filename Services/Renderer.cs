using System;
using System.IO;
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
                    RenderDocument(doc);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"렌더링 실패: {ex.Message}", ex);
            }
        }

        private void RenderDocument(ImageDocument doc)
        {
            if (!File.Exists(doc.SourcePath))
            {
                System.Diagnostics.Debug.WriteLine($"원본 이미지 파일을 찾을 수 없습니다: {doc.SourcePath}");
                return;
            }

            try
            {
                // 원본 이미지 로드
                var originalImage = new BitmapImage();
                originalImage.BeginInit();
                originalImage.CacheOption = BitmapCacheOption.OnLoad;
                originalImage.UriSource = new Uri(doc.SourcePath, UriKind.Absolute);
                originalImage.EndInit();
                originalImage.Freeze();

                // DrawingVisual로 렌더링
                var drawingVisual = new DrawingVisual();
                using (var drawingContext = drawingVisual.RenderOpen())
                {
                    // 원본 이미지 그리기
                    drawingContext.DrawImage(originalImage, new Rect(0, 0, doc.ImageWidth, doc.ImageHeight));

                    // 오버레이 그리기
                    var pen = new Pen(Brushes.Red, 2.0);
                    foreach (var overlay in doc.Overlays)
                    {
                        var rect = new Rect(overlay.X, overlay.Y, overlay.Width, overlay.Height);
                        drawingContext.DrawRectangle(null, pen, rect);
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

