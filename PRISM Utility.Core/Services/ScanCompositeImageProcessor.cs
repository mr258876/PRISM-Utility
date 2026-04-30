using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

public sealed class ScanCompositeImageProcessor : IScanCompositeImageProcessor
{
    private const double MaxSampleValue = ushort.MaxValue;
    private const double MinimumVisibleWavelengthNm = 380.0;
    private const double MaximumVisibleWavelengthNm = 780.0;
    private const double MinimumOutputGamma = 0.1;
    private const double ChannelScaleLowPercentile = 0.1;
    private const double ChannelScaleHighPercentile = 99.9;
    private const double LuminanceScalePercentile = 99.5;
    private const double Epsilon = 1e-12;

    private static readonly CieColorMatchingSample[] Cie1931ColorMatchingFunctions =
    {
        new(380, 0.001368, 0.000039, 0.006450), new(385, 0.002236, 0.000064, 0.010550), new(390, 0.004243, 0.000120, 0.020050),
        new(395, 0.007650, 0.000217, 0.036210), new(400, 0.014310, 0.000396, 0.067850), new(405, 0.023190, 0.000640, 0.110200),
        new(410, 0.043510, 0.001210, 0.207400), new(415, 0.077630, 0.002180, 0.371300), new(420, 0.134380, 0.004000, 0.645600),
        new(425, 0.214770, 0.007300, 1.039050), new(430, 0.283900, 0.011600, 1.385600), new(435, 0.328500, 0.016840, 1.622960),
        new(440, 0.348280, 0.023000, 1.747060), new(445, 0.348060, 0.029800, 1.782600), new(450, 0.336200, 0.038000, 1.772110),
        new(455, 0.318700, 0.048000, 1.744100), new(460, 0.290800, 0.060000, 1.669200), new(465, 0.251100, 0.073900, 1.528100),
        new(470, 0.195360, 0.090980, 1.287640), new(475, 0.142100, 0.112600, 1.041900), new(480, 0.095640, 0.139020, 0.812950),
        new(485, 0.057950, 0.169300, 0.616200), new(490, 0.032010, 0.208020, 0.465180), new(495, 0.014700, 0.258600, 0.353300),
        new(500, 0.004900, 0.323000, 0.272000), new(505, 0.002400, 0.407300, 0.212300), new(510, 0.009300, 0.503000, 0.158200),
        new(515, 0.029100, 0.608200, 0.111700), new(520, 0.063270, 0.710000, 0.078250), new(525, 0.109600, 0.793200, 0.057250),
        new(530, 0.165500, 0.862000, 0.042160), new(535, 0.225750, 0.914850, 0.029840), new(540, 0.290400, 0.954000, 0.020300),
        new(545, 0.359700, 0.980300, 0.013400), new(550, 0.433450, 0.994950, 0.008750), new(555, 0.512050, 1.000000, 0.005750),
        new(560, 0.594500, 0.995000, 0.003900), new(565, 0.678400, 0.978600, 0.002750), new(570, 0.762100, 0.952000, 0.002100),
        new(575, 0.842500, 0.915400, 0.001800), new(580, 0.916300, 0.870000, 0.001650), new(585, 0.978600, 0.816300, 0.001400),
        new(590, 1.026300, 0.757000, 0.001100), new(595, 1.056700, 0.694900, 0.001000), new(600, 1.062200, 0.631000, 0.000800),
        new(605, 1.045600, 0.566800, 0.000600), new(610, 1.002600, 0.503000, 0.000340), new(615, 0.938400, 0.441200, 0.000240),
        new(620, 0.854450, 0.381000, 0.000190), new(625, 0.751400, 0.321000, 0.000100), new(630, 0.642400, 0.265000, 0.000050),
        new(635, 0.541900, 0.217000, 0.000030), new(640, 0.447900, 0.175000, 0.000020), new(645, 0.360800, 0.138200, 0.000010),
        new(650, 0.283500, 0.107000, 0.000000), new(655, 0.218700, 0.081600, 0.000000), new(660, 0.164900, 0.061000, 0.000000),
        new(665, 0.121200, 0.044580, 0.000000), new(670, 0.087400, 0.032000, 0.000000), new(675, 0.063600, 0.023200, 0.000000),
        new(680, 0.046770, 0.017000, 0.000000), new(685, 0.032900, 0.011920, 0.000000), new(690, 0.022700, 0.008210, 0.000000),
        new(695, 0.015840, 0.005723, 0.000000), new(700, 0.011359, 0.004102, 0.000000), new(705, 0.008111, 0.002929, 0.000000),
        new(710, 0.005790, 0.002091, 0.000000), new(715, 0.004109, 0.001484, 0.000000), new(720, 0.002899, 0.001047, 0.000000),
        new(725, 0.002049, 0.000740, 0.000000), new(730, 0.001440, 0.000520, 0.000000), new(735, 0.001000, 0.000361, 0.000000),
        new(740, 0.000690, 0.000249, 0.000000), new(745, 0.000476, 0.000172, 0.000000), new(750, 0.000332, 0.000120, 0.000000),
        new(755, 0.000235, 0.000085, 0.000000), new(760, 0.000166, 0.000060, 0.000000), new(765, 0.000117, 0.000042, 0.000000),
        new(770, 0.000083, 0.000030, 0.000000), new(775, 0.000059, 0.000021, 0.000000), new(780, 0.000042, 0.000015, 0.000000),
    };

