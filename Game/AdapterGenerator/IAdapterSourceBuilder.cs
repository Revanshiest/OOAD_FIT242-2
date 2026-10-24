namespace Game.AdapterGenerator;

public interface IAdapterSourceBuilder
{
    string BuildAdapterSource(CommandMetadata metadata);
}
