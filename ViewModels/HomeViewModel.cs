using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SimpleOverlayEditor.Models;

namespace SimpleOverlayEditor.ViewModels
{
    /// <summary>
    /// 홈 화면용 ViewModel입니다.
    /// </summary>
    public class HomeViewModel : INotifyPropertyChanged
    {
        private readonly NavigationViewModel _navigation;

        public HomeViewModel(NavigationViewModel navigation)
        {
            _navigation = navigation ?? throw new System.ArgumentNullException(nameof(navigation));
            
            NavigateToTemplateEditCommand = new RelayCommand(() =>
            {
                _navigation.NavigateTo(Models.ApplicationMode.TemplateEdit);
            });

            NavigateToMarkingCommand = new RelayCommand(() =>
            {
                _navigation.NavigateTo(Models.ApplicationMode.Marking);
            });

            NavigateToRegistryCommand = new RelayCommand(() =>
            {
                _navigation.NavigateTo(Models.ApplicationMode.Registry);
            });

            NavigateToGradingCommand = new RelayCommand(() =>
            {
                _navigation.NavigateTo(Models.ApplicationMode.Grading);
            });

            NavigateToScoringRuleCommand = new RelayCommand(() =>
            {
                _navigation.NavigateTo(Models.ApplicationMode.ScoringRule);
            });

            NavigateToManualVerificationCommand = new RelayCommand(() =>
            {
                _navigation.NavigateTo(Models.ApplicationMode.ManualVerification);
            });
        }

        /// <summary>
        /// 템플릿 편집 모드로 이동
        /// </summary>
        public ICommand NavigateToTemplateEditCommand { get; }

        /// <summary>
        /// 마킹 리딩 모드로 이동
        /// </summary>
        public ICommand NavigateToMarkingCommand { get; }

        /// <summary>
        /// 수험생 명렬 관리 모드로 이동
        /// </summary>
        public ICommand NavigateToRegistryCommand { get; }

        /// <summary>
        /// 채점 및 성적 처리 모드로 이동
        /// </summary>
        public ICommand NavigateToGradingCommand { get; }

        /// <summary>
        /// 정답 및 배점 모드로 이동
        /// </summary>
        public ICommand NavigateToScoringRuleCommand { get; }

        /// <summary>
        /// 수기 검산 모드로 이동
        /// </summary>
        public ICommand NavigateToManualVerificationCommand { get; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}










