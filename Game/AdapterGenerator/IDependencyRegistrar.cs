namespace Game.AdapterGenerator;

public interface IDependencyRegistrar
{
    void RegisterAdapter(CommandMetadata metadata, Type adapterType);
    void RegisterCommand(CommandMetadata metadata);
}
