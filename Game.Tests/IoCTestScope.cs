namespace Game.Tests;

using App;
using App.Scopes;

public static class IoCTestScope
{
    public static void Initialize()
    {
        new InitCommand().Execute();
        var testScope = Ioc.Resolve<object>("IoC.Scope.Create");
        Ioc.Resolve<ICommand>("IoC.Scope.Current.Set", testScope).Execute();
    }
}
