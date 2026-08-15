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
            // /api/auth/me answers 401 *by design* when nobody is signed in — that is its
            // answer to "who is this?", not a failure. AuthApiClient.GetMeAsync expects it
            // and returns null; MainLayout's gate then redirects once, deliberately.
            // Redirecting from here as well means a forceLoad reload fires *underneath*
            // that check, which is where the second 0-100% progress bar came from.
            if (request.RequestUri?.AbsolutePath.EndsWith("/api/auth/me", StringComparison.OrdinalIgnoreCase) == true)
            {
                logger.LogDebug("401 from /api/auth/me — nobody signed in; leaving the redirect to the layout.");
                return response;
            }

            // Everything else: a 401 means the session went away mid-use (or a page reached
            // for data it is not allowed to have). Bounce to /login — unless already there,
            // which would reload forever, since forceLoad does not no-op on a same-URL nav
            // the way an SPA navigation does.
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
