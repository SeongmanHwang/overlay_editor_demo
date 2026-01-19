using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ClosedXML.Excel;
using SimpleOverlayEditor.Models;

namespace SimpleOverlayEditor.Services
{
    /// <summary>
    /// 수험생/면접위원 명부를 저장/로드하는 서비스입니다.
    /// </summary>
    public class RegistryStore
    {
        public static string StudentRegistryFilePath
        {
            get
            {
                if (PathService.CurrentRound != null)
                {
                    return Path.Combine(PathService.GetRoundRoot(PathService.CurrentRound), "student_registry.json");
                }
                return Path.Combine(PathService.AppDataFolder, "student_registry.json");
            }
        }

        public static string InterviewerRegistryFilePath
        {
            get
            {
                if (PathService.CurrentRound != null)
                {
                    return Path.Combine(PathService.GetRoundRoot(PathService.CurrentRound), "interviewer_registry.json");
                }
                return Path.Combine(PathService.AppDataFolder, "interviewer_registry.json");
            }
        }

        /// <summary>
        /// 수험생 명부를 저장합니다.
        /// </summary>
        public void SaveStudentRegistry(StudentRegistry registry)
        {
            try
            {
                PathService.EnsureDirectories();

                var data = new
                {
                    Students = registry.Students.Select(s => new
                    {
                        StudentId = s.StudentId,
                        RegistrationNumber = s.RegistrationNumber,
                        ExamType = s.ExamType,
                        Name = s.Name,
                        BirthDate = s.BirthDate,
                        MiddleSchool = s.MiddleSchool
                    }).ToList()
                };

                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });

