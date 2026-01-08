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
    /// <summary>
    /// 타이밍 마크 기반 이미지 정렬 서비스입니다.
    /// </summary>
    public class ImageAlignmentService
    {
        private const double MinConfidenceThreshold = 0.7; // 최소 신뢰도 70%
        private const double MaxRotationDegrees = 5.0; // 최대 회전 각도 ±5도
        private const double MinScale = 0.9; // 최소 스케일 90%
        private const double MaxScale = 1.1; // 최대 스케일 110%

        /// <summary>
        /// 이미지를 정렬하고 결과를 반환합니다.
        /// </summary>
        public AlignmentResult AlignImage(BitmapSource sourceImage, OmrTemplate template)
        {
            Logger.Instance.Info("이미지 정렬 시작");

            // 타이밍 마크가 없으면 정렬 불가
            if (template.TimingMarks.Count == 0)
            {
                Logger.Instance.Info("타이밍 마크가 없어 정렬 생략");
                return new AlignmentResult
                {
                    Success = false,
                    Confidence = 0.0,
                    AlignedImage = sourceImage
                };
            }

            try
            {
                // 1. 타이밍 마크 감지
                var detectedMarks = DetectTimingMarks(sourceImage, template.TimingMarks);

                if (detectedMarks.Count < template.TimingMarks.Count * MinConfidenceThreshold)
                {
                    Logger.Instance.Warning(
                        $"타이밍 마크 감지 부족: 요구 {template.TimingMarks.Count}개, 발견 {detectedMarks.Count}개");
                    return new AlignmentResult
                    {
                        Success = false,
                        Confidence = (double)detectedMarks.Count / template.TimingMarks.Count,
                        AlignedImage = sourceImage
                    };
                }

                // 2. 변환 행렬 계산
                var transform = CalculateTransform(template.TimingMarks, detectedMarks, template);

                // 3. 변환 검증
                if (!IsTransformValid(transform))
                {
                    Logger.Instance.Warning(
                        $"변환 범위 초과: 회전={transform.Rotation:F2}도, 스케일X={transform.ScaleX:F2}, 스케일Y={transform.ScaleY:F2}");
                    return new AlignmentResult
                    {
                        Success = false,
                        Confidence = 0.0,
                        AlignedImage = sourceImage
                    };
                }

                // 4. 이미지 정렬 적용
                var alignedImage = ApplyTransform(sourceImage, transform);

                // 5. 신뢰도 계산
                var confidence = CalculateConfidence(transform, detectedMarks.Count, template.TimingMarks.Count);

                Logger.Instance.Info(
                    $"이미지 정렬 성공: 신뢰도={confidence:F2}, 회전={transform.Rotation:F2}도, " +
                    $"스케일=({transform.ScaleX:F2}, {transform.ScaleY:F2})");

                return new AlignmentResult
                {
                    Success = true,
                    Confidence = confidence,
                    AlignedImage = alignedImage,
                    Transform = transform
                };
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("이미지 정렬 중 오류 발생", ex);
                return new AlignmentResult
                {
                    Success = false,
                    Confidence = 0.0,
                    AlignedImage = sourceImage
                };
            }
        }

        /// <summary>
        /// 스캔 이미지에서 타이밍 마크의 실제 위치를 감지합니다.
        /// </summary>
        private List<Point> DetectTimingMarks(BitmapSource image, IEnumerable<RectangleOverlay> templateTimingMarks)
        {
            var detectedPoints = new List<Point>();

            // 그레이스케일 변환
            var grayImage = new FormatConvertedBitmap(image, PixelFormats.Gray8, null, 0);
            grayImage.Freeze();

            foreach (var mark in templateTimingMarks)
            {
                try
                {
                    // 템플릿의 타이밍 마크 위치 (기준 위치)
                    var templateCenter = new Point(
                        mark.X + mark.Width / 2,
                        mark.Y + mark.Height / 2);

                    // ROI 영역 확장 (탐색 범위 넓히기)
                    int searchMargin = (int)Math.Max(mark.Width, mark.Height);
                    int searchX = Math.Max(0, (int)(mark.X - searchMargin));
                    int searchY = Math.Max(0, (int)(mark.Y - searchMargin));
                    int searchWidth = Math.Min(image.PixelWidth - searchX, (int)(mark.Width + searchMargin * 2));
                    int searchHeight = Math.Min(image.PixelHeight - searchY, (int)(mark.Height + searchMargin * 2));

                    // ROI 영역 추출
                    int stride = (searchWidth * grayImage.Format.BitsPerPixel + 7) / 8;
                    byte[] pixels = new byte[stride * searchHeight];
                    grayImage.CopyPixels(new Int32Rect(searchX, searchY, searchWidth, searchHeight), pixels, stride, 0);

                    // 이진화 및 중심점 찾기
                    var center = FindDarkRegionCenter(pixels, searchWidth, searchHeight, stride);

                    if (center.HasValue)
                    {
                        // 전체 이미지 기준 좌표로 변환
                        var detectedPoint = new Point(
                            searchX + center.Value.X,
                            searchY + center.Value.Y);
                        detectedPoints.Add(detectedPoint);

                        Logger.Instance.Debug(
                            $"타이밍 마크 감지: 템플릿=({templateCenter.X:F1}, {templateCenter.Y:F1}), " +
                            $"실제=({detectedPoint.X:F1}, {detectedPoint.Y:F1})");
                    }
                    else
                    {
                        // 감지 실패 시 템플릿 위치 사용
                        detectedPoints.Add(templateCenter);
                        Logger.Instance.Warning($"타이밍 마크 감지 실패, 템플릿 위치 사용: ({templateCenter.X:F1}, {templateCenter.Y:F1})");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Instance.Warning($"타이밍 마크 감지 중 오류: {ex.Message}");
                    // 오류 시 템플릿 위치 사용
                    var templateCenter = new Point(mark.X + mark.Width / 2, mark.Y + mark.Height / 2);
                    detectedPoints.Add(templateCenter);
                }
            }

            return detectedPoints;
        }

        /// <summary>
        /// ROI 영역에서 어두운 영역의 중심점을 찾습니다.
        /// </summary>
        private Point? FindDarkRegionCenter(byte[] pixels, int width, int height, int stride)
        {
            const int threshold = 128; // 이진화 임계값

            // 이진화 및 무게중심 계산
            long sumX = 0, sumY = 0, count = 0;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * stride + x;
                    if (index < pixels.Length && pixels[index] < threshold)
                    {
                        sumX += x;
                        sumY += y;
                        count++;
                    }
                }
            }

            if (count == 0)
                return null;

            return new Point(sumX / (double)count, sumY / (double)count);
        }

        /// <summary>
        /// 템플릿 좌표와 실제 좌표를 비교하여 변환 행렬을 계산합니다.
        /// </summary>
        private AlignmentTransform CalculateTransform(
            IEnumerable<RectangleOverlay> templateMarks,
            List<Point> detectedPoints,
            OmrTemplate template)
        {
            var templatePoints = templateMarks.Select(m => 
                new Point(m.X + m.Width / 2, m.Y + m.Height / 2)).ToList();

            if (templatePoints.Count != detectedPoints.Count || templatePoints.Count < 2)
            {
                // 최소 2개 필요, 부족하면 단순 이동만 계산
                if (templatePoints.Count > 0 && detectedPoints.Count > 0)
                {
                    var dx = detectedPoints[0].X - templatePoints[0].X;
                    var dy = detectedPoints[0].Y - templatePoints[0].Y;
                    return new AlignmentTransform
                    {
                        TranslationX = dx,
                        TranslationY = dy,
                        ScaleX = 1.0,
                        ScaleY = 1.0,
                        Rotation = 0.0
                    };
                }
            }

            // 최소 제곱법으로 평균 이동량 계산
            double sumDx = 0, sumDy = 0;
            int validCount = Math.Min(templatePoints.Count, detectedPoints.Count);

            for (int i = 0; i < validCount; i++)
            {
                sumDx += detectedPoints[i].X - templatePoints[i].X;
                sumDy += detectedPoints[i].Y - templatePoints[i].Y;
            }

            double avgDx = sumDx / validCount;
            double avgDy = sumDy / validCount;

            // 스케일 계산 (두 점 간 거리 비교)
            double scaleX = 1.0, scaleY = 1.0;
            if (templatePoints.Count >= 2)
            {
                double templateDist = Math.Sqrt(
                    Math.Pow(templatePoints[1].X - templatePoints[0].X, 2) +
                    Math.Pow(templatePoints[1].Y - templatePoints[0].Y, 2));
                double detectedDist = Math.Sqrt(
                    Math.Pow(detectedPoints[1].X - detectedPoints[0].X, 2) +
                    Math.Pow(detectedPoints[1].Y - detectedPoints[0].Y, 2));

                if (templateDist > 0)
                {
                    double scale = detectedDist / templateDist;
                    scaleX = scaleY = scale;
                }
            }

            // 회전 계산 (첫 번째 점 기준)
            double rotation = 0.0;
            if (templatePoints.Count >= 2)
            {
                double templateAngle = Math.Atan2(
                    templatePoints[1].Y - templatePoints[0].Y,
                    templatePoints[1].X - templatePoints[0].X);
                double detectedAngle = Math.Atan2(
                    detectedPoints[1].Y - detectedPoints[0].Y,
                    detectedPoints[1].X - detectedPoints[0].X);
                rotation = (detectedAngle - templateAngle) * 180.0 / Math.PI;
            }

            return new AlignmentTransform
            {
                TranslationX = avgDx,
                TranslationY = avgDy,
                ScaleX = scaleX,
                ScaleY = scaleY,
                Rotation = rotation
            };
        }

        /// <summary>
        /// 변환 행렬이 유효한 범위 내에 있는지 검증합니다.
        /// </summary>
        private bool IsTransformValid(AlignmentTransform transform)
        {
            if (Math.Abs(transform.Rotation) > MaxRotationDegrees)
                return false;

            if (transform.ScaleX < MinScale || transform.ScaleX > MaxScale)
                return false;

            if (transform.ScaleY < MinScale || transform.ScaleY > MaxScale)
                return false;

            return true;
        }

        /// <summary>
        /// 이미지에 변환을 적용합니다.
        /// </summary>
        private BitmapSource ApplyTransform(BitmapSource sourceImage, AlignmentTransform transform)
        {
            // 변환이 없거나 미미한 경우 원본 반환
            if (Math.Abs(transform.Rotation) < 0.1 &&
                Math.Abs(transform.ScaleX - 1.0) < 0.01 &&
                Math.Abs(transform.ScaleY - 1.0) < 0.01 &&
                Math.Abs(transform.TranslationX) < 1.0 &&
                Math.Abs(transform.TranslationY) < 1.0)
            {
                return sourceImage;
            }

            // 변환 그룹 생성
            var transformGroup = new TransformGroup();
            
            // 중심점 기준 변환을 위해 중심을 계산
            double centerX = sourceImage.PixelWidth / 2.0;
            double centerY = sourceImage.PixelHeight / 2.0;

            // 회전
            if (Math.Abs(transform.Rotation) > 0.1)
            {
                var rotateTransform = new RotateTransform(transform.Rotation, centerX, centerY);
                transformGroup.Children.Add(rotateTransform);
            }

            // 스케일
            if (Math.Abs(transform.ScaleX - 1.0) > 0.01 || Math.Abs(transform.ScaleY - 1.0) > 0.01)
            {
                var scaleTransform = new ScaleTransform(transform.ScaleX, transform.ScaleY, centerX, centerY);
                transformGroup.Children.Add(scaleTransform);
            }

            // 이동
            if (Math.Abs(transform.TranslationX) > 0.5 || Math.Abs(transform.TranslationY) > 0.5)
            {
                var translateTransform = new TranslateTransform(transform.TranslationX, transform.TranslationY);
                transformGroup.Children.Add(translateTransform);
            }

            if (transformGroup.Children.Count == 0)
                return sourceImage;

            // 변환 적용
            var transformedImage = new TransformedBitmap(sourceImage, transformGroup);
            transformedImage.Freeze();

            return transformedImage;
        }

        /// <summary>
        /// 정렬 신뢰도를 계산합니다.
        /// </summary>
        private double CalculateConfidence(AlignmentTransform transform, int detectedCount, int totalCount)
        {
            // 감지율
            double detectionRate = (double)detectedCount / totalCount;

            // 변환 크기 기반 신뢰도 (작을수록 좋음)
            double rotationConfidence = 1.0 - Math.Min(Math.Abs(transform.Rotation) / MaxRotationDegrees, 1.0);
            double scaleConfidence = 1.0 - Math.Min(
                Math.Max(Math.Abs(transform.ScaleX - 1.0), Math.Abs(transform.ScaleY - 1.0)) / 0.1, 1.0);

            // 가중 평균
            return (detectionRate * 0.5 + rotationConfidence * 0.25 + scaleConfidence * 0.25);
        }
    }

    /// <summary>
    /// 정렬 결과
    /// </summary>
    public class AlignmentResult
    {
        public bool Success { get; set; }
        public double Confidence { get; set; }
        public BitmapSource AlignedImage { get; set; } = null!;
        public AlignmentTransform? Transform { get; set; }
    }

    /// <summary>
    /// 정렬 변환 정보
    /// </summary>
    public class AlignmentTransform
    {
        public double Rotation { get; set; }
        public double ScaleX { get; set; }
        public double ScaleY { get; set; }
        public double TranslationX { get; set; }
        public double TranslationY { get; set; }
    }
}
