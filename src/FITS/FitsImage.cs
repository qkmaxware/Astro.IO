using System;
using System.IO;
using Colour = System.Drawing.Color;

namespace Qkmaxware.Astro.IO.Fits {

public enum ScalingMode {
    Automatic, DataMinMax
}

public class ColourRamp {
    public Colour StartColour {get; private set;}
    public Colour EndColour {get; private set;}

    public static readonly ColourRamp BW = new ColourRamp(start: Colour.Black, end: Colour.White);
    
    public ColourRamp(Colour start, Colour end) {
        this.StartColour = start;
        this.EndColour = end;
    }

    public Colour this[float blend] => Colour.FromArgb(
        alpha: lerp(StartColour.A, EndColour.A, blend),
        red:   lerp(StartColour.R, EndColour.R, blend),
        green: lerp(StartColour.G, EndColour.G, blend), 
        blue:  lerp(StartColour.B, EndColour.B, blend)
    );

    private static byte lerp(float v0, float v1, float t) {
        if (t < 0)
            t = 0;
        if (t > 1)
            t = 1;
        return (byte) ((1 - t) * v0 + t * v1);
    }
}

public class FitsImageOptions {
    public Colour UndefinedPixelColour = Colour.Black;
    public ColourRamp Colours = ColourRamp.BW;
    public ScalingMode ValueScaling = ScalingMode.Automatic;

    public static readonly FitsImageOptions Default = new FitsImageOptions();
}

public class FitsImage {
    private FitsImageOptions options;

    #region statistics
    public int MaxPixelValue {get; private set;}
    private int pixelMaxScale;
    public int MinPixelValue {get; private set;}
    private int pixelMinScale;
    public int Width {get; private set;}
    public int Height {get; private set;}
    #endregion

    private int[,] pixelData;

    public FitsImage (HeaderDataUnit image, FitsImageOptions options = null) {
        this.options = options;
        if (this.options == null) {
            this.options = FitsImageOptions.Default;
        }
        if (image.Type != DataUnitType.Image && image.Type != DataUnitType.Primary) {
            throw new ArgumentException("Data unit is not an image.");
        }
        if (image.DataGroups.Count < 1) {
            MaxPixelValue = 0;
            MinPixelValue = 0;
            Width = 0;
            Height = 0;
        } else {
            var data = image.DataGroups[0];
            if (data.Dimensions < 2) {
                throw new ArgumentException("Images require at least 2 dimensions");
            }
            switch (data) {
                case DataArray<byte> byteArray:
                    copyPixels<byte>(byteArray, 0, byte.MaxValue, (old) => (int)old); break;
                case DataArray<short> shortArray:
                    copyPixels<short>(shortArray, 0, short.MaxValue, (old) => (int)old); break;
                case DataArray<int> intArray:
                    copyPixels<int>(intArray, 0, int.MaxValue, (old) => (int)old); break;
                case DataArray<long> longArray:
                    copyPixels<long>(longArray, 0, int.MaxValue, (old) => (int)old); break;
                default:
                    throw new ArgumentException($"Cannot read images with pixels of type {data.Format}");
            };
        }
    }

    private void copyPixels<T>(DataArray<T> data, int min, int max, Func<T, int> converter) {
        this.MinPixelValue = 0;
        this.MaxPixelValue = 0;
        this.Width = data.DimensionLength(0);
        this.Height = data.DimensionLength(1);
        this.pixelData = new int[Width, Height];
        if (pixelData.Length > 0) {
            this.MinPixelValue = int.MaxValue;  // These are guaranteed to be recalculated as we have pixel data
            this.MaxPixelValue = int.MinValue;  // These are guaranteed to be recalculated as we have pixel data
        }
        // Copy pixel data
        for (var i = 0; i < Width; i++) {
            for (var j = 0; j < Height; j++) {
                var point = converter(data[i, j]);  
                this.pixelData[i, j] = point;
                if (point < MinPixelValue) {
                    MinPixelValue = point;
                } else if (point > MaxPixelValue) {
                    MaxPixelValue = point;
                }                
            }
        }
        if (options.ValueScaling == ScalingMode.Automatic) {
            this.pixelMaxScale = max; // int.max, long.max etc etc
            this.pixelMinScale = min; // int.min, int.min etc etc
        } else {
            this.pixelMaxScale = this.MaxPixelValue;
            this.pixelMinScale = this.MinPixelValue;
        }
    }

