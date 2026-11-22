using System.Text.RegularExpressions;
using Dsp.SharedKernel;

namespace Dsp.Platform.Domain;

/// <summary>
/// A named view of one list that a person saved for themselves: the search words, sort and filter conditions, kept as
/// the list state the tables send (JSON). Views belong to one person and follow them to any device.
/// </summary>
public sealed partial class SavedView : Entity
{
    public const int MaxPerList = 50;
    public const int MaxNameLength = 100;
    public const int MaxStateLength = 8000;

    private SavedView() { }

    public string Owner { get; private set; } = "";

    /// <summary>Which list, e.g. "backoffice.applications".</summary>
    public string ListKey { get; private set; } = "";
    public string Name { get; private set; } = "";

    /// <summary>{ q, sort, filter } as JSON.</summary>
    public string State { get; private set; } = "{}";
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static SavedView Create(string owner, string listKey, string name, string state)
    {
        if (string.IsNullOrWhiteSpace(owner)) throw new ForbiddenException("errors.staffOnly");
        var errors = new List<FieldError>();
        var key = (listKey ?? "").Trim();
        if (!ListKeyPattern().IsMatch(key)) errors.Add(new FieldError("list", "errors.view.list"));
        if (errors.Count > 0) throw new ValidationException(errors);
        var view = new SavedView { Owner = owner, ListKey = key, CreatedAt = DateTimeOffset.UtcNow };
        view.Update(name, state);
        return view;
    }

    /// <summary>Saving under an existing name replaces that view's state.</summary>
    public void Update(string name, string state)
    {
        var errors = new List<FieldError>();
        var trimmed = (name ?? "").Trim();
        if (trimmed.Length == 0 || trimmed.Length > MaxNameLength) errors.Add(new FieldError("name", "errors.view.name"));
        if (string.IsNullOrWhiteSpace(state) || state.Length > MaxStateLength) errors.Add(new FieldError("state", "errors.view.state"));
        if (errors.Count > 0) throw new ValidationException(errors);
        Name = trimmed;
        State = state;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void EnsureOwnedBy(string owner)
    {
        if (Owner != owner) throw new NotFoundException("errors.view.notFound");
    }

    [GeneratedRegex("^[a-z][a-z0-9.-]{0,99}$")]
    private static partial Regex ListKeyPattern();
}
