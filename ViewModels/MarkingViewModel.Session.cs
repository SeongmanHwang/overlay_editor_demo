using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using SimpleOverlayEditor.Models;
using SimpleOverlayEditor.Services;
using SimpleOverlayEditor.Utils;

namespace SimpleOverlayEditor.ViewModels
{
    public partial class MarkingViewModel
    {
        /// <summary>
        /// 세션에 저장된 문서/결과를 UI에 즉시 반영합니다 (정렬은 수행하지 않음).
        /// </summary>
        private void InitializeFromSessionWithoutBlocking()
        {
            Documents = _session.Documents;
            OnPropertyChanged(nameof(Documents));
            OnPropertyChanged(nameof(DocumentCount));

            Logger.Instance.Info($"Session.Documents 초기화(비차단): {_session.Documents.Count}개 문서 로드됨");

            // 기존 세션에 결과가 있으면 SheetResults 업데이트
            if ((_session.MarkingResults != null && _session.MarkingResults.Count > 0) ||
                (_session.BarcodeResults != null && _session.BarcodeResults.Count > 0))
            {
                UpdateSheetResults();
            }
        }

        private void ReloadFromSession()
        {
            _session = _sessionStore.Load();

            Documents = _session.Documents;
            OnPropertyChanged(nameof(Documents));
            OnPropertyChanged(nameof(DocumentCount));

            InitializeFilterOptions();

            SelectedDocument = null;
            CurrentMarkingResults = null;
            CurrentBarcodeResults = null;
            DisplayImage = null;

            if ((_session.MarkingResults?.Count ?? 0) > 0 ||
                (_session.BarcodeResults?.Count ?? 0) > 0)
            {
                UpdateSheetResults();
            }
            else
            {
                if (SheetResults != null)
                {
                    SheetResults.Clear();
                }
                else
                {
                    SheetResults = null;
                    FilteredSheetResults = null;
                }
                OnPropertyChanged(nameof(SheetResults));
                OnPropertyChanged(nameof(FilteredSheetResults));
                OnPropertyChanged(nameof(ErrorCount));
                OnPropertyChanged(nameof(DuplicateCount));
                OnPropertyChanged(nameof(NullCombinedIdCount));
                ReadyForReadingCount = CalculateReadyForReadingCount();
                UpdateFilterOptions();
            }

            var selectedId = _workspace.SelectedDocumentId;
            if (!string.IsNullOrWhiteSpace(selectedId) && Documents != null)
            {
                var target = Documents.FirstOrDefault(doc => doc.ImageId == selectedId);
                if (target != null)
                {
                    SelectedDocument = target;
                }
            }
        }

        /// <summary>
        /// 마킹 결과를 OMR 용지 구조에 맞게 분석하여 SheetResults를 업데이트합니다.
        /// </summary>
        private void UpdateSheetResults()
        {
            if (_session.Documents == null || _session.Documents.Count == 0)
            {
                // SheetResults가 있으면 Clear만, 없으면 null로 설정
                if (SheetResults != null)
                {
                    SheetResults.Clear();
                }
                else
                {
                    SheetResults = null;
                    FilteredSheetResults = null;
                }
                OnPropertyChanged(nameof(FilteredSheetResults));
                OnPropertyChanged(nameof(ErrorCount));
                OnPropertyChanged(nameof(DuplicateCount));
                OnPropertyChanged(nameof(NullCombinedIdCount));

                ReadyForReadingCount = CalculateReadyForReadingCount();
                
                // 필터 옵션 초기화 (데이터 없음)
                UpdateFilterOptions();
                return;
            }

            try
            {
                var results = _markingAnalyzer.AnalyzeAllSheets(_session);
                
                // 결합ID 기준으로 중복 검출
                var groupedByCombinedId = DuplicateDetector.DetectAndApplyCombinedIdDuplicates(results);
                
                // 컬렉션 인스턴스 유지: 기존 SheetResults가 있으면 Clear 후 다시 채우기
                // 이렇게 하면 FilteredSheetResults도 유지되어 사용자 정렬 상태가 보존됨
                if (SheetResults == null)
                {
                    // 처음 생성하는 경우만 새로운 ObservableCollection 생성
                    SheetResults = new System.Collections.ObjectModel.ObservableCollection<OmrSheetResult>(results);
                    
                    // CollectionViewSource 생성 및 정렬/필터 설정
                    FilteredSheetResults = System.Windows.Data.CollectionViewSource.GetDefaultView(SheetResults);
                    ApplyInitialSort();  // 초기 정렬 적용 (기존 정렬이 없을 때만)
                    ApplyFilter();  // 필터 적용
                }
                else
                {
                    // 기존 컬렉션 인스턴스 유지: 사용자 정렬 상태 보존을 위해
                    // CollectionViewSource.GetDefaultView는 같은 ObservableCollection에 대해 같은 ICollectionView를 반환하므로
                    // FilteredSheetResults도 유지됨
                    
                    SheetResults.Clear();
                    foreach (var item in results)
                    {
                        SheetResults.Add(item);
                    }
                    
                    // FilteredSheetResults는 재할당할 필요 없음 (같은 인스턴스이므로)
                    // 다만, 필터는 재적용 필요
                    ApplyFilter();
                }
                
                OnPropertyChanged(nameof(SheetResults));
                OnPropertyChanged(nameof(FilteredSheetResults));
                OnPropertyChanged(nameof(ErrorCount));
                OnPropertyChanged(nameof(DuplicateCount));
                OnPropertyChanged(nameof(NullCombinedIdCount));

                ReadyForReadingCount = CalculateReadyForReadingCount();
                
                // 필터 옵션 업데이트 (실/순 필터 동적 추출)
                UpdateFilterOptions();
                UpdateLoadFailureItems(null);
                
                var duplicateRowCount = groupedByCombinedId.Values.SelectMany(g => g).Count();
                if (duplicateRowCount > 0)
                {
                    Logger.Instance.Warning($"ID 중복 검출: {duplicateRowCount}개 행");
                }
                else
                {
                    Logger.Instance.Info($"ID 중복 없음");
                }
                
                Logger.Instance.Info($"OMR 결과 업데이트 완료: {results.Count}개 용지");
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("OMR 결과 업데이트 실패", ex);
                SheetResults = null;
                FilteredSheetResults = null;
                OnPropertyChanged(nameof(FilteredSheetResults));
                OnPropertyChanged(nameof(ErrorCount));
                OnPropertyChanged(nameof(DuplicateCount));
                OnPropertyChanged(nameof(NullCombinedIdCount));

                ReadyForReadingCount = CalculateReadyForReadingCount();
                UpdateLoadFailureItems(null);
                
            }
        }

