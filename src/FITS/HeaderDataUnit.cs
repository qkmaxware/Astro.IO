using System;
using System.Linq;
using System.Collections.Generic;

namespace Qkmaxware.Astro.IO.Fits {

public enum DataUnitType {
    Unknown, Primary, Image, Table, BinaryTable
}

public class HeaderValue {
    private string value;
    private string comment;
    public HeaderValue(string value, string comment = null) {
        this.value = value;
        this.comment = comment;
    }

    public static implicit operator string(HeaderValue header) {
        return header?.value;
    }
    public static HeaderValue operator + (HeaderValue value, string additional) {
        return new HeaderValue(value.value + additional, value.comment);
    }

    public override string ToString() => value;
    public string ToStringWithComment() => value + " / " + comment;

    public string Comment() => comment ?? string.Empty;
}
public class HeaderDataUnit {
    public Dictionary<string,HeaderValue> Headers {get; private set;}
    public string Name => Headers != null && Headers.ContainsKey("EXTNAME") ? Headers["EXTNAME"] : null;
    public DataUnitType Type {get; set;} = DataUnitType.Unknown;
    public List<DataArray> DataGroups {get; set;} = new List<DataArray>();

    public HeaderDataUnit () {
        this.Headers = new Dictionary<string, HeaderValue>();
    }

}

public class DataArray {
    /// <summary>
    /// Type of object stored in this matrix
    /// </summary>
    /// <returns>type of stored object</returns>
    public virtual Type Format => typeof(object);

    /// <summary>
    /// Total number of dimensions in this matrix
    /// </summary>
    public int Dimensions => dimensionLengths.Count;

    /// <summary>
    /// Total count of all elements in this matix
    /// </summary>
    public int Count {
        get {
            if (Dimensions == 0)
                return 0;

            var product = 1;
            foreach (var length in dimensionLengths) {
                product *= length;
            }
            return product;
        }
    }

    /// <summary>
    /// For a standard 2D matrix, count the number of rows
    /// </summary>
    public int RowCount => dimensionLengths.Count >= 2 ? dimensionLengths[1] : 0;

    /// <summary>
    /// For a standard 2D matrix, count the number of columns
    /// </summary>
    public int ColumnCount => dimensionLengths.Count >= 1 ? dimensionLengths[0] : 0;

    private List<int> dimensionLengths;

    /// <summary>
    /// The lengths of all dimensions in this matrix
    /// </summary>
    /// <returns>list of dimension lengths</returns>
    public IEnumerable<int> AllDimensionLengths => dimensionLengths.AsReadOnly();
    /// <summary>
    /// Get the length of the given matrix dimension
    /// </summary>
    /// <param name="dim">dimension to get the length of</param>
    /// <returns>length of the given dimension</returns>
    public int DimensionLength(int dim) {
        if (dim >= 0 && dim < Dimensions)
            return this.dimensionLengths[dim];
        else
            return 0;
    }

    /// <summary>
    /// Create a new data array with the given axis dimensions
    /// </summary>
    /// <param name="dimensions">dimension lengths for each axis</param>
    public DataArray(IEnumerable<int> dimensions) {
        this.dimensionLengths = dimensions.ToList();
    }

    /// <summary>
    /// Get the string representation of a particular element in the matrix
    /// </summary>
    /// <param name="d1Index">first axis index</param>
    /// <param name="dnIndices">remaining axis indices</param>
    /// <returns>string</returns>
    public virtual string GetElementString(int d1Index, params int[] dnIndices) {
        return null;
    }

    /// <summary>
    /// Flatten a multi-dimensional index to a 1d index
    /// </summary>
    /// <param name="d1Index">first axis index</param>
    /// <param name="dnIndices">remainder axis index</param>
    /// <returns>flattened index</returns>
    internal int flattenIndex(int d1Index, params int[] dnIndices) {
        /*
        x + y*WIDTH + Z*WIDTH*DEPTH. 
        Visualize it as a rectangular solid: first you traverse along x, then each y is a "line" width steps long, and each z is a "plane" WIDTH*DEPTH steps in area.
        */

        // Create an index on all dimensions
        var indexMap = new List<int>(this.Dimensions);
        if (Dimensions > 0)
            indexMap.Add(d1Index);
        for (var i = 1; i < indexMap.Capacity; i++) {
            var zI = i - 1;
            indexMap.Add(zI >= 0 && zI < dnIndices.Length ? dnIndices[zI] : 0);
        }

        // Flatten
        var index = 0;
        for (var i = 0; i < indexMap.Count; i++) {
            var multiplier = 1;
            for (var j = i - 1; j >= 0; j--) {
                multiplier *= this.DimensionLength(j);
            }
            index += multiplier * indexMap[i];
        }
        
        return index;
    }
}

public class DataArray<T> : DataArray {
    public override Type Format => typeof(T);
    public DataArray(IEnumerable<int> dimensions) : base(dimensions) {
        this.data = new T[this.Count];
    }
    private T[] data {get; set;}

    public T this[int d1Index] {
        get => this.data[d1Index];
        set => this.data[d1Index] = value;
    }
    public T this [int d1Index, params int[] dnIndices] {
        get {
            return this[flattenIndex(d1Index, dnIndices)];
        }
        set {
            this[flattenIndex(d1Index, dnIndices)] = value;
        }
    }

    /// <summary>
    /// Get the string representation of a particular element in the matrix
    /// </summary>
    /// <param name="d1Index">first axis index</param>
    /// <param name="dnIndices">remaining axis indices</param>
    /// <returns>string</returns>
    public override string GetElementString(int d1Index, params int[] dnIndices) {
        var index = flattenIndex(d1Index, dnIndices);
        if (index >= 0 && index < this.data.Length) {
            return this.data[index]?.ToString();
        } else {
            return null;
        }
    }
}


}