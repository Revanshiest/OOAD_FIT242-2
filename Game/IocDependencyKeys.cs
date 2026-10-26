namespace Game;

public static class IocDependencyKeys
{
    public const string Register = "IoC.Register";
    public const string CommandsPrefix = "Commands.";
    public const string MacroPrefix = "Macro.";
    public const string RepeatableCommandPrefix = "repeatable";
    public const string MoveCommand = "Commands.Move";
    public const string RotateCommand = "Commands.Rotate";
    public const string SendCommand = "Commands.Send";
    public const string MacroCommand = "Commands.Macro";
    public const string CommandInjectable = "Commands.CommandInjectable";
    public const string CommandsReceiver = "Game.CommandsReceiver";
    public const string ActionsStart = "Actions.Start";
    public const string ActionsStop = "Actions.Stop";
    public const string MovingObjectAdapter = "Adapters.IMovingObject";
    public const string RotatableAdapter = "Adapters.IRotatable";
    public const string MacroMove = "Macro.Move";
    public const string MacroRotate = "Macro.Rotate";
    public const string MoveSpecification = "Specs.Move";
    public const string RotateSpecification = "Specs.Rotate";
    public const string GameObjectRepository = "Repository.GameObject";
}
