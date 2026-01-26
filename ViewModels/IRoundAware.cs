namespace SimpleOverlayEditor.ViewModels
{
    public interface IRoundAware
    {
        void OnRoundChanged(string? previousRound, string? currentRound);
    }
}