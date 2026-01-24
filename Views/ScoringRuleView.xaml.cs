using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace SimpleOverlayEditor.Views
{
    /// <summary>
    /// ScoringRuleView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class ScoringRuleView : UserControl
    {
        private bool _showingIntegerValidationMessage;

        public ScoringRuleView()
        {
            InitializeComponent();
        }

        private void ScoresDataGrid_ValidationError(object sender, ValidationErrorEventArgs e)
        {
            // 유효성 에러가 추가될 때만 처리
            if (e.Action != ValidationErrorEventAction.Added) return;
            if (_showingIntegerValidationMessage) return;

            _showingIntegerValidationMessage = true;

            try
            {
                MessageBox.Show(
                    "점수는 1 이상의 정수만 입력 가능합니다.\n\n" +
                    "예: 1, 30\n\n" +
                    "소수점/0/음수는 허용되지 않습니다.",
                    "점수 입력 오류 (직접 입력)",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            finally
            {
                // 같은 포커스 이동 과정에서 ValidationError가 연속 발생할 수 있어, 다음 디스패치에서 플래그 해제
                Dispatcher.BeginInvoke(new Action(() => _showingIntegerValidationMessage = false),
                    DispatcherPriority.Background);
            }
        }

        private void ScoresDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit) return;

            // 점수 컬럼(1번~12번)만 검사: 첫 컬럼(문항)은 스킵
            if (e.Column.DisplayIndex == 0) return;

            if (e.EditingElement is not TextBox tb) return;

            var text = tb.Text?.Trim() ?? "";

            // 빈 값, 정수 아님, 0 이하 금지
            if (!int.TryParse(text, out var value) || value <= 0)
            {
                if (!_showingIntegerValidationMessage)
                {
                    _showingIntegerValidationMessage = true;
                    try
                    {
                        MessageBox.Show(
                            "점수는 1 이상의 정수만 입력 가능합니다.\n\n" +
                            "예: 1, 30\n\n" +
                            "소수점/0/음수는 허용되지 않습니다.",
                            "점수 입력 오류 (직접 입력)",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                    finally
                    {
                        Dispatcher.BeginInvoke(new Action(() => _showingIntegerValidationMessage = false),
                            DispatcherPriority.Background);
                    }
                }

                // 커밋 취소(값이 Scores에 반영되지 않도록)
                e.Cancel = true;
            }
        }
    }
}
