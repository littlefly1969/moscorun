using Microsoft.AspNetCore.Http.HttpResults;
using MoscoRun.Api.Hosting;

namespace MoscoRun.Api.Features.System;

public sealed record SystemInfo(string Version);

internal static class SystemEndpoints
{
    public static RouteGroupBuilder MapSystemEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/system/info", GetInfo)
            .AllowAnonymous()
            .WithTags("Sistema")
            .WithName("GetSystemInfo");
        return api;
    }

    internal static Ok<SystemInfo> GetInfo() => TypedResults.Ok(new SystemInfo(AppInfo.Version));
}
