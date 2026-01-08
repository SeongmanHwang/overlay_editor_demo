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
                        }).ToList()
                    },
                    Documents = workspace.Documents.Select(doc => new
                    {
                        ImageId = doc.ImageId,
                        SourcePath = doc.SourcePath,
                        ImageWidth = doc.ImageWidth,
                        ImageHeight = doc.ImageHeight,
                        LastEditedAt = doc.LastEditedAt.ToString("O")
                    }).ToList()
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
                }

                // Documents 로드
                if (root.TryGetProperty("Documents", out var documentsElement))
                {
                    foreach (var docElem in documentsElement.EnumerateArray())
                    {
                        var imageDoc = new ImageDocument
                        {
                            ImageId = docElem.GetProperty("ImageId").GetString() ?? Guid.NewGuid().ToString(),
                            SourcePath = docElem.GetProperty("SourcePath").GetString() ?? string.Empty,
                            ImageWidth = docElem.GetProperty("ImageWidth").GetInt32(),
                            ImageHeight = docElem.GetProperty("ImageHeight").GetInt32()
                        };

                        if (docElem.TryGetProperty("LastEditedAt", out var lastEdited))
                        {
                            if (DateTime.TryParse(lastEdited.GetString(), out var dateTime))
                            {
                                imageDoc.LastEditedAt = dateTime;
                            }
                        }

                        // 기존 형식 호환성: Documents에 Overlays가 있으면 템플릿으로 마이그레이션
                        // (첫 번째 이미지의 오버레이만 템플릿으로 사용)
                        if (docElem.TryGetProperty("Overlays", out var overlaysElement) && 
                            workspace.Documents.Count == 0 && 
                            workspace.Template.TimingMarks.Count == 0 && 
                            workspace.Template.ScoringAreas.Count == 0)
                        {
                            // 첫 번째 이미지의 오버레이를 템플릿으로 마이그레이션
                            var firstDocWidth = imageDoc.ImageWidth;
                            var firstDocHeight = imageDoc.ImageHeight;
                            
                            workspace.Template.ReferenceWidth = firstDocWidth;
                            workspace.Template.ReferenceHeight = firstDocHeight;
                            
                            foreach (var ovElem in overlaysElement.EnumerateArray())
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
                                
                                // OverlayType이 있으면 사용, 없으면 기본값 (ScoringArea)
                                if (ovElem.TryGetProperty("OverlayType", out var overlayType))
                                {
                                    if (Enum.TryParse<OverlayType>(overlayType.GetString(), out var type))
                                    {
                                        overlay.OverlayType = type;
                                    }
                                }
                                
                                // 타입에 따라 적절한 컬렉션에 추가
                                // (기존 데이터는 모두 ScoringArea로 가정)
                                workspace.Template.ScoringAreas.Add(overlay);
                            }
                        }

                        workspace.Documents.Add(imageDoc);
                    }
                }

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



