namespace Domain.Attributes
{
    /// <summary>
    /// Global list "like" qidiruvidan (<c>ApplyListQuery</c>) ushbu string ustunini chiqarib tashlaydi.
    /// Sezgir yoki qidiruvga ma'nosiz maydonlar uchun (parol hash, secret key, tokenlar).
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public sealed class NotSearchableAttribute : Attribute
    {
    }
}
