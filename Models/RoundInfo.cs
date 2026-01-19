using System;

namespace SimpleOverlayEditor.Models
{
    /// <summary>
    /// 회차 정보를 나타냅니다.
    /// </summary>
    public class RoundInfo
    {
        /// <summary>
        /// 회차 이름 (사용자가 입력한 원본 이름)
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 회차 생성 시각
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// 마지막 접근 시각 (회차 선택 시 업데이트)
        /// </summary>
        public DateTime LastAccessedAt { get; set; }
    }
}
