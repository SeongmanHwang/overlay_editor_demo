using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using ClosedXML.Excel;
using SimpleOverlayEditor.Models;

namespace SimpleOverlayEditor.Services
{
    /// <summary>
    /// 정답 및 배점 정보를 저장/로드하는 서비스입니다.
    /// </summary>
    public class ScoringRuleStore
    {
        public static string ScoringRuleFilePath
        {
            get
            {
                if (PathService.CurrentRound != null)
                {
                    return Path.Combine(PathService.GetRoundRoot(PathService.CurrentRound), "scoring_rule.json");
                }
                return Path.Combine(PathService.AppDataFolder, "scoring_rule.json");
            }
        }

        /// <summary>
        /// 정답 및 배점 정보를 저장합니다.
        /// </summary>
        public void SaveScoringRule(ScoringRule scoringRule)
        {
            try
            {
                PathService.EnsureDirectories();

                var data = new
                {
                    ScoreNames = scoringRule.ScoreNames.ToList(),
                    Questions = scoringRule.Questions.Select(q => new
                    {
                        QuestionNumber = q.QuestionNumber,
                        Scores = q.Scores.ToList()
                    }).ToList()
                };

                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });

                File.WriteAllText(ScoringRuleFilePath, json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"정답 및 배점 저장 실패: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 정답 및 배점 정보를 로드합니다.
        /// </summary>
        public ScoringRule LoadScoringRule()
        {
            if (!File.Exists(ScoringRuleFilePath))
            {
                return new ScoringRule();
            }

            try
            {
                var json = File.ReadAllText(ScoringRuleFilePath);
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var scoringRule = new ScoringRule();

                // 점수 이름 로드
                if (root.TryGetProperty("ScoreNames", out var scoreNamesElement))
                {
                    scoringRule.ScoreNames.Clear();
                    foreach (var nameElem in scoreNamesElement.EnumerateArray())
                    {
                        scoringRule.ScoreNames.Add(nameElem.GetString() ?? string.Empty);
                    }
                    
                    // 12개가 안 되면 빈 문자열로 채움
                    while (scoringRule.ScoreNames.Count < OmrConstants.OptionsPerQuestion)
                    {
                        scoringRule.ScoreNames.Add(string.Empty);
                    }
                }

                if (root.TryGetProperty("Questions", out var questionsElement))
                {
                    scoringRule.Questions.Clear();
                    
                    foreach (var questionElem in questionsElement.EnumerateArray())
                    {
                        var questionNumber = questionElem.TryGetProperty("QuestionNumber", out var qNum)
                            ? qNum.GetInt32()
                            : 0;

                        var question = new QuestionScoringRule { QuestionNumber = questionNumber };

                        if (questionElem.TryGetProperty("Scores", out var scoresElement))
                        {
                            question.Scores.Clear();
                            foreach (var scoreElem in scoresElement.EnumerateArray())
                            {
                                question.Scores.Add(scoreElem.GetDouble());
                            }
                            
                            // {OmrConstants.OptionsPerQuestion}개가 안 되면 0으로 채움
                            while (question.Scores.Count < OmrConstants.OptionsPerQuestion)
                            {
                                question.Scores.Add(0);
                            }
                        }

                        scoringRule.Questions.Add(question);
                    }
                }

                return scoringRule;
            }
            catch (Exception ex)
            {
                Logger.Instance.Warning($"정답 및 배점 로드 실패: {ex.Message}");
                return new ScoringRule();
            }
        }

        /// <summary>
        /// XLSX 파일에서 정답 및 배점 정보를 가져옵니다.
        /// 형식: 
        /// - 첫 번째 행: 헤더(문항, 1번, 2번, ..., 12번)
        /// - 두 번째 행: 점수 이름(점수, A, B, C, ..., L)
        /// - 3~6행: 문항1~4의 배점
        /// </summary>
        public ScoringRule LoadScoringRuleFromXlsx(string filePath)
        {
            var scoringRule = new ScoringRule();

            using (var workbook = new XLWorkbook(filePath))
            {
                var worksheet = workbook.Worksheet(1); // 첫 번째 시트
                
                var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
                
                if (lastRow < 3)
                {
                    throw new InvalidOperationException("시트에 데이터가 없습니다. (헤더와 점수 이름 행, 그리고 최소 1개 문항이 필요함)");
                }

                // 기존 데이터 초기화
                scoringRule.Questions.Clear();
                scoringRule.ScoreNames.Clear();

                // 두 번째 행(점수 이름 행) 읽기
                int lastScoreCol = OmrConstants.OptionsPerQuestion + 1; // 2열부터 (OptionsPerQuestion+1)열까지 (1번~{OmrConstants.OptionsPerQuestion}번 선택지)
                for (int col = 2; col <= lastScoreCol; col++)
                {
                    var nameCell = worksheet.Cell(2, col);
                    var name = nameCell.GetString().Trim();
                    scoringRule.ScoreNames.Add(name);
                }

                // 3행부터 시작 (문항1~{OmrConstants.QuestionsCount})
                int lastQuestionRow = OmrConstants.QuestionsCount + 2; // 최대 {OmrConstants.QuestionsCount}개 문항 (헤더 2행 + 문항 수)
                for (int row = 3; row <= Math.Min(lastRow, lastQuestionRow); row++)
                {
                    var questionNumber = row - 2; // 1~{OmrConstants.QuestionsCount} (3행=문항1, 4행=문항2, ...)
                    
                    var question = new QuestionScoringRule { QuestionNumber = questionNumber };
                    question.Scores.Clear(); // 기존 {OmrConstants.OptionsPerQuestion}개 0점 제거
                    
                    // 2열부터 (OptionsPerQuestion+1)열까지 (1번~{OmrConstants.OptionsPerQuestion}번 선택지의 배점)
                    // lastScoreCol은 위에서 이미 선언됨
                    for (int col = 2; col <= lastScoreCol; col++)
                    {
                        var scoreCell = worksheet.Cell(row, col);
                        var score = 0.0;
                        
                        // 숫자로 읽기 시도
                        if (scoreCell.DataType == XLDataType.Number)
                        {
                            score = scoreCell.GetDouble();
                        }
                        else
                        {
                            // 텍스트나 빈 셀인 경우 문자열로 읽어서 파싱 시도
                            var cellValue = scoreCell.GetValue<string>();
                            if (!string.IsNullOrWhiteSpace(cellValue))
                            {
                                if (double.TryParse(cellValue.Trim(), out var parsedScore))
                                {
                                    score = parsedScore;
                                }
                            }
                        }
                        
                        question.Scores.Add(score);
                    }
                    
                    scoringRule.Questions.Add(question);
                }
            }

            return scoringRule;
        }

        /// <summary>
        /// Excel 양식 파일을 내보냅니다.
        /// </summary>
        public void ExportTemplate(string filePath)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("정답 및 배점");

                // 헤더 행 (1행)
                int exportMaxScoreCol = OmrConstants.OptionsPerQuestion + 1;
                worksheet.Cell(1, 1).Value = "문항";
                for (int col = 2; col <= exportMaxScoreCol; col++)
                {
                    worksheet.Cell(1, col).Value = $"{col - 1}번";
                }

                // 점수 이름 행 (2행)
                worksheet.Cell(2, 1).Value = "점수";
                for (int col = 2; col <= exportMaxScoreCol; col++)
                {
                    worksheet.Cell(2, col).Value = ""; // 빈 값으로 시작
                }

                // 문항 행 (3~{OmrConstants.QuestionsCount+2}행)
                int exportMaxQuestionRow = OmrConstants.QuestionsCount + 2;
                for (int row = 3; row <= exportMaxQuestionRow; row++)
                {
                    var questionNumber = row - 2; // 1~{OmrConstants.QuestionsCount}
                    worksheet.Cell(row, 1).Value = $"문항{questionNumber}";
                    
                    // 배점은 0으로 초기화
                    for (int col = 2; col <= exportMaxScoreCol; col++)
                    {
                        worksheet.Cell(row, col).Value = 0;
                    }
                }

                // 스타일 적용
                var headerRange = worksheet.Range(1, 1, 1, exportMaxScoreCol);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                var scoreNameRange = worksheet.Range(2, 1, 2, exportMaxScoreCol);
                scoreNameRange.Style.Font.Bold = true;
                scoreNameRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
                scoreNameRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // 열 너비 조정
                worksheet.Column(1).Width = 10;
                for (int col = 2; col <= exportMaxScoreCol; col++)
                {
                    worksheet.Column(col).Width = 8;
                }

                workbook.SaveAs(filePath);
            }
        }
    }
}
