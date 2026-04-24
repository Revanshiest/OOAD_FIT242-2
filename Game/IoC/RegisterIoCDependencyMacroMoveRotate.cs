using System;
using System.Linq;

namespace Game;

public class RegisterIoCDependencyMacroMoveRotate : ICommand
{
    public void Execute()
    {
        Ioc.Resolve<ICommand>("IoC.Register", "Macro.Move",
            (object[] args) => new CreateMacroCommandStrategy("Specs.Move").Resolve(args)).Execute();

        Ioc.Resolve<ICommand>("IoC.Register", "Macro.Rotate",
            (object[] args) => new CreateMacroCommandStrategy("Specs.Rotate").Resolve(args)).Execute();
    }
}
