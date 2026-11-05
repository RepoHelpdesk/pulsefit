using Microsoft.AspNetCore.Diagnostics;
using PulseFit.Api.Services;

namespace PulseFit.Api.Auth;

/// <summary>Turns a rule that said no into a 422 the screen can show, and a missing record into a 404.</summary>
public class GymErrorHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception ex, CancellationToken ct)
    {
        (int status, string code, string message) = ex switch
        {
            GymRuleException r when r.Code == "forbidden" => (403, r.Code, r.Message),
            GymRuleException r => (422, r.Code, r.Message),
            NotFoundException n => (404, "not_found", n.Message),
            UnauthorizedAccessException => (401, "signed_out", "Sign in first."),
            _ => (0, "", ""),
        };
        if (status == 0) return false;
        http.Response.StatusCode = status;
        await http.Response.WriteAsJsonAsync(new { code, message }, ct);
        return true;
    }
}
