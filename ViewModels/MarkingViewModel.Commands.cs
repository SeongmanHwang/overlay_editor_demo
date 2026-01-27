using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using SimpleOverlayEditor.Models;
using SimpleOverlayEditor.Services;
using SimpleOverlayEditor.Utils;

namespace SimpleOverlayEditor.ViewModels
{
    public partial class MarkingViewModel
    {
        /// <summary>
        /// Command 초기화
        /// </summary>
        private void InitializeCommands()
        {
            DetectMarkingsCommand = new RelayCommand(
                OnDetectMarkings, 
                () => SelectedDocument != null && ScoringAreas != null && ScoringAreas.Count() == OmrConstants.TotalScoringAreas);
            DetectAllMarkingsCommand = new RelayCommand(
                OnDetectAllMarkings, 
                () => Documents != null && Documents.Count() > 0 && ScoringAreas != null && ScoringAreas.Count() == OmrConstants.TotalScoringAreas);
            
            DetectUnreadMarkingsCommand = new RelayCommand(
                OnDetectUnreadMarkings,
                () => ReadyForReadingCount > 0 && ScoringAreas != null && ScoringAreas.Count() == OmrConstants.TotalScoringAreas);
            
            LoadFolderCommand = new RelayCommand(OnLoadFolder);
            ExportToXlsxCommand = new RelayCommand(
                OnExportToXlsx,
                () => SheetResults != null && SheetResults.Count > 0);
            SetFilterModeCommand = new RelayCommand<string>(OnSetFilterMode);
            ResetFiltersCommand = new RelayCommand(OnResetFilters);
        }

        /// <summary>
        /// 필터 모드를 설정합니다.
        /// </summary>
        private void OnSetFilterMode(string? filterMode)
        {
            if (!string.IsNullOrEmpty(filterMode))
            {
                FilterMode = filterMode;
            }
        }

        /// <summary>
        /// 모든 필터(라디오 + 시각/실/순)를 기본값으로 리셋합니다.
        /// </summary>
        private void OnResetFilters()
        {
            _filterMode = "All";
            _selectedSessionFilter = OmrFilterUtils.AllLabel;
            _selectedRoomFilter = OmrFilterUtils.AllLabel;
            _selectedOrderFilter = OmrFilterUtils.AllLabel;

            OnPropertyChanged(nameof(FilterMode));
            OnPropertyChanged(nameof(SelectedSessionFilter));
            OnPropertyChanged(nameof(SelectedRoomFilter));
            OnPropertyChanged(nameof(SelectedOrderFilter));

            ApplyFilter();
        }

