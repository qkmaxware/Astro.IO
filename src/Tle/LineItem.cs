using System;
using Qkmaxware.Measurement;

namespace Qkmaxware.Astro.IO.Tle {

public class LineItem {
    public string Name {get; private set;}
    public string Catalogue {get; private set;}
    public DateTime Epoch {get; private set;}

    private static double G = 6.67384e-11;

    public Length SemimajorAxisAround(Mass parent) {
        var mu = G * (double)parent.TotalKilograms();
        // μ^1/3 / mean_motion.toRadsPerSecond^2/3
        var secondsPerDay = 86400;
        var radiansPerDay = (2 * Math.PI * RevolutionsPerDay);
        double a = 
            Math.Pow(mu, 1.0/3.0) 
            / Math.Pow(radiansPerDay / secondsPerDay, 2.0/3.0); 
        return Length.Metres(a);
    }
    public Angle Inclination {get; private set;}
    public double Eccentricity {get; private set;}
    public Angle RightAscension {get; private set;}
    public Angle ArgumentOfPerigee {get; private set;}
    public Angle MeanAnomaly {get; private set;}

    public double RevolutionsPerDay {get; private set;}

    public LineItem(
        string name, string catalogue, DateTime epoch,
        Angle i, double e, Angle ra, Angle pa, Angle mean,
        double mm
    ) {
        this.Name = name;
        this.Catalogue = catalogue;
        this.Epoch = epoch;

        this.Inclination = i;
        this.Eccentricity = e;
        this.RightAscension = ra;
        this.ArgumentOfPerigee = pa;
        this.MeanAnomaly = mean;
        this.RevolutionsPerDay = mm;
    }
}

}