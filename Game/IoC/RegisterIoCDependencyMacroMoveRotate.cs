namespace Game.IoC;

public sealed class RegisterIoCDependencyMacroMoveRotate : ICommand
{
    public void Execute()
    {
        Ioc.Resolve<ICommand>(IocDependencyKeys.Register, IocDependencyKeys.MacroMove,
            (object[] args) => new CreateMacroCommandStrategy(IocDependencyKeys.MoveSpecification).Resolve(args)).Execute();

        Ioc.Resolve<ICommand>(IocDependencyKeys.Register, IocDependencyKeys.MacroRotate,
            (object[] args) => new CreateMacroCommandStrategy(IocDependencyKeys.RotateSpecification).Resolve(args)).Execute();
    }
}