        /// <summary>
        /// 현재 선택된 문서의 마킹을 리딩합니다.
        /// </summary>
        private void OnDetectMarkings()
        {
            if (SelectedDocument == null)
            {
                MessageBox.Show("이미지를 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (ScoringAreas == null || !ScoringAreas.Any())
            {
                MessageBox.Show("채점 영역(ScoringArea)이 없습니다.\n먼저 채점 영역을 추가해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }


            if (_workspace.Template.BarcodeAreas != null && _workspace.Template.BarcodeAreas.Count > 0)
            {
                if (!_session.BarcodeResults.TryGetValue(SelectedDocument.ImageId, out var cachedBarcodeResults) ||
                    cachedBarcodeResults.Count == 0)
                {
                    Logger.Instance.Warning($"바코드 결과가 없어 마킹 리딩을 건너뜁니다: {SelectedDocument.SourcePath}");
                    MessageBox.Show("바코드 결과가 없어 마킹 리딩을 진행할 수 없습니다.\n먼저 폴더 로드에서 바코드를 디코딩해주세요.", "알림",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                CurrentBarcodeResults = cachedBarcodeResults;
            }


            try
            {
                Logger.Instance.Info($"마킹 리딩 시작: {SelectedDocument.SourcePath}");
                
                // 마킹 리딩
                var results = _markingDetector.DetectMarkings(
                    SelectedDocument, 
                    ScoringAreas, 
                    MarkingThreshold);

                CurrentMarkingResults = results;

                // Session에 마킹 결과 저장
                if (SelectedDocument != null)
                {
                    if (_session.MarkingResults == null)
                    {
                        _session.MarkingResults = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<MarkingResult>>();
                    }
                    _session.MarkingResults[SelectedDocument.ImageId] = results;
                    _sessionStore.Save(_session); // 세션 저장
                }


                // 오버레이 이미지 생성 및 표시
                UpdateDisplayImage();

                /*
                 * [옵션 A 적용] 오버레이 파일 자동 저장 비활성화
                 *
                 * - 사용자 입장에서는 "마킹 리딩 완료" 시점에 작업이 끝난 것으로 인지합니다.
                 * - 그런데 오버레이 이미지를 디스크(overlay_cache)에 추가로 생성/저장하면,
                 *   사용자 모르게 백그라운드 I/O가 지속되고 폴더 용량이 급증할 수 있습니다.
                 * - 따라서 아래 자동 저장은 추후 정책/UX를 재설계할 때까지 잠정 중단합니다.
                 *
                 * (필요 시) 명시적 버튼/옵션으로만 생성하도록 리팩토링 예정.
                 */
                // 결과 이미지 파일 저장 (비활성화)
                // if (SelectedDocument != null)
                // {
                //     try
                //     {
                //         _renderer.RenderSingleDocument(SelectedDocument, _session, _workspace);
                //         Logger.Instance.Info($"결과 이미지 파일 저장 완료: {SelectedDocument.SourcePath}");
                //     }
                //     catch (Exception ex)
                //     {
                //         Logger.Instance.Error("결과 이미지 파일 저장 실패", ex);
                //         // 저장 실패해도 마킹 리딩은 완료되었으므로 계속 진행
                //     }
                // }

                var markedCount = results.Count(r => r.IsMarked);
                var message = $"마킹 리딩 완료\n\n" +
                             $"총 영역: {results.Count}개\n" +
                             $"마킹 리딩: {markedCount}개\n" +
                             $"미마킹: {results.Count - markedCount}개\n\n" +
                             $"임계값: {MarkingThreshold}";
                
                // 바코드 결과 추가
                if (CurrentBarcodeResults != null && CurrentBarcodeResults.Count > 0)
                {
                    var barcodeSuccessCount = CurrentBarcodeResults.Count(r => r.Success);
                    message += $"\n\n총 영역: {CurrentBarcodeResults.Count}개\n" +
                              $"성공: {barcodeSuccessCount}개\n" +
                              $"바코드 디코딩 실패: {CurrentBarcodeResults.Count - barcodeSuccessCount}개";
                }

                message += "\n\n(오버레이 이미지는 파일로 저장하지 않습니다)";

                Logger.Instance.Info($"마킹 리딩 완료: {markedCount}/{results.Count}개 마킹 리딩");
                MessageBox.Show(message, "마킹 리딩 완료", MessageBoxButton.OK, MessageBoxImage.Information);

                // OMR 결과 업데이트
                UpdateSheetResults();
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("마킹 리딩 실패", ex);
                MessageBox.Show($"마킹 리딩 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 모든 문서의 마킹을 리딩합니다.
        /// </summary>
        private async void OnDetectAllMarkings()
        {
            if (Documents == null || !Documents.Any())
            {
                MessageBox.Show("로드된 이미지가 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (ScoringAreas == null || !ScoringAreas.Any())
            {
                MessageBox.Show("채점 영역(ScoringArea)이 없습니다.\n먼저 채점 영역을 추가해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<MarkingResult>>? allResults = null;
                var skippedBarcodeCount = 0;

                var cancelled = await ProgressRunner.RunAsync(
                    Application.Current.MainWindow,
                    async scope =>
                    {
                        var cancellationToken = scope.CancellationToken;
                        var documentsList = Documents.ToList();
                        var originalCount = documentsList.Count;
                        Logger.Instance.Info($"전체 문서 마킹 리딩 시작: {documentsList.Count}개 문서");

                        if (_workspace.Template.BarcodeAreas != null && _workspace.Template.BarcodeAreas.Count > 0)
                        {
                            var filteredDocuments = new System.Collections.Generic.List<ImageDocument>();
                            foreach (var document in documentsList)
                            {
                                if (_session.BarcodeResults.TryGetValue(document.ImageId, out var barcodeResults) &&
                                    barcodeResults.Count > 0)
                                {
                                    filteredDocuments.Add(document);
                                }
                            }
                            documentsList = filteredDocuments;
                            skippedBarcodeCount = originalCount - documentsList.Count;
                        }

                        if (documentsList.Count == 0)
                        {
                            scope.Ui(() =>
                            {
                                    MessageBox.Show("바코드 결과가 있는 문서가 없어 마킹 리딩을 진행할 수 없습니다.", "알림",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                            });
                            return;

                        }

                        cancellationToken.ThrowIfCancellationRequested();

                        scope.Report(0, documentsList.Count, "마킹 리딩 시작");

                        cancellationToken.ThrowIfCancellationRequested();

                        // 마킹 리딩
                        allResults = _markingDetector.DetectAllMarkings(
                            documentsList,
                            _workspace.Template,
                            MarkingThreshold,
                            (current, total, message) =>
                            {
                                if (cancellationToken.IsCancellationRequested) return;
                                scope.Report(current, total, message);
                            },
                            cancellationToken);

                        cancellationToken.ThrowIfCancellationRequested();

                        scope.Ui(() =>
                        {
                            _session.MarkingResults = allResults;
                            _sessionStore.Save(_session);
                        });

                        await Task.CompletedTask;
                    },
                    title: "진행 중...",
                    initialStatus: "처리 중...");

                if (cancelled)
                {
                    Logger.Instance.Info("전체 마킹 리딩이 취소되었습니다.");
                    MessageBox.Show("작업이 취소되었습니다.", "취소됨", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (allResults == null)
                {
                    throw new InvalidOperationException("마킹 리딩 결과가 생성되지 않았습니다.");
                }

                // 완료 메시지 구성/표시 (UI 스레드)
                int totalDocuments = allResults.Count;
                int totalAreas = 0;
                int totalMarked = 0;
                foreach (var kvp in allResults)
                {
                    totalAreas += kvp.Value.Count;
                    totalMarked += kvp.Value.Count(r => r.IsMarked);
                }

                var message = $"전체 문서 마킹 리딩 완료\n\n" +
                              $"처리된 문서: {totalDocuments}개\n" +
                              $"총 영역: {totalAreas}개\n" +
                              $"마킹 리딩: {totalMarked}개\n" +
                              $"미마킹: {totalAreas - totalMarked}개\n\n" +
                              $"임계값: {MarkingThreshold}";
                if (skippedBarcodeCount > 0)
                {
                    message += $"\n바코드 결과 없음으로 스킵: {skippedBarcodeCount}개";
                }

                if (_session.BarcodeResults != null && _session.BarcodeResults.Count > 0)
                {
                    int totalBarcodeAreas = 0;
                    int totalBarcodeSuccess = 0;
                    foreach (var kvp in _session.BarcodeResults)
                    {
                        totalBarcodeAreas += kvp.Value.Count;
                        totalBarcodeSuccess += kvp.Value.Count(r => r.Success);
                    }
                    message += $"\n\n총 영역: {totalBarcodeAreas}개\n" +
                               $"성공: {totalBarcodeSuccess}개\n" +
                               $"바코드 디코딩 실패: {totalBarcodeAreas - totalBarcodeSuccess}개";
                }

                Logger.Instance.Info($"전체 문서 마킹 리딩 완료: {totalMarked}/{totalAreas}개 마킹 리딩");

                // 현재 선택된 문서의 결과 표시
                if (SelectedDocument != null)
                {
                    if (allResults.TryGetValue(SelectedDocument.ImageId, out var currentResults))
                    {
                        CurrentMarkingResults = currentResults;
                    }

                    if (_session.BarcodeResults != null &&
                        _session.BarcodeResults.TryGetValue(SelectedDocument.ImageId, out var currentBarcodeResults))
                    {
                        CurrentBarcodeResults = currentBarcodeResults;
                    }

                    UpdateDisplayImage();
                }

                // OMR 결과 업데이트 및 통계 표시
                UpdateSheetResults();

                if (SheetResults != null && SheetResults.Count > 0)
                {
                    message += $"\n\n결과 분석:\n" +
                              $"총 용지: {SheetResults.Count}개\n" +
                              $"오류 있는 용지: {ErrorCount}개";

                    if (DuplicateCount > 0)
                    {
                        message += $"\nID 중복: {DuplicateCount}개";
                    }

                    if (NullCombinedIdCount > 0)
                    {
                        message += $"\n결합ID 없음: {NullCombinedIdCount}개";
                    }
                }

                MessageBox.Show(message, "전체 마킹 리딩 완료", MessageBoxButton.OK, MessageBoxImage.Information);

                /*
                 * [옵션 A 적용] 전체 문서 오버레이 파일 자동 캐싱 비활성화
                 *
                 * 기존에는 "전체 마킹 리딩 완료" 후 사용자 모르게 백그라운드에서
                 * overlay_cache에 PNG를 대량 생성했습니다.
                 * 이 동작은 용량 폭증/백그라운드 작업 지속 문제를 만들 수 있어 잠정 중단합니다.
                 */
                // 모든 문서의 결과 이미지 파일 저장 (백그라운드) - 비활성화
                // _ = Task.Run(() =>
                // {
                //     try
                //     {
                //         _renderer.RenderAll(_session, _workspace);
                //         Logger.Instance.Info("전체 결과 이미지 파일 저장 완료");
                //     }
                //     catch (Exception ex)
                //     {
                //         Logger.Instance.Error("전체 결과 이미지 파일 저장 실패", ex);
                //     }
                // });
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("전체 마킹 리딩 실패", ex);
                MessageBox.Show($"전체 마킹 리딩 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 미리딩 문서 중 Ready 상태만 마킹을 리딩합니다.
        /// </summary>
        private async void OnDetectUnreadMarkings()
        {
            if (Documents == null || !Documents.Any())
            {
                MessageBox.Show("로드된 이미지가 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (ScoringAreas == null || !ScoringAreas.Any())
            {
                MessageBox.Show("채점 영역(ScoringArea)이 없습니다.\n먼저 채점 영역을 추가해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var readyDocuments = GetReadyUnreadDocuments();
            if (readyDocuments.Count == 0)
            {
                MessageBox.Show("리딩 가능한 미리딩 문서가 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<MarkingResult>>? newResults = null;

                var cancelled = await ProgressRunner.RunAsync(
                    Application.Current.MainWindow,
                    async scope =>
                    {
                        var cancellationToken = scope.CancellationToken;

                        Logger.Instance.Info($"미리딩 문서 마킹 리딩 시작: {readyDocuments.Count}개 문서");

                        scope.Report(0, readyDocuments.Count, "마킹 리딩 시작");

                        cancellationToken.ThrowIfCancellationRequested();

                        newResults = _markingDetector.DetectAllMarkings(
                            readyDocuments,
                            _workspace.Template,
                            MarkingThreshold,
                            (current, total, message) =>
                            {
                                if (cancellationToken.IsCancellationRequested) return;
                                scope.Report(current, total, message);
                            },
                            cancellationToken);

                        cancellationToken.ThrowIfCancellationRequested();

                        scope.Ui(() =>
                        {
                            if (_session.MarkingResults == null)
                            {
                                _session.MarkingResults = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<MarkingResult>>();
                            }

                            foreach (var kvp in newResults)
                            {
                                _session.MarkingResults[kvp.Key] = kvp.Value;
                            }

                            _sessionStore.Save(_session);
                        });

                        await Task.CompletedTask;
                    },
                    title: "진행 중...",
                    initialStatus: "처리 중...");

                if (cancelled)
                {
                    Logger.Instance.Info("미리딩 문서 마킹 리딩이 취소되었습니다.");
                    MessageBox.Show("작업이 취소되었습니다.", "취소됨", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (newResults == null)
                {
                    throw new InvalidOperationException("마킹 리딩 결과가 생성되지 않았습니다.");
                }

                var totalDocuments = newResults.Count;
                var totalAreas = 0;
                var totalMarked = 0;
                foreach (var kvp in newResults)
                {
                    totalAreas += kvp.Value.Count;
                    totalMarked += kvp.Value.Count(r => r.IsMarked);
                }

                var message = $"미리딩 문서 마킹 리딩 완료\n\n" +
                              $"처리된 문서: {totalDocuments}개\n" +
                              $"총 영역: {totalAreas}개\n" +
                              $"마킹 리딩: {totalMarked}개\n" +
                              $"미마킹: {totalAreas - totalMarked}개\n\n" +
                              $"임계값: {MarkingThreshold}";

                Logger.Instance.Info($"미리딩 문서 마킹 리딩 완료: {totalMarked}/{totalAreas}개 마킹 리딩");

                if (SelectedDocument != null && newResults.TryGetValue(SelectedDocument.ImageId, out var currentResults))
                {
                    CurrentMarkingResults = currentResults;
                    UpdateDisplayImage();
                }

                UpdateSheetResults();

                if (SheetResults != null && SheetResults.Count > 0)
                {
                    message += $"\n\n결과 분석:\n" +
                               $"총 용지: {SheetResults.Count}개\n" +
                               $"오류 있는 용지: {ErrorCount}개";

                    if (DuplicateCount > 0)
                    {
                        message += $"\nID 중복: {DuplicateCount}개";
                    }

                    if (NullCombinedIdCount > 0)
                    {
                        message += $"\n결합ID 없음: {NullCombinedIdCount}개";
                    }
                }

                MessageBox.Show(message, "미리딩 문서 마킹 리딩 완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("미리딩 문서 마킹 리딩 실패", ex);
                MessageBox.Show($"미리딩 문서 마킹 리딩 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Excel(.xlsx) 파일로 내보냅니다.
        /// </summary>
        private void OnExportToXlsx()
        {
            if (SheetResults == null || SheetResults.Count == 0)
            {
                MessageBox.Show("내보낼 데이터가 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Excel 파일 (*.xlsx)|*.xlsx|모든 파일 (*.*)|*.*",
                    FileName = $"마킹 리딩 결과_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
                };

                if (dialog.ShowDialog() == true)
                {
                    // UI에 보이는(필터 적용된) 목록을 우선 내보냄
                    var resultsToExport = (FilteredSheetResults != null)
                        ? FilteredSheetResults.Cast<OmrSheetResult>()
                        : SheetResults;

                    ExportToXlsx(dialog.FileName, resultsToExport);
                    MessageBox.Show("Excel(.xlsx) 파일로 저장되었습니다.", "완료", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("Excel 내보내기 실패", ex);
                MessageBox.Show($"Excel 내보내기 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 폴더에서 이미지를 로드합니다.
        /// </summary>
        private async void OnLoadFolder()
        {
            Logger.Instance.Info("폴더 로드 시작");
            try
            {
                var dialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = "이미지 파일이 아니라 폴더를 선택합니다. 이미지 파일은 여기서 보이지 않습니다",
                    SelectedPath = _workspace.InputFolderPath
                };

                Logger.Instance.Debug($"FolderBrowserDialog 표시. 초기 경로: {_workspace.InputFolderPath}");
                
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var folderPath = dialog.SelectedPath;
                    Logger.Instance.Info($"선택된 폴더: {folderPath}");

                    try
                    {
                        List<ImageDocument>? loadedDocuments = null;

                        Dictionary<string, List<BarcodeResult>>? loadedBarcodeResults = null;
                        HashSet<string>? barcodeFailedImageIds = null;
                        var barcodeSuccessCount = 0;
                        var barcodeFailureCount = 0;
                        var alignmentFailedCount = 0;
                        var combinedIdMissingCount = 0;

                        var skippedByFilenameCount = 0;

                        var existingFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var existingDocument in _session.Documents)
                        {
                            var existingFileName = Path.GetFileName(existingDocument.SourcePath);
                            if (!string.IsNullOrEmpty(existingFileName))
                            {
                                existingFileNames.Add(existingFileName);
                            }
                        }

                        var cancelled = await ProgressRunner.RunAsync(
                            Application.Current.MainWindow,
                            async scope =>
                            {
                                var cancellationToken = scope.CancellationToken;

                                Logger.Instance.Debug("이미지 파일 로드 시작");
                                loadedDocuments = _imageLoader.LoadImagesFromFolder(
                                    folderPath,
                                    (current, total, message) =>
                                    {
                                        if (cancellationToken.IsCancellationRequested) return;
                                        scope.Report(current, total, message);
                                    },
                                    cancellationToken);

                                cancellationToken.ThrowIfCancellationRequested();

                                Logger.Instance.Info($"이미지 파일 로드 완료. 문서 수: {loadedDocuments.Count}");

                                scope.Report(0, loadedDocuments.Count, "정렬 작업 시작");

                                if (loadedDocuments.Count == 0)
                                {
                                    scope.Ui(() =>
                                    {
                                        Logger.Instance.Warning("선택한 폴더에 이미지 파일이 없음");
                                        MessageBox.Show("선택한 폴더에 이미지 파일이 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
                                    });
                                    return;
                                }

                                if (existingFileNames.Count > 0)
                                {
                                    var filteredDocuments = new List<ImageDocument>();
                                    foreach (var document in loadedDocuments)
                                    {
                                        var fileName = Path.GetFileName(document.SourcePath);
                                        if (!string.IsNullOrEmpty(fileName) && existingFileNames.Contains(fileName))
                                        {
                                            skippedByFilenameCount++;
                                            Logger.Instance.Info($"파일명 중복: {fileName}");
                                            continue;
                                        }

                                        if (!string.IsNullOrEmpty(fileName))
                                        {
                                            existingFileNames.Add(fileName);
                                        }

                                        filteredDocuments.Add(document);
                                    }

                                    loadedDocuments = filteredDocuments;
                                    if (skippedByFilenameCount > 0)
                                    {
                                        Logger.Instance.Info($"파일명 중복된 문서: {skippedByFilenameCount}개");
                                        scope.Status($"파일명 중복: {skippedByFilenameCount}개");
                                    }
                                }

                                if (loadedDocuments.Count == 0)
                                {
                                    scope.Ui(() =>
                                    {
                                        MessageBox.Show(
                                            $"새로 추가할 이미지가 없습니다.\n파일명 중복: {skippedByFilenameCount}개",
                                            "알림",
                                            MessageBoxButton.OK,
                                            MessageBoxImage.Information);
                                    });
                                    return;
                                }


                                scope.Ui(() =>
                                {
                                    SelectedDocument = null;
                                    _workspace.InputFolderPath = folderPath;
                                });

                                Logger.Instance.Debug($"문서 {loadedDocuments.Count}개 추가 및 정렬 적용 시작 (병렬 처리)");

                                var completedCount = 0;
                                var lockObject = new object();

                                var barcodeLock = new object();
                                loadedBarcodeResults = new Dictionary<string, List<BarcodeResult>>();
                                barcodeFailedImageIds = new HashSet<string>();

                                System.Threading.Tasks.Parallel.ForEach(loadedDocuments, new ParallelOptions
                                {
                                    MaxDegreeOfParallelism = Environment.ProcessorCount,
                                    CancellationToken = cancellationToken
                                }, doc =>
                                {
                                    cancellationToken.ThrowIfCancellationRequested();

                                    var localBarcodeSuccess = 0;
                                    var localBarcodeFailure = 0;
                                    var localAlignmentFailed = 0;
                                    var localCombinedIdMissing = 0;
                                    var localSkippedByFilename = 0;

                                    // 이미지 정렬 적용
                                    ApplyAlignmentToDocument(doc);

                                    if (doc.AlignmentInfo?.Success == true &&
                                        _workspace.Template.BarcodeAreas != null &&
                                        _workspace.Template.BarcodeAreas.Count > 0)
                                    {
                                        try
                                        {
                                            var results = _barcodeReaderService.DecodeBarcodes(
                                                doc,
                                                _workspace.Template.BarcodeAreas);

                                            lock (barcodeLock)
                                            {
                                                loadedBarcodeResults[doc.ImageId] = results;

                                                var barcodeSuccess = results.Count > 0 && results.All(r => r.Success);
                                                var ingestState = GetOrCreateIngestState(doc.ImageId);
                                                ingestState.SetBarcodeOk(barcodeSuccess);
                                                
                                                if (!barcodeSuccess)
                                                {
                                                    barcodeFailedImageIds.Add(doc.ImageId);
                                                    barcodeFailureCount++;
                                                    ingestState.SetCombinedIdOk(null);
                                                }
                                                else
                                                {
                                                    barcodeSuccessCount++;
                                                    var barcodeOnlyResult = _markingAnalyzer.AnalyzeSheet(doc, null, results);
                                                    var combinedIdOk = !string.IsNullOrWhiteSpace(barcodeOnlyResult.CombinedId);
                                                    ingestState.SetCombinedIdOk(combinedIdOk);
                                                    if (!combinedIdOk)
                                                    {
                                                        combinedIdMissingCount++;
                                                    }
                                                }

                                                localBarcodeSuccess = barcodeSuccessCount;
                                                localBarcodeFailure = barcodeFailureCount;
                                                localAlignmentFailed = alignmentFailedCount;
                                                localCombinedIdMissing = combinedIdMissingCount;
                                                localSkippedByFilename = skippedByFilenameCount;
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Logger.Instance.Warning($"바코드 디코딩 실패: {doc.SourcePath}, {ex.Message}");
                                            lock (barcodeLock)
                                            {
                                                loadedBarcodeResults[doc.ImageId] = new List<BarcodeResult>();
                                                barcodeFailedImageIds.Add(doc.ImageId);
                                                barcodeFailureCount++;

                                                var ingestState = GetOrCreateIngestState(doc.ImageId);
                                                ingestState.SetBarcodeOk(false);
                                                ingestState.SetCombinedIdOk(null);

                                                localBarcodeSuccess = barcodeSuccessCount;
                                                localBarcodeFailure = barcodeFailureCount;
                                                localAlignmentFailed = alignmentFailedCount;
                                                localCombinedIdMissing = combinedIdMissingCount;
                                                localSkippedByFilename = skippedByFilenameCount;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (_workspace.Template.BarcodeAreas != null &&
                                            _workspace.Template.BarcodeAreas.Count > 0)
                                        {
                                            lock (barcodeLock)
                                            {
                                                if (doc.AlignmentInfo?.Success != true)
                                                {
                                                    alignmentFailedCount++;
                                                }

                                                GetOrCreateIngestState(doc.ImageId).SetBarcodeOk(null);
                                                GetOrCreateIngestState(doc.ImageId).SetCombinedIdOk(null);

                                                localBarcodeSuccess = barcodeSuccessCount;
                                                localBarcodeFailure = barcodeFailureCount;
                                                localAlignmentFailed = alignmentFailedCount;
                                                localCombinedIdMissing = combinedIdMissingCount;
                                                localSkippedByFilename = skippedByFilenameCount;
                                            }
                                        }
                                    }

                                    int current;
                                    lock (lockObject)
                                    {
                                        completedCount++;
                                        current = completedCount;
                                    }

                                    scope.Ui(() =>
                                    {
                                        if (cancellationToken.IsCancellationRequested) return;

                                        var fileName = Path.GetFileName(doc.SourcePath);

                                        if (_workspace.Template.BarcodeAreas != null &&
                                            _workspace.Template.BarcodeAreas.Count > 0)
                                        {
                                            var statusMessage = $"정렬 중: {fileName}";
                                            if (localSkippedByFilename > 0)
                                            {
                                                statusMessage += $"\n파일명 중복: {localSkippedByFilename}";
                                            }
                                            statusMessage += $"\n정렬 실패: {localAlignmentFailed}\n" +
                                                            $"바코드 디코딩 실패: {localBarcodeFailure}\n" +
                                                            $"결합ID 없음: {localCombinedIdMissing}\n" +
                                                            $"성공: {localBarcodeSuccess}";
                                            scope.Report(current, loadedDocuments.Count, statusMessage);
                                        }
                                        else
                                        {
                                            var statusMessage = $"정렬 중: {fileName}";
                                            if (localSkippedByFilename > 0)
                                            {
                                                statusMessage += $"\n파일명 중복: {localSkippedByFilename}";
                                            }
                                            scope.Report(current, loadedDocuments.Count, statusMessage);
                                        }

                                        _session.Documents.Add(doc);
                                    });
                                });

                                cancellationToken.ThrowIfCancellationRequested();

                                scope.Ui(() =>
                                {
                                    if (loadedBarcodeResults != null)
                                    {
                                        foreach (var kvp in loadedBarcodeResults)
                                        {
                                            _session.BarcodeResults[kvp.Key] = kvp.Value;
                                        }
                                    }

                                    if (barcodeFailedImageIds != null)
                                    {
                                        foreach (var imageId in barcodeFailedImageIds)
                                        {
                                            _session.BarcodeFailedImageIds.Add(imageId);
                                        }
                                    }


                                    _stateStore.SaveWorkspaceState(_workspace);
                                    _sessionStore.Save(_session);
                                });

                                await Task.CompletedTask;
                            },
                            title: "진행 중...",
                            initialStatus: "처리 중...",
                            showDelayMs: 0);

                        if (cancelled)
                        {
                            Logger.Instance.Info("폴더 로드/정렬 작업이 취소되었습니다.");
                            MessageBox.Show("작업이 취소되었습니다.", "취소됨", MessageBoxButton.OK, MessageBoxImage.Information);
                            return;
                        }

                        if (loadedDocuments == null || loadedDocuments.Count == 0)
                        {
                            return;
                        }

                        // UI 반영
                        SelectedDocument = null;
                        _workspace.SelectedDocumentId = null;
                        CurrentMarkingResults = null;
                        CurrentBarcodeResults = null;
                        DisplayImage = null;

                        Documents = _session.Documents;
                        OnPropertyChanged(nameof(Documents));
                        OnPropertyChanged(nameof(DocumentCount));
                        UpdateSheetResults();

                        Logger.Instance.Info($"폴더 로드 완료. 새로 로드된 이미지: {loadedDocuments.Count}개, 파일명 중복: {skippedByFilenameCount}개");
                        var ingestStates = loadedDocuments
                            .Select(doc => _session.IngestStateByImageId.TryGetValue(doc.ImageId, out var state) ? state : null)
                            .ToList();
                        var alignFailedCount = ingestStates.Count(state =>
                            state?.FailureReasons.HasFlag(IngestFailureReason.AlignFailed) == true);
                        var barcodeFailedCount = ingestStates.Count(state =>
                            state?.FailureReasons.HasFlag(IngestFailureReason.BarcodeFailed) == true);
                        var combinedIdMissingSummaryCount = ingestStates.Count(state =>
                            state?.FailureReasons.HasFlag(IngestFailureReason.CombinedIdMissing) == true);
                        var missingFileCount = ingestStates.Count(state =>
                            state?.FailureReasons.HasFlag(IngestFailureReason.MissingFile) == true);
                        var quarantinedCount = ingestStates.Count(state => state?.IsQuarantined == true);
                        var unknownCount = ingestStates.Count(state => state == null || state.IsUnknown);

                        var duplicatesDetectedCount = 0;
                        if (loadedBarcodeResults != null)
                        {
                             var duplicateCandidates = new List<OmrSheetResult>();
                            foreach (var doc in loadedDocuments)
                            {
                                if (loadedBarcodeResults.TryGetValue(doc.ImageId, out var barcodeResults))
                                {
                                    var sheetResult = _markingAnalyzer.AnalyzeSheet(doc, null, barcodeResults);
                                    duplicateCandidates.Add(sheetResult);
                                }
                            }

                            var duplicateGroups = DuplicateDetector.DetectCombinedIdDuplicates(duplicateCandidates);
                            duplicatesDetectedCount = duplicateGroups.Values.Sum(group => group.Count);
                        }

                        var message = $"로드됨: {loadedDocuments.Count}개\n파일명 스킵: {skippedByFilenameCount}개";
                        
                        if (_workspace.Template.BarcodeAreas != null && _workspace.Template.BarcodeAreas.Count > 0)
                        {
                            message += $"\n격리: {quarantinedCount}개 " +
                                       $"(정렬실패 {alignFailedCount}, " +
                                       $"바코드실패 {barcodeFailedCount}, " +
                                       $"ID없음 {combinedIdMissingSummaryCount}, " +
                                       $"파일누락 {missingFileCount})" +
                                       $"\n성공: {barcodeSuccessCount}개";
                        }

                        if (duplicatesDetectedCount > 0)
                        {
                            message += $"\n중복 존재: {duplicatesDetectedCount}개";
                        }

                        if (unknownCount > 0)
                        {
                            message += $"\n상태 미기록: {unknownCount}개 (재-ingest 권장)";
                        }
                        MessageBox.Show(message, "로드 완료", MessageBoxButton.OK, MessageBoxImage.Information);

                    }
                    catch (Exception ex)
                    {
                        Logger.Instance.Error("폴더 로드 실패", ex);
                        MessageBox.Show($"폴더 로드 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    Logger.Instance.Info("폴더 선택 취소됨");
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("폴더 로드 실패", ex);
                MessageBox.Show($"폴더 로드 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
