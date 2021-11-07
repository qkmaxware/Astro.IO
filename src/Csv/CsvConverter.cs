using System;

namespace Qkmaxware.Astro.IO.Csv {
public interface ICsvConverter {
    string ConvertToString(object value);
    object ConvertFromString(string serialized);
}

}