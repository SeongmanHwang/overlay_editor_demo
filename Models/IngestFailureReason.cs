using System;

namespace SimpleOverlayEditor.Models
{
    [Flags]
    public enum IngestFailureReason
    {
        None = 0,
        AlignFailed = 1 << 0,
        BarcodeFailed = 1 << 1,
        CombinedIdMissing = 1 << 2,
        MissingFile = 1 << 3
    }
}