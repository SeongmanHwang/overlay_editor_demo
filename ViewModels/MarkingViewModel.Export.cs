using System;
using System.Collections.Generic;
using System.Linq;
using ClosedXML.Excel;
using SimpleOverlayEditor.Models;

namespace SimpleOverlayEditor.ViewModels
{
    public partial class MarkingViewModel
    {
        /// <summary>
        /// OMR 결과를 Excel(.xlsx) 형식으로 저장합니다.
        /// </summary>
        private void ExportToXlsx(string filePath, IEnumerable<OmrSheetResult> results)
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("OMR Results");

            var headers = new[]
            {
                "파일명",
                "수험번호",
                "시각",
                "실",
                "순",
                "면접번호",
                "결합ID",
                "문항1",
                "문항2",
                "문항3",
                "문항4",
                "오류",
                "오류 메시지"
            };

            for (int c = 0; c < headers.Length; c++)
            {
                worksheet.Cell(1, c + 1).Value = headers[c];
            }

            var headerRange = worksheet.Range(1, 1, 1, headers.Length);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#EDEDED");
            headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            int row = 2;
            foreach (var r in results)
            {
                worksheet.Cell(row, 1).Value = r.ImageFileName ?? "";
                worksheet.Cell(row, 2).Value = r.StudentId ?? "";
                worksheet.Cell(row, 3).Value = r.Session ?? "";
                worksheet.Cell(row, 4).Value = r.RoomNumber ?? "";
                worksheet.Cell(row, 5).Value = r.OrderNumber ?? "";
                worksheet.Cell(row, 6).Value = r.InterviewId ?? "";
                worksheet.Cell(row, 7).Value = r.CombinedId ?? "";
                worksheet.Cell(row, 8).Value = r.Question1Marking?.ToString() ?? "";
                worksheet.Cell(row, 9).Value = r.Question2Marking?.ToString() ?? "";
                worksheet.Cell(row, 10).Value = r.Question3Marking?.ToString() ?? "";
                worksheet.Cell(row, 11).Value = r.Question4Marking?.ToString() ?? "";
                worksheet.Cell(row, 12).Value = r.HasErrors ? "예" : "아니오";
                worksheet.Cell(row, 13).Value = r.ErrorMessage ?? "";
                row++;
            }

            worksheet.SheetView.FreezeRows(1);
            worksheet.Range(1, 1, Math.Max(1, row - 1), headers.Length).SetAutoFilter();
            worksheet.Columns(1, headers.Length).AdjustToContents(1, 200);

            workbook.SaveAs(filePath);
        }
    }
}
