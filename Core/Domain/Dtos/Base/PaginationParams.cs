namespace Domain.Dtos.Base
{
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
    }
}
