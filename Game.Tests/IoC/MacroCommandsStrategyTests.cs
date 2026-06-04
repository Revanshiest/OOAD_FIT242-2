using Moq;

namespace Game.Tests.Strategies
{
    public class CreateMacroCommandStrategyTests
    {
        public CreateMacroCommandStrategyTests()
        {
            new InitCommand().Execute();
            var testScope = Ioc.Resolve<object>("IoC.Scope.Create");
            Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", testScope).Execute();

            Ioc.Resolve<ICommand>("IoC.Register", "Commands.Macro",
                (object[] args) => new MacroCommand((ICommand[])args[0])).Execute();
        }

        [Fact]
        public void TestSpecsMacroCommandBuildsAndExecutes()
        {
            var cmd1 = new Mock<ICommand>();
            var cmd2 = new Mock<ICommand>();
            var cmd3 = new Mock<ICommand>();

            cmd1.Setup(c => c.Execute());
            cmd2.Setup(c => c.Execute());
            cmd3.Setup(c => c.Execute());

            Ioc.Resolve<ICommand>("IoC.Register", "Specs.Test",
                (object[] args) => new string[] { "A", "B" }).Execute();

            Ioc.Resolve<ICommand>("IoC.Register", "A",
                (object[] args) => cmd1.Object).Execute();
            Ioc.Resolve<ICommand>("IoC.Register", "B",
                (object[] args) => cmd2.Object).Execute();
            Ioc.Resolve<ICommand>("IoC.Register", "C",
                (object[] args) => cmd3.Object).Execute();

            var macro = new CreateMacroCommandStrategy("Specs.Test").Resolve(Array.Empty<object>());
            macro.Execute();

            cmd1.Verify(c => c.Execute(), Times.Once);
            cmd2.Verify(c => c.Execute(), Times.Once);
            cmd3.Verify(c => c.Execute(), Times.Never);
        }

        [Fact]
        public void Resolve_Should_ThrowException_When_SpecificationNotFound()
        {
            var strategy = new CreateMacroCommandStrategy("NonExistent");

            Assert.Throws<Exception>(() => strategy.Resolve(Array.Empty<object>()));
        }

        [Fact]
        public void OneOfCommandsIsNotResolved()
        {
            Ioc.Resolve<ICommand>("IoC.Register", "Specs.Test",
                (object[] args) => new[] { "A", "B" }).Execute();

            Ioc.Resolve<ICommand>("IoC.Register", "A",
                (object[] args) => new Mock<ICommand>().Object).Execute();

            var strategy = new CreateMacroCommandStrategy("Test");

            Assert.Throws<Exception>(() => strategy.Resolve(Array.Empty<object>()));
        }
    }
}
