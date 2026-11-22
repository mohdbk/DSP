using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dsp.AiOrchestrator.Application;
using Microsoft.AspNetCore.Http;

namespace Dsp.AiOrchestrator.Infrastructure;

/// <summary>Calls the public E-Services API with the user's own token (forwarded by BearerForwardingHandler).</summary>
public sealed class EServicesApiClient(HttpClient http) : IEServicesApi
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<ApplicationView> GetApplicationAsync(Guid id, CancellationToken ct) =>
        await http.GetFromJsonAsync<ApplicationView>($"/api/eservices/applications/{id}", Json, ct)
        ?? throw new InvalidOperationException("Application not found.");

    public async Task<ServiceView> GetServiceAsync(string code, CancellationToken ct) =>
        await http.GetFromJsonAsync<ServiceView>($"/api/eservices/services/{code}", Json, ct)
        ?? throw new InvalidOperationException("Service not found.");

    public async Task<SaveOutcome> SaveDataAsync(Guid id, IReadOnlyDictionary<string, string?> values,
        CancellationToken ct)
    {
        var response = await http.PutAsJsonAsync($"/api/eservices/applications/{id}/data", new { values }, Json, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SaveOutcome>(Json, ct)
               ?? throw new InvalidOperationException("Empty save response.");
    }

    public async Task<SubmitOutcome> SubmitAsync(Guid id, CancellationToken ct)
    {
        var response = await http.PostAsync($"/api/eservices/applications/{id}/submit", null, ct);
        if (response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>(Json, ct);
            return new SubmitOutcome(null, problem?.Errors ?? [new FieldErrorView("", "Some details are invalid.")]);
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>(Json, ct);
            return new SubmitOutcome(null, [new FieldErrorView("", problem?.Detail ?? "The application can't be submitted.")]);
        }

        response.EnsureSuccessStatusCode();
        return new SubmitOutcome(await response.Content.ReadFromJsonAsync<ApplicationView>(Json, ct), []);
    }

    private sealed record ValidationProblem(string? Detail, List<FieldErrorView>? Errors);
}

/// <summary>Forwards the caller's Authorization header so the agent acts with the user's permissions only.</summary>
public sealed class BearerForwardingHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var auth = accessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(auth)) request.Headers.TryAddWithoutValidation("Authorization", auth);
        return base.SendAsync(request, ct);
    }
}
