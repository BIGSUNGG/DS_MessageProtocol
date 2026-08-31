using MessageProtocol;
using MessageProtocol.Serialize;

namespace MinimalConsole;

[StandaloneMessage(1)]
public partial class HelloMessage
{
    public int Id { get; set; }
    public string Text { get; set; } = "";
}

public static class Program
{
    public static int Main()
    {
        var original = new HelloMessage { Id = 42, Text = "hello" };
        byte[] bytes = MessageSerializer.Serialize(original);
        var roundTrip = MessageSerializer.Deserialize<HelloMessage>(bytes);

        if (roundTrip.Id == original.Id && roundTrip.Text == original.Text)
        {
            Console.WriteLine("OK");
            return 0;
        }

        Console.WriteLine("FAIL");
        return 1;
    }
}
