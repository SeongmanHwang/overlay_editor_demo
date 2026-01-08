using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using SimpleOverlayEditor.Models;

namespace SimpleOverlayEditor.Services
{
    public class ImageLoader
    {
        private static readonly string[] SupportedExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff" };

        public List<ImageDocument> LoadImagesFromFolder(string folderPath)
        {
            Logger.Instance.Info($"LoadImagesFromFolder 시작: {folderPath}");
            
            if (!Directory.Exists(folderPath))
            {
                Logger.Instance.Error($"폴더를 찾을 수 없음: {folderPath}");
                throw new DirectoryNotFoundException($"폴더를 찾을 수 없습니다: {folderPath}");
            }

            var documents = new List<ImageDocument>();

            try
            {
                Logger.Instance.Debug("폴더에서 이미지 파일 검색 중");
                var imageFiles = Directory.GetFiles(folderPath)
                    .Where(file => SupportedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()))
                    .ToList();
                
                Logger.Instance.Info($"발견된 이미지 파일 수: {imageFiles.Count}");

                int successCount = 0;
                int failCount = 0;
                
                foreach (var filePath in imageFiles)
                {
                    try
                    {
                        Logger.Instance.Debug($"이미지 로드 시도: {filePath}");
                        var document = LoadImageDocument(filePath);
                        if (document != null)
                        {
                            documents.Add(document);
                            successCount++;
                            Logger.Instance.Debug($"이미지 로드 성공: {filePath} ({document.ImageWidth}x{document.ImageHeight})");
                        }
                    }
                    catch (Exception ex)
                    {
                        failCount++;
                        Logger.Instance.Warning($"이미지 로드 실패: {filePath}, 오류: {ex.Message}");
                    }
                }
                
                Logger.Instance.Info($"이미지 로드 완료. 성공: {successCount}, 실패: {failCount}, 총: {documents.Count}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Logger.Instance.Error($"폴더 접근 권한 없음: {folderPath}", ex);
                throw new UnauthorizedAccessException($"폴더 접근 권한이 없습니다: {folderPath}");
            }

            return documents;
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
                Logger.Instance.Debug($"BitmapImage 생성 시작: {filePath}");
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();

                var document = new ImageDocument
                {
                    SourcePath = filePath,
                    ImageWidth = bitmap.PixelWidth,
                    ImageHeight = bitmap.PixelHeight
                };

                Logger.Instance.Debug($"ImageDocument 생성 완료: {filePath} ({document.ImageWidth}x{document.ImageHeight})");
                return document;
            }
            catch (Exception ex)
            {
                Logger.Instance.Error($"이미지 파일 로드 실패: {filePath}", ex);
                throw new InvalidOperationException($"이미지 파일을 로드할 수 없습니다: {filePath}", ex);
            }
        }
    }
}

