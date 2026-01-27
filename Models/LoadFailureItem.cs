namespace SimpleOverlayEditor.Models
{
    public class LoadFailureItem
    {
        public string ImageId { get; }
        public string FileName { get; }
        public IngestFailureReason FailureReasons { get; }
        public string FailureReasonSummary { get; }

        public LoadFailureItem(string imageId, string fileName, IngestFailureReason failureReasons, string failureReasonSummary)
        {
            ImageId = imageId;
            FileName = fileName;
            FailureReasons = failureReasons;
            FailureReasonSummary = failureReasonSummary;
        }
    }
}