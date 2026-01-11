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

                    // 채점 영역 로드
                    if (templateElement.TryGetProperty("ScoringAreas", out var scoringAreasElement))
                    {
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
                            
                            template.ScoringAreas.Add(overlay);
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
        /// 현재 템플릿을 기본 템플릿으로 저장합니다.
        /// </summary>
        public void SaveDefaultTemplate(OmrTemplate template)
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
                
                File.WriteAllText(PathService.DefaultTemplateFilePath, json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"기본 템플릿 저장 실패: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 기본 템플릿을 로드합니다. 파일이 없으면 null을 반환합니다.
        /// </summary>
        public OmrTemplate? LoadDefaultTemplate()
        {
            if (!File.Exists(PathService.DefaultTemplateFilePath))
            {
                return null;
            }

            try
            {
                var json = File.ReadAllText(PathService.DefaultTemplateFilePath);
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

                // 채점 영역 로드
                if (root.TryGetProperty("ScoringAreas", out var scoringAreasElement))
                {
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
                        
                        template.ScoringAreas.Add(overlay);
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
                System.Diagnostics.Debug.WriteLine($"기본 템플릿 로드 실패: {ex.Message}");
                return null;
            }
        }
    }
}



