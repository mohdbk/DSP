using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Dsp.EServices.Application;
using Dsp.EServices.Domain.Applications;
using Dsp.EServices.Domain.Catalog;
using Dsp.SharedKernel;

namespace Dsp.EServices.Infrastructure.Clients;

/// <summary>Renders a certificate in every enabled language and stores it through the Documents service.</summary>
public sealed class CertificateIssuer(HttpClient http, IServiceCatalog catalog, ILocalization localization) : ICertificateIssuer
{
    public async Task<Guid> IssueAsync(ServiceApplication application, CancellationToken ct)
    {
        var service = await catalog.FindVersionAsync(application.ServiceCode, application.ServiceVersion, ct)
                      ?? await catalog.FindPublishedAsync(application.ServiceCode, ct);
        var settings = await localization.GetSettingsAsync(ct);
        var languages = new List<(LanguageInfo Language, IMessages Messages)>();
        foreach (var language in settings.Enabled.OrderByDescending(l => l.IsDefault))
            languages.Add((language, await localization.GetMessagesAsync(language.Code, ct)));

        var html = Render(application, service, languages);
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(html));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/html");
        content.Add(file, "file", $"certificate-{application.Number}.html");
        content.Add(new StringContent(application.ApplicantId), "ownerId");
        content.Add(new StringContent("Certificate"), "kind");

        var response = await http.PostAsync("/internal/documents", content, ct);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<Created>(ct)
                      ?? throw new InvalidOperationException("Documents returned no body.");
        return created.Id;
    }

    private static string Render(ServiceApplication a, ServiceDefinition? service,
        IReadOnlyList<(LanguageInfo Language, IMessages Messages)> languages)
    {
        var primary = languages.Count > 0 ? languages[0] : (new LanguageInfo("en", LocalizedText.Of("en", "English"), "ltr", true, true, 0), EmptyMessages.Instance);

        // One text per language, each in its own direction, joined with a middle dot.
        string All(Func<IMessages, string> text) => string.Join(" · ", languages
            .Select(l => (l.Language.Direction, Text: text(l.Messages)))
            .Where(t => t.Text.Length > 0)
            .DistinctBy(t => t.Text)
            .Select(t => $"<bdi dir=\"{t.Direction}\">{E(t.Text)}</bdi>"));
        string Key(string key) => All(m => m.Format(key));
        string Map(LocalizedText? text) => All(m => m.Pick(text));

        var data = a.GetData();
        var rows = new StringBuilder();
        if (service is not null)
        {
            foreach (var field in service.AllFields.Where(f => f.Type is not ("checkbox" or "file")))
            {
                if (!data.TryGetValue(field.Key, out var value) || string.IsNullOrWhiteSpace(value)) continue;
                var option = field.Options?.FirstOrDefault(o => o.Value == value);
                var display = option is null ? $"<bdi>{E(value)}</bdi>" : Map(option.Label);
                rows.Append($"<tr><th>{Map(field.Label)}</th><td>{display}</td></tr>");
            }
        }

        var statements = string.Join("", languages.Select(l =>
            $"<p dir=\"{l.Language.Direction}\" lang=\"{E(l.Language.Code)}\">{E(l.Messages.Format("certificate.statement"))}</p>"));

        return $$"""
            <!doctype html>
            <html lang="{{E(primary.Language.Code)}}" dir="{{primary.Language.Direction}}"><head><meta charset="utf-8"><title>{{E(a.Number)}}</title>
            <style>
              body{font-family:Inter,Roboto,"Noto Sans Arabic",Arial,sans-serif;color:#1a1c20;margin:0;padding:48px;background:#fefcf4}
              .card{max-width:760px;margin:auto;background:#fff;border:2px solid #90702a;border-radius:12px;padding:40px}
              h1{color:#90702a;margin:8px 0 4px} table{width:100%;border-collapse:collapse;margin-top:24px}
              th,td{text-align:start;padding:8px;border-bottom:1px solid #dbcca1} th{width:40%;color:#4b4f58;font-weight:600}
              .meta{margin-top:24px;color:#4b4f58;font-size:13px}
            </style></head>
            <body><div class="card">
              <div>{{Key("entity.name")}}</div>
              <h1>{{Map(a.ServiceName)}}</h1>
              {{statements}}
              <table>
                <tr><th>{{Key("certificate.number")}}</th><td><bdi>{{E(a.Number)}}</bdi></td></tr>
                <tr><th>{{Key("certificate.holder")}}</th><td><bdi>{{E(a.ApplicantName)}}</bdi></td></tr>
                {{rows}}
                <tr><th>{{Key("certificate.issued")}}</th><td><bdi>{{DateTimeOffset.UtcNow:dd MMMM yyyy}}</bdi></td></tr>
              </table>
              <div class="meta">{{Key("certificate.disclaimer")}}</div>
            </div></body></html>
            """;
    }

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? "");

    private sealed record Created(Guid Id);
}
