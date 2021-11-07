using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace Qkmaxware.Astro.IO.Votable {

// TODO copy from https://github.com/qkmaxware/Astro/blob/root/Astro/src/IO/VOTableSerializer.cs

/// <summary>
/// Class to deserialize data from XML formatted VO Table files
/// </summary>
public class VotableDeserializer {
    /// <summary>
    /// Deserialize VO Tables from the given file
    /// </summary>
    /// <param name="pathlike">path to the file on the filesystem</param>
    /// <returns>VOTables</returns>
    public IEnumerable<Votable> DeserializeFile(string pathlike) {
        using (var reader = new StreamReader(pathlike)) {
            return Deserialize(reader).ToList();
        }
    }

    /// <summary>
    /// Deserialize VO Tables from the given text reader
    /// </summary>
    /// <param name="reader">reader</param>
    /// <returns>VOTables</returns>
    public IEnumerable<Votable> Deserialize(TextReader reader) {
        var xmlReader = new XmlTextReader(reader);
        xmlReader.Namespaces = false;
        XmlDocument doc = new XmlDocument();
        doc.Load(xmlReader);

        if (doc.DocumentElement?.Name != "VOTABLE") {
            throw new ArgumentException("missing VOTABLE root element");
        }

        var raw_coordinate_system = doc.DocumentElement.SelectSingleNode("/VOTABLE/DEFINITIONS/COOSYS");
        if (raw_coordinate_system == null) {
            throw new ArgumentException("missing COOSYS element");
        }
        var equinox = raw_coordinate_system.Attributes.GetNamedItem("equinox").Value;
        var epoch = raw_coordinate_system.Attributes.GetNamedItem("epoch").Value;
        var system = raw_coordinate_system.Attributes.GetNamedItem("system").Value;

        var tables = doc.SelectNodes("/VOTABLE/RESOURCE/TABLE");
        if (tables == null) {
            throw new ArgumentException("missing TABLE element");
        }

        foreach (XmlNode table in tables) {
            // Create fields
            var raw_fields = table.SelectNodes("FIELD");
            List<string> fieldNames = new List<string>();
            foreach (XmlNode field in raw_fields) {
                var name = field.Attributes.GetNamedItem("name")?.Value;
                fieldNames.Add(name ?? string.Empty);
            }

            // Create table from fields
            var currentTable = new Votable(
                name: table.Attributes.GetNamedItem("name")?.Value ?? string.Empty,
                coordinateSystem: system,
                equinox: equinox,
                epoch: epoch,
                fields: fieldNames
            );
            
            // Create rows
            var raw_rows = table.SelectNodes("DATA/TABLEDATA/TR");
            if (raw_rows != null) {
                for (var rowid = 0; rowid < raw_rows.Count; rowid++) {
                    var data = new Dictionary<string,string>();
                    var row = raw_rows.Item(rowid);
                    var cells = row.SelectNodes("TD");
                    
                    List<string> rowCellValues = new List<string>();
                    for (var cellid = 0; cellid < cells.Count; cellid++) {
                        var cell = cells.Item(cellid);
                        rowCellValues.Add(cell.InnerText);
                    }

                    currentTable.Add(rowCellValues);
                }
            }

            yield return currentTable;
        }
    }
}

}