using System;
using System.Windows;

namespace SimpleOverlayEditor.Utils
{
    /// <summary>
    /// 화면 좌표와 원본 이미지 픽셀 좌표 간 변환을 담당합니다.
    /// 이미지는 Uniform 스케일로만 표시되므로 가로/세로 비율이 동일합니다.
    /// </summary>
    public class CoordinateConverter
    {
        /// <summary>
        /// 화면 좌표를 원본 이미지 픽셀 좌표로 변환합니다.
        /// </summary>
        /// <param name="screenPoint">화면 상의 클릭 위치</param>
        /// <param name="canvasSize">캔버스 크기</param>
        /// <param name="imageWidth">원본 이미지 너비 (픽셀)</param>
        /// <param name="imageHeight">원본 이미지 높이 (픽셀)</param>
        /// <param name="imageDisplayRect">이미지가 실제로 표시되는 영역 (Uniform 스케일 적용 후)</param>
        /// <returns>원본 이미지 기준 픽셀 좌표</returns>
        public Point ScreenToPixel(Point screenPoint, Size canvasSize, int imageWidth, int imageHeight, Rect imageDisplayRect)
        {
            // 이미지가 표시되는 영역 내의 상대 좌표 계산
            var relativeX = screenPoint.X - imageDisplayRect.X;
            var relativeY = screenPoint.Y - imageDisplayRect.Y;

            // 스케일 비율 계산 (Uniform이므로 가로/세로 동일)
            var scaleX = imageDisplayRect.Width / imageWidth;
            var scaleY = imageDisplayRect.Height / imageHeight;
            // Uniform이므로 scaleX == scaleY

            // 원본 픽셀 좌표로 변환
            var pixelX = relativeX / scaleX;
            var pixelY = relativeY / scaleY;

            // 경계 체크
            pixelX = Math.Max(0, Math.Min(pixelX, imageWidth));
            pixelY = Math.Max(0, Math.Min(pixelY, imageHeight));

            return new Point(pixelX, pixelY);
        }

        /// <summary>
        /// 원본 이미지 픽셀 좌표를 화면 좌표로 변환합니다.
        /// </summary>
        public Point PixelToScreen(Point pixelPoint, Rect imageDisplayRect, int imageWidth, int imageHeight)
        {
            var scaleX = imageDisplayRect.Width / imageWidth;
            var scaleY = imageDisplayRect.Height / imageHeight;

            var screenX = imageDisplayRect.X + pixelPoint.X * scaleX;
            var screenY = imageDisplayRect.Y + pixelPoint.Y * scaleY;

            return new Point(screenX, screenY);
        }

        /// <summary>
        /// 원본 이미지 픽셀 크기를 화면 크기로 변환합니다.
        /// </summary>
        public Size PixelToScreen(Size pixelSize, Rect imageDisplayRect, int imageWidth, int imageHeight)
        {
            var scaleX = imageDisplayRect.Width / imageWidth;
            var scaleY = imageDisplayRect.Height / imageHeight;

            return new Size(pixelSize.Width * scaleX, pixelSize.Height * scaleY);
        }
    }
}



