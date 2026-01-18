using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SimpleOverlayEditor.Models;
using SimpleOverlayEditor.Services.Mappers;
using SimpleOverlayEditor.Services.Strategies;
using SimpleOverlayEditor.Services.Validators;

namespace SimpleOverlayEditor.Tests
{
    /// <summary>
    /// OMR 설정 검증 테스트
    /// 
    /// 이 테스트들은 단위 테스트 프레임워크(MSTest, NUnit, xUnit 등)가 설정된 경우 실행 가능합니다.
    /// 현재 프로젝트에 테스트 프로젝트가 없다면, 이 파일은 참고용입니다.
    /// </summary>
    [TestClass]
    public class OmrConfigurationValidationTests
    {
        [TestMethod]
        public void ValidateConstants_ShouldNotThrow()
        {
            // Arrange & Act & Assert
            // 상수 유효성 검증이 예외를 던지지 않아야 함
            try
            {
                OmrConstants.Validate();
            }
            catch (InvalidOperationException ex)
            {
                Assert.Fail($"상수 검증 실패: {ex.Message}");
            }
        }

        [TestMethod]
        public void QuestionNumbers_ShouldBeInValidRange()
        {
            // Arrange & Act
            // 모든 유효한 문항 번호가 검증을 통과해야 함
            for (int i = 1; i <= OmrConstants.QuestionsCount; i++)
            {
                // Assert
                Assert.IsTrue(OmrConstants.IsValidQuestionNumber(i),
                    $"문항 번호 {i}는 유효해야 합니다.");
            }

            // 유효하지 않은 범위는 false를 반환해야 함
            Assert.IsFalse(OmrConstants.IsValidQuestionNumber(0),
                "문항 번호 0은 유효하지 않아야 합니다.");
            Assert.IsFalse(OmrConstants.IsValidQuestionNumber(OmrConstants.QuestionsCount + 1),
                $"문항 번호 {OmrConstants.QuestionsCount + 1}은 유효하지 않아야 합니다.");
        }

        [TestMethod]
        public void OptionNumbers_ShouldBeInValidRange()
        {
            // Arrange & Act
            // 모든 유효한 선택지 번호가 검증을 통과해야 함
            for (int i = 1; i <= OmrConstants.OptionsPerQuestion; i++)
            {
                // Assert
                Assert.IsTrue(OmrConstants.IsValidOptionNumber(i),
                    $"선택지 번호 {i}는 유효해야 합니다.");
            }

            // 유효하지 않은 범위는 false를 반환해야 함
            Assert.IsFalse(OmrConstants.IsValidOptionNumber(0),
                "선택지 번호 0은 유효하지 않아야 합니다.");
            Assert.IsFalse(OmrConstants.IsValidOptionNumber(OmrConstants.OptionsPerQuestion + 1),
                $"선택지 번호 {OmrConstants.OptionsPerQuestion + 1}은 유효하지 않아야 합니다.");
        }

        [TestMethod]
        public void BarcodeSemantics_ShouldMatchBarcodeAreasCount()
        {
            // Arrange & Act & Assert
            // 바코드 의미 정의가 바코드 개수와 일치해야 함
            Assert.IsTrue(OmrConstants.BarcodeSemantics.Count <= OmrConstants.BarcodeAreasCount,
                $"바코드 의미 정의({OmrConstants.BarcodeSemantics.Count}개)가 " +
                $"바코드 영역 개수({OmrConstants.BarcodeAreasCount}개)보다 많을 수 없습니다.");
        }

        [TestMethod]
        public void TemplateInitialization_ShouldCreateCorrectSlotCounts()
        {
            // Arrange
            var template = new OmrTemplate();

            // Act & Assert
            Assert.AreEqual(OmrConstants.TimingMarksCount, template.TimingMarks.Count,
                "TimingMarks 개수가 일치해야 합니다.");
            Assert.AreEqual(OmrConstants.BarcodeAreasCount, template.BarcodeAreas.Count,
                "BarcodeAreas 개수가 일치해야 합니다.");
            Assert.AreEqual(OmrConstants.QuestionsCount, template.Questions.Count,
                "Questions 개수가 일치해야 합니다.");
            Assert.AreEqual(OmrConstants.TotalScoringAreas, template.ScoringAreas.Count,
                "ScoringAreas 개수가 일치해야 합니다.");

            foreach (var question in template.Questions)
            {
                Assert.AreEqual(OmrConstants.OptionsPerQuestion, question.Options.Count,
                    $"문항 {question.QuestionNumber}의 Options 개수가 일치해야 합니다.");
            }
        }

        [TestMethod]
        public void ConfigurationValidator_ShouldPassWithDefaultSettings()
        {
            // Arrange & Act
            var result = OmrConfigurationValidator.ValidateConfiguration();

            // Assert
            Assert.IsTrue(result.IsValid,
                $"설정 검증이 통과해야 합니다. 오류: {string.Join(", ", result.Errors)}");
        }
    }

