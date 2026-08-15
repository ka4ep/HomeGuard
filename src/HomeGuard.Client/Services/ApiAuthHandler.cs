// HomeGuard.Client/Services/ApiAuthHandler.cs
using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace HomeGuard.Client.Services;

public sealed class ApiAuthHandler(NavigationManager nav, ILogger<ApiAuthHandler> logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        var response = await base.SendAsync(request, ct);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            // MainLayout calls /api/auth/me on every render to find out who's signed in,
            // and that endpoint answers 401 *by design* when nobody is — AuthApiClient.
            // GetMeAsync already expects and swallows that. Redirecting on it anyway is
            // where the loop comes from: already-on-/login gets "redirected" to /login,
            // forceLoad reloads for real (unlike a same-URL SPA nav, which no-ops), the
            // fresh MainLayout instance asks /api/auth/me again, 401 again — forever.
            // Skipping the redirect when we're already there breaks that without touching
            // the many call sites that never expected a 401 to be a normal answer.
            var loginUri = nav.ToAbsoluteUri("/login").ToString();
            if (!string.Equals(nav.Uri, loginUri, StringComparison.OrdinalIgnoreCase))
            {
                // The log line below is the whole point of this being a message here and
                // not just a browser Network-tab entry: the request/response timeline alone
                // does not say *which* 401 triggered the redirect, or from what page — this
                // does, in one line, every single time it happens.
                logger.LogWarning(
                    "401 from {Method} {Url} while on {CurrentUri} — redirecting to /login.",
                    request.Method, request.RequestUri, nav.Uri);

                // forceLoad: false only *starts* a client-side navigation — it does not stop
                // this method from returning the 401 straight back to the caller, which then
                // throws in EnsureSuccessStatusCode() before the navigation has swapped the
                // component tree out. That race is what "crit: Unhandled exception rendering
                // component" was: whichever page's data load loses it takes the WASM render
                // loop down with it, breaking every page after it too, not just the one that
                // 401'd. forceLoad: true does a real browser navigation, which tears down
                // this WASM instance outright — there is no race to lose.
                nav.NavigateTo("/login", forceLoad: true);
                await Task.Delay(Timeout.Infinite, ct);
            }
            else
            {
                logger.LogDebug(
                    "401 from {Method} {Url} while already on /login — not redirecting.",
                    request.Method, request.RequestUri);
            }
        }

        return response;
    }
}
