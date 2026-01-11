using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SimpleOverlayEditor.Models
{
    /// <summary>
    /// 프로그램의 전반적인 상태 및 진행 상황을 나타냅니다.
    /// 이미지 로드 및 리딩 작업은 Session에 저장됩니다.
    /// </summary>
    public class Workspace : INotifyPropertyChanged
    {
        private string _inputFolderPath = string.Empty;
        private string? _selectedDocumentId;
        private OmrTemplate _template = new OmrTemplate();

        public string InputFolderPath
        {
            get => _inputFolderPath;
            set { _inputFolderPath = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 현재 선택된 문서 ID (Session.Documents에서 찾을 수 있음)
        /// </summary>
        public string? SelectedDocumentId
        {
            get => _selectedDocumentId;
            set { _selectedDocumentId = value; OnPropertyChanged(); }
        }

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

        // Documents, MarkingResults, BarcodeResults는 Session으로 분리됨

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}


