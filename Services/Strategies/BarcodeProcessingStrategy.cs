using System;
using SimpleOverlayEditor.Models;

namespace SimpleOverlayEditor.Services.Strategies
{
    /// <summary>
    /// 바코드 결과를 OmrSheetResult에 적용하는 전략 인터페이스 (Strategy Pattern)
    /// </summary>
    public interface IBarcodeProcessingStrategy
    {
        /// <summary>
        /// 바코드 결과를 OmrSheetResult에 적용합니다.
        /// </summary>
        /// <param name="result">대상 OmrSheetResult 객체</param>
        /// <param name="barcodeResult">바코드 디코딩 결과</param>
        /// <param name="barcodeIndex">바코드 인덱스 (0부터 시작)</param>
        void ApplyBarcodeResult(OmrSheetResult result, BarcodeResult barcodeResult, int barcodeIndex);

        /// <summary>
        /// 바코드 인덱스에 해당하는 의미를 가져옵니다.
        /// </summary>
        /// <param name="barcodeIndex">바코드 인덱스 (0부터 시작)</param>
        /// <returns>바코드 의미 문자열 (예: "StudentId", "InterviewId"), 없으면 null</returns>
        string? GetBarcodeSemantic(int barcodeIndex);
    }

    /// <summary>
    /// OmrConstants 기반 바코드 처리 전략 (기본 구현)
    /// </summary>
    public class DefaultBarcodeProcessingStrategy : IBarcodeProcessingStrategy
    {
        public void ApplyBarcodeResult(OmrSheetResult result, BarcodeResult barcodeResult, int barcodeIndex)
        {
            if (result == null)
                throw new ArgumentNullException(nameof(result));
            if (barcodeResult == null)
                throw new ArgumentNullException(nameof(barcodeResult));
            if (barcodeIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(barcodeIndex), "바코드 인덱스는 0 이상이어야 합니다.");

            var semantic = GetBarcodeSemantic(barcodeIndex);

            if (semantic == "StudentId")
            {
                result.StudentId = barcodeResult.Success ? barcodeResult.DecodedText : null;

                // 수험번호 바코드 디코딩 성공했지만 값이 null이거나 빈 문자열인 경우
                if (barcodeResult.Success && string.IsNullOrWhiteSpace(result.StudentId))
                {
                    result.HasErrors = true;
                    result.ErrorMessage = string.IsNullOrEmpty(result.ErrorMessage)
                        ? "수험번호 바코드 값 없음"
                        : result.ErrorMessage + "; 수험번호 바코드 값 없음";
                }
            }
            else if (semantic == "InterviewId")
            {
                result.InterviewId = barcodeResult.Success ? barcodeResult.DecodedText : null;

                // 면접번호 바코드 디코딩 성공했지만 값이 null이거나 빈 문자열인 경우
                if (barcodeResult.Success && string.IsNullOrWhiteSpace(result.InterviewId))
                {
                    result.HasErrors = true;
                    result.ErrorMessage = string.IsNullOrEmpty(result.ErrorMessage)
                        ? "면접번호 바코드 값 없음"
                        : result.ErrorMessage + "; 면접번호 바코드 값 없음";
                }
            }

            // 바코드 디코딩 실패 체크
            if (!barcodeResult.Success)
            {
                result.HasErrors = true;
                var semanticName = semantic ?? $"바코드{barcodeIndex + 1}";
                result.ErrorMessage = string.IsNullOrEmpty(result.ErrorMessage)
                    ? $"{semanticName} 바코드 디코딩 실패"
                    : result.ErrorMessage + $"; {semanticName} 바코드 디코딩 실패";
            }
        }

        public string? GetBarcodeSemantic(int barcodeIndex)
        {
            return OmrConstants.GetBarcodeSemantic(barcodeIndex);
        }
    }
}
