namespace Domain.Interfaces
{
    /// <summary>
    /// Generic Excel eksporter — har xil hisobotlar uchun qayta ishlatish mumkin.
    /// Implementatsiya ClosedXML yoki MiniExcel kabi kutubxona orqali bo'ladi.
    /// </summary>
    public interface IExcelReportExporter
    {
        /// <summary>
        /// <paramref name="rows"/> ichidagi har bir element uchun bitta satr yozadi.
        /// IAsyncEnumerable orqali stream qilinadi.
        /// </summary>
        Task ExportAsync<T>(
            string sheetName,
            IReadOnlyList<ExcelColumn<T>> columns,
            IAsyncEnumerable<T> rows,
            Stream output,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Bitta Excel ustun ta'rifi.
    /// </summary>
    public sealed class ExcelColumn<T>
    {
        public required string Header { get; init; }

        public required Func<T, object?> ValueSelector { get; init; }

        /// <summary>Excel format ifodasi: <c>"#,##0.00"</c>, <c>"yyyy-MM-dd HH:mm"</c> va h.k.</summary>
        public string? NumberFormat { get; init; }

        /// <summary>Ustun kengligi (0 — auto-fit).</summary>
        public double Width { get; init; }
    }
}
