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
                    Documents = workspace.Documents.Select(doc => new
                    {
                        ImageId = doc.ImageId,
                        SourcePath = doc.SourcePath,
                        ImageWidth = doc.ImageWidth,
                        ImageHeight = doc.ImageHeight,
                        LastEditedAt = doc.LastEditedAt.ToString("O"),
                        Overlays = doc.Overlays.Select(ov => new
                        {
                            X = ov.X,
                            Y = ov.Y,
                            Width = ov.Width,
                            Height = ov.Height,
                            StrokeThickness = ov.StrokeThickness
                        }).ToList()
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

                        if (docElem.TryGetProperty("Overlays", out var overlaysElement))
                        {
                            foreach (var ovElem in overlaysElement.EnumerateArray())
                            {
                                imageDoc.Overlays.Add(new RectangleOverlay
                                {
                                    X = ovElem.GetProperty("X").GetDouble(),
                                    Y = ovElem.GetProperty("Y").GetDouble(),
                                    Width = ovElem.GetProperty("Width").GetDouble(),
                                    Height = ovElem.GetProperty("Height").GetDouble(),
                                    StrokeThickness = ovElem.TryGetProperty("StrokeThickness", out var thickness)
                                        ? thickness.GetDouble()
                                        : 2.0
                                });
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
    }
}



