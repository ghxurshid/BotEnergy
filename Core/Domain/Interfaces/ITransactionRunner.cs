namespace Domain.Interfaces
{
    /// <summary>
    /// Bir nechta repository chaqiruvini bitta DB tranzaksiyada bajaradi
    /// (barcha repository'lar bitta scoped DbContext'ni bo'lishadi).
    /// Ichma-ich chaqirilsa tashqi tranzaksiyaga qo'shiladi (yangi ochmaydi).
    /// Action ichida exception bo'lsa — rollback.
    /// </summary>
    public interface ITransactionRunner
    {
        Task<T> RunAsync<T>(Func<Task<T>> action);
        Task RunAsync(Func<Task> action);
    }
}
