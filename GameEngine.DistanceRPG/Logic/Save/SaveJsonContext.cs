using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameEngine.DistanceRPG.Logic;

/// <summary>
/// The save's <c>System.Text.Json</c> contract (§5.2), source-generated: the
/// one place in the game a serialisation attribute sits. Camel-cased names, in
/// each record's declaration order; indented with <c>"\n"</c> line ends; a
/// comment or a trailing comma skipped on read; a member the record does not
/// know ignored, so a later build's addition never breaks an older reader of
/// the same version. And strict where it matters: a member the record requires
/// is an error when it is missing, a null where the record has none is an
/// error, and so is a member named twice — a save restores whole or is refused
/// whole, and a document with two answers to one question has neither.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    NewLine = "\n",
    RespectRequiredConstructorParameters = true,
    RespectNullableAnnotations = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true,
    AllowDuplicateProperties = false)]
[JsonSerializable(typeof(SaveFile))]
internal sealed partial class SaveJsonContext : JsonSerializerContext;
