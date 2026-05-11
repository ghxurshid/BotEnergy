using ClosedXML.Excel;
using Domain.Interfaces;

namespace CommonConfiguration.Reporting
{
    /// <summary>
    /// ClosedXML asosidagi Excel eksporter.
    ///
    /// Eslatma: ClosedXML butun workbookni xotirada quradi (~5000 satr ≈ 5-10 MB).
    /// Bu hajmgacha optimal. Agar eksport hajmi 100k+ satrga yetsa,
    /// SAX-asosidagi kutubxona (MiniExcel, OpenXML SAX) ga o'tish tavsiya etiladi —
    /// faqat shu klassdagi implementatsiyani almashtirish kifoya.
    /// </summary>
    public sealed class ClosedXmlReportExporter : IExcelReportExporter
    {
        public async Task ExportAsync<T>(
            string sheetName,
            IReadOnlyList<ExcelColumn<T>> columns,
            IAsyncEnumerable<T> rows,
            Stream output,
            CancellationToken cancellationToken = default)
        {
            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add(sheetName);

            // Sarlavha satri
            for (var i = 0; i < columns.Count; i++)
            {
                var cell = sheet.Cell(1, i + 1);
                cell.Value = columns[i].Header;
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.LightGray;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            // Ma'lumot satrlari — IAsyncEnumerable orqali stream
            var rowIndex = 2;
            await foreach (var item in rows.WithCancellation(cancellationToken))
            {
                for (var i = 0; i < columns.Count; i++)
                {
                    var col = columns[i];
                    var value = col.ValueSelector(item);
                    var cell = sheet.Cell(rowIndex, i + 1);

                    SetCellValue(cell, value);

                    if (!string.IsNullOrEmpty(col.NumberFormat))
                        cell.Style.NumberFormat.Format = col.NumberFormat;
                }
                rowIndex++;
            }

            // Ustun kengliklari
            for (var i = 0; i < columns.Count; i++)
            {
                var col = columns[i];
                if (col.Width > 0)
                    sheet.Column(i + 1).Width = col.Width;
                else
                    sheet.Column(i + 1).AdjustToContents();
            }

            sheet.SheetView.FreezeRows(1);
            sheet.RangeUsed()?.SetAutoFilter();

            // Sinxron Save — ClosedXML async API yo'q. Stream HTTP response body bo'lishi mumkin.
            workbook.SaveAs(output);
        }

        private static void SetCellValue(IXLCell cell, object? value)
        {
            // Nullable<T> qiymatlari boxing chog'ida null yoki underlying T sifatida keladi.
            switch (value)
            {
                case null:
                    cell.Value = Blank.Value;
                    break;
                case DateTime dt:
                    cell.Value = dt;
                    break;
                case decimal d:
                    cell.Value = d;
                    break;
                case double db:
                    cell.Value = db;
                    break;
                case float f:
                    cell.Value = f;
                    break;
                case long l:
                    cell.Value = l;
                    break;
                case int i:
                    cell.Value = i;
                    break;
                case bool b:
                    cell.Value = b;
                    break;
                default:
                    cell.Value = value.ToString();
                    break;
            }
        }
    }
}
