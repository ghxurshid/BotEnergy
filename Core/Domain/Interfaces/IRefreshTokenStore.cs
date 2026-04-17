namespace Domain.Interfaces
{
    public interface IRefreshTokenStore
    {
        Task SaveAsync(string token, long userId, TimeSpan expiry);
        Task<long?> GetUserIdAsync(string token);
        Task RevokeAsync(string token);
    }
}
