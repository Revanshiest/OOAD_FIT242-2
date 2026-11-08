namespace GameServer.Endpoint;

public interface IEndpoint
{
    bool IsAcceptanceSealed { get; }

    void SealAcceptance();

    void ResumeAcceptance();

    EndpointResponse Accept(string requestPayload);
}

