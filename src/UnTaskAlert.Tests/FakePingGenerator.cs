namespace UnTaskAlert.Tests
{
    public class FakePingGenerator : IPinGenerator
    {
        public static int Pin = 1234;

        public int GetRandomPin() => Pin;
    }
}
