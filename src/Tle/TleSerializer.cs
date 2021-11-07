using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using Qkmaxware.Measurement;

namespace Qkmaxware.Astro.IO.Tle {

/// <summary>
/// Class for deserializing entities from two line element sets
/// </summary>
public class TleDeserializer {
    /// <summary>
    /// Deserialize a TLE file
    /// </summary>
    /// <param name="pathlike">path to the file on the filesystem</param>
    /// <returns>list of all two line element sets within the TLE file</returns>
    public IEnumerable<LineItem> DeserializeFile(string pathlike) {
        using (var reader = new StreamReader(pathlike)) {
            return Deserialize(reader).ToList();
        }
    }
    /// <summary>
    /// Deserialize a TLE file
    /// </summary>
    /// <param name="reader">text reader to a TLE file</param>
    /// <returns>list of all two line element sets within the TLE file</returns>
    public IEnumerable<LineItem> Deserialize(TextReader reader) {
        var now = DateTime.Now;
        var nowYearPrefix = now.Year.ToString().Substring(0, 2);

        string title_line = null;
        while ((title_line = reader.ReadLine()) != null) {
            string line_1 = reader.ReadLine();
            string line_2 = reader.ReadLine();
            if (line_1 == null || line_2 == null)
                break;

            // Parse line 1
            int line_1_number = (int)line_1[0];
            var catalog = line_1.Substring(2, 5);
            char @class = (char)line_1[7];
            int year = int.Parse(nowYearPrefix + line_1.Substring(18, 2));
            double dayOfYear = double.Parse(line_1.Substring(20, 12));
            DateTime epoch = DateTime.SpecifyKind(new DateTime(year, 1, 1), DateTimeKind.Utc).AddDays(dayOfYear);

            // Parse line 2
            int line_2_number = (int)line_2[0];
            double inc = double.Parse(line_2.Substring(8, 8));  // Degrees
            double ra = double.Parse(line_2.Substring(17, 8));  // Degrees
            double e = double.Parse("0." + line_2.Substring(26, 7));
            double pa = double.Parse(line_2.Substring(34, 8));  // Degrees
            double mean = double.Parse(line_2.Substring(43, 8));// Degrees
            double rev_day = double.Parse(line_2.Substring(52, 11));

            yield return new LineItem(
                name: title_line.Trim(),
                catalogue: catalog,
                epoch: epoch,
                
                i: Angle.Degrees(inc),
                e: e,
                ra: Angle.Degrees(ra),
                pa: Angle.Degrees(pa),
                mean: Angle.Degrees(mean),
                mm: rev_day
            );
        }
    }
}

}