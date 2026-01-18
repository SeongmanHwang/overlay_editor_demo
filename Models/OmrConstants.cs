using System;
using System.Collections.Generic;

namespace SimpleOverlayEditor.Models
{
    /// <summary>
    /// OMR 템플릿 구조 관련 상수를 중앙화하여 관리합니다.
    /// 개발/빌드 단계에서 이 값들만 변경하면 다른 OMR 용지 구조에 대응할 수 있습니다.
    /// </summary>
    public static class OmrConstants
    {
        /// <summary>
        /// 타이밍 마크 개수 (기본값: 5)
        /// </summary>
        public static int TimingMarksCount => 5;

        /// <summary>
        /// 바코드 영역 개수 (기본값: 2)
        /// </summary>
        public static int BarcodeAreasCount => 2;

        /// <summary>
        /// 문항 수 (기본값: 4)
        /// </summary>
        public static int QuestionsCount => 4;

        /// <summary>
        /// 문항당 선택지 수 (기본값: 12)
        /// </summary>
        public static int OptionsPerQuestion => 12;

        /// <summary>
        /// 총 채점 영역 수 (QuestionsCount × OptionsPerQuestion)
        /// </summary>
        public static int TotalScoringAreas => QuestionsCount * OptionsPerQuestion;

        /// <summary>
        /// 바코드 영역별 의미 정의 (인덱스 = 바코드 영역 번호 - 1)
        /// Strategy Pattern을 위한 설정입니다.
        /// 리팩토링 시 이 딕셔너리만 수정하면 됩니다.
        /// </summary>
        public static Dictionary<int, string> BarcodeSemantics { get; } = new()
        {
            { 0, "StudentId" },    // 첫 번째 바코드 = 수험번호
            { 1, "InterviewId" }   // 두 번째 바코드 = 면접번호
        };

        /// <summary>
        /// 상수 설정의 유효성을 검증합니다 (Specification Pattern).
        /// 리팩토링 전후에 이 메서드를 호출하여 설정이 올바른지 확인하세요.
        /// </summary>
        /// <exception cref="InvalidOperationException">상수 값이 유효하지 않은 경우</exception>
        public static void Validate()
        {
            if (QuestionsCount < 1 || QuestionsCount > 20)
                throw new InvalidOperationException($"QuestionsCount는 1~20 사이여야 합니다. 현재: {QuestionsCount}");

            if (OptionsPerQuestion < 2 || OptionsPerQuestion > 26)
                throw new InvalidOperationException($"OptionsPerQuestion은 2~26 사이여야 합니다. 현재: {OptionsPerQuestion}");

            if (TimingMarksCount < 3 || TimingMarksCount > 10)
                throw new InvalidOperationException($"TimingMarksCount는 3~10 사이여야 합니다. 현재: {TimingMarksCount}");

            if (BarcodeAreasCount < 1 || BarcodeAreasCount > 5)
                throw new InvalidOperationException($"BarcodeAreasCount는 1~5 사이여야 합니다. 현재: {BarcodeAreasCount}");

            if (BarcodeSemantics.Count > BarcodeAreasCount)
                throw new InvalidOperationException($"바코드 의미 정의({BarcodeSemantics.Count}개)가 바코드 영역 개수({BarcodeAreasCount}개)보다 많을 수 없습니다.");
        }

        /// <summary>
        /// 문항 번호 범위 검증 (1부터 QuestionsCount까지)
        /// </summary>
        public static bool IsValidQuestionNumber(int questionNumber)
        {
            return questionNumber >= 1 && questionNumber <= QuestionsCount;
        }

        /// <summary>
        /// 선택지 번호 범위 검증 (1부터 OptionsPerQuestion까지)
        /// </summary>
        public static bool IsValidOptionNumber(int optionNumber)
        {
            return optionNumber >= 1 && optionNumber <= OptionsPerQuestion;
        }

        /// <summary>
        /// 바코드 인덱스로 의미를 가져옵니다.
        /// </summary>
        /// <param name="barcodeIndex">바코드 인덱스 (0부터 시작)</param>
        /// <returns>바코드 의미 문자열 (예: "StudentId", "InterviewId"), 없으면 null</returns>
        public static string? GetBarcodeSemantic(int barcodeIndex)
        {
            return BarcodeSemantics.TryGetValue(barcodeIndex, out var semantic) ? semantic : null;
        }
    }
}
