using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Qkmaxware.Astro.IO.Csv {

public class CsvSerializer {
    private static Type converterType = typeof(ICsvConverter);

    private IEnumerable<string> readBlocks(string line) {
        StringBuilder sb = new StringBuilder(line.Length);
        bool inQuotes = false;
        for (var i = 0; i < line.Length; i++) {
            var c = line[i];
            if (inQuotes == false) {
                if (c == '\"') {
                    inQuotes = true;
                } else if (c == ',') {
                    yield return sb.ToString();
                    sb.Clear(); 
                }
            } else {
                if (c == '\\') {
                    // read next character as an escape sequence
                    i++;
                    if (i >= line.Length)
                        break; // Short circuit and end of input
                    var q = line[i];
                    switch (q) {
                        case '\"':
                            sb.Append('"'); break;
                        case '\\':
                            sb.Append('\\'); break;
                        case 'b':
                            sb.Append('\b'); break;
                        case 'f':
                            sb.Append('\f'); break;
                        case 'n':
                            sb.Append('\n'); break;
                        case 'r':
                            sb.Append('\r'); break;
                        case 't':
                            sb.Append('\t'); break;
                        case '0':
                            sb.Append('\0'); break;
                        // Default case, read character literally
                        default:
                            sb.Append(q); break;
                    }
                } else if (i == '\"') {
                    inQuotes = false;
                }
            }
        }
        yield return sb.ToString(); // Return last value
    }
    public IEnumerable<T> DeserializeFile<T>(string pathlike) where T:new() {
        using (var reader = new StreamReader(pathlike)) {
            return Deserialize<T>(reader);
        }
    }
    public IEnumerable<T> Deserialize<T>(TextReader reader) where T:new() {
        // Read type 'T' and register columns/converters
        var type = typeof(T);
        var converters = new Dictionary<PropertyInfo, ICsvConverter>();
        var columnNames = new Dictionary<string, PropertyInfo>();
        var columnList = type.GetProperties(BindingFlags.Public).Where(prop => prop.GetCustomAttribute<CsvIgnore>() == null).ToList();
        foreach (var column in columnList) {
            var columnSpec = column.GetCustomAttribute<CsvColumn>();
            // Assign name
            var name = columnSpec?.Name ?? column.Name;
            columnNames[name] = column;
            // Assign converter if one exists
            if (columnSpec != null && columnSpec.Converter != null && converterType.IsAssignableFrom(columnSpec.Converter)) {
                var converter = (ICsvConverter)Activator.CreateInstance(columnSpec.Converter);
                converters[column] = converter;
            }
        }

        // Read header
        var line = reader.ReadLine();
        var columnOrder = new List<string>();
        if (line == null) {
            // No header
            yield break;
        } else {
            // Has a header
            columnOrder = readBlocks(line).ToList();
        }

        // Foreach line under header
        while ((line = reader.ReadLine()) != null) {
            // Create instance
            var instance = new T();

            // Map instance properties to 
            var values = readBlocks(line).ToList();
            for (var i = 0; i < Math.Min(columnOrder.Count, values.Count); i++) {
                var columnName = columnOrder[i];
                var value = values[i];

                PropertyInfo property;
                if (columnNames.TryGetValue(columnName, out property)) {
                    // This column does map to a property, convert it from a string and save it
                    object parsed;
                    if (converters.ContainsKey(property)) {
                        parsed = converters[property].ConvertFromString(value);
                    } else {
                        parsed = Convert.ChangeType(value, property.PropertyType);
                    }
                    property.SetValue(instance, parsed);
                }
            }

            // Return instance
            yield return instance;
        }
    }

    private string clean(string str) {
        // Replace special characters by their escaped versions
        return str
            .Replace("\"", "\\\"")
            .Replace("\\", "\\\\")
            .Replace("\b", "\\b")
            .Replace("\f", "\\f")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t")
            .Replace("\0", "\\0");
    }

    private string quote(string str) {
        return "\"" + clean(str) + "\"";
    }

    private string quotedValueUnlessNumeric (object value) {
        if (value == null)
            return string.Empty;

        if (value is int || value is uint || value is long || value is ulong || value is byte || value is char || value is float || value is double) {
            return value.ToString();
        } else {
            return quote(value.ToString());
        }
    }

    public void Serialize<T>(TextWriter writer, IEnumerable<T> items) {
        // Read type 'T' and register columns/converters
        var type = typeof(T);
        var converters = new Dictionary<PropertyInfo, ICsvConverter>();
        var columnNames = new Dictionary<string, PropertyInfo>();
        var columnList = type.GetProperties(BindingFlags.Public).Where(prop => prop.GetCustomAttribute<CsvIgnore>() == null).ToList();
        foreach (var column in columnList) {
            var columnSpec = column.GetCustomAttribute<CsvColumn>();
            // Assign name
            var name = columnSpec?.Name ?? column.Name;
            columnNames[name] = column;
            // Assign converter if one exists
            if (columnSpec != null && columnSpec.Converter != null && converterType.IsAssignableFrom(columnSpec.Converter)) {
                var converter = (ICsvConverter)Activator.CreateInstance(columnSpec.Converter);
                converters[column] = converter;
            }
        }

        // Write header
        writer.WriteLine(
            string.Join( 
                ",", 
                columnNames.Keys.Select(column => quote(column)) 
            )
        );

        // Write rows
        foreach (var row in items) {
            if (row == null)
                continue;
            
            writer.WriteLine(
                string.Join(
                    "",
                    columnNames.Select(kv => quotedValueUnlessNumeric(kv.Value.GetValue(row)))
                )
            );
        }

    }
}

}