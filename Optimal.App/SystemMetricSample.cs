namespace Optimal.App;

internal sealed record SystemMetricSample(double CpuPercent, double MemoryPercent, double StoragePercent, double DiskActivityPercent, double? GpuPercent, double? GpuTemperature, string StorageText);
