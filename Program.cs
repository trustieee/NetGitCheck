using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NetGitCheck
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            var directory = new DirectoryInfo("temp_git");
            if (directory.Exists)
            {
                NormalizeDirectoryAttributes(directory);
                directory.Delete(true);
            }

            void NormalizeDirectoryAttributes(DirectoryInfo directoryInfo)
            {
                foreach (var subPath in directoryInfo.GetDirectories())
                {
                    NormalizeDirectoryAttributes(subPath);
                }

                foreach (var file in directoryInfo.GetFiles())
                {
                    file.Attributes = FileAttributes.Normal;
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(1));

            var info = new ProcessStartInfo("git", "clone https://github.com/discord-csharp/MODiX temp_git");
            var p = Process.Start(info);
            if (p == null) throw new InvalidOperationException("process handle was null");
            p.WaitForExit();

            await Task.Delay(TimeSpan.FromSeconds(2));

            var extensions = new[] { ".txt", ".md", ".cs" };

            var spellingInfos = new List<FileSpellingInfo>();
            var spell = new SymSpell();
            if (!spell.LoadDictionary(Path.Combine(Environment.CurrentDirectory, "frequency_dictionary_en_82_765.txt"), 0, 1)) throw new InvalidOperationException();
            foreach (var file in Directory.GetFiles(Path.Combine(Environment.CurrentDirectory, "temp_git"), "*", SearchOption.AllDirectories))
            {
                if (!extensions.Contains(Path.GetExtension(file))) continue;

                var spellingInfo = new FileSpellingInfo { Path = file };
                var fileContents = File.ReadAllLines(file);
                for (var i = 0; i < fileContents.Length; i++)
                {
                    var line = fileContents[i].Trim();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    spellingInfo.LineMistakes.Add((i + 1, line), new List<(string, string)>());
                    var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(c => c.RemoveSpecialCharacters());
                    foreach (var word in words.Where(c => !string.IsNullOrWhiteSpace(c)))
                    {
                        var results = spell.Lookup(word.ToLower(), SymSpell.Verbosity.Top);
                        if (results == null || results.Any() == false) continue;
                        var suggestion = results.First();
                        if (suggestion.term == word.ToLower()) continue;
                        spellingInfo.LineMistakes[(i + 1, line)].Add((word, suggestion.term));
                    }
                }

                spellingInfos.Add(spellingInfo);
            }

            var wroteError = false;
            foreach (var spellingInfo in spellingInfos)
            {
                var wrotePath = false;
                foreach (var lineMistake in spellingInfo.LineMistakes.Where(c => c.Value.Any()))
                {
                    foreach (var (originalString, suggestedString) in lineMistake.Value)
                    {
                        if (!wrotePath)
                        {
                            Console.WriteLine(spellingInfo.Path);
                            wrotePath = true;
                            wroteError = true;
                        }

                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("{0,-5}{1}", $"{lineMistake.Key.Item1}:", originalString);
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("{0,-5}{1}", $"{lineMistake.Key.Item1}:", suggestedString);
                        Console.ResetColor();
                    }
                }

                if (!wrotePath) continue;
                Console.WriteLine();
            }

            if (!wroteError)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("No issues found.");
                Console.ResetColor();
            }
        }
    }

    internal class FileSpellingInfo
    {
        public Dictionary<(int, string), List<(string, string)>> LineMistakes = new Dictionary<(int, string), List<(string, string)>>();
        public string Path { get; set; }
    }

    internal static class StringExtensions
    {
        private static readonly Regex _regex = new Regex("[^a-zA-Z0-9]+", RegexOptions.Compiled);

        public static string RemoveSpecialCharacters(this string self) => _regex.Replace(self, string.Empty);
    }
}