namespace Workshop.Common;

public static class ConsoleExtensions
{
    extension(Console)
    {
        public static void WriteColorful(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.Write(text);
            Console.ResetColor();
        }

        public static void WriteLineColorful(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ResetColor();
        }

        public static void WriteError(string text)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.Write(text);
            Console.ResetColor();
        }

        public static void WriteLineError(string text)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(text);
            Console.ResetColor();
        }
    }
}
