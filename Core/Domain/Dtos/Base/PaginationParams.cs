namespace Domain.Dtos.Base
{
    /// <summary>Bitta ustun bo'yicha sort yo'nalishi.</summary>
    public enum ListSortDirection
    {
        Asc = 0,
        Desc = 1
    }

    /// <summary>
    /// Barcha list (ro'yxat) endpointlari uchun yagona so'rov parametrlari:
    /// paginatsiya + bitta ustun bo'yicha sort + barcha maydonlar bo'yicha "like" qidiruv.
    /// Hech qanday sort/search berilmasa — default holatda ID bo'yicha ASC (DB tartibi).
    /// </summary>
    public class PaginationParams
    {
        private const int MaxPageSize = 100;

        private int _pageSize = 20;
        private int _pageNumber = 1;

        public int PageNumber
        {
            get => _pageNumber;
            set => _pageNumber = value < 1 ? 1 : value;
        }

        public int PageSize
        {
            get => _pageSize;
            set => _pageSize = value < 1
                ? 1
                : (value > MaxPageSize ? MaxPageSize : value);
        }

        /// <summary>
        /// Sort qilinadigan yagona ustun nomi (entity property nomi, case-insensitive).
        /// Bo'sh yoki noto'g'ri bo'lsa — Id bo'yicha default sortga qaytadi.
        /// </summary>
        public string? SortBy { get; set; }

        /// <summary>Sort yo'nalishi (Asc/Desc). Default — Asc.</summary>
        public ListSortDirection SortDir { get; set; } = ListSortDirection.Asc;

        /// <summary>
        /// Barcha string ustunlar bo'yicha (case-insensitive "like") qidiruv matni.
        /// Bo'sh bo'lsa — filtrsiz.
        /// </summary>
        public string? Search { get; set; }

        /// <summary>
        /// Aniq-moslik (equality) filtrlari — bool / enum / raqamli ustunlar uchun.
        /// Har bir element "field:value" ko'rinishida (masalan: "isActive:true", "deviceType:2").
        /// Query: <c>?Filters=isActive:true&amp;Filters=deviceType:2</c>. Bir nechtasi AND bilan birlashadi.
        /// Noto'g'ri nom/qiymat jimgina o'tkazib yuboriladi.
        /// </summary>
        public List<string>? Filters { get; set; }
    }
}
