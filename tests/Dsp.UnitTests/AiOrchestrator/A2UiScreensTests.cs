using System.Text.Json;
using Dsp.AiOrchestrator.Application;

namespace Dsp.UnitTests.AiOrchestrator;

public class A2UiScreensTests
{
    private static readonly A2UiScreens Screens = new(TestText.Messages(), TestText.Currencies);

    private static ServiceView Service() => new("SVC", "Trade Name Reservation", "d", 200, "AED",
    [
        new StepView("one", "Applicant", null,
        [
            new FieldView("fullName", "Full name", "text", true, null, null),
            new FieldView("emirate", "Emirate", "select", true, null, [new("DXB", "Dubai"), new("AUH", "Abu Dhabi")]),
            new FieldView("startDate", "Start date", "date", true, null, null),
            new FieldView("agreement", "Signed agreement", "file", true, null, null),
        ]),
    ]);

    private static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value, AgUiJson.Options);

    [Fact]
    public async Task The_next_missing_answers_become_a2ui_inputs_in_our_catalogue()
    {
        var app = new ApplicationView(Guid.NewGuid(), "N", "SVC", "Service", "Draft", new() { ["fullName"] = "Fatima" });

        var screen = Json((await Screens.NextAsync(Service(), app, [], default))!);

        Assert.Equal(3, screen.GetArrayLength());
        var create = screen[0].GetProperty("createSurface");
        Assert.Equal(A2UiCatalog.Id, create.GetProperty("catalogId").GetString());
        Assert.Equal("1.0", screen[0].GetProperty("version").GetString());
        var components = screen[1].GetProperty("updateComponents").GetProperty("components").EnumerateArray().ToList();
        Assert.Equal("root", components[0].GetProperty("id").GetString());
        var kinds = components.Select(c => c.GetProperty("component").GetString()).ToList();
        Assert.Contains("ChoiceField", kinds);
        Assert.Contains("DateField", kinds);
        Assert.Contains("FileUpload", kinds);
        Assert.DoesNotContain(components, c => c.TryGetProperty("name", out var n) && n.GetString() == "fullName"); // already answered
        Assert.Equal(A2UiScreens.MaxFields, kinds.Count(k => k is "ChoiceField" or "DateField" or "FileUpload" or "TextField"));
        var save = components.Single(c => c.GetProperty("component").GetString() == "Button");
        Assert.Equal(A2UiCatalog.SaveAction, save.GetProperty("action").GetProperty("event").GetProperty("name").GetString());
        Assert.Equal("/values/emirate", save.GetProperty("action").GetProperty("event").GetProperty("context")
            .GetProperty("values").GetProperty("emirate").GetProperty("@path").GetString());
    }

    [Fact]
    public async Task When_everything_is_answered_the_screen_is_a_summary_with_the_fee_and_submit()
    {
        var app = new ApplicationView(Guid.NewGuid(), "N", "SVC", "Service", "Draft", new()
        {
            ["fullName"] = "Fatima", ["emirate"] = "DXB", ["startDate"] = "2026-12-01", ["agreement"] = Guid.NewGuid().ToString(),
        });

        var screen = Json((await Screens.NextAsync(Service(), app, [], default))!);

        var components = screen[1].GetProperty("updateComponents").GetProperty("components").EnumerateArray().ToList();
        var facts = components.Single(c => c.GetProperty("component").GetString() == "Facts").GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(facts, f => f.GetProperty("value").GetString() == "Dubai");      // option label, not value
        Assert.Contains(facts, f => f.GetProperty("value").GetString() == "AED 200");    // fee per currency settings
        Assert.Contains(components, c => c.TryGetProperty("action", out var a) &&
            a.GetProperty("event").GetProperty("name").GetString() == A2UiCatalog.SubmitAction);
    }

    [Fact]
    public async Task A_refused_answer_comes_back_with_its_message()
    {
        var app = new ApplicationView(Guid.NewGuid(), "N", "SVC", "Service", "Draft", new() { ["fullName"] = "F" });

        var screen = Json((await Screens.NextAsync(Service(), app, [new FieldErrorView("fullName", "Enter your full name.")], default))!);

        var field = screen[1].GetProperty("updateComponents").GetProperty("components").EnumerateArray()
            .Single(c => c.TryGetProperty("name", out var n) && n.GetString() == "fullName");
        Assert.Equal("Enter your full name.", field.GetProperty("error").GetString());
    }
}
