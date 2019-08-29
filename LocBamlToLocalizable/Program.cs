using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration.Attributes;

namespace LocBamlToLocalizable
{
    class Program
    {
        // This tool takes CSV files from Microsoft's "sample" LocBaml localization tool and outputs sanitized, easily translatable Key-Value CSVs for Crowdin
        static void Main(string[] args)
        {
            using (var reader = new StreamReader("en-US.csv"))
            using (var csv = new CsvReader(reader))
            {
                csv.Configuration.HasHeaderRecord = false;
                var records = csv.GetRecords<LocBamlLine>();

                var localizable = new List<LocalizableLine>();
                var locBamlLines = records as LocBamlLine[] ?? records.ToArray();
                foreach (var locBamlLine in locBamlLines)
                {
                    if (locBamlLine.LocalizerModifiable == true && locBamlLine.LocalizerModifiable == true
                                                                && AllowedCategories.Contains(locBamlLine.Category)
                                                                && !string.IsNullOrEmpty(locBamlLine.Value))
                    {
                        Console.WriteLine($"{locBamlLine.ResourceKey}: {locBamlLine.Value}");
                        localizable.Add(new LocalizableLine
                        {
                            Key = locBamlLine.ResourceKey,
                            Value = locBamlLine.Value
                        });
                    }
                }

                using (var writer = new StreamWriter("localizable.csv"))
                using (var csvWriter = new CsvWriter(writer))
                {    
                    csvWriter.WriteRecords(localizable);
                    writer.Flush();
                }

                Console.WriteLine($"\nWrote {localizable.Count} localizable strings.");
                Console.ReadLine();

                if (File.Exists("localized.csv"))
                {
                    using (var localizedReader = new StreamReader("localized.csv"))
                    using (var localizedCsv = new CsvReader(localizedReader))
                    {
                        localizedCsv.Configuration.HasHeaderRecord = false;
                        var localizedRecords = localizedCsv.GetRecords<LocalizableLine>();
                        var localizedLines = localizedRecords as LocalizableLine[] ?? localizedRecords.ToArray();
                        
                        foreach (var locBamlLine in locBamlLines)
                        {
                            var localizedLine = localizedLines.FirstOrDefault(x => x.Key == locBamlLine.ResourceKey);

                            if (localizedLine != null)
                            {
                                locBamlLine.Value = localizedLine.Value;
                                Console.WriteLine($"Modified line: {locBamlLine.ResourceKey}: {locBamlLine.Value}");
                            }
                        }

                        using (var writer = new StreamWriter("out.csv"))
                        using (var csvWriter = new CsvWriter(writer))
                        {
                            csvWriter.WriteRecords(locBamlLines);
                            writer.Flush();
                        }

                        Console.ReadLine();
                    }
                }
            }
        }

        private static readonly string[] AllowedCategories = new[]
        {
            "Text",
            "Label",
            "ComboBox",
            "ToolTip",
            "Title"
        };
    }
    public class LocBamlLine
    {
        [Index(0)]
        public string BamlName { get; set; }
        [Index(1)]
        public string ResourceKey { get; set; }
        [Index(2)]
        public string Category { get; set; }

        [Index(3)]
        public bool LocalizerReadable { get; set; }
        [Index(4)]
        public bool LocalizerModifiable { get; set; }
        [Index(5)]
        public string Comment { get; set; }
        [Index(6)]
        public string Value { get; set; }
    }

    public class LocalizableLine
    {
        [Index(0)]
        public string Key { get; set; }
        [Index(1)]
        public string Value { get; set; }
    }
}
