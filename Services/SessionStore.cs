using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SimpleOverlayEditor.Models;

namespace SimpleOverlayEditor.Services
{
    /// <summary>
    /// 이미지 로드 및 리딩 작업 세션을 저장/로드하는 서비스입니다.
    /// session.json 파일에 Documents, MarkingResults, BarcodeResults를 저장합니다.
    /// </summary>
    public class SessionStore
    {
        /// <summary>
        /// 세션을 저장합니다.
        /// </summary>
        public void Save(Session session)
        {
            try
            {
                PathService.EnsureDirectories();

                var sessionData = new
                {
                    Documents = session.Documents.Select(doc => new
                    {
                        ImageId = doc.ImageId,
                        SourcePath = doc.SourcePath,
                        ImageWidth = doc.ImageWidth,
                        ImageHeight = doc.ImageHeight,
                        LastEditedAt = doc.LastEditedAt.ToString("O"),
                        AlignmentInfo = doc.AlignmentInfo != null ? new
                        {
                            Success = doc.AlignmentInfo.Success,
                            Confidence = doc.AlignmentInfo.Confidence,
                            Rotation = doc.AlignmentInfo.Rotation,
                            ScaleX = doc.AlignmentInfo.ScaleX,
                            ScaleY = doc.AlignmentInfo.ScaleY,
                            TranslationX = doc.AlignmentInfo.TranslationX,
                            TranslationY = doc.AlignmentInfo.TranslationY,
                            AlignedImagePath = doc.AlignmentInfo.AlignedImagePath
                        } : null
                    }).ToList(),
                    MarkingResults = session.MarkingResults.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Select(mr => new
                        {
                            ScoringAreaId = mr.ScoringAreaId,
                            QuestionNumber = mr.QuestionNumber,
                            OptionNumber = mr.OptionNumber,
                            IsMarked = mr.IsMarked,
                            AverageBrightness = mr.AverageBrightness,
                            Threshold = mr.Threshold
                        }).ToList()
                    ),
                    BarcodeResults = session.BarcodeResults.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value.Select(br => new
                        {
                            BarcodeAreaId = br.BarcodeAreaId,
                            DecodedText = br.DecodedText,
                            Success = br.Success,
                            Format = br.Format,
                            ErrorMessage = br.ErrorMessage
                        }).ToList()
                    ),
                    AlignmentFailedImageIds = session.AlignmentFailedImageIds.ToList(),
                    BarcodeFailedImageIds = session.BarcodeFailedImageIds.ToList()
                };

                var json = JsonSerializer.Serialize(sessionData, new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                });
                
