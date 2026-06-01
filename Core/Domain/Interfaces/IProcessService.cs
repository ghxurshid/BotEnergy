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

        // Qurilma tomondan keladigan voqealar (MQTT handler'lar to'g'ridan-to'g'ri chaqiradi)
        Task<GenericDto<ProcessTelemetryResultDto>> ReportTelemetryAsync(ProcessTelemetryDto dto);
        Task<GenericDto<DeviceProcessReportResultDto>> ReportDeviceFinishedAsync(DeviceProcessReportDto dto);

        /// <summary>
        /// Qurilma pauzani inersiya bilan yakunlab tasdiqladi — statusni Paused ga o'tkazadi.
        /// Balans yechilmaydi (process tugamadi). Resume qilinishi mumkin.
        /// </summary>
        Task<GenericDto<ProcessControlResultDto>> ReportDevicePausedAsync(DeviceProcessPausedDto dto);

        /// <summary>
        /// Watchdog — qurilmadan stop/pause tasdig'i (yoki telemetry) belgilangan vaqtdan beri kelmagan
        /// aktiv jarayonlarni majburan yakunlaydi. IdleSessionCleanerService davriy chaqiradi.
        /// </summary>
        Task FinalizeStalledProcessesAsync();
    }
}
