using System;
using System.Collections.Generic;
using SimpleOverlayEditor.Models;

namespace SimpleOverlayEditor.Services.Validators
{
    /// <summary>
    /// OMR 설정 검증 결과
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; }
        public List<string> Errors { get; }

        public ValidationResult(bool isValid, List<string> errors)
        {
            IsValid = isValid;
            Errors = errors ?? new List<string>();
        }
    }

    /// <summary>
    /// OMR 설정 검증을 수행하는 클래스 (Specification Pattern)
    /// </summary>
    public static class OmrConfigurationValidator
    {
        /// <summary>
        /// OMR 설정의 유효성을 검증합니다.
        /// </summary>
        /// <returns>검증 결과</returns>
        public static ValidationResult ValidateConfiguration()
        {
            var errors = new List<string>();

            // 1. 상수 유효성 검증
            try
            {
                OmrConstants.Validate();
            }
            catch (InvalidOperationException ex)
            {
                errors.Add(ex.Message);
            }

            // 2. 템플릿 구조 검증
            try
            {
                var template = new OmrTemplate();

                if (template.TimingMarks.Count != OmrConstants.TimingMarksCount)
                {
                    errors.Add($"TimingMarks 개수 불일치: 예상 {OmrConstants.TimingMarksCount}개, 실제 {template.TimingMarks.Count}개");
                }

                if (template.BarcodeAreas.Count != OmrConstants.BarcodeAreasCount)
                {
                    errors.Add($"BarcodeAreas 개수 불일치: 예상 {OmrConstants.BarcodeAreasCount}개, 실제 {template.BarcodeAreas.Count}개");
                }

                if (template.Questions.Count != OmrConstants.QuestionsCount)
                {
                    errors.Add($"Questions 개수 불일치: 예상 {OmrConstants.QuestionsCount}개, 실제 {template.Questions.Count}개");
                }

                if (template.ScoringAreas.Count != OmrConstants.TotalScoringAreas)
                {
                    errors.Add($"ScoringAreas 개수 불일치: 예상 {OmrConstants.TotalScoringAreas}개, 실제 {template.ScoringAreas.Count}개");
                }

                // 각 문항의 선택지 개수 검증
                foreach (var question in template.Questions)
                {
                    if (question.Options.Count != OmrConstants.OptionsPerQuestion)
                    {
                        errors.Add($"문항 {question.QuestionNumber}의 Options 개수 불일치: 예상 {OmrConstants.OptionsPerQuestion}개, 실제 {question.Options.Count}개");
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add($"템플릿 구조 검증 실패: {ex.Message}");
            }

            // 3. 바코드 의미론 검증
            if (OmrConstants.BarcodeSemantics.Count > OmrConstants.BarcodeAreasCount)
            {
                errors.Add($"바코드 의미 정의({OmrConstants.BarcodeSemantics.Count}개)가 바코드 영역 개수({OmrConstants.BarcodeAreasCount}개)보다 많습니다.");
            }

            return new ValidationResult(errors.Count == 0, errors);
        }
    }
}
