using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SimpleOverlayEditor.Models
{
    public class Workspace : INotifyPropertyChanged
    {
        private string _inputFolderPath = string.Empty;
        private string? _selectedDocumentId;
        private OmrTemplate _template = new OmrTemplate();
        private Dictionary<string, List<MarkingResult>> _markingResults = new();

        public string InputFolderPath
        {
            get => _inputFolderPath;
            set { _inputFolderPath = value; OnPropertyChanged(); }
        }

        public ObservableCollection<ImageDocument> Documents { get; set; } = new();

        public string? SelectedDocumentId
        {
            get => _selectedDocumentId;
            set { _selectedDocumentId = value; OnPropertyChanged(); OnPropertyChanged(nameof(SelectedDocument)); }
        }

        public ImageDocument? SelectedDocument =>
            Documents.FirstOrDefault(d => d.ImageId == SelectedDocumentId);

        /// <summary>
        /// OMR 템플릿 (모든 이미지에 공통으로 적용)
        /// </summary>
        public OmrTemplate Template
        {
            get => _template;
            set
            {
                _template = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 문서별 마킹 감지 결과 (ImageId -> MarkingResult 리스트)
        /// </summary>
        public Dictionary<string, List<MarkingResult>> MarkingResults
        {
            get => _markingResults;
            set
            {
                _markingResults = value ?? new Dictionary<string, List<MarkingResult>>();
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}


