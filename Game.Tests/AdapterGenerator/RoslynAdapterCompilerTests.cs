using Game;
using Game.AdapterGenerator;
using Game.Commands;
using Xunit;

namespace Game.Tests;

public class RoslynAdapterCompilerTests
{
    [Fact]
    public void Compile_ThrowsInvalidOperationException_WhenSourceCodeIsInvalid()
    {
        var compiler = new RoslynAdapterCompiler();

        Assert.Throws<InvalidOperationException>(() =>
            compiler.Compile("this is not valid C# source code", new[] { typeof(MoveCommand).Assembly }));
    }
}
