using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SimpleOverlayEditor.Models;

namespace SimpleOverlayEditor.Services
{
    /// <summary>
    /// 마킹 결과를 OMR 시트 구조에 맞게 분석합니다.
    /// </summary>
    public class MarkingAnalyzer
    {
        private const int QuestionsCount = 4; // 문항 수
        private const int OptionsPerQuestion = 12; // 문항당 선택지 수

        /// <summary>
        /// 마킹 결과와 바코드 결과를 결합하여 OmrSheetResult를 생성합니다.
        /// </summary>
        public OmrSheetResult AnalyzeSheet(
            ImageDocument document,
            List<MarkingResult>? markingResults,
            List<BarcodeResult>? barcodeResults,
            int questionsCount = QuestionsCount,
            int optionsPerQuestion = OptionsPerQuestion)
        {
            var result = new OmrSheetResult
            {
                ImageId = document.ImageId,
                ImageFileName = Path.GetFileName(document.SourcePath)
            };

            // 바코드 결과 처리 (첫 번째: 수험번호, 두 번째: 면접번호)
            if (barcodeResults != null && barcodeResults.Count >= 1)
            {
                result.StudentId = barcodeResults[0].Success ? barcodeResults[0].DecodedText : null;
                
                // 수험번호 바코드 디코딩 성공했지만 값이 null이거나 빈 문자열인 경우
                if (barcodeResults[0].Success && string.IsNullOrWhiteSpace(result.StudentId))
                {
                    result.HasErrors = true;
                    result.ErrorMessage = string.IsNullOrEmpty(result.ErrorMessage)
                        ? "수험번호 바코드 값 없음"
                        : result.ErrorMessage + "; 수험번호 바코드 값 없음";
                }
                
                if (barcodeResults.Count >= 2)
                {
                    result.InterviewId = barcodeResults[1].Success ? barcodeResults[1].DecodedText : null;
                    
                    // 면접번호 바코드 디코딩 성공했지만 값이 null이거나 빈 문자열인 경우
                    if (barcodeResults[1].Success && string.IsNullOrWhiteSpace(result.InterviewId))
                    {
                        result.HasErrors = true;
                        result.ErrorMessage = string.IsNullOrEmpty(result.ErrorMessage)
                            ? "면접번호 바코드 값 없음"
                            : result.ErrorMessage + "; 면접번호 바코드 값 없음";
                    }
                }
            }

            // 마킹 결과 처리
            if (markingResults != null && markingResults.Count >= questionsCount * optionsPerQuestion)
            {
                // 문항별로 그룹화 (QuestionNumber 기반)
                for (int questionNumber = 1; questionNumber <= questionsCount; questionNumber++)
                {
                    var questionMarkings = markingResults
                        .Where(mr => mr.QuestionNumber == questionNumber)
                        .OrderBy(mr => mr.OptionNumber)
                        .ToList();

                    if (questionMarkings.Count == 0)
                    {
                        result.HasErrors = true;
                        result.ErrorMessage = string.IsNullOrEmpty(result.ErrorMessage)
                            ? $"문항{questionNumber}: 마킹 결과 없음"
                            : result.ErrorMessage + $"; 문항{questionNumber}: 마킹 결과 없음";
                        continue;
                    }

                    var markedOptions = questionMarkings
                        .Where(mr => mr.IsMarked)
                        .Select(mr => mr.OptionNumber)
                        .OrderBy(n => n)
                        .ToList();

                    int? marking = null;
                    string? errorMessage = null;

                    if (markedOptions.Count == 0)
                    {
                        errorMessage = $"문항{questionNumber}: 마킹 없음";
                    }
                    else if (markedOptions.Count > 1)
                    {
                        errorMessage = $"문항{questionNumber}: 다중 마킹 ({string.Join(", ", markedOptions)})";
                    }
                    else
                    {
                        marking = markedOptions[0];
                    }

                    // 결과 할당
                    switch (questionNumber)
                    {
                        case 1:
                            result.Question1Marking = marking;
                            break;
                        case 2:
                            result.Question2Marking = marking;
                            break;
                        case 3:
                            result.Question3Marking = marking;
                            break;
                        case 4:
                            result.Question4Marking = marking;
                            break;
                    }

                    if (errorMessage != null)
                    {
                        result.HasErrors = true;
                        result.ErrorMessage = string.IsNullOrEmpty(result.ErrorMessage)
                            ? errorMessage
                            : result.ErrorMessage + "; " + errorMessage;
                    }
                }
            }
            else if (markingResults != null)
            {
                result.HasErrors = true;
                result.ErrorMessage = $"마킹 영역 수 부족: 예상 {questionsCount * optionsPerQuestion}개, 실제 {markingResults.Count}개";
            }

            // 바코드 오류 체크
            if (barcodeResults != null)
            {
                if (barcodeResults.Count >= 1 && !barcodeResults[0].Success)
                {
                    result.HasErrors = true;
                    result.ErrorMessage = string.IsNullOrEmpty(result.ErrorMessage)
                        ? "수험번호 바코드 디코딩 실패"
                        : result.ErrorMessage + "; 수험번호 바코드 디코딩 실패";
                }
                if (barcodeResults.Count >= 2 && !barcodeResults[1].Success)
                {
                    result.HasErrors = true;
                    result.ErrorMessage = string.IsNullOrEmpty(result.ErrorMessage)
                        ? "면접번호 바코드 디코딩 실패"
                        : result.ErrorMessage + "; 면접번호 바코드 디코딩 실패";
                }
            }

            // CombinedId가 null인 경우 체크
            if (string.IsNullOrEmpty(result.CombinedId))
            {
                result.HasErrors = true;
                result.ErrorMessage = string.IsNullOrEmpty(result.ErrorMessage)
                    ? "결합ID 없음 (수험번호 또는 면접번호 누락)"
                    : result.ErrorMessage + "; 결합ID 없음 (수험번호 또는 면접번호 누락)";
            }

            return result;
        }

        /// <summary>
        /// 세션의 모든 문서에 대해 OmrSheetResult 리스트를 생성합니다.
        /// </summary>
        public List<OmrSheetResult> AnalyzeAllSheets(
            Session session,
            int questionsCount = QuestionsCount,
            int optionsPerQuestion = OptionsPerQuestion)
        {
            var results = new List<OmrSheetResult>();

            foreach (var document in session.Documents)
            {
                List<MarkingResult>? markingResults = null;
                List<BarcodeResult>? barcodeResults = null;
                
                session.MarkingResults?.TryGetValue(document.ImageId, out markingResults);
                session.BarcodeResults?.TryGetValue(document.ImageId, out barcodeResults);

                var sheetResult = AnalyzeSheet(document, markingResults, barcodeResults, questionsCount, optionsPerQuestion);
                results.Add(sheetResult);
            }

            return results;
        }
    }
}
