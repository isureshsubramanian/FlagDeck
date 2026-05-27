namespace FlagDeck.Sample.Services;

public interface IGreetingService
{
    string Greet();
}

public sealed class ClassicGreetingService : IGreetingService
{
    public string Greet() => "Hello from FlagDeck.Sample (classic).";
}

public sealed class NewGreetingService : IGreetingService
{
    public string Greet() => "Welcome to the new experience — powered by FlagDeck.";
}
