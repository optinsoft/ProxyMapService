using System.Text;

namespace ProxyMapService.Utils
{
    public class RandomStringGenerator
    {
        private static readonly Random random = new(); // Initialize Random once

        public static string GenerateRandomString(int length, bool includeNumbers = true, bool includeLowercase = true, bool includeUppercase = true)
        {
            if (length <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length), "Length must be greater than zero.");
            }

            StringBuilder characterSet = new();
            if (includeLowercase)
            {
                characterSet.Append("abcdefghijklmnopqrstuvwxyz");
            }
            if (includeUppercase)
            {
                characterSet.Append("ABCDEFGHIJKLMNOPQRSTUVWXYZ");
            }
            if (includeNumbers)
            {
                characterSet.Append("0123456789");
            }

            if (characterSet.Length == 0)
            {
                throw new InvalidOperationException("At least one character type (lowercase, uppercase, or numbers) must be included.");
            }

            StringBuilder result = new(length);
            string availableChars = characterSet.ToString();

            for (int i = 0; i < length; i++)
            {
                int index = random.Next(0, availableChars.Length);
                result.Append(availableChars[index]);
            }

            return result.ToString();
        }
    }
}
