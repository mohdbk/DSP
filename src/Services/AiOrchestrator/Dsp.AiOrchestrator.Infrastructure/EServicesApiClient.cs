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
        await http.GetFromJsonAsync<ApplicationView>($"/api/v1/eservices/applications/{id}", Json, ct)
        ?? throw new InvalidOperationException("errors.application.notFound");

    public async Task<ServiceView> GetServiceAsync(string code, CancellationToken ct) =>
        await http.GetFromJsonAsync<ServiceView>($"/api/v1/eservices/services/{code}", Json, ct)
        ?? throw new InvalidOperationException("Service not found.");

    public async Task<SaveOutcome> SaveDataAsync(Guid id, IReadOnlyDictionary<string, string?> values,
        CancellationToken ct)
    {
        var response = await http.PutAsJsonAsync($"/api/v1/eservices/applications/{id}/data", new { values }, Json, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SaveOutcome>(Json, ct)
               ?? throw new InvalidOperationException("Empty save response.");
    }

    public async Task<SubmitOutcome> SubmitAsync(Guid id, CancellationToken ct)
    {
        var response = await http.PostAsync($"/api/v1/eservices/applications/{id}/submit", null, ct);
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

/// <summary>Forwards the caller's Authorization (and language) so the agent acts with the user's permissions only.</summary>
public sealed class BearerForwardingHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var headers = accessor.HttpContext?.Request.Headers;
        var auth = headers?.Authorization.ToString();
        if (!string.IsNullOrEmpty(auth)) request.Headers.TryAddWithoutValidation("Authorization", auth);

        // So E-Services returns validation messages in the user's language.
        var language = headers?.AcceptLanguage.ToString();
        if (!string.IsNullOrEmpty(language)) request.Headers.TryAddWithoutValidation("Accept-Language", language);
        return base.SendAsync(request, ct);
    }
}
