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
        /// <summary>
        /// 작업 상황만 저장합니다 (InputFolderPath, SelectedDocumentId).
        /// 템플릿은 저장하지 않습니다.
        /// </summary>
        public void SaveWorkspaceState(Workspace workspace)
        {
            try
            {
                PathService.EnsureDirectories();

                var state = new
                {
                    InputFolderPath = workspace.InputFolderPath,
                    SelectedDocumentId = workspace.SelectedDocumentId
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

        /// <summary>
        /// 전체 Workspace를 저장합니다 (하위 호환성용).
        /// 템플릿은 TemplateStore를 사용하도록 권장합니다.
        /// </summary>
        [Obsolete("템플릿은 TemplateStore를 사용하세요. 작업 상황만 저장하려면 SaveWorkspaceState를 사용하세요.")]
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
            try
            {
                // state.json이 없어도 템플릿은 로드되어야 합니다.
                // (첫 실행/새 회차에서 state.json이 아직 없을 수 있음)
                var workspace = new Workspace { InputFolderPath = PathService.DefaultInputFolder };

                JsonElement? rootOpt = null;
                if (File.Exists(PathService.StateFilePath))
                {
                    var json = File.ReadAllText(PathService.StateFilePath);
                    var doc = JsonDocument.Parse(json);
                    rootOpt = doc.RootElement;
                }

                if (rootOpt.HasValue)
                {
                    var root = rootOpt.Value;

                    workspace.InputFolderPath = root.TryGetProperty("InputFolderPath", out var inputPath)
                        ? inputPath.GetString() ?? PathService.DefaultInputFolder
                        : PathService.DefaultInputFolder;

                    workspace.SelectedDocumentId = root.TryGetProperty("SelectedDocumentId", out var selectedId)
                        ? selectedId.GetString()
                        : null;

                    // 하위 호환성: state.json에 Template이 있으면 template.json으로 마이그레이션
                    if (root.TryGetProperty("Template", out var templateElement))
                    {
                        Logger.Instance.Info("state.json에서 템플릿 발견. template.json으로 마이그레이션합니다.");
                        try
                        {
                            // 고정 슬롯 정책과 일관되게: state.json의 Template 오브젝트를 그대로 template.json으로 저장하고,
                            // 실제 슬롯 매핑/호환 처리는 TemplateStore.Import/Load에서 수행합니다.
                            PathService.EnsureDirectories();
                            File.WriteAllText(PathService.TemplateFilePath, templateElement.GetRawText());
                            Logger.Instance.Info("템플릿 마이그레이션 완료: template.json에 저장됨");
                        }
                        catch (Exception ex)
                        {
                            Logger.Instance.Error("템플릿 마이그레이션 실패", ex);
                        }
                    }
                }

                // template.json(또는 번들 기본값)에서 템플릿 로드: state.json 유무와 무관하게 항상 수행
                var templateStore = new TemplateStore();
                var loadedTemplate = templateStore.Load();
                if (loadedTemplate != null)
                {
                    workspace.Template = loadedTemplate;
                }
                else
                {
                    // 템플릿이 null인 경우 (파일이 없고 기본 템플릿도 로드 실패)
                    Logger.Instance.Warning("템플릿 로드 실패, 빈 템플릿 사용");
                    workspace.Template = new OmrTemplate();
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
            // 레거시 API 유지: 실제 구현은 TemplateStore로 위임
            var templateStore = new TemplateStore();
            templateStore.Export(template, filePath);
        }

        /// <summary>
        /// JSON 파일에서 템플릿을 가져옵니다. 파일이 없거나 형식이 맞지 않으면 null을 반환합니다.
        /// </summary>
        public OmrTemplate? ImportTemplate(string filePath)
        {
            // 레거시 API 유지: 실제 구현은 TemplateStore로 위임
            var templateStore = new TemplateStore();
            return templateStore.Import(filePath);
        }
    }
}



