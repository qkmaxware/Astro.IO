using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;

namespace Qkmaxware.Astro.IO.Fits {

public class FitsDeserializer {
    public IEnumerable<HeaderDataUnit> DeserializeFile(string pathlike) {
        using var file = File.Open(pathlike, FileMode.Open);
        using var reader = new BinaryReader(file);

        var fits = Deserialize(reader);
        return fits.ToList(); // Flush the entire stream before closing
    }

    private KeyValuePair<string,HeaderValue> readHeaderValue(BinaryReader reader) {
        var keyword = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(8)).Trim();
                      reader.ReadByte(); // Skip = sign
        //if (keyword == "END") {
            //return new KeyValuePair<string, string>(keyword, null);
        //}
        var rhs     = System.Text.Encoding.ASCII.GetString(reader.ReadBytes(80 - 9));
        var commentDividerIdx = rhs.LastIndexOf("/ ");
        var parts = new string[2];
        if (commentDividerIdx != -1) {
            parts[0] = (rhs.Substring(0, commentDividerIdx));      
            parts[1] = (rhs.Substring(commentDividerIdx + 1));
        } else{
            parts[0] = rhs;
            parts[1] = string.Empty;
        }
        var value = parts[0].Trim();
        var comment = parts.Length == 2 ? parts[1] : string.Empty;
        return new KeyValuePair<string, HeaderValue>(keyword, new HeaderValue(value, comment));
    }
    private int roundToMultipleOf(int value, int multiple) {
        var t = multiple + value - 1; 
        return (t - (t % multiple));
    }

    private const int DataBlockSize = 2880;
    public IEnumerable<HeaderDataUnit> Deserialize(BinaryReader reader) {
        var primary = parseDataBlock(reader, true);
        if (primary != null) {
            yield return primary;
        } else {
            throw new FileLoadException("File is missing a primary header data unit or is not a FITS file");
        }

        HeaderDataUnit additional;
        while((additional = parseDataBlock(reader)) != null) {
            yield return additional;
        }
    }

    private HeaderDataUnit parseDataBlock(BinaryReader reader, bool isPrimaryHdu = false) {
        if (reader.PeekChar() == -1) {
            // END OF STREAM
            return null;
        }

        // Create instance
        var hdu = new HeaderDataUnit();

        // Parse header
        int headerSize = 0;
        bool firstHeader = true;
        while (true) {
            var header = readHeaderValue(reader);
            if (firstHeader && isPrimaryHdu) {
                if (header.Key != "SIMPLE") {
                    throw new FileLoadException("Source is not a valid FITS file. The primary data unit of a FITS file must begin with the SIMPLE keyword.");
                }
            }
            headerSize += 80;
            firstHeader = false;
            if (header.Key == "END")
                break;
            else {
                if (hdu.Headers.ContainsKey(header.Key)) {
                    hdu.Headers[header.Key] += System.Environment.NewLine + header.Value; // Append old
                } else {
                    hdu.Headers.Add(header.Key, header.Value);
                }
            }
        }

        // Set unit type
        if (isPrimaryHdu == true) {
            hdu.Type = DataUnitType.Primary;
        } else {
            var xtension = hdu.Headers.ContainsKey("XTENSION") ? hdu.Headers["XTENSION"] : null;
            if (xtension == null) {
                hdu.Type = DataUnitType.Unknown;
            } else {
                hdu.Type = xtension.ToString() switch {
                    "IMAGE"         => DataUnitType.Image,
                    "'IMAGE'"       => DataUnitType.Image,
                    "TABLE"         => DataUnitType.Table,
                    "'TABLE'"       => DataUnitType.Table,
                    "'BINTABLE'"    => DataUnitType.BinaryTable,
                    "BINTABLE"      => DataUnitType.BinaryTable,
                    _               => DataUnitType.Unknown
                };
            }
        }

        // Eat whitespace till end of header (multiple size of 2880)
        var nearestMultiple = roundToMultipleOf(headerSize, DataBlockSize);
        var whitespaceSize = nearestMultiple - headerSize;
        for (var i = 0; i < whitespaceSize; i++) {
            reader.ReadByte();
        }
        
        // Size of each data point
        var bitsPerDataPoint = hdu.Headers.ContainsKey("BITPIX") ? int.Parse(hdu.Headers["BITPIX"]) : UINT8;
        var absBitsPerDataPoint = Math.Abs(bitsPerDataPoint);

        // Parse data
        var dimensions = hdu.Headers.ContainsKey("NAXIS") ? int.Parse(hdu.Headers["NAXIS"]) : 0;
        var pcount = hdu.Headers.ContainsKey("PCOUNT") ? int.Parse(hdu.Headers["PCOUNT"]) : 0;
        var gcount = hdu.Headers.ContainsKey("GCOUNT") ? int.Parse(hdu.Headers["GCOUNT"]) : 1;
        if (dimensions > 0) {
            // Total number of data-points / size of each dimension
            var dims = new List<int>();
            for (var i = 0; i < dimensions; i++) {
                var keyword = "NAXIS" + (i + 1); // IN FITS dimensions are 1 indexed, in C# zero indexed
                var dimSize = hdu.Headers.ContainsKey(keyword) ? int.Parse(hdu.Headers[keyword]) : 0;
                if (hdu.Type == DataUnitType.BinaryTable && i == 0) {
                    dimSize = (dimSize - 1) / absBitsPerDataPoint + 1;
                }
                dims.Add(dimSize);
            }

            // Start reading data array groups (1 in primary header *implicit* given number in XTENSIONs)
            for (var i = 0; i < gcount; i++) {
                DataArray data = null;
                switch (bitsPerDataPoint) {
                    case UINT8: 
                        hdu.DataGroups.Add(data = readUint8(reader, dims)); break;
                    case INT16:
                        hdu.DataGroups.Add(data = readInt16(reader, dims)); break;
                    case INT32:
                        hdu.DataGroups.Add(data = readInt32(reader, dims)); break;
                    case INT64:
                        hdu.DataGroups.Add(data = readInt64(reader, dims)); break;
                    case FLOAT32:
                        hdu.DataGroups.Add(data = readFloat32(reader, dims)); break;
                    case FLOAT64:
                        hdu.DataGroups.Add(data = readFloat64(reader, dims)); break;
                    default:
                        throw new FileLoadException($"Unknown BITPIX value of '{bitsPerDataPoint}'");
                }

                // Eat the special data heap indicated by pcount (do nothing with it)
                var pcountBytes = (pcount * absBitsPerDataPoint) / 8;
                for (var j = 0; j < pcountBytes; j++) {
                    reader.ReadByte();
                }
                 // Eat excess 
                if (data != null) {
                    var dataBytes = (data.Count * absBitsPerDataPoint) / 8;
                    var read = pcountBytes + dataBytes;
                    nearestMultiple = roundToMultipleOf(read, DataBlockSize);
                    whitespaceSize = nearestMultiple - read;
                    for (var s = 0; s < whitespaceSize; s++) {
                        reader.ReadByte();
                    }
                }
            }
        }

        return hdu;
    }


