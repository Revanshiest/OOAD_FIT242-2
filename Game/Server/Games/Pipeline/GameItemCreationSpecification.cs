using Game;
namespace GameServer.Games.Pipeline;

public sealed record GameItemCreationSpecification(
    string ItemId,
    string DomainType,
    Vector Position,
    Vector Speed);
