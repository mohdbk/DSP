using System.Text.RegularExpressions;
using Dsp.SharedKernel;

namespace Dsp.EServices.Domain.Catalog;

/// <summary>
/// What makes a service definition publishable. The service builder shows these errors while staff edit,
/// and publishing is refused until there are none. Error keys point at the place in the definition
/// (e.g. "steps[1].fields[0].key") so the builder can highlight it.
/// </summary>
public static partial class ServiceDefinitionRules
{
    /// <summary>Profile claims a field can be pre-filled from (UAE PASS profile).</summary>
    public static readonly IReadOnlyList<string> ProfileClaims = ["name", "emirates_id", "email", "mobile"];

    public const int MaxSteps = 12;
    public const int MaxFieldsPerStep = 30;
    public const int MaxChecklistItems = 20;

    public static List<FieldError> Validate(ServiceDefinition d)
    {
        var errors = new List<FieldError>();
        void Add(string at, string message) => errors.Add(new FieldError(at, message));

        if (string.IsNullOrWhiteSpace(d.Code) || !CodePattern().IsMatch(d.Code))
            Add("code", "Use 3–64 capital letters, digits or underscores, starting with a letter (e.g. TRADE_NAME_RESERVATION).");
        if (string.IsNullOrWhiteSpace(d.Name)) Add("name", "Enter the service name in English.");
        if (string.IsNullOrWhiteSpace(d.NameAr)) Add("nameAr", "Enter the service name in Arabic.");
        if (string.IsNullOrWhiteSpace(d.Description)) Add("description", "Enter a short description in English.");
        if (string.IsNullOrWhiteSpace(d.DescriptionAr)) Add("descriptionAr", "Enter a short description in Arabic.");
        if (d.Fee <= 0) Add("fee", "The fee must be more than zero.");
        if (string.IsNullOrWhiteSpace(d.Currency) || d.Currency.Length != 3) Add("currency", "Use a 3-letter currency code, e.g. AED.");
        if (!WorkflowVersions.IsKnown(d.WorkflowVersion)) Add("workflowVersion", "Choose the standard or the AI-assisted process.");

        if (d.ReviewChecklist is { } checklist)
        {
            if (checklist.Count > MaxChecklistItems) Add("reviewChecklist", $"Use at most {MaxChecklistItems} checklist items.");
            for (var i = 0; i < checklist.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(checklist[i]) || checklist[i].Length > 300)
                    Add($"reviewChecklist[{i}]", "Each checklist item needs 1–300 characters.");
            }
        }

        if (d.Steps is null || d.Steps.Count == 0)
        {
            Add("steps", "Add at least one step.");
            return errors;
        }
        if (d.Steps.Count > MaxSteps) Add("steps", $"Use at most {MaxSteps} steps.");

        var stepIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fieldKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var s = 0; s < d.Steps.Count; s++)
        {
            var step = d.Steps[s];
            var at = $"steps[{s}]";
            if (string.IsNullOrWhiteSpace(step.Id) || !StepIdPattern().IsMatch(step.Id))
                Add($"{at}.id", "Use lowercase letters, digits and dashes for the step ID.");
            else if (!stepIds.Add(step.Id)) Add($"{at}.id", "Step IDs must be unique.");
            if (string.IsNullOrWhiteSpace(step.Title)) Add($"{at}.title", "Enter the step title in English.");
            if (string.IsNullOrWhiteSpace(step.TitleAr)) Add($"{at}.titleAr", "Enter the step title in Arabic.");

            if (step.Fields is null || step.Fields.Count == 0)
            {
                Add($"{at}.fields", "Add at least one field to this step.");
                continue;
            }
            if (step.Fields.Count > MaxFieldsPerStep) Add($"{at}.fields", $"Use at most {MaxFieldsPerStep} fields per step.");

            for (var f = 0; f < step.Fields.Count; f++)
                ValidateField(step.Fields[f], $"{at}.fields[{f}]", fieldKeys, Add);
        }

        if (!string.IsNullOrWhiteSpace(d.ReferenceField))
        {
            var reference = d.FindField(d.ReferenceField);
            if (reference is null) Add("referenceField", "The reference field must be one of the service's fields.");
            else if (reference.Type is not (FieldTypes.Text or FieldTypes.Email or FieldTypes.Phone))
                Add("referenceField", "The reference field must be a text field.");
        }

        return errors;
    }

    private static void ValidateField(FieldDefinition field, string at, HashSet<string> keys, Action<string, string> add)
    {
        if (string.IsNullOrWhiteSpace(field.Key) || !FieldKeyPattern().IsMatch(field.Key))
            add($"{at}.key", "Start with a letter and use letters and digits only (e.g. tradeNameEn).");
        else if (!keys.Add(field.Key)) add($"{at}.key", "Field keys must be unique across the whole service.");

        if (string.IsNullOrWhiteSpace(field.Label)) add($"{at}.label", "Enter the label in English.");
        if (string.IsNullOrWhiteSpace(field.LabelAr)) add($"{at}.labelAr", "Enter the label in Arabic.");
        if (!FieldTypes.All.Contains(field.Type)) add($"{at}.type", "Choose a field type.");

        if (field.Type == FieldTypes.Select)
        {
            if (field.Options is null || field.Options.Count == 0) add($"{at}.options", "Add at least one option.");
            else
            {
                var values = new HashSet<string>();
                for (var o = 0; o < field.Options.Count; o++)
                {
                    var option = field.Options[o];
                    if (string.IsNullOrWhiteSpace(option.Value)) add($"{at}.options[{o}].value", "Each option needs a value.");
                    else if (!values.Add(option.Value)) add($"{at}.options[{o}].value", "Option values must be unique.");
                    if (string.IsNullOrWhiteSpace(option.Label)) add($"{at}.options[{o}].label", "Each option needs an English label.");
                    if (string.IsNullOrWhiteSpace(option.LabelAr)) add($"{at}.options[{o}].labelAr", "Each option needs an Arabic label.");
                }
            }
        }

        if (field.MinLength is < 0) add($"{at}.minLength", "Minimum length can't be negative.");
        if (field.MinLength is { } minL && field.MaxLength is { } maxL && minL > maxL)
            add($"{at}.maxLength", "Maximum length must be at least the minimum length.");
        if (field.Min is { } min && field.Max is { } max && min > max)
            add($"{at}.max", "Maximum must be at least the minimum.");

        if (!string.IsNullOrEmpty(field.Pattern))
        {
            try { _ = new Regex(field.Pattern, RegexOptions.None, TimeSpan.FromMilliseconds(200)); }
            catch (ArgumentException) { add($"{at}.pattern", "This is not a valid pattern (regular expression)."); }
        }

        if (field.PrefillFrom is not null && !ProfileClaims.Contains(field.PrefillFrom))
            add($"{at}.prefillFrom", "Pre-fill only from name, Emirates ID, email or mobile.");
    }

    [GeneratedRegex("^[A-Z][A-Z0-9_]{2,63}$")]
    private static partial Regex CodePattern();

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,63}$")]
    private static partial Regex StepIdPattern();

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9]{0,63}$")]
    private static partial Regex FieldKeyPattern();
}
