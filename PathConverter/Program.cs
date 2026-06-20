using System;

namespace PathConverter
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== Windows Path to JSON Converter ===");
            Console.WriteLine("Paste a Windows path and press Enter to convert it to JSON-safe format.");
            Console.WriteLine("Type 'exit' to quit.");
            Console.WriteLine();

            while (true)
            {
                Console.Write("Enter path: ");
                var input = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(input))
                {
                    Console.WriteLine("Please enter a path.");
                    continue;
                }

                if (input.ToLower() == "exit")
                {
                    break;
                }

                // Convert to JSON-safe format
                var jsonSafe = input.Replace("\\", "\\\\");
                
                Console.WriteLine();
                Console.WriteLine("JSON-safe format:");
                Console.WriteLine(jsonSafe);
                Console.WriteLine();
                Console.WriteLine("Copy this line and paste it into appsettings.folder.json");
                Console.WriteLine();
                Console.WriteLine("---");
                Console.WriteLine();
            }
        }
    }
}
