namespace GameServer.Resharding;

public interface IGameMigrationCoordinator
{
    void BeginThreadMigration(GameMigrationRequest migrationRequest);
}

