namespace SimpleOverlayEditor.ViewModels
{
    /// <summary>
    /// 네비게이션 전환 시 ViewModel이 진입/이탈 이벤트를 받을 수 있도록 합니다.
    /// </summary>
    public interface INavigationAware
    {
        /// <summary>
        /// 해당 화면으로 진입할 때 호출됩니다. (필요 시 파라미터 전달)
        /// </summary>
        void OnNavigatedTo(object? parameter);

        /// <summary>
        /// 해당 화면에서 이탈할 때 호출됩니다.
        /// </summary>
        void OnNavigatedFrom();
    }
}

