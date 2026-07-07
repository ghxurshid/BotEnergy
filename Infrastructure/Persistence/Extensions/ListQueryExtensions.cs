using System.Collections.Concurrent;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Domain.Attributes;
using Domain.Dtos.Base;
using Microsoft.EntityFrameworkCore;

namespace Persistence.Extensions
{
    /// <summary>
    /// Barcha list endpointlari uchun yagona sort + qidiruv + filtr qoidasi (DRY).
    /// <list type="bullet">
    ///   <item>Search — BARCHA string ustunlar bo'yicha case-insensitive ILIKE (OR bilan). Sezgir maydonlar <see cref="NotSearchableAttribute"/> bilan chiqariladi.</item>
    ///   <item>Filters — aniq-moslik (equality) filtri: bool / enum / raqamli ustunlar uchun ("field:value" ko'rinishida). AND bilan birlashadi.</item>
    ///   <item>Sort — faqat BITTA tanlangan ustun bo'yicha. Nomi noto'g'ri/bo'sh bo'lsa Id ASC (+ stabil paginatsiya uchun Id tiebreaker).</item>
    /// </list>
    /// Reflection natijalari tur bo'yicha keshlanadi.
    /// </summary>
    public static class ListQueryExtensions
    {
        private static readonly ConcurrentDictionary<Type, PropertyInfo[]> SearchableCache = new();
        private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, PropertyInfo>> ScalarCache = new();

        private static readonly MethodInfo ILikeMethod = typeof(NpgsqlDbFunctionsExtensions)
            .GetMethod(nameof(NpgsqlDbFunctionsExtensions.ILike),
                new[] { typeof(DbFunctions), typeof(string), typeof(string) })!;

        /// <summary>Search (ILIKE) + Filters (equality) + Sort (bitta ustun / default Id ASC) ni birgalikda qo'llaydi.</summary>
        public static IQueryable<T> ApplyListQuery<T>(this IQueryable<T> query, PaginationParams param)
            => query
                .ApplySearch(param.Search)
                .ApplyFilters(param.Filters)
                .ApplySort(param.SortBy, param.SortDir);

        /// <summary>Barcha (sezgir bo'lmagan) string ustunlar bo'yicha ILIKE OR filtri.</summary>
        public static IQueryable<T> ApplySearch<T>(this IQueryable<T> query, string? search)
        {
            if (string.IsNullOrWhiteSpace(search))
                return query;

            var props = SearchableCache.GetOrAdd(typeof(T), static t => t
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType == typeof(string)
                            && p.GetCustomAttribute<NotSearchableAttribute>() is null)
                .ToArray());

            if (props.Length == 0)
                return query;

            var pattern = Expression.Constant($"%{EscapeLike(search.Trim())}%", typeof(string));
            var efFunctions = Expression.Constant(EF.Functions, typeof(DbFunctions));
            var parameter = Expression.Parameter(typeof(T), "x");

            Expression? body = null;
            foreach (var prop in props)
            {
                var access = Expression.Property(parameter, prop);
                var ilike = Expression.Call(ILikeMethod, efFunctions, access, pattern);
                body = body is null ? ilike : Expression.OrElse(body, ilike);
            }

            var predicate = Expression.Lambda<Func<T, bool>>(body!, parameter);
            return query.Where(predicate);
        }

        /// <summary>
        /// Aniq-moslik (equality) filtri. Har bir element "field:value" ko'rinishida.
        /// bool / enum (int yoki nom) / raqamli ustunlar uchun. Noto'g'ri element jimgina o'tkazib yuboriladi.
        /// </summary>
        public static IQueryable<T> ApplyFilters<T>(this IQueryable<T> query, IEnumerable<string>? filters)
        {
            if (filters is null)
                return query;

            var map = GetScalarMap(typeof(T));

            foreach (var raw in filters)
            {
                if (string.IsNullOrWhiteSpace(raw))
                    continue;

                var idx = raw.IndexOf(':');
                if (idx <= 0 || idx == raw.Length - 1)
                    continue;

                var field = raw[..idx].Trim().ToLowerInvariant();
                var value = raw[(idx + 1)..].Trim();

                if (!map.TryGetValue(field, out var prop))
                    continue;
                if (!TryParseValue(prop.PropertyType, value, out var typed))
                    continue;

                query = BuildEquals(query, prop, typed);
            }

            return query;
        }

