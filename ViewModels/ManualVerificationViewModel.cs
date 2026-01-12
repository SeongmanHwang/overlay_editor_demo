using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SimpleOverlayEditor.Models;

namespace SimpleOverlayEditor.ViewModels
{
    /// <summary>
    /// 수기 검산 화면용 ViewModel입니다.
    /// </summary>
    public class ManualVerificationViewModel : INotifyPropertyChanged
    {
        private readonly NavigationViewModel _navigation;

        public ManualVerificationViewModel(NavigationViewModel navigation)
        {
            _navigation = navigation ?? throw new System.ArgumentNullException(nameof(navigation));
            
            NavigateToHomeCommand = new RelayCommand(() =>
            {
                _navigation.NavigateTo(ApplicationMode.Home);
            });
        }

        /// <summary>
        /// 홈 화면으로 이동
        /// </summary>
        public ICommand NavigateToHomeCommand { get; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
