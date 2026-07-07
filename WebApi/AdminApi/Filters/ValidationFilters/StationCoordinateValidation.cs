namespace AdminApi.Filters.ValidationFilters
{
    /// <summary>Koordinata diapazon tekshiruvi — Create/Update filtrlarida umumiy.</summary>
    public static class StationCoordinateValidation
    {
        /// <summary>Xatolik matni qaytaradi; hammasi joyida bo'lsa null.</summary>
        public static string? Validate(decimal latitude, decimal longitude)
        {
            if (latitude < -90m || latitude > 90m)
                return "Kenglik (latitude) -90 dan 90 gacha bo'lishi kerak.";
            if (longitude < -180m || longitude > 180m)
                return "Uzunlik (longitude) -180 dan 180 gacha bo'lishi kerak.";
            return null;
        }
    }
}
