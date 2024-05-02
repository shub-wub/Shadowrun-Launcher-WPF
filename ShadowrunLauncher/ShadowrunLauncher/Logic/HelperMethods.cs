using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ShadowrunLauncher.Logic
{
    internal class HelperMethods
    {
        public static void CopyToClipboard(string text)
        {
            try
            {
                Clipboard.Clear(); // Clear existing clipboard content
                Clipboard.SetText(text); // Set new text to clipboard
                                         // No need for 'this.Update()' as there's no equivalent in WPF for this specific update
            }
            catch (Exception)
            {
                MessageBox.Show("Failed to copy to clipboard.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        public static string DecimalToHexFormat(int decimalValue)
        {
            string hexValue = decimalValue.ToString("X"); // Convert to hex
            char[] hexChars = hexValue.ToCharArray(); // Convert to char array
            Array.Reverse(hexChars); // Reverse the array

            string reversedHex = new string(hexChars); // Convert back to string
            string formattedHex = string.Join(",", SplitIntoChunks(reversedHex, 2)); // Group into pairs and join with commas

            return formattedHex;
        }
        private static string[] SplitIntoChunks(string str, int chunkSize)
        {
            int numChunks = (int)Math.Ceiling((double)str.Length / chunkSize);
            string[] chunks = new string[numChunks];

            for (int i = 0; i < numChunks; i++)
            {
                int startIndex = i * chunkSize;
                int length = Math.Min(chunkSize, str.Length - startIndex);
                chunks[i] = str.Substring(startIndex, length);
            }

            return chunks;
        }
        public void ForceCloseGame()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            path = Path.Combine(path, @"Microsoft\Xlive\Titles\4d5307d6");
            string tokenFile = Path.Combine(path, "Token.bin");

            if (File.Exists(tokenFile))
            {
                // Check if Shadowrun.exe is running
                Process[] processes = Process.GetProcessesByName("Shadowrun");
                if (processes.Length > 0)
                {
                    // If Shadowrun.exe is running, force close it
                    foreach (Process process in processes)
                    {
                        process.Kill();
                        Console.WriteLine("Shadowrun.exe has been terminated.");
                    }
                }
                else
                {
                    Console.WriteLine("Shadowrun.exe is not running.");
                }
            }
            else
            {
                Console.WriteLine("Token.bin does not exist.");
            }
        }
    }
}
