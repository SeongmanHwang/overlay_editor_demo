using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using SimpleOverlayEditor.Models;

namespace SimpleOverlayEditor.Services
{
    /// <summary>
    /// 템플릿을 저장/로드하는 서비스입니다.
    /// template.json 파일에 템플릿을 저장합니다.
    /// </summary>
    public class TemplateStore
    {
        private const string BundledDefaultTemplateRelativePath = "Assets/default_template.json";

        private static string? TryGetBundledDefaultTemplatePath()
        {
            // 실행 폴더 기준(배포 시 CopyToOutputDirectory로 따라오는 위치)
            var baseDir = AppContext.BaseDirectory;
            var candidate1 = Path.Combine(baseDir, BundledDefaultTemplateRelativePath);
            if (File.Exists(candidate1)) return candidate1;

            // 개발/디버그 시 WorkingDirectory 기준
            var candidate2 = Path.Combine(Directory.GetCurrentDirectory(), BundledDefaultTemplateRelativePath);
            if (File.Exists(candidate2)) return candidate2;

            return null;
        }

        /// <summary>
        /// 지정한 파일 경로에 템플릿을 JSON으로 저장합니다.
        /// </summary>
        public void Export(OmrTemplate template, string filePath)
        {
            try
            {
                PathService.EnsureDirectories();

                var templateData = new
                {
                    ReferenceWidth = template.ReferenceWidth,
                    ReferenceHeight = template.ReferenceHeight,
                    TimingMarks = template.TimingMarks.Select(ov => new
                    {
                        OptionNumber = ov.OptionNumber,
                        X = ov.X,
                        Y = ov.Y,
                        Width = ov.Width,
                        Height = ov.Height,
                        StrokeThickness = ov.StrokeThickness,
                        OverlayType = ov.OverlayType.ToString()
                    }).ToList(),
                    ScoringAreas = template.ScoringAreas.Select(ov => new
                    {
                        X = ov.X,
                        Y = ov.Y,
                        Width = ov.Width,
                        Height = ov.Height,
                        StrokeThickness = ov.StrokeThickness,
                        OverlayType = ov.OverlayType.ToString()
                    }).ToList(),
                    Questions = template.Questions.Select(q => new
                    {
                        QuestionNumber = q.QuestionNumber,
                        Options = q.Options.Select(ov => new
                        {
                            OptionNumber = ov.OptionNumber,
                            QuestionNumber = ov.QuestionNumber,
                            X = ov.X,
                            Y = ov.Y,
                            Width = ov.Width,
                            Height = ov.Height,
                            StrokeThickness = ov.StrokeThickness,
                            OverlayType = ov.OverlayType.ToString()
                        }).ToList()
                    }).ToList(),
                    BarcodeAreas = template.BarcodeAreas.Select(ov => new
                    {
                        OptionNumber = ov.OptionNumber,
                        X = ov.X,
                        Y = ov.Y,
                        Width = ov.Width,
                        Height = ov.Height,
                        StrokeThickness = ov.StrokeThickness,
                        OverlayType = ov.OverlayType.ToString()
                    }).ToList()
                };

                var json = JsonSerializer.Serialize(templateData, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });

                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"템플릿 내보내기 실패: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 지정한 파일 경로에서 템플릿을 JSON으로 로드합니다. 파일이 없거나 형식이 맞지 않으면 null을 반환합니다.
        /// </summary>
        public OmrTemplate? Import(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            try
            {
                var json = File.ReadAllText(filePath);
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var template = new OmrTemplate();

                if (root.TryGetProperty("ReferenceWidth", out var refWidth))
                {
                    template.ReferenceWidth = refWidth.GetInt32();
                }

                if (root.TryGetProperty("ReferenceHeight", out var refHeight))
                {
                    template.ReferenceHeight = refHeight.GetInt32();
                }

                // 타이밍 마크 로드
                if (root.TryGetProperty("TimingMarks", out var timingMarksElement))
                {
                    int i = 0;
                    foreach (var ovElem in timingMarksElement.EnumerateArray())
                    {
                        i++;
                        var optionNumber = ovElem.TryGetProperty("OptionNumber", out var optNum)
                            ? optNum.GetInt32()
                            : i;

                        var slot = template.TimingMarks.FirstOrDefault(t => t.OptionNumber == optionNumber);
                        if (slot == null)
                        {
                            // 고정 슬롯 정책: 범위를 벗어나면 무시
                            continue;
                        }

                        slot.OverlayType = OverlayType.TimingMark;
                        slot.X = ovElem.GetProperty("X").GetDouble();
                        slot.Y = ovElem.GetProperty("Y").GetDouble();
                        slot.Width = ovElem.GetProperty("Width").GetDouble();
                        slot.Height = ovElem.GetProperty("Height").GetDouble();
                        slot.StrokeThickness = ovElem.TryGetProperty("StrokeThickness", out var thickness)
                            ? thickness.GetDouble()
                            : 2.0;
                    }
                }

                // Questions 로드 (새 형식)
                if (root.TryGetProperty("Questions", out var questionsElement))
                {
                    foreach (var qElem in questionsElement.EnumerateArray())
                    {
                        var questionNumber = qElem.TryGetProperty("QuestionNumber", out var qNum)
                            ? qNum.GetInt32()
                            : 1;

                        var question = template.Questions.FirstOrDefault(q => q.QuestionNumber == questionNumber);
                        if (question == null)
                        {
                            question = new Question { QuestionNumber = questionNumber };
                            template.Questions.Add(question);
                        }

                        if (qElem.TryGetProperty("Options", out var optionsElement))
                        {
                            foreach (var ovElem in optionsElement.EnumerateArray())
                            {
                                var overlay = new RectangleOverlay
                                {
                                    OptionNumber = ovElem.TryGetProperty("OptionNumber", out var optNum)
                                        ? optNum.GetInt32()
                                        : null,
                                    QuestionNumber = questionNumber,
                                    X = ovElem.GetProperty("X").GetDouble(),
                                    Y = ovElem.GetProperty("Y").GetDouble(),
                                    Width = ovElem.GetProperty("Width").GetDouble(),
                                    Height = ovElem.GetProperty("Height").GetDouble(),
                                    StrokeThickness = ovElem.TryGetProperty("StrokeThickness", out var thickness)
                                        ? thickness.GetDouble()
                                        : 2.0
                                };

                                if (ovElem.TryGetProperty("OverlayType", out var overlayType))
                                {
                                    if (Enum.TryParse<OverlayType>(overlayType.GetString(), out var type))
                                    {
                                        overlay.OverlayType = type;
                                    }
                                }

                                // OptionNumber가 있으면 해당 슬롯에 배치, 없으면 빈 슬롯에 순차 배치
                                if (overlay.OptionNumber.HasValue)
                                {
                                    var slot = question.Options.FirstOrDefault(o => o.OptionNumber == overlay.OptionNumber.Value);
                                    if (slot != null)
                                    {
                                        slot.X = overlay.X;
                                        slot.Y = overlay.Y;
                                        slot.Width = overlay.Width;
                                        slot.Height = overlay.Height;
                                        slot.StrokeThickness = overlay.StrokeThickness;
                                        slot.OverlayType = OverlayType.ScoringArea;
                                    }
                                    // 고정 슬롯 정책: 슬롯이 없으면 무시
                                }
                                else
                                {
                                    var emptySlot = question.Options.FirstOrDefault(o => !o.IsPlaced);
                                    if (emptySlot != null)
                                    {
                                        emptySlot.X = overlay.X;
                                        emptySlot.Y = overlay.Y;
                                        emptySlot.Width = overlay.Width;
                                        emptySlot.Height = overlay.Height;
                                        emptySlot.StrokeThickness = overlay.StrokeThickness;
                                        emptySlot.OverlayType = OverlayType.ScoringArea;
                                    }
                                    // 고정 슬롯 정책: 빈 슬롯이 없으면 무시
                                }
                            }
                        }
                    }
                }
                // 하위 호환성: ScoringAreas만 있는 경우 Questions로 변환
                else if (root.TryGetProperty("ScoringAreas", out var scoringAreasElement))
                {
                    var scoringAreasList = new System.Collections.Generic.List<RectangleOverlay>();
                    foreach (var ovElem in scoringAreasElement.EnumerateArray())
                    {
                        var overlay = new RectangleOverlay
                        {
                            X = ovElem.GetProperty("X").GetDouble(),
                            Y = ovElem.GetProperty("Y").GetDouble(),
                            Width = ovElem.GetProperty("Width").GetDouble(),
                            Height = ovElem.GetProperty("Height").GetDouble(),
                            StrokeThickness = ovElem.TryGetProperty("StrokeThickness", out var thickness)
                                ? thickness.GetDouble()
                                : 2.0,
                            OverlayType = OverlayType.ScoringArea
                        };

                        scoringAreasList.Add(overlay);
                    }

                    int questionsCount = OmrConstants.QuestionsCount;
                    int optionsPerQuestion = OmrConstants.OptionsPerQuestion;
                    for (int q = 0; q < questionsCount; q++)
                    {
                        var question = template.Questions.FirstOrDefault(qu => qu.QuestionNumber == q + 1);
                        if (question == null)
                        {
                            question = new Question { QuestionNumber = q + 1 };
                            template.Questions.Add(question);
                        }

                        int startIndex = q * optionsPerQuestion;
                        for (int o = 0; o < optionsPerQuestion && startIndex + o < scoringAreasList.Count; o++)
                        {
                            var overlay = scoringAreasList[startIndex + o];
                            var slot = question.Options.FirstOrDefault(s => s.OptionNumber == o + 1);
                            if (slot != null)
                            {
                                slot.X = overlay.X;
                                slot.Y = overlay.Y;
                                slot.Width = overlay.Width;
                                slot.Height = overlay.Height;
                                slot.StrokeThickness = overlay.StrokeThickness;
                                slot.OverlayType = OverlayType.ScoringArea;
                            }
                            else
                            {
                                overlay.OptionNumber = o + 1;
                                overlay.QuestionNumber = q + 1;
                                // 고정 슬롯 정책: 슬롯이 없으면 무시
                            }
                        }
                    }
                }

                // 바코드 영역 로드
                if (root.TryGetProperty("BarcodeAreas", out var barcodeAreasElement))
                {
                    int i = 0;
                    foreach (var ovElem in barcodeAreasElement.EnumerateArray())
                    {
                        i++;
                        var optionNumber = ovElem.TryGetProperty("OptionNumber", out var optNum)
                            ? optNum.GetInt32()
                            : i;

                        var slot = template.BarcodeAreas.FirstOrDefault(t => t.OptionNumber == optionNumber);
                        if (slot == null)
                        {
                            // 고정 슬롯 정책: 범위를 벗어나면 무시
                            continue;
                        }

                        slot.OverlayType = OverlayType.BarcodeArea;
                        slot.X = ovElem.GetProperty("X").GetDouble();
                        slot.Y = ovElem.GetProperty("Y").GetDouble();
                        slot.Width = ovElem.GetProperty("Width").GetDouble();
                        slot.Height = ovElem.GetProperty("Height").GetDouble();
                        slot.StrokeThickness = ovElem.TryGetProperty("StrokeThickness", out var thickness)
                            ? thickness.GetDouble()
                            : 2.0;
                    }
                }

                return template;
            }
            catch (Exception ex)
            {
                Logger.Instance.Warning($"템플릿 가져오기 실패: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 템플릿을 저장합니다.
        /// </summary>
        public void Save(OmrTemplate template)
        {
            try
            {
                PathService.EnsureDirectories();

                var templateData = new
                {
                    ReferenceWidth = template.ReferenceWidth,
                    ReferenceHeight = template.ReferenceHeight,
                    TimingMarks = template.TimingMarks.Select(ov => new
                    {
                        OptionNumber = ov.OptionNumber,
                        X = ov.X,
                        Y = ov.Y,
                        Width = ov.Width,
                        Height = ov.Height,
                        StrokeThickness = ov.StrokeThickness,
                        OverlayType = ov.OverlayType.ToString()
                    }).ToList(),
                    ScoringAreas = template.ScoringAreas.Select(ov => new
                    {
                        X = ov.X,
                        Y = ov.Y,
                        Width = ov.Width,
                        Height = ov.Height,
                        StrokeThickness = ov.StrokeThickness,
                        OverlayType = ov.OverlayType.ToString()
                    }).ToList(),
                    Questions = template.Questions.Select(q => new
                    {
                        QuestionNumber = q.QuestionNumber,
                        Options = q.Options.Select(ov => new
                        {
                            OptionNumber = ov.OptionNumber,  // IdentityIndex 저장
                            QuestionNumber = ov.QuestionNumber,  // 문항 번호 저장
                            X = ov.X,
                            Y = ov.Y,
                            Width = ov.Width,
                            Height = ov.Height,
                            StrokeThickness = ov.StrokeThickness,
                            OverlayType = ov.OverlayType.ToString()
                        }).ToList()
                    }).ToList(),
                    BarcodeAreas = template.BarcodeAreas.Select(ov => new
                    {
                        OptionNumber = ov.OptionNumber,
                        X = ov.X,
                        Y = ov.Y,
                        Width = ov.Width,
                        Height = ov.Height,
                        StrokeThickness = ov.StrokeThickness,
                        OverlayType = ov.OverlayType.ToString()
                    }).ToList()
                };

                var json = JsonSerializer.Serialize(templateData, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
                
                File.WriteAllText(PathService.TemplateFilePath, json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"템플릿 저장 실패: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 템플릿을 로드합니다. 파일이 없으면 null을 반환합니다.
        /// </summary>
        public OmrTemplate? Load()
        {
            // 1) AppData에 템플릿이 있으면 로드
            if (File.Exists(PathService.TemplateFilePath))
            {
                return Import(PathService.TemplateFilePath);
            }

            // 2) 없으면 번들된 기본 템플릿을 찾아 설치/로드
            var bundledPath = TryGetBundledDefaultTemplatePath();
            if (bundledPath != null)
            {
                var template = Import(bundledPath);
                if (template != null)
                {
                    try
                    {
                        Save(template);
                    }
                    catch (Exception ex)
                    {
                        // 저장 실패해도 템플릿은 반환 (첫 실행 UX 우선)
                        Logger.Instance.Warning($"기본 템플릿 저장 실패(계속 진행): {ex.Message}");
                    }
                    return template;
                }
            }

            return null;
        }
    }
}