    private readonly IScanImageDecoder _decoder;

    public ScanCompositeImageProcessor(IScanImageDecoder decoder)
    {
        _decoder = decoder;
    }

    public byte[] NormalizePassBuffer(ScanPassCapture capture, bool manuallyReverse)
    {
        var shouldReverse = !capture.DirectionPositive ^ manuallyReverse;
        if (!shouldReverse)
            return capture.ImageBytes;

        var normalized = new byte[capture.ImageBytes.Length];
        var rowBytes = ScanDebugConstants.BytesPerLine;
        for (var y = 0; y < capture.Rows; y++)
        {
            var sourceOffset = y * rowBytes;
            var destinationOffset = (capture.Rows - 1 - y) * rowBytes;
            Buffer.BlockCopy(capture.ImageBytes, sourceOffset, normalized, destinationOffset, rowBytes);
        }

        return normalized;
    }

    public bool TryBuildRgbComposite(ScanWorkflowResult result, ScanChannelAssignment assignment, ScanColorManagementOptions colorManagement, out ScanCompositePixelBuffer? frame, out string error)
    {
        frame = null;
        error = string.Empty;

        if (result.Passes.Count == 0)
        {
            error = "No scan passes are available for RGB composition.";
            return false;
        }

        if (!TryValidateRgbAssignment(assignment, out error))
            return false;

        if (!TryValidateColorManagement(colorManagement, out error))
            return false;

        var passByRole = BuildRoleMap(result, assignment);
        var width = _decoder.GetDecodedPixelsPerLine();
        var rows = result.Rows;
        if (width <= 0 || rows <= 0)
        {
            error = "Scan dimensions are invalid for RGB composition.";
            return false;
        }

        var colorTransform = BuildColorTransform(colorManagement, passByRole, width, rows);
        var pixelCount = width * rows;
        var xyzPixels = colorManagement.IsEnabled ? new XyzColor[pixelCount] : Array.Empty<XyzColor>();
        var luminanceValues = colorManagement.IsEnabled ? new double[pixelCount] : Array.Empty<double>();
        var pixelBytes = new byte[pixelCount * 4];

        if (colorManagement.IsEnabled)
        {
            for (var y = 0; y < rows; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var red = GetRoleSample(passByRole, "Red", x, y, rows);
                    var green = GetRoleSample(passByRole, "Green", x, y, rows);
                    var blue = GetRoleSample(passByRole, "Blue", x, y, rows);
                    var pixelOffset = (y * width) + x;
                    var xyz = colorTransform.ToXyz(red, green, blue);
                    xyzPixels[pixelOffset] = xyz;
                    luminanceValues[pixelOffset] = xyz.Y;
                }
            }

            colorTransform.SetLuminanceScale(ComputePositivePercentile(luminanceValues, LuminanceScalePercentile));
        }

        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pixelOffset = (y * width) + x;
                var displayColor = colorManagement.IsEnabled
                    ? colorTransform.TransformXyz(xyzPixels[pixelOffset])
                    : colorTransform.TransformSamples(
                        GetRoleSample(passByRole, "Red", x, y, rows),
                        GetRoleSample(passByRole, "Green", x, y, rows),
                        GetRoleSample(passByRole, "Blue", x, y, rows));

