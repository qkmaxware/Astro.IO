using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Qkmaxware.Astro.IO.Fits;
using Qkmaxware.Astro.IO.Tle;

class Program {
    public static void Main(string[] args) {
        TestFits();
    }

    public static void TestTle() {
        var tlePath = "data/stations.tle";

        var parser = new TleDeserializer();
        var tle = parser.DeserializeFile(tlePath).ToList();
        Console.WriteLine(tle.Count);
        Console.WriteLine(tle.First().Name);
    }

    public static void TestFits() {
        //var sampleTable1 = "data/sample.table.fits";
        //var sampleTable2 = "data/rosat_pspc_rdf2_3_basic.fits";
        var sampleImage1 = "data/sample.image.fits";
        //var sampleImage2 = "data/rosat_pspc_rdf2_3_bk1.fits";

        //incrementallyReadFits(sampleImage1);
        convertToBmp(sampleImage1);
        // incrementallyReadFits(imgFilePath);
    }

    private static void convertToBmp(string dataFilePath) {
        var hdu = new FitsDeserializer().DeserializeFile(dataFilePath).PrimaryDataUnit();
        var img = new FitsImage(hdu, new FitsImageOptions {
            ValueScaling = ScalingMode.DataMinMax
        });
        using (var fs = File.Open("sample.bmp", FileMode.Create))
        using (var fsw = new BinaryWriter(fs)) {
            img.Bmp(fsw);
        }
    }
    private static void incrementallyReadFits(string dataFilePath) {
        var parser = new FitsDeserializer();
        using (var file = File.Open(dataFilePath, FileMode.Open))
        using (var reader = new BinaryReader(file)) {
            var dataUnits = parser.Deserialize(reader);
            var i = 1;
            foreach (var unit in dataUnits) {
                var sizeString = string.Join("x", unit.DataGroups.FirstOrDefault()?.AllDimensionLengths ?? new List<int>{0});
                Console.WriteLine($"{i++} | {unit.Name ?? "null"} | {unit.Type} | {unit.DataGroups?.Count??0} groups of {sizeString}");
                foreach (var header in unit.Headers) {
                    Console.WriteLine($"    {header.Key} = {header.Value}");
                }
                string c;
                if (unit.DataGroups.Count > 0) {
                    Console.WriteLine("Read Table? (y/n)...");
                    var _00 = unit.DataGroups[0].flattenIndex(0, 0);
                    var _10 = unit.DataGroups[0].flattenIndex(1, 0);
                    var _01 = unit.DataGroups[0].flattenIndex(0, 1);
                    var _11 = unit.DataGroups[0].flattenIndex(1, 1);
                    Console.WriteLine("    " + _00 + "," + _10);
                    Console.WriteLine("    " + _01 + "," + _11);
                    c = Console.ReadLine().Trim();
                    if (c == "y" || c == "Y") {
                        var table = unit.DataGroups[0];
                        for (var row = 0; row < table.RowCount; row++) {
                            Console.Write(row + ",");
                            for (var column = 0; column < table.ColumnCount; column++) {
                                Console.Write(table.GetElementString(column, row) + ",");
                            }
                            Console.WriteLine();
                        }
                    } else {
                        break;
                    }
                }

                Console.Write("Continue (y/n)...");
                c = Console.ReadLine().Trim();
                if (c == "y" || c == "Y") {
                    Console.WriteLine();
                } else {
                    break;
                }
            }   
        }
    }
}