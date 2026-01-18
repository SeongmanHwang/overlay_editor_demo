using System;
using System.Collections.Generic;
using System.Linq;
using SimpleOverlayEditor.Models;

namespace SimpleOverlayEditor.Services.Mappers
{
    /// <summary>
    /// 문항 마킹 결과 매핑을 위한 Mapper 인터페이스 (Template Method Pattern)
    /// OmrConstants 변경 시 이 인터페이스의 구현체만 수정하면 됩니다.
    /// </summary>
    /// <typeparam name="T">매핑 대상 모델 타입 (OmrSheetResult, GradingResult 등)</typeparam>
    public interface IQuestionResultMapper<T>
    {
        /// <summary>
        /// 특정 문항의 마킹 결과를 설정합니다.
        /// </summary>
        /// <param name="target">대상 객체</param>
        /// <param name="questionNumber">문항 번호 (1부터 시작)</param>
        /// <param name="marking">마킹 결과 (null = 미마킹 또는 다중마킹)</param>
        /// <exception cref="ArgumentOutOfRangeException">문항 번호가 유효 범위를 벗어난 경우</exception>
        void SetQuestionMarking(T target, int questionNumber, int? marking);

        /// <summary>
        /// 특정 문항의 마킹 결과를 가져옵니다.
        /// </summary>
        /// <param name="source">소스 객체</param>
        /// <param name="questionNumber">문항 번호 (1부터 시작)</param>
        /// <returns>마킹 결과 (null = 미마킹 또는 다중마킹)</returns>
        /// <exception cref="ArgumentOutOfRangeException">문항 번호가 유효 범위를 벗어난 경우</exception>
        int? GetQuestionMarking(T source, int questionNumber);

        /// <summary>
        /// 모든 문항 번호 목록을 반환합니다 (1부터 QuestionsCount까지).
        /// </summary>
        IEnumerable<int> GetAllQuestionNumbers();
    }

    /// <summary>
    /// OmrSheetResult용 Mapper 구현
    /// </summary>
    public class OmrSheetResultMapper : IQuestionResultMapper<OmrSheetResult>
    {
        public void SetQuestionMarking(OmrSheetResult target, int questionNumber, int? marking)
        {
            if (!OmrConstants.IsValidQuestionNumber(questionNumber))
            {
                throw new ArgumentOutOfRangeException(nameof(questionNumber),
                    $"문항 번호는 1부터 {OmrConstants.QuestionsCount}까지여야 합니다. 현재: {questionNumber}");
            }

            // switch문을 통한 하드코딩된 속성 설정
            // 리팩토링 시 이 메서드만 수정하면 됨
            switch (questionNumber)
            {
                case 1:
                    target.Question1Marking = marking;
                    break;
                case 2:
                    target.Question2Marking = marking;
                    break;
                case 3:
                    target.Question3Marking = marking;
                    break;
                case 4:
                    target.Question4Marking = marking;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(questionNumber),
                        $"현재 QuestionsCount({OmrConstants.QuestionsCount})보다 큰 문항 번호: {questionNumber}");
            }
        }

        public int? GetQuestionMarking(OmrSheetResult source, int questionNumber)
        {
            if (!OmrConstants.IsValidQuestionNumber(questionNumber))
            {
                throw new ArgumentOutOfRangeException(nameof(questionNumber),
                    $"문항 번호는 1부터 {OmrConstants.QuestionsCount}까지여야 합니다. 현재: {questionNumber}");
            }

            return questionNumber switch
            {
                1 => source.Question1Marking,
                2 => source.Question2Marking,
                3 => source.Question3Marking,
                4 => source.Question4Marking,
                _ => throw new ArgumentOutOfRangeException(nameof(questionNumber),
                    $"현재 QuestionsCount({OmrConstants.QuestionsCount})보다 큰 문항 번호: {questionNumber}")
            };
        }

        public IEnumerable<int> GetAllQuestionNumbers()
        {
            for (int i = 1; i <= OmrConstants.QuestionsCount; i++)
            {
                yield return i;
            }
        }
    }

    /// <summary>
    /// GradingResult용 Mapper 구현
    /// </summary>
    public class GradingResultMapper : IQuestionResultMapper<GradingResult>
    {
        public void SetQuestionMarking(GradingResult target, int questionNumber, int? marking)
        {
            if (!OmrConstants.IsValidQuestionNumber(questionNumber))
            {
                throw new ArgumentOutOfRangeException(nameof(questionNumber),
                    $"문항 번호는 1부터 {OmrConstants.QuestionsCount}까지여야 합니다. 현재: {questionNumber}");
            }

            // switch문을 통한 하드코딩된 속성 설정
            switch (questionNumber)
            {
                case 1:
                    target.Question1Marking = marking;
                    break;
                case 2:
                    target.Question2Marking = marking;
                    break;
                case 3:
                    target.Question3Marking = marking;
                    break;
                case 4:
                    target.Question4Marking = marking;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(questionNumber),
                        $"현재 QuestionsCount({OmrConstants.QuestionsCount})보다 큰 문항 번호: {questionNumber}");
            }
        }

        public int? GetQuestionMarking(GradingResult source, int questionNumber)
        {
            if (!OmrConstants.IsValidQuestionNumber(questionNumber))
            {
                throw new ArgumentOutOfRangeException(nameof(questionNumber),
                    $"문항 번호는 1부터 {OmrConstants.QuestionsCount}까지여야 합니다. 현재: {questionNumber}");
            }

            return questionNumber switch
            {
                1 => source.Question1Marking,
                2 => source.Question2Marking,
                3 => source.Question3Marking,
                4 => source.Question4Marking,
                _ => throw new ArgumentOutOfRangeException(nameof(questionNumber),
                    $"현재 QuestionsCount({OmrConstants.QuestionsCount})보다 큰 문항 번호: {questionNumber}")
            };
        }

        public IEnumerable<int> GetAllQuestionNumbers()
        {
            for (int i = 1; i <= OmrConstants.QuestionsCount; i++)
            {
                yield return i;
            }
        }
    }
}
