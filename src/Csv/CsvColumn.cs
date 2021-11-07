using System;

namespace Qkmaxware.Astro.IO.Csv {

[AttributeUsage(AttributeTargets.Property)]
public class CsvColumn : Attribute {
    public string Name {get; set;}

    public Type Converter {get; set;}

    public CsvColumn(string Name = null, Type Converter = null) {
        this.Name = Name;
        this.Converter = Converter;
    }
}

}