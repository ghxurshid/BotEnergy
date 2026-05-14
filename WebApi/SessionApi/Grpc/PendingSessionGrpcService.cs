using CommonConfiguration.Grpc;
using Domain.Interfaces;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace UserApi.Grpc
{
    /// <summary>
    /// DeviceApi'ga pending sessiya tokenini qaytaradi. Token solishtirish DeviceApi'da bo'ladi.
    /// Bu service faqat cache'dan o'qiydi — DB'ga tegmaydi. Sessiya DeviceApi tomonidan yaratiladi.
    /// </summary>
    public sealed class PendingSessionGrpcService : PendingSessionService.PendingSessionServiceBase
    {
        private readonly IPendingSessionStore _store;
        private readonly ILogger<PendingSessionGrpcService> _logger;

        public PendingSessionGrpcService(
            IPendingSessionStore store,
            ILogger<PendingSessionGrpcService> logger)
        {
            _store = store;
            _logger = logger;
        }

        public override async Task<GetPendingTokenResponse> GetPendingToken(
            GetPendingTokenRequest request,
            ServerCallContext context)
        {
            var entry = await _store.GetAsync(request.UserId);

            if (entry is null)
            {
                _logger.LogInformation(
                    "gRPC GetPendingToken: pending topilmadi userId={UserId}", request.UserId);
                return new GetPendingTokenResponse { Exists = false, SessionToken = string.Empty };
            }

            return new GetPendingTokenResponse
            {
                Exists = true,
                SessionToken = entry.SessionToken
            };
        }
    }
}
