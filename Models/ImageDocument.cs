using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SimpleOverlayEditor.Models
{
    public class ImageDocument : INotifyPropertyChanged
    {
        private string _imageId = Guid.NewGuid().ToString();
        private string _sourcePath = string.Empty;
        private int _imageWidth;
        private int _imageHeight;
        private DateTime _lastEditedAt = DateTime.Now;
        private AlignmentInfo? _alignmentInfo;

        public string ImageId
        {
            get => _imageId;
            set { _imageId = value; OnPropertyChanged(); }
        }

        public string SourcePath
        {
            get => _sourcePath;
            set { _sourcePath = value; OnPropertyChanged(); }
        }

        public int ImageWidth
        {
            get => _imageWidth;
            set { _imageWidth = value; OnPropertyChanged(); }
        }

        public int ImageHeight
        {
            get => _imageHeight;
            set { _imageHeight = value; OnPropertyChanged(); }
        }

        [Obsolete("오버레이는 이제 Workspace.Template에서 관리됩니다. 이 속성은 하위 호환성을 위해 유지되지만 사용하지 마세요.")]
        public ObservableCollection<RectangleOverlay> Overlays { get; set; } = new();

        public DateTime LastEditedAt
        {
            get => _lastEditedAt;
            set { _lastEditedAt = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 이미지 정렬 정보
        /// </summary>
        public AlignmentInfo? AlignmentInfo
        {
            get => _alignmentInfo;
            set { _alignmentInfo = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// 표시 및 리딩에 사용할 이미지 경로를 반환합니다.
        /// 정렬된 이미지가 있으면 정렬된 이미지를, 없으면 원본을 반환합니다.
        /// </summary>
        public string GetImagePathForUse()
        {
            if (AlignmentInfo?.Success == true && !string.IsNullOrEmpty(AlignmentInfo.AlignedImagePath))
            {
                return AlignmentInfo.AlignedImagePath;
            }
            return SourcePath;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}


