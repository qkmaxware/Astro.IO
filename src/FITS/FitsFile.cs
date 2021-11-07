using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Qkmaxware.Astro.IO.Fits {

public static class FitsFile {
    public static HeaderDataUnit PrimaryDataUnit(this IEnumerable<HeaderDataUnit> dataUnits) => dataUnits.FirstOrDefault();
}

}