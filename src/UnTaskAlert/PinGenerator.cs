namespace UnTaskAlert;

public class PinGenerator : IPinGenerator
{
    private static readonly Random Random = new Random();

    public int GetRandomPin()
    {
        lock (Random)
        {
            return Random.Next(1000, 9999);
        }
    }
}