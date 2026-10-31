namespace GameServer.Workers;

using GameServer.Resharding;

public sealed record GameWorkerSupportServices(
    ISnapshotCodec SnapshotCodec,
    ITransitResolver TransitResolver,
    ITransitOverflowSink OverflowSink,
    ISnapshotStore SnapshotStore);
