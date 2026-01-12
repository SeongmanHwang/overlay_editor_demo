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
            if (!File.Exists(PathService.TemplateFilePath))
            {
                return null;
            }

            try
            {
                var json = File.ReadAllText(PathService.TemplateFilePath);
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
                    foreach (var ovElem in timingMarksElement.EnumerateArray())
                    {
                        var overlay = new RectangleOverlay
                        {
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
                        
                        template.TimingMarks.Add(overlay);
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

                                question.Options.Add(overlay);
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
                                : 2.0
                        };

                        if (ovElem.TryGetProperty("OverlayType", out var overlayType))
                        {
                            if (Enum.TryParse<OverlayType>(overlayType.GetString(), out var type))
                            {
                                overlay.OverlayType = type;
                            }
                        }

                        scoringAreasList.Add(overlay);
                    }

                    // 48개를 4문항 × 12선택지로 분할
                    const int questionsCount = 4;
                    const int optionsPerQuestion = 12;
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
                            question.Options.Add(scoringAreasList[startIndex + o]);
                        }
                    }
                }

                // 바코드 영역 로드
                if (root.TryGetProperty("BarcodeAreas", out var barcodeAreasElement))
                {
                    foreach (var ovElem in barcodeAreasElement.EnumerateArray())
                    {
                        var overlay = new RectangleOverlay
                        {
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
                        
                        template.BarcodeAreas.Add(overlay);
                    }
                }

                return template;
            }
            catch (Exception ex)
            {
                Logger.Instance.Warning($"템플릿 로드 실패: {ex.Message}");
                return null;
            }
        }
    }
}