        /// <summary>
        /// Bitta ustun bo'yicha sort. <paramref name="sortBy"/> topilmasa yoki bo'sh bo'lsa — Id ASC (default DB tartibi).
        /// </summary>
        public static IQueryable<T> ApplySort<T>(this IQueryable<T> query, string? sortBy, ListSortDirection dir)
        {
            var map = GetScalarMap(typeof(T));

            PropertyInfo? prop = null;
            if (!string.IsNullOrWhiteSpace(sortBy))
                map.TryGetValue(sortBy.Trim().ToLowerInvariant(), out prop);

            // Default / fallback — Id ASC.
            if (prop is null)
            {
                if (map.TryGetValue("id", out var idProp))
                    return BuildOrderBy(query, idProp, ListSortDirection.Asc);
                return query; // Id yo'q bo'lsa (kutilmaydi) — tartibsiz.
            }

            return BuildOrderBy(query, prop, dir);
        }

        /// <summary>Tur uchun sort/filtrga yaroqli skalyar ustunlar xaritasi (nom → PropertyInfo, lower-case kalit).</summary>
        private static IReadOnlyDictionary<string, PropertyInfo> GetScalarMap(Type type)
            => ScalarCache.GetOrAdd(type, static t => t
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => IsScalar(p.PropertyType))
                .ToDictionary(p => p.Name.ToLowerInvariant(), p => p));

        private static IQueryable<T> BuildOrderBy<T>(IQueryable<T> query, PropertyInfo prop, ListSortDirection dir)
        {
            var ordered = OrderByCall(query, prop, dir, isFirst: true);

            // Stabil paginatsiya (Skip/Take) uchun Id bo'yicha yashirin tiebreaker —
            // takrorlanuvchi qiymatli ustunlarda sahifalar aralashib ketmasligi uchun.
            // Bu tanlangan ustun ko'rinadigan tartibini o'zgartirmaydi.
            if (!prop.Name.Equals("Id", StringComparison.OrdinalIgnoreCase))
            {
                var idProp = typeof(T).GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
                if (idProp is not null)
                    ordered = OrderByCall(ordered, idProp, ListSortDirection.Asc, isFirst: false);
            }

            return ordered;
        }

        private static IQueryable<T> OrderByCall<T>(IQueryable<T> query, PropertyInfo prop, ListSortDirection dir, bool isFirst)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var access = Expression.Property(parameter, prop);
            var keySelector = Expression.Lambda(access, parameter);

            var methodName = isFirst
                ? (dir == ListSortDirection.Desc ? "OrderByDescending" : "OrderBy")
                : (dir == ListSortDirection.Desc ? "ThenByDescending" : "ThenBy");

            var call = Expression.Call(
                typeof(Queryable), methodName,
                new[] { typeof(T), prop.PropertyType },
                query.Expression, Expression.Quote(keySelector));

            return query.Provider.CreateQuery<T>(call);
        }

        private static IQueryable<T> BuildEquals<T>(IQueryable<T> query, PropertyInfo prop, object? typed)
        {
            var parameter = Expression.Parameter(typeof(T), "x");
            var target = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

            Expression valueExpr = Expression.Constant(typed, target);
            if (prop.PropertyType != target)
                valueExpr = Expression.Convert(valueExpr, prop.PropertyType);

            var body = Expression.Equal(Expression.Property(parameter, prop), valueExpr);
            var predicate = Expression.Lambda<Func<T, bool>>(body, parameter);
            return query.Where(predicate);
        }

        private static bool TryParseValue(Type propType, string value, out object? typed)
        {
            typed = null;
            var target = Nullable.GetUnderlyingType(propType) ?? propType;
            try
            {
                if (target.IsEnum)
                {
                    if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var iv))
                    {
                        typed = Enum.ToObject(target, iv);
                        return Enum.IsDefined(target, typed);
                    }
                    typed = Enum.Parse(target, value, ignoreCase: true);
                    return true;
                }
                if (target == typeof(bool)) { typed = bool.Parse(value); return true; }
                if (target == typeof(string)) { typed = value; return true; }
                typed = Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsScalar(Type type)
        {
            var t = Nullable.GetUnderlyingType(type) ?? type;
            return t.IsPrimitive
                || t.IsEnum
                || t == typeof(string)
                || t == typeof(decimal)
                || t == typeof(DateTime)
                || t == typeof(DateTimeOffset)
                || t == typeof(Guid)
                || t == typeof(TimeSpan);
        }

        /// <summary>ILIKE maxsus belgilarini ekranlash (default escape char — backslash).</summary>
        private static string EscapeLike(string input) => input
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
    }
}
