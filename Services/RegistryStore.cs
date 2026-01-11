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
        public static string StudentRegistryFilePath =>
            Path.Combine(PathService.AppDataFolder, "student_registry.json");

        public static string InterviewerRegistryFilePath =>
            Path.Combine(PathService.AppDataFolder, "interviewer_registry.json");

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
                        Time = s.Time,
                        Group = s.Group,
                        InterviewRoom = s.InterviewRoom,
                        Number = s.Number,
                        RegistrationNumber = s.RegistrationNumber,
                        MiddleSchool = s.MiddleSchool,
                        Name = s.Name
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
                            Time = studentElem.TryGetProperty("Time", out var time)
                                ? time.GetString()
                                : null,
                            Group = studentElem.TryGetProperty("Group", out var group)
                                ? group.GetString()
                                : null,
                            InterviewRoom = studentElem.TryGetProperty("InterviewRoom", out var room)
                                ? room.GetString()
                                : null,
                            Number = studentElem.TryGetProperty("Number", out var num)
                                ? num.GetString()
                                : null,
                            RegistrationNumber = studentElem.TryGetProperty("RegistrationNumber", out var regNum)
                                ? regNum.GetString()
                                : null,
                            MiddleSchool = studentElem.TryGetProperty("MiddleSchool", out var school)
                                ? school.GetString()
                                : null,
                            Name = studentElem.TryGetProperty("Name", out var name)
                                ? name.GetString()
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
                        Time = worksheet.Cell(row, 2).GetString().Trim(),
                        Group = worksheet.Cell(row, 3).GetString().Trim(),
                        InterviewRoom = worksheet.Cell(row, 4).GetString().Trim(),
                        Number = worksheet.Cell(row, 5).GetString().Trim(),
                        RegistrationNumber = worksheet.Cell(row, 6).GetString().Trim(),
                        MiddleSchool = worksheet.Cell(row, 7).GetString().Trim(),
                        Name = worksheet.Cell(row, 8).GetString().Trim()
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
    }
}
