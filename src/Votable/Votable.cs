using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Qkmaxware.Astro.IO.Votable {

// TODO copy from https://github.com/qkmaxware/Astro/blob/root/Astro/src/IO/VOTableSerializer.cs

/// <summary>
/// VO Table
/// </summary>
public class Votable : IEnumerable<List<KeyValuePair<string,string>>> {
    /// <summary>
    /// Name of the table
    /// </summary>
    /// <value>name</value>
    public string Name {get; private set;}
    /// <summary>
    /// Coordinate system for all VO Tables
    /// </summary>
    /// <value>coordinate system</value>
    public string CoordinateSystem {get; private set;}
    /// <summary>
    /// Equinox for all VO Tables
    /// </summary>
    /// <value>equinox</value>
    public string Equinox {get; private set;}
    /// <summary>
    /// Epoch for all VO Tables
    /// </summary>
    /// <value>epoch</value>
    public string Epoch {get; private set;}

    private List<string> fields;
    public IEnumerable<string> Fields => fields.AsReadOnly();
    private List<List<string>> values;

    /// <summary>
    /// Fetch a desired table element by its row number and column name
    /// </summary>
    /// <value>cell value</value>
    public string this[int row, string column] {
        get => this[row, fields.IndexOf(column)];
        set => this[row, fields.IndexOf(column)] = value;
    }
    /// <summary>
    /// Fetch a desired table element by its row number and column number
    /// </summary>
    /// <value>cell value</value>
    public string this[int row, int column] {
        get {
            if (row >= 0 && row < this.RowCount) {
                var instance = this.values[row];
                if (column >= 0 && column < instance.Count) {
                    return instance[column];
                } else {
                    return null;
                }
            } else{
                return null;
            }
        }
        set {
            if (row >= 0 && row < this.RowCount) {
                var instance = this.values[row];
                if (column >= 0 && column < fields.Count) {
                    // Grow column to allow if row is smaller than column count
                    while (column <= instance.Count)
                        instance.Add(null);
                    instance[column] = value;
                } 
            } 
        }
    }

    /// <summary>
    /// Total number of rows in this table
    /// </summary>
    public int RowCount => this.values.Count;

    /// <summary>
    /// Total number of columns in this table
    /// </summary>
    public int ColumnCount => this.fields.Count;

    public Votable(string name, string coordinateSystem, string equinox, string epoch, List<string> fields) {
        this.Name = name;
        this.CoordinateSystem = coordinateSystem;
        this.Equinox = equinox;
        this.Epoch = epoch;

        this.fields = fields ?? new List<string>();
        this.values = new List<List<string>>();
    }

    public void Add(List<string> row) {
        this.values.Add(row);
    }

    public IEnumerator<List<KeyValuePair<string, string>>> GetEnumerator() {
        foreach (var row in this.values) {
            yield return fields.Select((field, index) => new KeyValuePair<string, string>(
                field,
                row.ElementAtOrDefault(index)
            )).ToList();
        }
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return this.GetEnumerator();
    }
}

}