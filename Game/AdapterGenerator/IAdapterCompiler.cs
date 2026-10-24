using System.Reflection;

namespace Game.AdapterGenerator;

public interface IAdapterCompiler
{
    Assembly Compile(string sourceCode, IEnumerable<Assembly> sourceAssemblies);
}
