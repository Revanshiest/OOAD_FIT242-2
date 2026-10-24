using System.Reflection;

namespace Game.AdapterGenerator;

public interface ICommandScanner
{
    IReadOnlyList<CommandMetadata> Scan(IEnumerable<Assembly> assemblies);
}
