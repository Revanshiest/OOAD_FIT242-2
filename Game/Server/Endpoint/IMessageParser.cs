namespace GameServer.Endpoint;

public interface IMessageParser<out TMessage>
{
    TMessage Parse(string requestPayload);
}