    [TestClass]
    public class MapperTests
    {
        [TestMethod]
        public void OmrSheetResultMapper_ShouldSetAndGetQuestionMarking()
        {
            // Arrange
            var mapper = new OmrSheetResultMapper();
            var result = new OmrSheetResult();

            // Act & Assert
            // 모든 문항에 대해 Set/Get 테스트
            foreach (var questionNumber in mapper.GetAllQuestionNumbers())
            {
                // Set 테스트
                mapper.SetQuestionMarking(result, questionNumber, questionNumber);
                
                // Get 테스트
                var retrieved = mapper.GetQuestionMarking(result, questionNumber);
                Assert.AreEqual(questionNumber, retrieved,
                    $"문항 {questionNumber}의 마킹 값을 올바르게 설정하고 가져와야 합니다.");

                // Null 설정 테스트
                mapper.SetQuestionMarking(result, questionNumber, null);
                var retrievedNull = mapper.GetQuestionMarking(result, questionNumber);
                Assert.IsNull(retrievedNull,
                    $"문항 {questionNumber}의 마킹 값을 null로 설정할 수 있어야 합니다.");
            }
        }

        [TestMethod]
        public void OmrSheetResultMapper_ShouldThrowOnInvalidQuestionNumber()
        {
            // Arrange
            var mapper = new OmrSheetResultMapper();
            var result = new OmrSheetResult();

            // Act & Assert
            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                mapper.SetQuestionMarking(result, 0, 1),
                "문항 번호 0은 예외를 던져야 합니다.");

            Assert.ThrowsException<ArgumentOutOfRangeException>(() =>
                mapper.SetQuestionMarking(result, OmrConstants.QuestionsCount + 1, 1),
                $"문항 번호 {OmrConstants.QuestionsCount + 1}은 예외를 던져야 합니다.");
        }

        [TestMethod]
        public void GradingResultMapper_ShouldSetAndGetQuestionMarking()
        {
            // Arrange
            var mapper = new GradingResultMapper();
            var result = new GradingResult();

            // Act & Assert
            // 모든 문항에 대해 Set/Get 테스트
            foreach (var questionNumber in mapper.GetAllQuestionNumbers())
            {
                // Set 테스트
                mapper.SetQuestionMarking(result, questionNumber, questionNumber);

                // Get 테스트
                var retrieved = mapper.GetQuestionMarking(result, questionNumber);
                Assert.AreEqual(questionNumber, retrieved,
                    $"문항 {questionNumber}의 마킹 값을 올바르게 설정하고 가져와야 합니다.");
            }
        }
    }

    [TestClass]
    public class StrategyTests
    {
        [TestMethod]
        public void DefaultBarcodeProcessingStrategy_ShouldApplyStudentId()
        {
            // Arrange
            var strategy = new DefaultBarcodeProcessingStrategy();
            var result = new OmrSheetResult();
            var barcodeResult = new BarcodeResult
            {
                Success = true,
                DecodedText = "12345"
            };

            // Act
            strategy.ApplyBarcodeResult(result, barcodeResult, 0); // 첫 번째 바코드 = StudentId

            // Assert
            Assert.AreEqual("12345", result.StudentId,
                "수험번호 바코드가 올바르게 적용되어야 합니다.");
        }

        [TestMethod]
        public void DefaultBarcodeProcessingStrategy_ShouldApplyInterviewId()
        {
            // Arrange
            var strategy = new DefaultBarcodeProcessingStrategy();
            var result = new OmrSheetResult();
            var barcodeResult = new BarcodeResult
            {
                Success = true,
                DecodedText = "67890"
            };

            // Act
            strategy.ApplyBarcodeResult(result, barcodeResult, 1); // 두 번째 바코드 = InterviewId

            // Assert
            Assert.AreEqual("67890", result.InterviewId,
                "면접번호 바코드가 올바르게 적용되어야 합니다.");
        }

        [TestMethod]
        public void DefaultBarcodeProcessingStrategy_ShouldSetErrorOnFailedDecoding()
        {
            // Arrange
            var strategy = new DefaultBarcodeProcessingStrategy();
            var result = new OmrSheetResult();
            var barcodeResult = new BarcodeResult
            {
                Success = false
            };

            // Act
            strategy.ApplyBarcodeResult(result, barcodeResult, 0);

            // Assert
            Assert.IsTrue(result.HasErrors,
                "바코드 디코딩 실패 시 오류가 설정되어야 합니다.");
            Assert.IsFalse(string.IsNullOrEmpty(result.ErrorMessage),
                "바코드 디코딩 실패 시 오류 메시지가 설정되어야 합니다.");
        }

        [TestMethod]
        public void DefaultBarcodeProcessingStrategy_ShouldGetBarcodeSemantic()
        {
            // Arrange
            var strategy = new DefaultBarcodeProcessingStrategy();

            // Act & Assert
            Assert.AreEqual("StudentId", strategy.GetBarcodeSemantic(0),
                "첫 번째 바코드의 의미는 StudentId여야 합니다.");
            Assert.AreEqual("InterviewId", strategy.GetBarcodeSemantic(1),
                "두 번째 바코드의 의미는 InterviewId여야 합니다.");
        }
    }
}