                var pixelIndex = pixelOffset * 4;
                pixelBytes[pixelIndex] = displayColor.Blue;
                pixelBytes[pixelIndex + 1] = displayColor.Green;
                pixelBytes[pixelIndex + 2] = displayColor.Red;
                pixelBytes[pixelIndex + 3] = 255;
            }
        }

        frame = new ScanCompositePixelBuffer(pixelBytes, width, rows);
        return true;
    }

    private Dictionary<string, (ScanPassCapture Capture, bool ManuallyReversed)> BuildRoleMap(ScanWorkflowResult result, ScanChannelAssignment assignment)
    {
        var map = new Dictionary<string, (ScanPassCapture Capture, bool ManuallyReversed)>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < result.Passes.Count && index < assignment.Roles.Count; index++)
        {
            var role = assignment.Roles[index];
            if (string.Equals(role, "Unused", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!map.ContainsKey(role))
                map[role] = (result.Passes[index], assignment.ReversedFlags[index]);
        }

        return map;
    }

    private ushort GetRoleSample(Dictionary<string, (ScanPassCapture Capture, bool ManuallyReversed)> passByRole, string role, int x, int y, int rows)
    {
        if (!passByRole.TryGetValue(role, out var entry))
            return 0;

        var sampleY = ResolveSampleRow(entry.Capture, y, rows, entry.ManuallyReversed);
        return _decoder.TryGetSample16(entry.Capture.ImageBytes, rows, x, sampleY, out var sample) ? sample : (ushort)0;
    }

    private static bool TryValidateRgbAssignment(ScanChannelAssignment assignment, out string error)
    {
        var roles = assignment.Roles;
        var rgbRoles = new[] { "Red", "Green", "Blue" };
        foreach (var role in rgbRoles)
        {
            var count = roles.Count(selected => string.Equals(selected, role, StringComparison.OrdinalIgnoreCase));
            if (count == 0)
            {
                error = $"RGB Composite requires exactly one {role} channel assignment.";
                return false;
            }

            if (count > 1)
            {
                error = $"RGB Composite cannot use duplicate {role} channel assignments.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    private static bool TryValidateColorManagement(ScanColorManagementOptions options, out string error)
    {
        if (!IsFinite(options.RedWavelengthNm)
            || !IsFinite(options.GreenWavelengthNm)
            || !IsFinite(options.BlueWavelengthNm)
            || !IsFinite(options.OutputGamma))
        {
            error = "Color management values must be finite numbers.";
            return false;
        }

        if (!IsVisibleWavelength(options.RedWavelengthNm)
            || !IsVisibleWavelength(options.GreenWavelengthNm)
            || !IsVisibleWavelength(options.BlueWavelengthNm))
        {
            error = $"RGB wavelengths must be between {MinimumVisibleWavelengthNm:0} nm and {MaximumVisibleWavelengthNm:0} nm.";
            return false;
        }

        if (options.OutputGamma < MinimumOutputGamma)
        {
            error = "Output gamma must be at least 0.1.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static int ResolveSampleRow(ScanPassCapture capture, int y, int rows, bool manuallyReverse)
    {
        var shouldReverse = !capture.DirectionPositive ^ manuallyReverse;
        return shouldReverse ? (rows - 1 - y) : y;
    }

    private RgbDisplayColorTransform BuildColorTransform(
        ScanColorManagementOptions options,
        Dictionary<string, (ScanPassCapture Capture, bool ManuallyReversed)> passByRole,
        int width,
        int rows)
    {
        if (!options.IsEnabled)
            return RgbDisplayColorTransform.CreateIdentity(options.OutputGamma);

        return new RgbDisplayColorTransform(
            InterpolateCie1931(options.RedWavelengthNm),
            InterpolateCie1931(options.GreenWavelengthNm),
            InterpolateCie1931(options.BlueWavelengthNm),
            BuildChannelScale(passByRole, "Red", width, rows),
            BuildChannelScale(passByRole, "Green", width, rows),
            BuildChannelScale(passByRole, "Blue", width, rows),
            options.OutputGamma);
    }

    private ChannelScale BuildChannelScale(
        Dictionary<string, (ScanPassCapture Capture, bool ManuallyReversed)> passByRole,
        string role,
        int width,
        int rows)
    {
        var values = new double[width * rows];
        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < width; x++)
                values[(y * width) + x] = GetRoleSample(passByRole, role, x, y, rows);
        }

        var low = ComputePercentile(values, ChannelScaleLowPercentile);
        var high = ComputePercentile(values, ChannelScaleHighPercentile);
        return high > low ? new ChannelScale(low, high) : new ChannelScale(0.0, MaxSampleValue);
    }

    private static XyzColor InterpolateCie1931(double wavelengthNm)
    {
        if (wavelengthNm <= Cie1931ColorMatchingFunctions[0].WavelengthNm)
            return Cie1931ColorMatchingFunctions[0].Color;

        var last = Cie1931ColorMatchingFunctions[^1];
        if (wavelengthNm >= last.WavelengthNm)
            return last.Color;

        for (var index = 0; index < Cie1931ColorMatchingFunctions.Length - 1; index++)
        {
            var lower = Cie1931ColorMatchingFunctions[index];
            var upper = Cie1931ColorMatchingFunctions[index + 1];
            if (wavelengthNm > upper.WavelengthNm)
                continue;

            var t = (wavelengthNm - lower.WavelengthNm) / (upper.WavelengthNm - lower.WavelengthNm);
            return new XyzColor(
                Lerp(lower.Color.X, upper.Color.X, t),
                Lerp(lower.Color.Y, upper.Color.Y, t),
                Lerp(lower.Color.Z, upper.Color.Z, t));
        }

        return last.Color;
    }

    private static double Lerp(double start, double end, double amount)
        => start + ((end - start) * amount);

    private static bool IsVisibleWavelength(double wavelengthNm)
        => wavelengthNm >= MinimumVisibleWavelengthNm && wavelengthNm <= MaximumVisibleWavelengthNm;

    private static bool IsFinite(double value)
        => !double.IsNaN(value) && !double.IsInfinity(value);

    private static double NormalizeSample(ushort sample)
        => sample / MaxSampleValue;

    private static double NormalizeSample(ushort sample, ChannelScale scale)
        => Math.Clamp((sample - scale.Low) / ((scale.High - scale.Low) + Epsilon), 0.0, 1.0);

    private static byte EncodeOutputByte(double linear)
    {
        var clamped = Math.Clamp(linear, 0.0, 1.0);
        var encoded = clamped <= 0.0031308
            ? 12.92 * clamped
            : (1.055 * Math.Pow(clamped, 1.0 / 2.4)) - 0.055;

        return (byte)Math.Clamp((int)Math.Round(encoded * byte.MaxValue), 0, byte.MaxValue);
    }

    private static byte EncodeGammaByte(double linear, double gamma)
    {
        var clamped = Math.Clamp(linear, 0.0, 1.0);
        var encoded = Math.Pow(clamped, 1.0 / gamma);
        return (byte)Math.Clamp((int)Math.Round(encoded * byte.MaxValue), 0, byte.MaxValue);
    }

    private static double ComputePositivePercentile(double[] values, double percentile)
    {
        var result = ComputePercentile(values, percentile);
        return result > Epsilon ? result : 1.0;
    }

    private static double ComputePercentile(double[] values, double percentile)
    {
        if (values.Length == 0)
            return 0.0;

        var sorted = (double[])values.Clone();
        Array.Sort(sorted);

        var rank = Math.Clamp(percentile, 0.0, 100.0) / 100.0 * (sorted.Length - 1);
        var lowerIndex = (int)Math.Floor(rank);
        var upperIndex = (int)Math.Ceiling(rank);
        if (lowerIndex == upperIndex)
            return sorted[lowerIndex];

        var t = rank - lowerIndex;
        return Lerp(sorted[lowerIndex], sorted[upperIndex], t);
    }

    private readonly record struct CieColorMatchingSample(double WavelengthNm, double X, double Y, double Z)
    {
        public XyzColor Color => new(X, Y, Z);
    }

    private readonly record struct XyzColor(double X, double Y, double Z);
    private readonly record struct ChannelScale(double Low, double High);
    private readonly record struct BgraDisplayColor(byte Blue, byte Green, byte Red);

    private sealed class RgbDisplayColorTransform
    {
        private readonly XyzColor _redPrimary;
        private readonly XyzColor _greenPrimary;
        private readonly XyzColor _bluePrimary;
        private readonly ChannelScale _redScale;
        private readonly ChannelScale _greenScale;
        private readonly ChannelScale _blueScale;
        private readonly double _outputGamma;
        private double _luminanceScale = 1.0;

        public RgbDisplayColorTransform(XyzColor redPrimary, XyzColor greenPrimary, XyzColor bluePrimary, ChannelScale redScale, ChannelScale greenScale, ChannelScale blueScale, double outputGamma)
        {
            _redPrimary = redPrimary;
            _greenPrimary = greenPrimary;
            _bluePrimary = bluePrimary;
            _redScale = redScale;
            _greenScale = greenScale;
            _blueScale = blueScale;
            _outputGamma = outputGamma;
        }

        public static RgbDisplayColorTransform CreateIdentity(double outputGamma)
            => new(
                new XyzColor(1.0, 0.0, 0.0),
                new XyzColor(0.0, 1.0, 0.0),
                new XyzColor(0.0, 0.0, 1.0),
                new ChannelScale(0.0, MaxSampleValue),
                new ChannelScale(0.0, MaxSampleValue),
                new ChannelScale(0.0, MaxSampleValue),
                outputGamma);

        public XyzColor ToXyz(ushort redSample, ushort greenSample, ushort blueSample)
        {
            var red = NormalizeSample(redSample, _redScale);
            var green = NormalizeSample(greenSample, _greenScale);
            var blue = NormalizeSample(blueSample, _blueScale);

            return new XyzColor(
                (red * _redPrimary.X) + (green * _greenPrimary.X) + (blue * _bluePrimary.X),
                (red * _redPrimary.Y) + (green * _greenPrimary.Y) + (blue * _bluePrimary.Y),
                (red * _redPrimary.Z) + (green * _greenPrimary.Z) + (blue * _bluePrimary.Z));
        }

        public void SetLuminanceScale(double luminanceScale)
            => _luminanceScale = luminanceScale > Epsilon ? luminanceScale : 1.0;

        public BgraDisplayColor TransformXyz(XyzColor xyz)
        {
            var scaledX = xyz.X / (_luminanceScale + Epsilon);
            var scaledY = xyz.Y / (_luminanceScale + Epsilon);
            var scaledZ = xyz.Z / (_luminanceScale + Epsilon);

            var linearRed = (3.2406255 * scaledX) + (-1.5372080 * scaledY) + (-0.4986286 * scaledZ);
            var linearGreen = (-0.9689307 * scaledX) + (1.8757561 * scaledY) + (0.0415175 * scaledZ);
            var linearBlue = (0.0557101 * scaledX) + (-0.2040211 * scaledY) + (1.0569959 * scaledZ);

            return new BgraDisplayColor(
                EncodeOutputByte(linearBlue),
                EncodeOutputByte(linearGreen),
                EncodeOutputByte(linearRed));
        }

        public BgraDisplayColor TransformSamples(ushort redSample, ushort greenSample, ushort blueSample)
        {
            var red = NormalizeSample(redSample);
            var green = NormalizeSample(greenSample);
            var blue = NormalizeSample(blueSample);

            return new BgraDisplayColor(
                EncodeGammaByte(blue, _outputGamma),
                EncodeGammaByte(green, _outputGamma),
                EncodeGammaByte(red, _outputGamma));
        }
    }
}