        private int CalculateReadyForReadingCount()
        {
            return GetReadyUnreadDocuments().Count;
        }

        private List<ImageDocument> GetReadyUnreadDocuments()
        {
            if (_session.Documents == null || _session.Documents.Count == 0)
            {
                return new List<ImageDocument>();
            }

            var documents = new List<ImageDocument>();

            foreach (var document in _session.Documents)
            {
                if (document.AlignmentInfo?.Success != true)
                {
                    continue;
                }

                if (_session.MarkingResults != null &&
                    _session.MarkingResults.TryGetValue(document.ImageId, out var markingResults) &&
                    markingResults != null &&
                    markingResults.Count > 0)
                {
                    continue;
                }

                if (_session.BarcodeResults == null ||
                    !_session.BarcodeResults.TryGetValue(document.ImageId, out var barcodeResults) ||
                    barcodeResults == null ||
                    barcodeResults.Count == 0)
                {
                    continue;
                }

                if (_session.BarcodeFailedImageIds.Contains(document.ImageId))
                {
                    continue;
                }

                var barcodeOnlyResult = _markingAnalyzer.AnalyzeSheet(document, null, barcodeResults);
                if (string.IsNullOrEmpty(barcodeOnlyResult.CombinedId))
                {
                    continue;
                }

                documents.Add(document);
            }

            return documents;
        }

        /// <summary>
        /// 단일 항목을 삭제합니다.
        /// </summary>
        public void DeleteSingleItem(string imageId)
        {
            if (SheetResults == null || string.IsNullOrEmpty(imageId)) return;
            
            var itemToDelete = SheetResults.FirstOrDefault(r => r.ImageId == imageId);
            if (itemToDelete == null) return;
            
            // 확인 다이얼로그
            var message = $"'{itemToDelete.ImageFileName}' 항목을 삭제하시겠습니까?\n\n" +
                         "이 작업은 되돌릴 수 없습니다.";
            
            var result = MessageBox.Show(message, "삭제 확인", 
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            
            if (result != MessageBoxResult.Yes) return;
            
            // 중복 그룹 검증 (단일 삭제이므로 단순화)
            if (itemToDelete.IsDuplicate && !string.IsNullOrEmpty(itemToDelete.CombinedId))
            {
                var allInGroup = SheetResults
                    .Where(r => r.CombinedId == itemToDelete.CombinedId)
                    .ToList();
                
                if (allInGroup.Count > 1)
                {
                    // 같은 그룹에 다른 항목이 있으면 삭제 가능 (단일 항목만 삭제)
                    // 경고 없이 진행 (사용자가 명시적으로 선택했으므로)
                }
            }
            
            // Session에서 삭제
            DeleteDocumentsFromSession(new[] { imageId });
            
            // Session 저장
            _sessionStore.Save(_session);
            
            // SheetResults 재생성
            UpdateSheetResults();
            
            Logger.Instance.Info($"단일 항목 삭제 완료: {imageId}");
        }

        /// <summary>
        /// Session에서 지정된 ImageId들을 삭제합니다.
        /// </summary>
        private void DeleteDocumentsFromSession(IEnumerable<string> imageIdsToDelete)
        {
            var imageIdSet = imageIdsToDelete.ToHashSet();
            
            // 1. Documents에서 제거
            var documentsToRemove = _session.Documents
                .Where(d => imageIdSet.Contains(d.ImageId))
                .ToList();
            
            foreach (var doc in documentsToRemove)
            {
                _session.Documents.Remove(doc);
                Logger.Instance.Info($"Document 삭제: {doc.SourcePath} (ImageId: {doc.ImageId})");
            }
            
            // 2. MarkingResults에서 제거
            if (_session.MarkingResults != null)
            {
                foreach (var imageId in imageIdSet)
                {
                    if (_session.MarkingResults.Remove(imageId))
                    {
                        Logger.Instance.Info($"MarkingResults 삭제: ImageId={imageId}");
                    }
                }
            }
            
            // 3. BarcodeResults에서 제거
            if (_session.BarcodeResults != null)
            {
                foreach (var imageId in imageIdSet)
                {
                    if (_session.BarcodeResults.Remove(imageId))
                    {
                        Logger.Instance.Info($"BarcodeResults 삭제: ImageId={imageId}");
                    }
                }
            }
        }
    }
}
