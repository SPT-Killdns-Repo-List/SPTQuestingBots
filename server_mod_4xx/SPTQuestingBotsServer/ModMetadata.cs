using SemanticVersioning;
using SPTarkov.Server.Core.Models.Spt.Mod;

namespace SPTQuestingBotsServer;

#pragma warning disable CS8764 // Nullable mismatch with base — expected for optional list properties
public record ModMetadata : AbstractModMetadata
{
    public override string ModGuid { get; init; } = "com.DanW.QuestingBotsServer";
    public override string Name { get; init; } = "DanW-SPTQuestingBotsServer";
    public override string Author { get; init; } = "DanW";
    public override List<string>? Contributors { get; init; }
    public override SemanticVersioning.Version Version { get; init; } = new("0.11.0");
    public override SemanticVersioning.Range SptVersion { get; init; } = new("~4.0.3");
    public override List<string>? Incompatibilities { get; init; } = ["Andrudis-QuestManiac"];
    public override Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; }
    public override string? Url { get; init; }
    public override bool? IsBundleMod { get; init; }
    public override string? License { get; init; } = "MIT";
}
#pragma warning restore CS8764
