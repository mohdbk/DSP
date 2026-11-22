using System.Text.RegularExpressions;
using Dsp.SharedKernel;

namespace Dsp.EServices.Domain.Catalog;

/// <summary>
/// What makes a service definition publishable. The service builder shows these errors while staff edit,
/// and publishing is refused until there are none. Error paths point at the place in the definition
/// (e.g. "steps[1].fields[0].key", or "name.en" for one language of a text) so the builder can highlight it.
/// Only the platform's default language is required; other languages are optional.
/// </summary>
public static partial class ServiceDefinitionRules
{
    /// <summary>Profile claims a field can be pre-filled from (UAE PASS profile).</summary>
    public static readonly IReadOnlyList<string> ProfileClaims = ["name", "emirates_id", "email", "mobile"];

    public const int MaxSteps = 12;
    public const int MaxFieldsPerStep = 30;
    public const int MaxChecklistItems = 20;

    /// <param name="messages">Messages in the editor's language; its default language is the one every text needs.</param>
    public static List<FieldError> Validate(ServiceDefinition d, IMessages messages)
    {
        var errors = new List<FieldError>();
        var lang = messages.DefaultLanguage;
        void Add(string at, string key, params (string, object?)[] args) =>
            errors.Add(new FieldError(at, messages.Format(key, [("language", lang), .. args])));
        void Require(LocalizedText? text, string at, string key)
        {
            if (text is null || !text.Has(lang)) Add($"{at}.{lang}", key);
        }

        if (string.IsNullOrWhiteSpace(d.Code) || !CodePattern().IsMatch(d.Code))
            Add("code", "builder.rule.code");
        Require(d.Name, "name", "builder.rule.name");
        Require(d.Description, "description", "builder.rule.description");
        Require(d.Category, "category", "builder.rule.category");
        if (d.Fee <= 0) Add("fee", "builder.rule.fee");
        if (string.IsNullOrWhiteSpace(d.Currency) || d.Currency.Length != 3) Add("currency", "builder.rule.currency");
        if (!WorkflowVersions.IsKnown(d.WorkflowVersion)) Add("workflowVersion", "builder.rule.workflow");
        if (d.AutoApproval && d.WorkflowVersion != WorkflowVersions.AiAssisted) Add("autoApproval", "builder.rule.autoApprovalV2");
        if (d.AuditSamplePercent is < 0 or > 100) Add("auditSamplePercent", "builder.rule.auditSample");

        if (d.ReviewChecklist is { } checklist)
        {
            if (checklist.Count > MaxChecklistItems) Add("reviewChecklist", "builder.rule.checklistMax", ("max", MaxChecklistItems));
            for (var i = 0; i < checklist.Count; i++)
            {
                if (checklist[i] is null || !checklist[i].Has(lang))
                    Add($"reviewChecklist[{i}].{lang}", "builder.rule.checklistItem");
                else if (checklist[i].Values.Any(v => v.Length > 300))
                    Add($"reviewChecklist[{i}]", "builder.rule.checklistItem");
            }
        }

        if (d.Steps is null || d.Steps.Count == 0)
        {
            Add("steps", "builder.rule.stepsNone");
            return errors;
        }
        if (d.Steps.Count > MaxSteps) Add("steps", "builder.rule.stepsMax", ("max", MaxSteps));

        var stepIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fieldKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var s = 0; s < d.Steps.Count; s++)
        {
            var step = d.Steps[s];
            var at = $"steps[{s}]";
            if (string.IsNullOrWhiteSpace(step.Id) || !StepIdPattern().IsMatch(step.Id))
                Add($"{at}.id", "builder.rule.stepId");
            else if (!stepIds.Add(step.Id)) Add($"{at}.id", "builder.rule.stepIdUnique");
            Require(step.Title, $"{at}.title", "builder.rule.stepTitle");

            if (step.Fields is null || step.Fields.Count == 0)
            {
                Add($"{at}.fields", "builder.rule.fieldsNone");
                continue;
            }
            if (step.Fields.Count > MaxFieldsPerStep) Add($"{at}.fields", "builder.rule.fieldsMax", ("max", MaxFieldsPerStep));

            for (var f = 0; f < step.Fields.Count; f++)
                ValidateField(step.Fields[f], $"{at}.fields[{f}]", fieldKeys, Add, Require, lang);
        }

        if (!string.IsNullOrWhiteSpace(d.ReferenceField))
        {
            var reference = d.FindField(d.ReferenceField);
            if (reference is null) Add("referenceField", "builder.rule.referenceMissing");
            else if (reference.Type is not (FieldTypes.Text or FieldTypes.Email or FieldTypes.Phone))
                Add("referenceField", "builder.rule.referenceType");
        }

        return errors;
    }

    private delegate void AddError(string at, string key, params (string, object?)[] args);

    private static void ValidateField(FieldDefinition field, string at, HashSet<string> keys, AddError add,
        Action<LocalizedText?, string, string> require, string lang)
    {
        if (string.IsNullOrWhiteSpace(field.Key) || !FieldKeyPattern().IsMatch(field.Key))
            add($"{at}.key", "builder.rule.fieldKey");
        else if (!keys.Add(field.Key)) add($"{at}.key", "builder.rule.fieldKeyUnique");

        require(field.Label, $"{at}.label", "builder.rule.label");
        if (!FieldTypes.All.Contains(field.Type)) add($"{at}.type", "builder.rule.fieldType");

        if (field.Type == FieldTypes.Select)
        {
            if (field.Options is null || field.Options.Count == 0) add($"{at}.options", "builder.rule.optionsNone");
            else
            {
                var values = new HashSet<string>();
                for (var o = 0; o < field.Options.Count; o++)
                {
                    var option = field.Options[o];
                    if (string.IsNullOrWhiteSpace(option.Value)) add($"{at}.options[{o}].value", "builder.rule.optionValue");
                    else if (!values.Add(option.Value)) add($"{at}.options[{o}].value", "builder.rule.optionValueUnique");
                    require(option.Label, $"{at}.options[{o}].label", "builder.rule.optionLabel");
                }
            }
        }

        if (field.MinLength is < 0) add($"{at}.minLength", "builder.rule.minLength");
        if (field.MinLength is { } minL && field.MaxLength is { } maxL && minL > maxL)
            add($"{at}.maxLength", "builder.rule.maxLength");
        if (field.Min is { } min && field.Max is { } max && min > max)
            add($"{at}.max", "builder.rule.max");

        if (!string.IsNullOrEmpty(field.Pattern))
        {
            try { _ = new Regex(field.Pattern, RegexOptions.None, TimeSpan.FromMilliseconds(200)); }
            catch (ArgumentException) { add($"{at}.pattern", "builder.rule.pattern"); }
        }

        if (field.PrefillFrom is not null && !ProfileClaims.Contains(field.PrefillFrom))
            add($"{at}.prefillFrom", "builder.rule.prefill");
    }

    [GeneratedRegex("^[A-Z][A-Z0-9_]{2,63}$")]
    private static partial Regex CodePattern();

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,63}$")]
    private static partial Regex StepIdPattern();

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9]{0,63}$")]
    private static partial Regex FieldKeyPattern();
}
