using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using SimpleOverlayEditor.Models;
using SimpleOverlayEditor.Services;

namespace SimpleOverlayEditor.ViewModels
{
    /// <summary>
    /// 마킹 감지 전용 ViewModel입니다.
    /// </summary>
    public class MarkingViewModel : INotifyPropertyChanged
    {
        private readonly MarkingDetector _markingDetector;
        private List<MarkingResult>? _currentMarkingResults;
        private double _markingThreshold = 180.0;

        public MarkingViewModel(MarkingDetector markingDetector)
        {
            _markingDetector = markingDetector ?? throw new ArgumentNullException(nameof(markingDetector));

            DetectMarkingsCommand = new RelayCommand(
                OnDetectMarkings, 
                () => SelectedDocument != null && ScoringAreas != null && ScoringAreas.Count() > 0);
            DetectAllMarkingsCommand = new RelayCommand(
                OnDetectAllMarkings, 
                () => Documents != null && Documents.Count() > 0 && ScoringAreas != null && ScoringAreas.Count() > 0);
        }

        public ImageDocument? SelectedDocument { get; set; }
        public IEnumerable<ImageDocument>? Documents { get; set; }
        public IEnumerable<RectangleOverlay>? ScoringAreas { get; set; }

        public List<MarkingResult>? CurrentMarkingResults
        {
            get => _currentMarkingResults;
            set
            {
                _currentMarkingResults = value;
                OnPropertyChanged();
            }
        }

        public double MarkingThreshold
        {
            get => _markingThreshold;
            set
            {
                _markingThreshold = value;
                OnPropertyChanged();
            }
        }

        public ICommand DetectMarkingsCommand { get; }
        public ICommand DetectAllMarkingsCommand { get; }

        /// <summary>
        /// 현재 선택된 문서의 마킹을 감지합니다.
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

            try
            {
                Logger.Instance.Info($"마킹 감지 시작: {SelectedDocument.SourcePath}");
                
                var results = _markingDetector.DetectMarkings(
                    SelectedDocument, 
                    ScoringAreas, 
                    MarkingThreshold);

                CurrentMarkingResults = results;

                var markedCount = results.Count(r => r.IsMarked);
                var message = $"마킹 감지 완료\n\n" +
                             $"총 영역: {results.Count}개\n" +
                             $"마킹 감지: {markedCount}개\n" +
                             $"미마킹: {results.Count - markedCount}개\n\n" +
                             $"임계값: {MarkingThreshold}";

                Logger.Instance.Info($"마킹 감지 완료: {markedCount}/{results.Count}개 마킹 감지");
                MessageBox.Show(message, "마킹 감지 완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("마킹 감지 실패", ex);
                MessageBox.Show($"마킹 감지 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 모든 문서의 마킹을 감지합니다.
        /// </summary>
        private void OnDetectAllMarkings()
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
                // Workspace를 임시로 생성하여 DetectAllMarkings 호출
                var workspace = new Workspace
                {
                    Documents = new System.Collections.ObjectModel.ObservableCollection<ImageDocument>(Documents)
                };
                foreach (var area in ScoringAreas)
                {
                    workspace.Template.ScoringAreas.Add(area);
                }

                Logger.Instance.Info($"전체 문서 마킹 감지 시작: {Documents.Count()}개 문서");
                
                var allResults = _markingDetector.DetectAllMarkings(workspace, MarkingThreshold);

                int totalDocuments = allResults.Count;
                int totalAreas = 0;
                int totalMarked = 0;

                foreach (var kvp in allResults)
                {
                    totalAreas += kvp.Value.Count;
                    totalMarked += kvp.Value.Count(r => r.IsMarked);
                }

                var message = $"전체 문서 마킹 감지 완료\n\n" +
                             $"처리된 문서: {totalDocuments}개\n" +
                             $"총 영역: {totalAreas}개\n" +
                             $"마킹 감지: {totalMarked}개\n" +
                             $"미마킹: {totalAreas - totalMarked}개\n\n" +
                             $"임계값: {MarkingThreshold}";

                Logger.Instance.Info($"전체 문서 마킹 감지 완료: {totalMarked}/{totalAreas}개 마킹 감지");
                MessageBox.Show(message, "전체 마킹 감지 완료", MessageBoxButton.OK, MessageBoxImage.Information);

                // 현재 선택된 문서의 결과 표시
                if (SelectedDocument != null && allResults.TryGetValue(SelectedDocument.ImageId, out var currentResults))
                {
                    CurrentMarkingResults = currentResults;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.Error("전체 마킹 감지 실패", ex);
                MessageBox.Show($"전체 마킹 감지 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

