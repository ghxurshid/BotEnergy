namespace DeviceApi.Clients
{
    public interface IUserApiClient
    {
        Task DeviceConnectAsync(string serialNumber, string sessionToken, string productType);
        Task DeviceProgressAsync(string serialNumber, string sessionToken, decimal quantity, decimal totalQuantity);
        Task DeviceFinishAsync(string serialNumber, string sessionToken, decimal finalQuantity);
    }
}
