namespace Game;

public class MovingObjectAdapter : IMovingObject
{
    private readonly IDictionary<string, object> _gameObject;

    public MovingObjectAdapter(IDictionary<string, object> gameObject)
    {
        _gameObject = gameObject ?? throw new ArgumentNullException(nameof(gameObject));
    }

    public Vector Position
    {
        get => (Vector)_gameObject["Position"];
        set => _gameObject["Position"] = value;
    }

    public Vector Speed
    {
        get => (Vector)_gameObject["Speed"];
    }
}
