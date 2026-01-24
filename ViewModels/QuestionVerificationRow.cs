namespace SimpleOverlayEditor.ViewModels
{
    public record QuestionVerificationRow(
        int QuestionNumber,
        string MarkingStatus,
        int? SelectedOption,
        string? ScoreName,
        double? ScoreValue)
    {
        public string ScoreValueDisplay => ScoreValue.HasValue ? ScoreValue.Value.ToString("0.##") : "";
    }
}

