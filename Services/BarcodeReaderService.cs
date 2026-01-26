using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SimpleOverlayEditor.Models;
using ZXing;
using ZXing.Common;
using System.Drawing;
using System.Drawing.Imaging;
using SimpleOverlayEditor.Services;

namespace SimpleOverlayEditor.Services
{
    /// <summary>
    /// 이미지의 BarcodeArea ROI에서 바코드를 디코딩하는 서비스입니다.
    /// </summary>
    public class BarcodeReaderService
    {
        private readonly List<BarcodeFormat> _possibleFormats;
        private readonly bool _tryHarder;

        public BarcodeReaderService()
        {
            _possibleFormats = new List<BarcodeFormat>
            {
                BarcodeFormat.CODE_128,
                BarcodeFormat.CODE_39,
                BarcodeFormat.EAN_13,
                BarcodeFormat.EAN_8,
                BarcodeFormat.CODABAR,
                BarcodeFormat.ITF
            };
            _tryHarder = true;
        }

        /// <summary>
        /// 단일 이미지 문서의 모든 BarcodeArea에서 바코드를 디코딩합니다.
        /// </summary>
        /// <param name="document">이미지 문서</param>
        /// <param name="barcodeAreas">바코드 영역 목록</param>
        /// <returns>각 BarcodeArea에 대한 바코드 디코딩 결과</returns>
        public List<BarcodeResult> DecodeBarcodes(
            ImageDocument document,
            IEnumerable<RectangleOverlay> barcodeAreas)
        {
            Logger.Instance.Info($"바코드 디코딩 시작: {document.SourcePath}");


            var results = new List<BarcodeResult>();

            try
            {
                // 정렬된 이미지 경로 사용 (정렬 실패 시 처리 중단)
                var imagePath = document.GetImagePathForUse();
                if (string.IsNullOrWhiteSpace(imagePath))
                {
                    Logger.Instance.Warning($"정렬된 이미지 경로가 없어 바코드 디코딩을 건너뜁니다: {document.SourcePath}");
                    return results;
                }

                if (!File.Exists(imagePath))
                {
                    Logger.Instance.Warning($"정렬된 이미지 파일을 찾을 수 없음: {imagePath}");
                    return results;
                }

                // 이미지 로드
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                bitmap.EndInit();
                bitmap.Freeze();

                int areaIndex = 0;
                foreach (var area in barcodeAreas)
                {
                    try
                    {
                        var result = DecodeBarcodeInArea(bitmap, area, areaIndex);
                        results.Add(result);
                        areaIndex++;
                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Warning($"BarcodeArea {areaIndex} 디코딩 실패: {ex.Message}");
                        // 실패한 영역도 결과에 추가 (Success = false)
                        results.Add(new BarcodeResult
                        {
                            BarcodeAreaId = areaIndex.ToString(),
                            Success = false,
                            DecodedText = null,
                            Format = null,
                            ErrorMessage = ex.Message
                        });
                        areaIndex++;
                    }
                }

                var successCount = results.Count(r => r.Success);
                Logger.Instance.Info($"바코드 디코딩 완료: 총 {results.Count}개 영역 중 {successCount}개 성공");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error($"바코드 디코딩 중 오류 발생: {document.SourcePath}", ex);
                throw;
            }

            return results;
        }

