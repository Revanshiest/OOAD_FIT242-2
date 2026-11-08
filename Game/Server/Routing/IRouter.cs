namespace GameServer.Routing;

public interface IRouter
{
    RouteTarget ResolveTarget(int routingKey, string gameId);
}

