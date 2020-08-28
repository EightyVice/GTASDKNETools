using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CommandLine;

namespace GTASDK.Generator
{
    public static class Cli
    {
        public class Options
        {
            [Option('d', "dryrun", Required = false, HelpText = "Prints what the generator is going to write to the output files, but does not write anything.")]
            public bool DryRun { get; set; }

            [Option('o', "output", Required = true, HelpText = "The output directory for the generated .cs files.")]
            public string OutputDirectory { get; set; }

            [Value(0, MetaName = "input", HelpText = "The input directory, to be parsed recursively for template files.")]
            public string TemplateDirectory { get; set; }
        }

        private static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(RunWithOptions)
                .WithNotParsed(HandleParseError);
        }
        
        private static void RunWithOptions(Options options)
        {
            if (!Directory.Exists(options.TemplateDirectory))
            {
                throw new ArgumentException($"The directory at {options.TemplateDirectory} does not exist or is not a directory", nameof(options.TemplateDirectory));
            }

            if (!Directory.Exists(options.OutputDirectory))
            {
                throw new ArgumentException($"The directory at {options.OutputDirectory} does not exist or is not a directory", nameof(options.OutputDirectory));
            }

            string GetOutputBasePathForInput(string file)
            {
                var targetPath = Path.Combine(options.OutputDirectory, MakeRelative(file, options.TemplateDirectory));
                return Path.GetDirectoryName(targetPath);
            }

            foreach (var subdirectory in Directory.EnumerateDirectories(options.TemplateDirectory))
            {
                var generator = new Generator(subdirectory);

                foreach (var file in Directory.EnumerateFiles(subdirectory))
                {
                    switch (Path.GetExtension(file))
                    {
                        case ".yml":
                            var type = generator.GetCachedTypeGraph(Path.GetFileNameWithoutExtension(file));

                            var outputBasePath = GetOutputBasePathForInput(file);
                            if (options.DryRun)
                            {
                                Debug.WriteLine($"Writing the following text to {outputBasePath}, generated from {file}:");
                                foreach (var kvp in type.GraphToString())
                                {
                                    Debug.WriteLine($"{Path.Combine(outputBasePath, kvp.Key)}: {kvp.Value}");
                                }
                            }
                            else
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(outputBasePath) ?? throw new InvalidOperationException($"Invalid output path: {outputBasePath}"));
                                foreach (var kvp in type.GraphToString())
                                {
                                    File.WriteAllText(Path.Combine(outputBasePath, kvp.Key), kvp.Value);
                                }
                            }

                            Debug.WriteLine($"Processed {outputBasePath}");
                            break;
                    }
                }
            }
        }

        private static void HandleParseError(IEnumerable<Error> errors)
        {
            Debug.WriteLine("Error while parsing command-line arguments:");
            foreach (var error in errors)
            {
                Debug.WriteLine(error);
            }
        }

        // https://stackoverflow.com/a/41578297
        /// <summary>
        /// Rebases file with path fromPath to folder with baseDir.
        /// </summary>
        /// <param name="fromPath">Full file path (absolute)</param>
        /// <param name="baseDir">Full base directory path (absolute)</param>
        /// <returns>Relative path to file in respect of baseDir</returns>
        private static string MakeRelative(string fromPath, string baseDir)
        {
            const string pathSep = "\\";
            var fromPathFull = Path.GetFullPath(fromPath);
            var baseDirFull = Path.GetFullPath(baseDir); // If folder contains upper folder references, they gets lost here. "c:\test\..\test2" => "c:\test2"

            var p1 = Regex.Split(fromPathFull, "[\\\\/]").Where(x => x.Length != 0).ToArray();
            var p2 = Regex.Split(baseDirFull, "[\\\\/]").Where(x => x.Length != 0).ToArray();
            var i = 0;

            for (; i < p1.Length && i < p2.Length; i++)
                if (string.Compare(p1[i], p2[i], StringComparison.OrdinalIgnoreCase) != 0) // Case insensitive match
                    break;

            if (i == 0) // Cannot make relative path, for example if resides on different drive
                return fromPathFull;

            var r = string.Join(pathSep, Enumerable.Repeat("..", p2.Length - i).Concat(p1.Skip(i).Take(p1.Length - i)));
            return r;
        }
    }
}