                File.WriteAllText(StudentRegistryFilePath, json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"수험생 명부 저장 실패: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 수험생 명부를 로드합니다.
        /// </summary>
        public StudentRegistry LoadStudentRegistry()
        {
            if (!File.Exists(StudentRegistryFilePath))
            {
                return new StudentRegistry();
            }

            try
            {
                var json = File.ReadAllText(StudentRegistryFilePath);
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var registry = new StudentRegistry();

                if (root.TryGetProperty("Students", out var studentsElement))
                {
                    foreach (var studentElem in studentsElement.EnumerateArray())
                    {
                        var student = new StudentInfo
                        {
                            StudentId = studentElem.TryGetProperty("StudentId", out var id)
                                ? id.GetString() ?? string.Empty
                                : string.Empty,
                            RegistrationNumber = studentElem.TryGetProperty("RegistrationNumber", out var regNum)
                                ? regNum.GetString()
                                : null,
                            ExamType = studentElem.TryGetProperty("ExamType", out var examType)
                                ? examType.GetString()
                                : null,
                            Name = studentElem.TryGetProperty("Name", out var name)
                                ? name.GetString()
                                : null,
                            BirthDate = studentElem.TryGetProperty("BirthDate", out var birthDate)
                                ? birthDate.GetString()
                                : null,
                            MiddleSchool = studentElem.TryGetProperty("MiddleSchool", out var school)
                                ? school.GetString()
                                : null
                        };

                        registry.Students.Add(student);
                    }
                }

                return registry;
            }
            catch (Exception ex)
            {
                Logger.Instance.Warning($"수험생 명부 로드 실패: {ex.Message}");
                return new StudentRegistry();
            }
        }

        /// <summary>
        /// 면접위원 명부를 저장합니다.
        /// </summary>
        public void SaveInterviewerRegistry(InterviewerRegistry registry)
        {
            try
            {
                PathService.EnsureDirectories();

                var data = new
                {
                    Interviewers = registry.Interviewers.Select(i => new
                    {
                        InterviewerId = i.InterviewerId,
                        Name = i.Name
                    }).ToList()
                };

                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });

                File.WriteAllText(InterviewerRegistryFilePath, json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"면접위원 명부 저장 실패: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 면접위원 명부를 로드합니다.
        /// </summary>
        public InterviewerRegistry LoadInterviewerRegistry()
        {
            if (!File.Exists(InterviewerRegistryFilePath))
            {
                return new InterviewerRegistry();
            }

            try
            {
                var json = File.ReadAllText(InterviewerRegistryFilePath);
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var registry = new InterviewerRegistry();

                if (root.TryGetProperty("Interviewers", out var interviewersElement))
                {
                    foreach (var interviewerElem in interviewersElement.EnumerateArray())
                    {
                        var interviewer = new InterviewerInfo
                        {
                            InterviewerId = interviewerElem.TryGetProperty("InterviewerId", out var id)
                                ? id.GetString() ?? string.Empty
                                : string.Empty,
                            Name = interviewerElem.TryGetProperty("Name", out var name)
                                ? name.GetString()
                                : null
                        };

                        registry.Interviewers.Add(interviewer);
                    }
                }

                return registry;
            }
            catch (Exception ex)
            {
                Logger.Instance.Warning($"면접위원 명부 로드 실패: {ex.Message}");
                return new InterviewerRegistry();
            }
        }

        /// <summary>
        /// XLSX 파일에서 수험생 명부를 가져옵니다.
        /// </summary>
        public StudentRegistry LoadStudentRegistryFromXlsx(string filePath)
        {
            var registry = new StudentRegistry();

            using (var workbook = new XLWorkbook(filePath))
            {
                var worksheet = workbook.Worksheet(1); // 첫 번째 시트
                
                var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
                
                if (lastRow < 2)
                {
                    throw new InvalidOperationException("시트에 데이터가 없습니다. (헤더만 있거나 비어있음)");
                }

                var startRow = 2; // 헤더 다음 행부터 (1행은 헤더)

                for (int row = startRow; row <= lastRow; row++)
                {
                    var studentId = worksheet.Cell(row, 1).GetString().Trim();
                    
                    if (string.IsNullOrEmpty(studentId))
                        continue; // 빈 행 건너뛰기

                    var student = new StudentInfo
                    {
                        StudentId = studentId,
                        RegistrationNumber = worksheet.Cell(row, 2).GetString().Trim(),
                        ExamType = worksheet.Cell(row, 3).GetString().Trim(),
                        Name = worksheet.Cell(row, 4).GetString().Trim(),
                        BirthDate = worksheet.Cell(row, 5).GetString().Trim(),
                        MiddleSchool = worksheet.Cell(row, 6).GetString().Trim()
                    };

                    registry.Students.Add(student);
                }
            }

            return registry;
        }

        /// <summary>
        /// XLSX 파일에서 면접위원 명부를 가져옵니다.
        /// </summary>
        public InterviewerRegistry LoadInterviewerRegistryFromXlsx(string filePath)
        {
            var registry = new InterviewerRegistry();

            using (var workbook = new XLWorkbook(filePath))
            {
                var worksheet = workbook.Worksheet(1); // 첫 번째 시트
                
                var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
                
                if (lastRow < 2)
                {
                    throw new InvalidOperationException("시트에 데이터가 없습니다. (헤더만 있거나 비어있음)");
                }

                var startRow = 2; // 헤더 다음 행부터

                for (int row = startRow; row <= lastRow; row++)
                {
                    var interviewerId = worksheet.Cell(row, 1).GetString().Trim();
                    
                    if (string.IsNullOrEmpty(interviewerId))
                        continue; // 빈 행 건너뛰기

                    var interviewer = new InterviewerInfo
                    {
                        InterviewerId = interviewerId,
                        Name = worksheet.Cell(row, 2).GetString().Trim()
                    };

                    registry.Interviewers.Add(interviewer);
                }
            }

            return registry;
        }

        /// <summary>
        /// 수험생 명부 양식 파일을 내보냅니다.
        /// </summary>
        public void ExportStudentRegistryTemplate(string filePath)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("수험생 명부");

                // 헤더 행 (1행)
                worksheet.Cell(1, 1).Value = "수험번호";
                worksheet.Cell(1, 2).Value = "접수번호";
                worksheet.Cell(1, 3).Value = "전형명";
                worksheet.Cell(1, 4).Value = "성명";
                worksheet.Cell(1, 5).Value = "생년월일";
                worksheet.Cell(1, 6).Value = "출신교명";

                // 스타일 적용
                var headerRange = worksheet.Range(1, 1, 1, 6);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // 열 너비 조정
                worksheet.Column(1).Width = 12;
                worksheet.Column(2).Width = 12;
                worksheet.Column(3).Width = 12;
                worksheet.Column(4).Width = 12;
                worksheet.Column(5).Width = 12;
                worksheet.Column(6).Width = 15;

                workbook.SaveAs(filePath);
            }
        }

        /// <summary>
        /// 면접위원 명부 양식 파일을 내보냅니다.
        /// </summary>
        public void ExportInterviewerRegistryTemplate(string filePath)
        {
            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("면접위원 명부");

                // 헤더 행 (1행)
                worksheet.Cell(1, 1).Value = "면접위원번호";
                worksheet.Cell(1, 2).Value = "성명";

                // 스타일 적용
                var headerRange = worksheet.Range(1, 1, 1, 2);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // 열 너비 조정
                worksheet.Column(1).Width = 15;
                worksheet.Column(2).Width = 15;

                workbook.SaveAs(filePath);
            }
        }
    }
}
