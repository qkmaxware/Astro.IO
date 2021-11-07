# Astro.Equipment
Qkmaxware.Astro.Equipment is a catalogue of astronomy equipment and utility calculation functions. Catalogue of equipment was copied by hand from the amazing [Astronomy Tools](https://astronomy.tools/calculators/field_of_view/) website.

## Example Usage
In this example we are going to perform some math based on visual observation using a telescope and an eyepiece with no barlow lens. 

First, fetch a telescope from the telescope catalogue
```cs
var telescope = Telescope.Catalogue.Where(scope => scope.Manufacturer == "Celestron" && scope.Model.Contains("5SE")).First();
```
Or create one manually
```cs
var telescope = new Telescope {
    Manufaturer = "Celestron",
    Model = "5SE",
    FocalLength = Length.Millimetres(2500),
    Aperture = Length.Millimetres(250)
};
```
Get an eyepiece from the eyepiece catalogue
```cs
var eyepiece = Eyepiece.Catalogue.Where(eye => eye.Manufacturer == "Celestron" && eye.Model == "Plossl" && eye.FocalLength >= Length.Millimetres(25)).First();
```
or create one manually 
```cs
var eyepiece = new Eyepiece {
    Manufacturer = "Celestron",
    Model = "Plossl",
    FocalLength = Length.Millimetres(25),
    FieldOfView = Angle.Degrees(60)
}
```
Perform computations
```cs
var F             = telescope.FocalRatio;
var Dawes         = telescope.DawesLimit;
var Rayleigh      = telescope.RayleighLimit;

var magnification = telescope.Magnification(eyepiece);
var fov           = telescope.FieldOfView(eyepiece);
var exitPupil     = telescope.ExitPupilDistance(eyepiece);
```