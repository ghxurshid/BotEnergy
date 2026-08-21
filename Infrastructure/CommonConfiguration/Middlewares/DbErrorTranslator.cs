using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CommonConfiguration.Middlewares
{
    /// <summary>
    /// PostgreSQL cheklov xatolarini foydalanuvchi o'qiy oladigan javobga o'giradi.
    ///
    /// Nega kerak: servis qatlamida oldindan tekshiruv bo'lsa ham, poyga holati
    /// (ikki so'rov bir vaqtda) yoki e'tibordan chetda qolgan yangi indeks baribir
    /// bazadan xato keltiradi. Busiz ular "Kutilmagan xatolik yuz berdi" (500) bo'lib
    /// chiqadi va operator nima noto'g'ri ekanini bilmaydi.
    ///
    /// Ustun nomi indeks nomidan AJRATIB OLINADI (<c>IX_platform_users_mail</c> → <c>mail</c>),
    /// shuning uchun kelajakda qo'shiladigan indekslar ham ro'yxatga kiritilmasdan
    /// mazmunli xabar beradi — lug'atda bo'lmasa ustun nomi o'zi ishlatiladi.
    /// </summary>
    internal static class DbErrorTranslator
    {
        /// <summary>Ustun nomi → foydalanuvchiga ko'rinadigan nom.</summary>
        private static readonly Dictionary<string, string> ColumnLabels = new(StringComparer.OrdinalIgnoreCase)
        {
            ["mail"] = "elektron pochta",
            ["phone_number"] = "telefon raqam",
            ["phone_id"] = "telefon identifikatori",
            ["serial_number"] = "seriya raqami",
            ["inn"] = "INN",
            ["name"] = "nom",
            ["session_token"] = "sessiya tokeni",
            ["provider_order_id"] = "to'lov order id",
            ["idempotency_key"] = "idempotentlik kaliti",
            ["role_id_permission_id"] = "rol va permission juftligi",
            ["device_id"] = "qurilma",
        };

        /// <summary>
        /// Xatoni tanib, (status, xabar) qaytaradi. Tanimasa <c>null</c> —
        /// chaqiruvchi odatdagi 500 bilan davom etadi.
        /// </summary>
        public static (int Status, string Message)? Translate(Exception ex)
        {
            var pg = FindPostgresException(ex);
            if (pg is null)
                return null;

            return pg.SqlState switch
            {
                // unique_violation — bir xil qiymat allaqachon mavjud.
                "23505" => (409, $"Bu {DescribeConstraint(pg)} allaqachon band. Boshqa qiymat kiriting."),

                // foreign_key_violation — ko'rsatilgan bog'liq yozuv yo'q yoki hali ishlatilmoqda.
                "23503" => (409, "Bog'liq yozuv topilmadi yoki u boshqa joyda ishlatilmoqda."),

                // check_violation — format/qiymat cheklovi (masalan telefon formati).
                "23514" => (400, $"Kiritilgan qiymat talab qilingan formatga mos emas ({pg.ConstraintName})."),

                // not_null_violation — majburiy maydon bo'sh.
                "23502" => (400, $"Majburiy maydon to'ldirilmagan ({pg.ColumnName ?? "noma'lum"})."),

                _ => null
            };
        }

        /// <summary>
        /// <c>IX_platform_users_mail</c> → "elektron pochta".
        /// Indeks nomidan jadval prefiksini olib tashlab, qolgan qismni lug'atdan qidiradi.
        /// </summary>
        private static string DescribeConstraint(PostgresException pg)
        {
            var name = pg.ConstraintName;
            if (string.IsNullOrEmpty(name))
                return "qiymat";

            // EF konvensiyasi: IX_{jadval}_{ustun[_ustun...]} yoki AK_/PK_.
            var rest = name;
            foreach (var prefix in new[] { "IX_", "AK_", "PK_", "ix_" })
            {
                if (rest.StartsWith(prefix, StringComparison.Ordinal))
                {
                    rest = rest[prefix.Length..];
                    break;
                }
            }

            // Jadval nomi bilan boshlanadi — uni olib tashlaymiz.
            if (!string.IsNullOrEmpty(pg.TableName) &&
                rest.StartsWith(pg.TableName, StringComparison.OrdinalIgnoreCase))
            {
                rest = rest[pg.TableName.Length..].TrimStart('_');
            }

            if (string.IsNullOrEmpty(rest))
                return "qiymat";

            return ColumnLabels.TryGetValue(rest, out var label) ? label : rest.Replace('_', ' ');
        }

        private static PostgresException? FindPostgresException(Exception? ex)
        {
            while (ex is not null)
            {
                if (ex is PostgresException pg)
                    return pg;

                // DbUpdateException haqiqiy sababni InnerException'da olib yuradi.
                ex = ex is DbUpdateException dbEx ? dbEx.InnerException : ex.InnerException;
            }

            return null;
        }
    }
}
