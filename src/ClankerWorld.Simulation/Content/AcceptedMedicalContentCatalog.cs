namespace ClankerWorld.Simulation.Content;

public enum AcceptedMedicalContentKind
{
    Item,
    Building,
}

public sealed record AcceptedMedicalContentEntry(string Id, string DisplayName, AcceptedMedicalContentKind Kind);

public readonly record struct AcceptedMedicalFootprint(int Width, int Height);

/// <summary>
/// Catalog-only representation of accepted future medical content. These
/// identifiers are not active inventory/building definitions and carry no
/// recipe, cost, treatment action, or simulation effect.
/// </summary>
public static class AcceptedMedicalContentCatalog
{
    public const string BandageId = "bandage";
    public const string MedicineId = "medicine";
    public const string ClinicId = "clinic";

    public static IReadOnlyList<AcceptedMedicalContentEntry> Entries { get; } = Array.AsReadOnly<AcceptedMedicalContentEntry>(
    [
        new(BandageId, "Bandage", AcceptedMedicalContentKind.Item),
        new(MedicineId, "Medicine", AcceptedMedicalContentKind.Item),
        new(ClinicId, "Clinic", AcceptedMedicalContentKind.Building),
    ]);

    public static IReadOnlyList<AcceptedMedicalFootprint> ClinicFootprints { get; } = Array.AsReadOnly<AcceptedMedicalFootprint>(
    [
        new AcceptedMedicalFootprint(1, 1),
        new AcceptedMedicalFootprint(1, 2),
    ]);
}
