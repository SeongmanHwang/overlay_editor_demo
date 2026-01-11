using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SimpleOverlayEditor.Models;

namespace SimpleOverlayEditor.Services
{
    public class StateStore
    {
        public void Save(Workspace workspace)
        {
            try
            {
                PathService.EnsureDirectories();

                var state = new
                {
                    InputFolderPath = workspace.InputFolderPath,
                    SelectedDocumentId = workspace.SelectedDocumentId,
                    Template = new
                    {
                        ReferenceWidth = workspace.Template.ReferenceWidth,
                        ReferenceHeight = workspace.Template.ReferenceHeight,
                        TimingMarks = workspace.Template.TimingMarks.Select(ov => new
                        {
                            X = ov.X,
                            Y = ov.Y,
                            Width = ov.Width,
                            Height = ov.Height,
                            StrokeThickness = ov.StrokeThickness,
                            OverlayType = ov.OverlayType.ToString()
                        }).ToList(),
                        ScoringAreas = workspace.Template.ScoringAreas.Select(ov => new
                        {
                            X = ov.X,
                            Y = ov.Y,
                            Width = ov.Width,
                            Height = ov.Height,
                            StrokeThickness = ov.StrokeThickness,
                            OverlayType = ov.OverlayType.ToString()
                        }).ToList(),
                        Questions = workspace.Template.Questions.Select(q => new
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
                        BarcodeAreas = workspace.Template.BarcodeAreas.Select(ov => new
                        {
                            X = ov.X,
                            Y = ov.Y,
                            Width = ov.Width,
                            Height = ov.Height,
                            StrokeThickness = ov.StrokeThickness,
                            OverlayType = ov.OverlayType.ToString()
                        }).ToList()
                    }
                    // Documents, MarkingResults, BarcodeResults는 session.json으로 분리됨
                };

                var json = JsonSerializer.Serialize(state, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
                
                File.WriteAllText(PathService.StateFilePath, json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"상태 저장 실패: {ex.Message}", ex);
            }
        }

        public Workspace Load()
        {
            if (!File.Exists(PathService.StateFilePath))
            {
                return new Workspace { InputFolderPath = PathService.DefaultInputFolder };
            }

            try
            {
                var json = File.ReadAllText(PathService.StateFilePath);
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var workspace = new Workspace
                {
                    InputFolderPath = root.TryGetProperty("InputFolderPath", out var inputPath) 
                        ? inputPath.GetString() ?? PathService.DefaultInputFolder 
                        : PathService.DefaultInputFolder,
                    SelectedDocumentId = root.TryGetProperty("SelectedDocumentId", out var selectedId) 
                        ? selectedId.GetString() 
                        : null
                };

                // 템플릿 로드 (새 형식)
                if (root.TryGetProperty("Template", out var templateElement))
                {
                    var template = workspace.Template;
                    
                    if (templateElement.TryGetProperty("ReferenceWidth", out var refWidth))
                    {
                        template.ReferenceWidth = refWidth.GetInt32();
                    }
                    
                    if (templateElement.TryGetProperty("ReferenceHeight", out var refHeight))
                    {
                        template.ReferenceHeight = refHeight.GetInt32();
                    }

                    // 타이밍 마크 로드
                    if (templateElement.TryGetProperty("TimingMarks", out var timingMarksElement))
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
                    if (templateElement.TryGetProperty("Questions", out var questionsElement))
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
                                // Questions.Add()를 호출하면 OmrTemplate의 CollectionChanged 이벤트 핸들러가 자동으로 이벤트 구독
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
                    else if (templateElement.TryGetProperty("ScoringAreas", out var scoringAreasElement))
                    {
                        var scoringAreasList = new List<RectangleOverlay>();
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
                    if (templateElement.TryGetProperty("BarcodeAreas", out var barcodeAreasElement))
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
                }

                // Documents, MarkingResults, BarcodeResults는 session.json에서 로드됨
                // 기존 state.json에서 마이그레이션을 위해 호환성 코드는 유지하되, 
                // 실제 로드는 SessionStore에서 처리

                return workspace;
            }
            catch (Exception ex)
            {
                // 로드 실패 시 빈 워크스페이스 반환
                System.Diagnostics.Debug.WriteLine($"상태 로드 실패: {ex.Message}");
                return new Workspace { InputFolderPath = PathService.DefaultInputFolder };
            }
        }

        /// <summary>
        /// 템플릿을 JSON 파일로 내보냅니다.
        /// </summary>
        public void ExportTemplate(OmrTemplate template, string filePath)
        {
            try
            {
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
                
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"템플릿 내보내기 실패: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// JSON 파일에서 템플릿을 가져옵니다. 파일이 없거나 형식이 맞지 않으면 null을 반환합니다.
        /// </summary>
        public OmrTemplate? ImportTemplate(string filePath)
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
                    var scoringAreasList = new List<RectangleOverlay>();
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
                System.Diagnostics.Debug.WriteLine($"템플릿 가져오기 실패: {ex.Message}");
                return null;
            }
        }
    }
}



