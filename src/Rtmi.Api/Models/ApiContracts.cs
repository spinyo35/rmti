namespace Rtmi.Api.Models;

public sealed record EventsQuery(string? Since, string? Type, int? FromMinute, int? ToMinute, int Take = 500);
public sealed record HealthReport(string Status, string Api, string Simulator, string Foundry);
public sealed record ApiResult<T>(bool Ok, T? Data, string? Error = null);
