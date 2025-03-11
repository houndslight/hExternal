using System;

namespace hExternal.Modules
{
    public static class Log
    {
        private static readonly object _lock = new object();

        public static void Info(string message)
        {
            Print(message, ConsoleColor.Cyan);
        }

        public static void Alert(string message)
        {
            Print(message, ConsoleColor.Yellow);
        }

        public static void Error(string message)
        {
            Print(message, ConsoleColor.Red);
        }

        public static void Success(string message)
        {
            Print(message, ConsoleColor.Green);
        }

        private static void Print(string message, ConsoleColor color)
        {
            lock (_lock)
            {
                string time = DateTime.Now.ToString("HH:mm:ss");

                string lightPink = "\u001b[38;2;255;182;193m";
                string resetColor = "\u001b[0m";

                Console.Write("[" + time + "] ");
                Console.Write(lightPink + "[hExternal]" + resetColor + " ");
                Console.ForegroundColor = color;
                Console.WriteLine(message);

                Console.ResetColor();
            }
        }

    }
}