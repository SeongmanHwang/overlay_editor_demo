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

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}


