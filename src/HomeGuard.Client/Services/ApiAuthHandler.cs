// HomeGuard.Client/Services/ApiAuthHandler.cs
using System.Net;
using Microsoft.AspNetCore.Components;

namespace HomeGuard.Client.Services;

public sealed class ApiAuthHandler(NavigationManager nav) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        var response = await base.SendAsync(request, ct);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            nav.NavigateTo("/login", forceLoad: false);

        return response;
    }
}