    private const int UINT8 = 8;
    private const int INT16 = 16;
    private const int INT32 = 32;
    private const int INT64 = 64;
    private const int FLOAT32 = -32;
    private const int FLOAT64 = -64;
    private DataArray<byte> readUint8(BinaryReader reader, IEnumerable<int> dimensionSizes) {
        var array = new DataArray<byte>(dimensionSizes);
        var amountToRead = array.Count;
        for (var i = 0; i < amountToRead; i++) {
            array[i] = reader.ReadByte();
        }
        return array;
    }

    private DataArray<short> readInt16(BinaryReader reader, IEnumerable<int> dimensionSizes) {
        var array = new DataArray<short>(dimensionSizes);
        var amountToRead = array.Count;
        for (var i = 0; i < amountToRead; i++) {
            array[i] = reader.ReadInt16();
        }
        return array;
    }
    private DataArray<int> readInt32(BinaryReader reader, IEnumerable<int> dimensionSizes) {
        var array = new DataArray<int>(dimensionSizes);
        var amountToRead = array.Count;
        for (var i = 0; i < amountToRead; i++) {
            array[i] = reader.ReadInt32();
        }
        return array;
    }
    private DataArray<long> readInt64(BinaryReader reader, IEnumerable<int> dimensionSizes) {
        var array = new DataArray<long>(dimensionSizes);
        var amountToRead = array.Count;
        for (var i = 0; i < amountToRead; i++) {
            array[i] = reader.ReadInt64();
        }
        return array;
    }
    private DataArray<float> readFloat32(BinaryReader reader, IEnumerable<int> dimensionSizes) {
        var array = new DataArray<float>(dimensionSizes);
        var amountToRead = array.Count;
        for (var i = 0; i < amountToRead; i++) {
            array[i] = reader.ReadSingle();
        }
        return array;
    }
    private DataArray<double> readFloat64(BinaryReader reader, IEnumerable<int> dimensionSizes) {
        var array = new DataArray<double>(dimensionSizes);
        var amountToRead = array.Count;
        for (var i = 0; i < amountToRead; i++) {
            array[i] = reader.ReadDouble();
        }
        return array;
    }

    private void skipDataUnit(BinaryReader reader, List<int> dims, int bitsPerDataPoint) {
        int bytesPerDataPoint = bitsPerDataPoint / 8;
        var totalDataPoints = 0;
        if (dims.Count > 0) {
            totalDataPoints = 1;
            foreach (var length in dims) {
                totalDataPoints *= length;
            }
        }

        for (var i = 0; i < totalDataPoints; i++) {
            for (var j = 0; j < bytesPerDataPoint; j++) {
                reader.ReadByte();
            }
        }

    }

}

}