using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Siemplify.Common.ExternalChannels.Utils
{
    static class ConsoleUtils
    {
        // Obscure the password being entered
        public static string ReadPasswordFromConsole()
        {
            string password = string.Empty;
            ConsoleKeyInfo key;
            do
            {
                key = Console.ReadKey(true);
                if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
                {
                    password += key.KeyChar;
                    Console.Write("*");
                }
                else
                {
                    if (key.Key == ConsoleKey.Backspace && password.Length > 0)
                    {
                        password = password.Substring(0, (password.Length - 1));
                        Console.Write("\b \b");
                    }
                }
            }
            while (key.Key != ConsoleKey.Enter);
            return password;
        }

        // Gather user credentials form the command line
        public static UserCredential TextualPrompt()
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("There is no token in the cache or you are not connected to your domain.");
            Console.WriteLine("Please enter Microsoft username and password to sign in.");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("User>");
            string user = Console.ReadLine();
            Console.WriteLine("Password>");
            string password = ReadPasswordFromConsole();
            Console.WriteLine("");
            return new UserPasswordCredential(user, password);
        }
    }
}
