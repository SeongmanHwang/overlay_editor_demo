using System;

namespace SimpleOverlayEditor.Models
{
    /// <summary>
    /// 데이터 사용 정보 항목 (JSON 파일 또는 폴더)
    /// </summary>
    public class DataUsageItem
    {
        /// <summary>
        /// 항목 타입
        /// </summary>
        public enum ItemType
        {
            JsonFile,
            Folder
        }

        /// <summary>
        /// 항목명 (예: "템플릿", "스캔 이미지")
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 항목 타입 (JSON 파일 또는 폴더)
        /// </summary>
        public ItemType Type { get; set; }

        /// <summary>
        /// 경로 (폴더인 경우 전체 경로, JSON인 경우 파일 경로)
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// 마지막 변경 날짜/시간
        /// </summary>
        public DateTime? LastModified { get; set; }

        /// <summary>
        /// 표시용 경로 (폴더인 경우만, JSON은 null)
        /// </summary>
        public string? DisplayPath { get; set; }

        /// <summary>
        /// 폴더 크기 (바이트 단위, 폴더인 경우만)
        /// </summary>
        public long? Size { get; set; }

        /// <summary>
        /// 목록 내에서 가장 최근에 변경된 항목인지 여부
        /// </summary>
        public bool IsMostRecent { get; set; }
    }
}
