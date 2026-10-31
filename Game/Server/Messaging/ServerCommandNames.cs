namespace GameServer.Messaging;

public static class ServerCommandNames
{
    public const string CreateItem = "CreateItem";
    public const string Move = "Move";
    public const string Suspend = "Suspend";
    public const string Resume = "Resume";
    public const string TickMarker = "Server.Tick";
    public const string MigrateOut = "Server.MigrateOut";
    public const string MigrateIn = "Server.MigrateIn";
}
