namespace Dsp.SharedKernel;

/// <summary>Base type for aggregate roots and entities.</summary>
public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
}