                File.WriteAllText(PathService.SessionFilePath, json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"세션 저장 실패: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 세션을 로드합니다. 파일이 없으면 빈 세션을 반환합니다.
        /// </summary>
        public Session Load()
        {
            if (!File.Exists(PathService.SessionFilePath))
            {
                return new Session();
            }

            try
            {
                var json = File.ReadAllText(PathService.SessionFilePath);
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var session = new Session();

                // Documents 로드
                if (root.TryGetProperty("Documents", out var documentsElement))
                {
                    foreach (var docElem in documentsElement.EnumerateArray())
                    {
                        AlignmentInfo? alignmentInfo = null;
                        
                        // 정렬 정보 로드
                        if (docElem.TryGetProperty("AlignmentInfo", out var alignmentElement) && 
                            alignmentElement.ValueKind != System.Text.Json.JsonValueKind.Null)
                        {
                            alignmentInfo = new AlignmentInfo
                            {
                                Success = alignmentElement.TryGetProperty("Success", out var success) 
                                    ? success.GetBoolean() 
                                    : false,
                                Confidence = alignmentElement.TryGetProperty("Confidence", out var confidence) 
                                    ? confidence.GetDouble() 
                                    : 0.0,
                                Rotation = alignmentElement.TryGetProperty("Rotation", out var rotation) 
                                    ? rotation.GetDouble() 
                                    : 0.0,
                                ScaleX = alignmentElement.TryGetProperty("ScaleX", out var scaleX) 
                                    ? scaleX.GetDouble() 
                                    : 1.0,
                                ScaleY = alignmentElement.TryGetProperty("ScaleY", out var scaleY) 
                                    ? scaleY.GetDouble() 
                                    : 1.0,
                                TranslationX = alignmentElement.TryGetProperty("TranslationX", out var tx) 
                                    ? tx.GetDouble() 
                                    : 0.0,
                                TranslationY = alignmentElement.TryGetProperty("TranslationY", out var ty) 
                                    ? ty.GetDouble() 
                                    : 0.0,
                                AlignedImagePath = alignmentElement.TryGetProperty("AlignedImagePath", out var alignedPath) 
                                    ? alignedPath.GetString() 
                                    : null
                            };
                            
                            // 정렬된 이미지 파일이 존재하는지 확인
                            if (!string.IsNullOrEmpty(alignmentInfo.AlignedImagePath) && 
                                !File.Exists(alignmentInfo.AlignedImagePath))
                            {
                                // 캐시 파일이 없으면 정렬 정보 초기화
                                Logger.Instance.Warning($"정렬된 이미지 캐시 파일이 없음: {alignmentInfo.AlignedImagePath}");
                                alignmentInfo = null;
                            }
                        }

                        var imageDoc = new ImageDocument
                        {
                            ImageId = docElem.GetProperty("ImageId").GetString() ?? Guid.NewGuid().ToString(),
                            SourcePath = docElem.GetProperty("SourcePath").GetString() ?? string.Empty,
                            ImageWidth = docElem.GetProperty("ImageWidth").GetInt32(),
                            ImageHeight = docElem.GetProperty("ImageHeight").GetInt32(),
                            AlignmentInfo = alignmentInfo
                        };

                        if (docElem.TryGetProperty("LastEditedAt", out var lastEdited))
                        {
                            if (DateTime.TryParse(lastEdited.GetString(), out var dateTime))
                            {
                                imageDoc.LastEditedAt = dateTime;
                            }
                        }

                        session.Documents.Add(imageDoc);
                    }
                }

                // MarkingResults 로드
                if (root.TryGetProperty("MarkingResults", out var markingResultsElement))
                {
                    var markingResults = new Dictionary<string, List<MarkingResult>>();
                    foreach (var kvp in markingResultsElement.EnumerateObject())
                    {
                        var imageId = kvp.Name;
                        var results = new List<MarkingResult>();
                        foreach (var resultElem in kvp.Value.EnumerateArray())
                        {
                            var markingResult = new MarkingResult
                            {
                                ScoringAreaId = resultElem.TryGetProperty("ScoringAreaId", out var areaId)
                                    ? areaId.GetString() ?? string.Empty
                                    : string.Empty,
                                QuestionNumber = resultElem.TryGetProperty("QuestionNumber", out var qNum)
                                    ? qNum.GetInt32()
                                    : 0,
                                OptionNumber = resultElem.TryGetProperty("OptionNumber", out var oNum)
                                    ? oNum.GetInt32()
                                    : 0,
                                IsMarked = resultElem.TryGetProperty("IsMarked", out var isMarked)
                                    ? isMarked.GetBoolean()
                                    : false,
                                AverageBrightness = resultElem.TryGetProperty("AverageBrightness", out var brightness)
                                    ? brightness.GetDouble()
                                    : 0.0,
                                Threshold = resultElem.TryGetProperty("Threshold", out var threshold)
                                    ? threshold.GetDouble()
                                    : 128.0
                            };
                            results.Add(markingResult);
                        }
                        markingResults[imageId] = results;
                    }
                    session.MarkingResults = markingResults;
                }

                // BarcodeResults 로드
                if (root.TryGetProperty("BarcodeResults", out var barcodeResultsElement))
                {
                    var barcodeResults = new Dictionary<string, List<BarcodeResult>>();
                    foreach (var kvp in barcodeResultsElement.EnumerateObject())
                    {
                        var imageId = kvp.Name;
                        var results = new List<BarcodeResult>();
                        foreach (var resultElem in kvp.Value.EnumerateArray())
                        {
                            var barcodeResult = new BarcodeResult
                            {
                                BarcodeAreaId = resultElem.TryGetProperty("BarcodeAreaId", out var areaId)
                                    ? areaId.GetString() ?? string.Empty
                                    : string.Empty,
                                DecodedText = resultElem.TryGetProperty("DecodedText", out var text)
                                    ? text.GetString()
                                    : null,
                                Success = resultElem.TryGetProperty("Success", out var success)
                                    ? success.GetBoolean()
                                    : false,
                                Format = resultElem.TryGetProperty("Format", out var format)
                                    ? format.GetString()
                                    : null,
                                ErrorMessage = resultElem.TryGetProperty("ErrorMessage", out var errorMsg)
                                    ? errorMsg.GetString()
                                    : null
                            };
                            results.Add(barcodeResult);
                        }
                        barcodeResults[imageId] = results;
                    }
                    session.BarcodeResults = barcodeResults;
                }

                if (root.TryGetProperty("AlignmentFailedImageIds", out var alignmentFailedElement))
                {
                    var failedIds = new HashSet<string>();
                    foreach (var idElem in alignmentFailedElement.EnumerateArray())
                    {
                        var id = idElem.GetString();
                        if (!string.IsNullOrWhiteSpace(id))
                        {
                            failedIds.Add(id);
                        }
                    }
                    session.AlignmentFailedImageIds = failedIds;
                }

                if (root.TryGetProperty("BarcodeFailedImageIds", out var barcodeFailedElement))
                {
                    var failedIds = new HashSet<string>();
                    foreach (var idElem in barcodeFailedElement.EnumerateArray())
                    {
                        var id = idElem.GetString();
                        if (!string.IsNullOrWhiteSpace(id))
                        {
                            failedIds.Add(id);
                        }
                    }
                    session.BarcodeFailedImageIds = failedIds;
                }

                return session;
            }
            catch (Exception ex)
            {
                // 로드 실패 시 빈 세션 반환
                Logger.Instance.Warning($"세션 로드 실패: {ex.Message}");
                return new Session();
            }
        }
    }
}
