using System.Reflection;
using Game;
using Game.AdapterGenerator;
using Game.Commands;
using Game.IoC;
using Xunit;

namespace Game.Tests;

public class CommandScannerTests
{
    private readonly CommandScanner _scanner = new();

    [Fact]
    public void Scan_FindsMoveCommand_WithIMovingObjectParameter()
    {
        var assemblies = new[] { typeof(MoveCommand).Assembly };

        var result = _scanner.Scan(assemblies);

        var moveMetadata = result.FirstOrDefault(m => m.CommandType == typeof(MoveCommand));
        Assert.NotNull(moveMetadata);
        Assert.Equal(typeof(IMovingObject), moveMetadata.InterfaceType);
    }

    [Fact]
    public void Scan_FindsRotateCommand_WithIRotatableParameter()
    {
        var assemblies = new[] { typeof(RotateCommand).Assembly };

        var result = _scanner.Scan(assemblies);

        var rotateMetadata = result.FirstOrDefault(m => m.CommandType == typeof(RotateCommand));
        Assert.NotNull(rotateMetadata);
        Assert.Equal(typeof(IRotatable), rotateMetadata.InterfaceType);
    }

    [Fact]
    public void Scan_IgnoresClassesWithoutCommandSuffix()
    {
        var assemblies = new[] { typeof(MoveCommand).Assembly };

        var result = _scanner.Scan(assemblies);

        Assert.DoesNotContain(result, m => m.CommandType == typeof(CreateMacroCommandStrategy));
    }

    [Fact]
    public void Scan_IgnoresCommandsWithMultipleConstructorParameters()
    {
        var assemblies = new[] { typeof(SendCommand).Assembly };

        var result = _scanner.Scan(assemblies);

        Assert.DoesNotContain(result, m => m.CommandType == typeof(SendCommand));
    }

    [Fact]
    public void Scan_ReturnsEmptyForEmptyAssemblyList()
    {
        var result = _scanner.Scan(Array.Empty<Assembly>());

        Assert.Empty(result);
    }
}
