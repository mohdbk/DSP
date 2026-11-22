using Dsp.AiOrchestrator.Application;
using Dsp.AiOrchestrator.Domain;

namespace Dsp.UnitTests.AiOrchestrator;

public class GuidedConversationTests
{
    private readonly FakeEServices _api = new();
    private readonly GuidedConversation _chat;

    public GuidedConversationTests() => _chat = new GuidedConversation(_api, TestText.Messages());

    private Task<ChatResponse> Say(string? text, string? pending, params ChatMessage[] history)
    {
        var messages = history.ToList();
        if (text is not null) messages.Add(new ChatMessage(ChatRoles.User, text));
        return _chat.RespondAsync(new ChatRequest(_api.App.Id, messages, pending), _api.Service, _api.App, default);
    }

    [Fact]
    public async Task First_turn_greets_mentions_prefilled_data_and_asks_the_first_missing_field()
    {
        var reply = await Say(null, null);

        Assert.Contains("Hello", reply.Reply);
        Assert.Contains("Full name (Fatima)", reply.Reply);
        Assert.Equal("emirate", reply.PendingField);
        Assert.Equal("guided", reply.Mode);
    }

    [Fact]
    public async Task Select_answer_matches_the_option_label_and_saves_its_value()
    {
        var reply = await Say("dubai", "emirate");

        Assert.Equal("DXB", _api.App.Data["emirate"]);
        Assert.Equal("startDate", reply.PendingField);
    }

    [Fact]
    public async Task Dates_are_understood_in_common_formats()
    {
        _api.App.Data["emirate"] = "DXB";

        await Say("31/01/2027", "startDate");

        Assert.Equal("2027-01-31", _api.App.Data["startDate"]);
    }

    [Fact]
    public async Task Unclear_answer_asks_again_without_saving()
    {
        var reply = await Say("Atlantis", "emirate");

        Assert.Contains("didn't catch", reply.Reply);
        Assert.Equal("emirate", reply.PendingField);
        Assert.False(_api.App.Data.ContainsKey("emirate"));
    }

    [Fact]
    public async Task Server_validation_errors_are_relayed()
    {
        _api.RejectWith = ("emirate", "Emirate is closed for new names.");

        var reply = await Say("Dubai", "emirate");

        Assert.Contains("closed for new names", reply.Reply);
        Assert.Equal("emirate", reply.PendingField);
    }

    [Fact]
    public async Task Summary_then_submit_when_everything_is_filled()
    {
        _api.App.Data["emirate"] = "DXB";
        _api.App.Data["startDate"] = "2027-01-31";

        var summary = await Say("yes", "declaration");
        Assert.Contains("Everything is filled in", summary.Reply);
        Assert.Contains("Dubai", summary.Reply);
        Assert.Null(summary.PendingField);

        var done = await Say("submit", null);
        Assert.Contains("submitted", done.Reply);
        Assert.True(_api.Submitted);
    }

    [Fact]
    public async Task File_fields_are_redirected_to_the_form_view()
    {
        _api.AddFileField();
        _api.App.Data["emirate"] = "DXB";
        _api.App.Data["startDate"] = "2027-01-31";
        _api.App.Data["declaration"] = "true";

        var reply = await Say("ok", "declaration");

        Assert.Contains("form view", reply.Reply);
        Assert.Equal("agreement", reply.PendingField);
    }

    private sealed class FakeEServices : IEServicesApi
    {
        private readonly List<FieldView> _fields =
        [
            new("fullName", "Full name", "text", true, null, null),
            new("emirate", "Emirate", "select", true, null, [new("DXB", "Dubai"), new("AUH", "Abu Dhabi")]),
            new("startDate", "Start date", "date", true, null, null),
            new("declaration", "I confirm the details", "checkbox", true, null, null),
        ];

        public ApplicationView App { get; } = new(Guid.NewGuid(), "DSP-2026-000001", "SVC", "Service", "Draft",
            new Dictionary<string, string?> { ["fullName"] = "Fatima" });

        public ServiceView Service => new("SVC", "Trade Name Reservation", "d", 200, "AED",
            [new StepView("one", "One", null, _fields)]);

        public (string Field, string Message)? RejectWith { get; set; }
        public bool Submitted { get; private set; }

        public void AddFileField() => _fields.Add(new FieldView("agreement", "Signed agreement", "file", true, null, null));

        public Task<ApplicationView> GetApplicationAsync(Guid id, CancellationToken ct) => Task.FromResult(App);
        public Task<ServiceView> GetServiceAsync(string code, CancellationToken ct) => Task.FromResult(Service);

        public Task<SaveOutcome> SaveDataAsync(Guid id, IReadOnlyDictionary<string, string?> values, CancellationToken ct)
        {
            if (RejectWith is { } r && values.ContainsKey(r.Field))
                return Task.FromResult(new SaveOutcome(App, [new FieldErrorView(r.Field, r.Message)]));
            foreach (var (k, v) in values) App.Data[k] = v;
            return Task.FromResult(new SaveOutcome(App, []));
        }

        public Task<SubmitOutcome> SubmitAsync(Guid id, CancellationToken ct)
        {
            Submitted = true;
            return Task.FromResult(new SubmitOutcome(App with { Status = "Submitted" }, []));
        }
    }
}
