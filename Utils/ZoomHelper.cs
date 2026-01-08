using System;
using System.Windows;

namespace SimpleOverlayEditor.Utils
{
    /// <summary>
    /// 이미지 표시 영역 계산 (Uniform 스케일, Fit, 최소 줌 제한 등)
    /// </summary>
    public class ZoomHelper
    {
        private const double MinZoomPercent = 0.4; // 최소 40% 줌

        public enum ImageAlignment
        {
            TopLeft,
            TopRight,
            Center,
            BottomLeft,
            BottomRight
        }

        /// <summary>
        /// Uniform 스케일로 이미지를 표시할 때의 실제 표시 영역을 계산합니다.
        /// </summary>
        /// <param name="imageWidth">원본 이미지 너비</param>
        /// <param name="imageHeight">원본 이미지 높이</param>
        /// <param name="availableSize">사용 가능한 영역 크기</param>
        /// <param name="alignment">이미지 정렬 방식 (기본값: TopRight)</param>
        /// <returns>이미지가 표시될 Rect</returns>
        public static Rect CalculateImageDisplayRect(int imageWidth, int imageHeight, Size availableSize, ImageAlignment alignment = ImageAlignment.TopRight)
        {
            if (imageWidth <= 0 || imageHeight <= 0 || availableSize.Width <= 0 || availableSize.Height <= 0)
            {
                return new Rect(0, 0, 0, 0);
            }

            // Uniform 스케일 계산 (가로/세로 동일 비율)
            var scaleX = availableSize.Width / imageWidth;
            var scaleY = availableSize.Height / imageHeight;
            var scale = Math.Min(scaleX, scaleY); // 더 작은 쪽에 맞춤

            // 최소 줌 제한
            var minScale = MinZoomPercent;
            if (scale < minScale)
            {
                scale = minScale;
            }

            // 실제 표시 크기
            var displayWidth = imageWidth * scale;
            var displayHeight = imageHeight * scale;

            // 정렬에 따른 위치 계산
            // 주의: 이미지가 뷰포트보다 크면 x나 y가 음수가 될 수 있음 (이건 정상, Canvas가 확장됨)
            double x, y;
            switch (alignment)
            {
                case ImageAlignment.TopLeft:
                    x = 0;
                    y = 0;
                    break;
                case ImageAlignment.TopRight:
                    // 오른쪽 위: x는 오른쪽에 붙이되, 이미지가 크면 음수가 될 수 있음
                    x = availableSize.Width - displayWidth;
                    y = 0;
                    break;
                case ImageAlignment.Center:
                    x = (availableSize.Width - displayWidth) / 2;
                    y = (availableSize.Height - displayHeight) / 2;
                    break;
                case ImageAlignment.BottomLeft:
                    x = 0;
                    y = availableSize.Height - displayHeight;
                    break;
                case ImageAlignment.BottomRight:
                    x = availableSize.Width - displayWidth;
                    y = availableSize.Height - displayHeight;
                    break;
                default:
                    x = availableSize.Width - displayWidth;
                    y = 0;
                    break;
            }

            return new Rect(x, y, displayWidth, displayHeight);
        }

        /// <summary>
        /// 스크롤이 필요한지 확인합니다.
        /// </summary>
        public static bool NeedsScroll(int imageWidth, int imageHeight, Size availableSize)
        {
            var displayRect = CalculateImageDisplayRect(imageWidth, imageHeight, availableSize);
            return displayRect.Width > availableSize.Width || displayRect.Height > availableSize.Height;
        }
    }
}