        /// <summary>
        /// 단일 ROI 영역에서 바코드를 디코딩합니다.
        /// </summary>
        private BarcodeResult DecodeBarcodeInArea(
            BitmapSource bitmap,
            RectangleOverlay area,
            int areaIndex)
        {
            // ROI 좌표를 정수로 변환 (픽셀 단위)
            int x = (int)Math.Round(area.X);
            int y = (int)Math.Round(area.Y);
            int width = (int)Math.Round(area.Width);
            int height = (int)Math.Round(area.Height);

            // 경계 체크
            if (x < 0) x = 0;
            if (y < 0) y = 0;
            if (x + width > bitmap.PixelWidth) width = bitmap.PixelWidth - x;
            if (y + height > bitmap.PixelHeight) height = bitmap.PixelHeight - y;

            if (width <= 0 || height <= 0)
            {
                Logger.Instance.Warning($"BarcodeArea {areaIndex}: 유효하지 않은 크기 ({width}x{height})");
                return new BarcodeResult
                {
                    BarcodeAreaId = areaIndex.ToString(),
                    Success = false,
                    DecodedText = null,
                    Format = null,
                    ErrorMessage = $"유효하지 않은 크기: {width}x{height}"
                };
            }

            try
            {
                // ROI 영역 크롭
                var croppedBitmap = new CroppedBitmap(bitmap, new System.Windows.Int32Rect(x, y, width, height));
                croppedBitmap.Freeze();

                // 그레이스케일로 변환 (바코드 디코딩에 유리)
                FormatConvertedBitmap grayBitmap = new FormatConvertedBitmap(croppedBitmap, PixelFormats.Gray8, null, 0);
                grayBitmap.Freeze();

                // WriteableBitmap으로 변환
                var writeableBitmap = new WriteableBitmap(grayBitmap);
                writeableBitmap.Freeze();

                // WriteableBitmap을 System.Drawing.Bitmap으로 변환
                using var systemBitmap = WriteableBitmapToBitmap(writeableBitmap);

                // 디버깅: 크롭된 이미지 저장 (선택적)
                #if DEBUG
                try
                {
                    var debugDir = PathService.BarcodeDebugFolder;
                    Directory.CreateDirectory(debugDir);
                    var debugPath = Path.Combine(debugDir, $"barcode_area_{areaIndex}_{DateTime.Now:yyyyMMddHHmmss}.png");
                    systemBitmap.Save(debugPath, System.Drawing.Imaging.ImageFormat.Png);
                    Logger.Instance.Debug($"BarcodeArea {areaIndex}: 크롭된 이미지 저장됨 - {debugPath}");
                }
                catch { /* 디버그 이미지 저장 실패는 무시 */ }
                #endif

                // 이미지 전처리: 대비 강화 및 이진화 시도
                using var processedBitmap = EnhanceImageForBarcode(systemBitmap);

                // ZXing.Net 0.15에서는 RGBLuminanceSource를 직접 생성해야 함
                Result? result = null;
                try
                {
                    // 원본 이미지로 먼저 시도
                    result = DecodeBitmapWithZxing(systemBitmap);
                    
                    // 실패 시 전처리된 이미지로 시도
                    if (result == null)
                    {
                        Logger.Instance.Debug($"BarcodeArea {areaIndex}: 원본 이미지로 디코딩 실패, 전처리된 이미지로 재시도");
                        result = DecodeBitmapWithZxing(processedBitmap);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.Warning($"BarcodeArea {areaIndex} 디코딩 중 예외 발생: {ex.GetType().Name} - {ex.Message}");
                    result = null;
                }

                if (result != null && !string.IsNullOrEmpty(result.Text))
                {
                    Logger.Instance.Debug($"BarcodeArea {areaIndex}: 디코딩 성공, 텍스트={result.Text}, 포맷={result.BarcodeFormat}");
                    return new BarcodeResult
                    {
                        BarcodeAreaId = areaIndex.ToString(),
                        Success = true,
                        DecodedText = result.Text,
                        Format = result.BarcodeFormat.ToString(),
                        ErrorMessage = null
                    };
                }
                else
                {
                    Logger.Instance.Debug($"BarcodeArea {areaIndex}: 바코드를 찾을 수 없음");
                    return new BarcodeResult
                    {
                        BarcodeAreaId = areaIndex.ToString(),
                        Success = false,
                        DecodedText = null,
                        Format = null,
                        ErrorMessage = "바코드를 찾을 수 없습니다."
                    };
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Warning($"BarcodeArea {areaIndex} 디코딩 중 예외 발생: {ex.Message}");
                return new BarcodeResult
                {
                    BarcodeAreaId = areaIndex.ToString(),
                    Success = false,
                    DecodedText = null,
                    Format = null,
                    ErrorMessage = ex.Message
                };
            }
        }

        /// <summary>
        /// 모든 문서에 대해 바코드를 디코딩합니다.
        /// </summary>
        public Dictionary<string, List<BarcodeResult>> DecodeAllBarcodes(
            IEnumerable<ImageDocument> documents,
            OmrTemplate template)
        {
            var documentsList = documents.ToList();
            Logger.Instance.Info($"전체 문서 바코드 디코딩 시작: {documentsList.Count}개 문서");

            var allResults = new Dictionary<string, List<BarcodeResult>>();

            foreach (var document in documentsList)
            {
                try
                {
                    var results = DecodeBarcodes(document, template.BarcodeAreas);
                    allResults[document.ImageId] = results;
                }
                catch (Exception ex)
                {
                    Logger.Instance.Error($"문서 바코드 디코딩 실패: {document.SourcePath}", ex);
                    // 실패한 문서는 빈 결과로 추가
                    allResults[document.ImageId] = new List<BarcodeResult>();
                }
            }

            Logger.Instance.Info($"전체 문서 바코드 디코딩 완료: {allResults.Count}개 문서 처리");
            return allResults;
        }

        /// <summary>
        /// 바코드 디코딩을 위해 이미지를 전처리합니다 (대비 강화, 이진화).
        /// </summary>
        private System.Drawing.Bitmap EnhanceImageForBarcode(System.Drawing.Bitmap original)
        {
            int width = original.Width;
            int height = original.Height;
            var enhanced = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            // 이미지 데이터 가져오기
            var originalData = original.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            var enhancedData = enhanced.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.WriteOnly,
                System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            try
            {
                // 픽셀 데이터 읽기
                int originalStride = originalData.Stride;
                int enhancedStride = enhancedData.Stride;
                byte[] originalBytes = new byte[Math.Abs(originalStride) * height];
                byte[] enhancedBytes = new byte[Math.Abs(enhancedStride) * height];

                System.Runtime.InteropServices.Marshal.Copy(originalData.Scan0, originalBytes, 0, originalBytes.Length);

                // 픽셀 값 수집 (대비 계산용)
                var pixelValues = new List<int>();
                for (int y = 0; y < height; y++)
                {
                    int rowOffset = y * originalStride;
                    for (int x = 0; x < width; x++)
                    {
                        int pixelOffset = rowOffset + x * 3;
                        int gray = (originalBytes[pixelOffset] + originalBytes[pixelOffset + 1] + originalBytes[pixelOffset + 2]) / 3;
                        pixelValues.Add(gray);
                    }
                }

                // 대비 강화를 위한 히스토그램 스트레칭
                int min = pixelValues.Min();
                int max = pixelValues.Max();
                int range = max - min;
                if (range == 0) range = 1;

                // 대비 강화 및 이진화 적용
                for (int y = 0; y < height; y++)
                {
                    int originalRowOffset = y * originalStride;
                    int enhancedRowOffset = y * enhancedStride;

                    for (int x = 0; x < width; x++)
                    {
                        int originalPixelOffset = originalRowOffset + x * 3;
                        int enhancedPixelOffset = enhancedRowOffset + x * 3;

                        // 그레이스케일 계산
                        int gray = (originalBytes[originalPixelOffset] + originalBytes[originalPixelOffset + 1] + originalBytes[originalPixelOffset + 2]) / 3;

                        // 대비 강화 (히스토그램 스트레칭)
                        int enhancedValue = (int)(((gray - min) / (double)range) * 255);
                        enhancedValue = Math.Max(0, Math.Min(255, enhancedValue));

                        // 적응적 이진화 (Otsu-like threshold)
                        int threshold = (min + max) / 2;
                        byte binary = (byte)(enhancedValue > threshold ? 255 : 0);

                        // RGB 모두 동일한 값으로 설정 (그레이스케일)
                        enhancedBytes[enhancedPixelOffset] = binary;
                        enhancedBytes[enhancedPixelOffset + 1] = binary;
                        enhancedBytes[enhancedPixelOffset + 2] = binary;
                    }
                }

                // 처리된 데이터 쓰기
                System.Runtime.InteropServices.Marshal.Copy(enhancedBytes, 0, enhancedData.Scan0, enhancedBytes.Length);
            }
            finally
            {
                original.UnlockBits(originalData);
                enhanced.UnlockBits(enhancedData);
            }

            return enhanced;
        }

        /// <summary>
        /// WriteableBitmap을 System.Drawing.Bitmap으로 변환합니다.
        /// </summary>
        private System.Drawing.Bitmap WriteableBitmapToBitmap(WriteableBitmap writeableBitmap)
        {
            int width = writeableBitmap.PixelWidth;
            int height = writeableBitmap.PixelHeight;
            
            // 픽셀 포맷에 따라 stride 계산
            int bytesPerPixel = writeableBitmap.Format.BitsPerPixel / 8;
            int stride = width * bytesPerPixel;
            byte[] pixelData = new byte[stride * height];
            writeableBitmap.CopyPixels(pixelData, stride, 0);

            // 그레이스케일(Gray8)인 경우 RGB로 변환
            System.Drawing.Bitmap bitmap;
            if (writeableBitmap.Format == PixelFormats.Gray8)
            {
                bitmap = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                var bitmapData = bitmap.LockBits(
                    new Rectangle(0, 0, width, height),
                    ImageLockMode.WriteOnly,
                    System.Drawing.Imaging.PixelFormat.Format24bppRgb);

                try
                {
                    int rgbStride = bitmapData.Stride;
                    byte[] rgbData = new byte[Math.Abs(rgbStride) * height];
                    
                    // 그레이스케일을 RGB로 변환
                    for (int y = 0; y < height; y++)
                    {
                        int grayRowOffset = y * stride;
                        int rgbRowOffset = y * rgbStride;
                        for (int x = 0; x < width; x++)
                        {
                            byte gray = pixelData[grayRowOffset + x];
                            int rgbOffset = rgbRowOffset + x * 3;
                            rgbData[rgbOffset] = gray;     // R
                            rgbData[rgbOffset + 1] = gray; // G
                            rgbData[rgbOffset + 2] = gray; // B
                        }
                    }
                    
                    System.Runtime.InteropServices.Marshal.Copy(rgbData, 0, bitmapData.Scan0, rgbData.Length);
                }
                finally
                {
                    bitmap.UnlockBits(bitmapData);
                }
            }
            else
            {
                // RGBA 또는 다른 포맷
                bitmap = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                var bitmapData = bitmap.LockBits(
                    new Rectangle(0, 0, width, height),
                    ImageLockMode.WriteOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                try
                {
                    System.Runtime.InteropServices.Marshal.Copy(pixelData, 0, bitmapData.Scan0, pixelData.Length);
                }
                finally
                {
                    bitmap.UnlockBits(bitmapData);
                }
            }

            return bitmap;
        }

        /// <summary>
        /// System.Drawing.Bitmap을 ZXing.Net으로 디코딩합니다.
        /// ZXing.Net 0.15에서는 RGBLuminanceSource를 직접 생성해야 합니다.
        /// </summary>
        private Result? DecodeBitmapWithZxing(System.Drawing.Bitmap bitmap)
        {
            try
            {
                // Bitmap을 RGB 바이트 배열로 변환
                var rgbData = BitmapToRgbBytes(bitmap);
                
                // RGBLuminanceSource 생성 (ZXing.Net이 이미지를 분석할 수 있는 형식)
                var luminanceSource = new RGBLuminanceSource(
                    rgbData,
                    bitmap.Width,
                    bitmap.Height,
                    RGBLuminanceSource.BitmapFormat.RGB24);

                // HybridBinarizer로 이진화 (흑백 변환 및 노이즈 제거)
                var binarizer = new HybridBinarizer(luminanceSource);
                
                // BinaryBitmap 생성 (이진화된 이미지)
                var binaryBitmap = new BinaryBitmap(binarizer);

                // BarcodeReader 생성 및 디코딩 (BinaryBitmap 사용)
                var reader = new BarcodeReader();
                reader.Options.PossibleFormats = _possibleFormats;
                reader.Options.TryHarder = _tryHarder;
                
                // BinaryBitmap을 LuminanceSource로 변환하여 디코딩
                // ZXing.Net 0.15에서는 BinaryBitmap을 직접 디코딩할 수 없으므로
                // LuminanceSource를 사용하여 디코딩
                var readerGeneric = new BarcodeReaderGeneric<LuminanceSource>();
                readerGeneric.Options.PossibleFormats = _possibleFormats;
                readerGeneric.Options.TryHarder = _tryHarder;
                
                return readerGeneric.Decode(luminanceSource);
            }
            catch (Exception ex)
            {
                Logger.Instance.Debug($"ZXing 디코딩 중 예외: {ex.GetType().Name} - {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// System.Drawing.Bitmap을 RGB 바이트 배열로 변환합니다.
        /// Stride(패딩)를 고려하여 행 단위로 복사하고, BGR → RGB 변환을 수행합니다.
        /// </summary>
        private byte[] BitmapToRgbBytes(System.Drawing.Bitmap bitmap)
        {
            var width = bitmap.Width;
            var height = bitmap.Height;
            
            // 출력 버퍼: stride 없는 깔끔한 RGB 배열
            var rgbData = new byte[width * height * 3];

            // Bitmap 데이터를 읽기 위해 LockBits 사용
            var bitmapData = bitmap.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.ReadOnly,
                System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            try
            {
                int srcStride = bitmapData.Stride;  // 원본 stride (패딩 포함, 보통 width*3보다 큼)
                int srcRowSize = width * 3;         // 실제 픽셀 데이터 크기 (패딩 제외)
                
                // 원본 데이터를 임시 버퍼에 읽기 (stride 포함)
                byte[] srcData = new byte[Math.Abs(srcStride) * height];
                System.Runtime.InteropServices.Marshal.Copy(bitmapData.Scan0, srcData, 0, srcData.Length);
                
                // 행 단위로 복사하며 BGR → RGB 변환
                int dstIndex = 0;
                for (int y = 0; y < height; y++)
                {
                    int srcRowStart = y * srcStride;  // stride를 고려한 행 시작 위치
                    
                    // 현재 행의 픽셀 복사 (BGR → RGB 변환)
                    for (int x = 0; x < width; x++)
                    {
                        int srcPixelOffset = srcRowStart + (x * 3);
                        
                        // BGR 순서로 읽기 (메모리: B, G, R)
                        byte b = srcData[srcPixelOffset + 0];
                        byte g = srcData[srcPixelOffset + 1];
                        byte r = srcData[srcPixelOffset + 2];
                        
                        // RGB 순서로 저장
                        rgbData[dstIndex + 0] = r;
                        rgbData[dstIndex + 1] = g;
                        rgbData[dstIndex + 2] = b;
                        
                        dstIndex += 3;
                    }
                    // stride의 패딩 바이트는 자동으로 건너뜀 (srcRowStart가 다음 행으로 이동)
                }
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }

            return rgbData;
        }
    }
}

