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
        {
            // forceLoad: false only *starts* a client-side navigation — it does not stop
            // this method from returning the 401 straight back to the caller, which then
            // throws in EnsureSuccessStatusCode() before the navigation has swapped the
            // component tree out. That race is what "crit: Unhandled exception rendering
            // component" was: whichever page's data load loses it takes the WASM render
            // loop down with it, breaking every page after it too, not just the one that
            // 401'd. forceLoad: true does a real browser navigation, which tears down this
            // WASM instance outright — there is no race to lose.
            nav.NavigateTo("/login", forceLoad: true);
            await Task.Delay(Timeout.Infinite, ct);
        }

        return response;
    }
}
