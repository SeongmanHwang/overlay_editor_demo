using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SimpleOverlayEditor.Models;
using SimpleOverlayEditor.Services.Mappers;
using SimpleOverlayEditor.Services.Strategies;

namespace SimpleOverlayEditor.Services
{
    /// <summary>
    /// 마킹 결과를 OMR 시트 구조에 맞게 분석합니다.
    /// </summary>
    public class MarkingAnalyzer
    {
        private readonly IQuestionResultMapper<OmrSheetResult> _questionMapper;
        private readonly IBarcodeProcessingStrategy _barcodeStrategy;

        /// <summary>
        /// 기본 생성자 (기본 구현 사용)
        /// </summary>
        public MarkingAnalyzer()
            : this(new OmrSheetResultMapper(), new DefaultBarcodeProcessingStrategy())
        {
        }

        /// <summary>
        /// 테스트/확장을 위한 생성자 (의존성 주입)
        /// </summary>
        public MarkingAnalyzer(
            IQuestionResultMapper<OmrSheetResult> questionMapper,
            IBarcodeProcessingStrategy barcodeStrategy)
        {
            _questionMapper = questionMapper ?? throw new ArgumentNullException(nameof(questionMapper));
            _barcodeStrategy = barcodeStrategy ?? throw new ArgumentNullException(nameof(barcodeStrategy));
        }

        /// <summary>
        /// 마킹 결과와 바코드 결과를 결합하여 OmrSheetResult를 생성합니다.
        /// </summary>
        public OmrSheetResult AnalyzeSheet(
            ImageDocument document,
            List<MarkingResult>? markingResults,
            List<BarcodeResult>? barcodeResults,
            int questionsCount = -1,
            int optionsPerQuestion = -1)
        {
            // 기본값을 OmrConstants에서 가져옴
            if (questionsCount < 0) questionsCount = OmrConstants.QuestionsCount;
            if (optionsPerQuestion < 0) optionsPerQuestion = OmrConstants.OptionsPerQuestion;

            var result = new OmrSheetResult
            {
                ImageId = document.ImageId,
                ImageFileName = Path.GetFileName(document.SourcePath)
            };

            // 바코드 결과 처리 - Strategy 사용
            if (barcodeResults != null)
            {
                for (int i = 0; i < barcodeResults.Count && i < OmrConstants.BarcodeAreasCount; i++)
                {
                    _barcodeStrategy.ApplyBarcodeResult(result, barcodeResults[i], i);
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

                    // 결과 할당 - Mapper 사용 (switch문 제거)
                    _questionMapper.SetQuestionMarking(result, questionNumber, marking);

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

            // 바코드 오류 체크는 Strategy에서 이미 처리됨 (ApplyBarcodeResult 메서드 내)

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
            int questionsCount = -1,
            int optionsPerQuestion = -1)
        {
            // 기본값을 OmrConstants에서 가져옴
            if (questionsCount < 0) questionsCount = OmrConstants.QuestionsCount;
            if (optionsPerQuestion < 0) optionsPerQuestion = OmrConstants.OptionsPerQuestion;

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