    public void Tga(BinaryWriter writer) {
        var width = this.Width;
        var height = this.Height;
        //Header
        byte[] header = new byte[18];
        header[0] = (byte)0; //ID Length
        header[1] = (byte)0; //Colour map type (no colour map)
        header[2] = (byte)2; //Image type (uncompressed true-colour image)
        //Width (2 bytes)
        header[12] = (byte)(255 & width);
        header[13] = (byte)(255 & (width >> 8));
        //Height (2 bytes)
        header[14] = (byte)(255 & height);
        header[15] = (byte)(255 & (height >> 8));
        header[16] = (byte)24; //Pixel depth
        header[17] = (byte)32; //
        writer.Write(header);

        //Body
        for(int row = 0; row < height; row++) {
            for(int column = 0; column < width; column++){
                var pixel = (float)this.pixelData[column, row];
                float interpolationFactor = (pixel - pixelMinScale) / (pixelMaxScale - pixelMinScale);
                Colour c = options.Colours[interpolationFactor];
                writer.Write(c.B);
                writer.Write(c.G);
                writer.Write(c.R);
            }
        }
    }

    private byte[] toLittleEndian(int value) {
        byte[] bytes = BitConverter.GetBytes(value);
        //Then, if we need big endian for our protocol for instance,
        //Just check if you need to convert it or not:
        if (!BitConverter.IsLittleEndian)
            Array.Reverse(bytes); //reverse it so we get big endian.
        return bytes;
    }
    private byte[] toLittleEndianUnsigned(ushort value) {
        byte[] bytes = BitConverter.GetBytes(value);
        //Then, if we need big endian for our protocol for instance,
        //Just check if you need to convert it or not:
        if (!BitConverter.IsLittleEndian)
            Array.Reverse(bytes); //reverse it so we get big endian.
        return bytes;
    }
    private void writeLittleEndian(byte[] array, int offset, int value) {
        var size = toLittleEndian(value);
        array[offset + 0] = size[0]; 
        array[offset + 1] = size[1]; 
        array[offset + 2] = size[2]; 
        array[offset + 3] = size[3];
    }
    public void Bmp(BinaryWriter writer) {
        // File Type Data
        {
            var ftd = new byte[14];

            // BM in ASCII (2 bytes)
            ftd[0] = 0x42; ftd[1] = 0x4D;
            // File size; total number of bytes in file (4 bytes)
            var size = toLittleEndian(0);
            ftd[2] = size[0]; ftd[3] = size[1]; ftd[4] = size[2]; ftd[5] = size[3];
            // Reserved (2 bytes)
            ftd[6] = 0; ftd[7] = 0;
            // Reserved (2 bytes)
            ftd[8] = 0; ftd[9] = 0;
            // Pixel data offset (4 bytes)
            var offset = toLittleEndian(54); // 54 is the total sum of 14byte file  type data header + 40 byte image data header
            ftd[10] = offset[0]; ftd[11] = offset[1]; ftd[12] = offset[2]; ftd[13] = offset[3];

            writer.Write(ftd);
        }

        // Image Information Data
        {
            var info = new byte[40];

            // Header size (4 bytes)
            writeLittleEndian(info, 0, info.Length); //0-3

            // Image width (4 bytes)
            writeLittleEndian(info, 4, this.Width);  //4-7

            // Image height (4 bytes)
            writeLittleEndian(info, 8, this.Height); //8-11

            // Number of planes (2 bytes)
            info[12] = 0x01; info[13] = 0x00;        // 1 plane

            // Bits per pixel (2 bytes)
            info[14] = 0x18; info[15] = 0x00;        // 24 bits per pixel

            // Compression (4 bytes)
            // Skip (leave at 0)

            // Image size (4 bytes)
            // Skip (leave at 0)

            // x pixels per meter (4 bytes)
            // Skip (leave at 0)

            // y pixels per meter (4 bytes)
            // Skip (leave at 0)

            // total colours (4 bytes)
            // Skip (leave at 0)

            // important colours (4 bytes)
            // Skip (leave at 0)

            writer.Write(info);
        }

        // Colour Pallet
        // Skip

        // Raw Pixel Data
        // Since bit-depth is 24, 3 bytes are used to represent BGR color in order
        // Data is ordered by row, bottom up scanning
        for (var row = this.Height - 1; row >= 0; row--) {
            for (var column = 0; column < this.Width; column++) {
                var pixel = (float)this.pixelData[column, row];
                float interpolationFactor = (pixel - pixelMinScale) / (pixelMaxScale - pixelMinScale);
                Colour c = options.Colours[interpolationFactor];

                writer.Write(c.B);
                writer.Write(c.G);
                writer.Write(c.R);
            }
        }
    }
}

}