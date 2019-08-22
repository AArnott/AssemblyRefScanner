using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace AssemblyRefScanner
{
    class Program
    {
        private const string baseDir = @"C:\Program Files (x86)\Microsoft Visual Studio\2019\master";

        static void Main(string[] args)
        {
            var results = new Dictionary<Version, List<string>>();
            foreach (var file in Directory.EnumerateFiles(baseDir, "*.dll", SearchOption.AllDirectories))
            {
                try
                {
                    var assembly = Assembly.LoadFile(file);
                    var interestingRefs = assembly.GetReferencedAssemblies().Where(n => n.Name == "StreamJsonRpc");
                    foreach (var r in interestingRefs)
                    {
                        if (!results.TryGetValue(r.Version, out var list))
                        {
                            list = new List<string>();
                            results.Add(r.Version, list);
                        }

                        list.Add(file.Substring(baseDir.Length));
                        Console.Write('.');
                    }
                }
                catch (BadImageFormatException) { }
                catch (FileNotFoundException) { }
                catch (FileLoadException) { }
            }

            Console.WriteLine();
            foreach (var version in results.OrderBy(kv => kv.Key))
            {
                Console.WriteLine(version.Key);
                foreach (var file in version.Value)
                {
                    Console.WriteLine(file);
                }

                Console.WriteLine();
            }

            Console.WriteLine("All done!");
            Console.ReadLine();
        }
    }
}
