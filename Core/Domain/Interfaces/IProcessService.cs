using Domain.Dtos.Base;
using Domain.Dtos.Process;

namespace Domain.Interfaces
{
    public interface IProcessService
    {
        Task<GenericDto<StartProcessResultDto>> StartAsync(StartProcessDto dto);
        Task<GenericDto<ProcessControlResultDto>> StopByUserAsync(ProcessControlDto dto);
        Task<GenericDto<ProcessControlResultDto>> PauseAsync(ProcessControlDto dto);
        Task<GenericDto<ProcessControlResultDto>> ResumeAsync(ProcessControlDto dto);

        // Qurilma tomondan keladigan voqealar (RabbitMQ → DeviceEventConsumer chaqiradi)
        Task<GenericDto<ProcessTelemetryResultDto>> ReportTelemetryAsync(ProcessTelemetryDto dto);
        Task<GenericDto<DeviceProcessReportResultDto>> ReportDeviceFinishedAsync(DeviceProcessReportDto dto);
    }
}
