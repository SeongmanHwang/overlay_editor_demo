namespace SimpleOverlayEditor.Models
{
    public class IngestDocState
    {
        public bool? AlignedOk { get; set; }
        public bool? BarcodeOk { get; set; }
        public bool? CombinedIdOk { get; set; }
        public bool? MissingFile { get; set; }
        public IngestFailureReason FailureReasons { get; set; }
        public bool? QuarantineOverride { get; set; }

        public bool IsQuarantined => QuarantineOverride ?? FailureReasons != IngestFailureReason.None;

        public bool IsUnknown =>
            !AlignedOk.HasValue &&
            !BarcodeOk.HasValue &&
            !CombinedIdOk.HasValue &&
            !MissingFile.HasValue &&
            FailureReasons == IngestFailureReason.None &&
            !QuarantineOverride.HasValue;

        public void SetAlignedOk(bool? value)
        {
            AlignedOk = value;
            UpdateFailureReason(value, IngestFailureReason.AlignFailed);
        }

        public void SetBarcodeOk(bool? value)
        {
            BarcodeOk = value;
            UpdateFailureReason(value, IngestFailureReason.BarcodeFailed);
        }

        public void SetCombinedIdOk(bool? value)
        {
            CombinedIdOk = value;
            UpdateFailureReason(value, IngestFailureReason.CombinedIdMissing);
        }

        public void SetMissingFile(bool? value)
        {
            MissingFile = value;
            UpdateFailureReason(value, IngestFailureReason.MissingFile);
        }

        private void UpdateFailureReason(bool? value, IngestFailureReason reason)
        {
            if (value == true)
            {
                FailureReasons &= ~reason;
            }
            else if (value == false)
            {
                FailureReasons |= reason;
            }
        }
    }
}