using PRISM_Utility.Contracts.Services;
using PRISM_Utility.Models;

namespace PRISM_Utility.Services;

public class ScanParameterService : IScanParameterService
{
    private readonly IScanProtocolService _protocol;

    private static readonly ScanParameterDefinition ExposureTicksParameter = new("Exposure", "prism.exposure_ticks");
    private static readonly ScanParameterDefinition Adc1OffsetParameter = new("ADC1 Offset", "prism.adc1.offset");
    private static readonly ScanParameterDefinition Adc1GainParameter = new("ADC1 Gain", "prism.adc1.gain");
    private static readonly ScanParameterDefinition Adc2OffsetParameter = new("ADC2 Offset", "prism.adc2.offset");
    private static readonly ScanParameterDefinition Adc2GainParameter = new("ADC2 Gain", "prism.adc2.gain");

    private static readonly ScanParameterDefinition[] _definitions =
    {
        ExposureTicksParameter,
        Adc1OffsetParameter,
        Adc1GainParameter,
        Adc2OffsetParameter,
        Adc2GainParameter
    };

    public IReadOnlyList<ScanParameterDefinition> Definitions => _definitions;

    public ScanParameterService(IScanProtocolService protocol)
    {
        _protocol = protocol;
    }

    public bool TryParseInput(string exposureTicks, string adc1Offset, string adc1Gain, string adc2Offset, string adc2Gain, out ScanParameterSnapshot snapshot, out string error)
    {
        snapshot = default!;

        if (!TryParseUInt16(exposureTicks, ExposureTicksParameter.DisplayName, out var exposure, out error)
            || !TryParseOffset(adc1Offset, Adc1OffsetParameter.DisplayName, out var adc1OffsetParsed, out error)
            || !TryParseGain(adc1Gain, Adc1GainParameter.DisplayName, out var adc1GainParsed, out error)
            || !TryParseOffset(adc2Offset, Adc2OffsetParameter.DisplayName, out var adc2OffsetParsed, out error)
            || !TryParseGain(adc2Gain, Adc2GainParameter.DisplayName, out var adc2GainParsed, out error))
        {
            return false;
        }

        snapshot = new ScanParameterSnapshot(exposure, adc1OffsetParsed, adc1GainParsed, adc2OffsetParsed, adc2GainParsed);
        error = string.Empty;
        return true;
    }

    public ScanParameterDisplays BuildDisplays(string exposureTicks, string adc1Offset, string adc1Gain, string adc2Offset, string adc2Gain)
    {
        var exposureDisplay = BuildExposureDisplay(exposureTicks);
        var adc1OffsetDisplay = BuildOffsetDisplay(adc1Offset);
        var adc2OffsetDisplay = BuildOffsetDisplay(adc2Offset);
        var adc1GainDisplay = BuildGainDisplay(adc1Gain);
        var adc2GainDisplay = BuildGainDisplay(adc2Gain);

        return new ScanParameterDisplays(exposureDisplay, adc1OffsetDisplay, adc2OffsetDisplay, adc1GainDisplay, adc2GainDisplay);
    }

    public string FormatOffsetForInput(int offset)
        => offset >= 0 ? $"+{offset}" : offset.ToString();

    public async Task<ScanParameterSnapshot> LoadAsync(IScanSessionService session, CancellationToken ct)
    {
        var loaded = new Dictionary<string, ushort>(_definitions.Length);
        foreach (var parameter in _definitions)
        {
            var keyHash = _protocol.ComputeParamKeyHash(parameter.Key);
            var command = _protocol.BuildGetParamByHashCommand(keyHash);
            var response = await session.SendControlCommandAndWaitAckAsync(command, ScanDebugConstants.UsbCmdGetParamByHash, ScanDebugConstants.AckTimeoutMs, ct, true);
            if (response.Status != 0x00)
                throw new IOException($"GET_PARAM '{parameter.DisplayName}' failed: {_protocol.MapStatus(response.Status)} (0x{response.Status:X2})");

            loaded[parameter.Key] = _protocol.ParseU16ParamPayload(response.Payload, keyHash, parameter.DisplayName);
        }

        return new ScanParameterSnapshot(
            loaded[ExposureTicksParameter.Key],
            DecodeSignedOffset(loaded[Adc1OffsetParameter.Key]),
            loaded[Adc1GainParameter.Key],
            DecodeSignedOffset(loaded[Adc2OffsetParameter.Key]),
            loaded[Adc2GainParameter.Key]);
    }

    public async Task ApplyAsync(IScanSessionService session, ScanParameterSnapshot snapshot, CancellationToken ct)
    {
        await SetParameterAsync(session, ExposureTicksParameter, snapshot.ExposureTicks, ct);
        await SetParameterAsync(session, Adc1OffsetParameter, EncodeSignedOffset(snapshot.Adc1Offset), ct);
        await SetParameterAsync(session, Adc1GainParameter, snapshot.Adc1Gain, ct);
        await SetParameterAsync(session, Adc2OffsetParameter, EncodeSignedOffset(snapshot.Adc2Offset), ct);
        await SetParameterAsync(session, Adc2GainParameter, snapshot.Adc2Gain, ct);
    }

    private async Task SetParameterAsync(IScanSessionService session, ScanParameterDefinition parameter, ushort value, CancellationToken ct)
    {
        var keyHash = _protocol.ComputeParamKeyHash(parameter.Key);
        var command = _protocol.BuildSetParamByHashCommand(keyHash, value);
        var response = await session.SendControlCommandAndWaitAckAsync(command, ScanDebugConstants.UsbCmdSetParamByHash, ScanDebugConstants.AckTimeoutMs, ct, true);
        if (response.Status != 0x00)
            throw new IOException($"SET_PARAM '{parameter.DisplayName}' failed: {_protocol.MapStatus(response.Status)} (0x{response.Status:X2})");

        var echoed = _protocol.ParseU16ParamPayload(response.Payload, keyHash, parameter.DisplayName);
        if (echoed != value)
            throw new IOException($"SET_PARAM '{parameter.DisplayName}' verify mismatch: expected {value}, echoed {echoed}");
    }

    private static bool TryParseUInt16(string text, string fieldName, out ushort value, out string error)
    {
        if (!ushort.TryParse(text, out value))
        {
            error = $"{fieldName} must be an integer in [{ScanDebugConstants.MinExposureTicks}, 65535].";
            return false;
        }

        if (value < ScanDebugConstants.MinExposureTicks)
        {
            error = $"{fieldName} must be an integer in [{ScanDebugConstants.MinExposureTicks}, 65535].";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool TryParseGain(string text, string fieldName, out ushort value, out string error)
    {
        value = 0;
        if (!ushort.TryParse(text, out var parsed) || parsed > 63)
        {
            error = $"{fieldName} must be an integer in [0, 63].";
            return false;
        }

        value = parsed;
        error = string.Empty;
        return true;
    }

    private static bool TryParseOffset(string text, string fieldName, out int value, out string error)
    {
        value = 0;
        if (!int.TryParse(text, out var parsedOffset) || parsedOffset < -255 || parsedOffset > 255)
        {
            error = $"{fieldName} must be an integer in [-255, +255].";
            return false;
        }

        value = parsedOffset;
        error = string.Empty;
        return true;
    }

    private static int DecodeSignedOffset(ushort raw)
    {
        var magnitude = raw & 0x00FF;
        var isNegative = (raw & 0x0100) != 0;
        return isNegative ? -magnitude : magnitude;
    }

    private static ushort EncodeSignedOffset(int signedOffset)
    {
        var magnitude = Math.Abs(signedOffset);
        var signBit = signedOffset < 0 ? 0x0100 : 0;
        return (ushort)(signBit | magnitude);
    }

    private static string BuildExposureDisplay(string exposureText)
    {
        if (!ushort.TryParse(exposureText, out var ticks))
            return "Exposure time: -";

        var exposureNs = (((ticks + 1.0) * 12.0) + 45636.0) * 8.0;
        var exposureUs = exposureNs / 1000.0;
        return $"Exposure time: {exposureNs:0.##} ns ({exposureUs:0.###} us)";
    }

    private static string BuildOffsetDisplay(string offsetText)
    {
        if (!int.TryParse(offsetText, out var offset) || offset < -255 || offset > 255)
            return "Offset amplitude: -";

        var offsetMv = offset * 300.0 / 256.0;
        return $"Offset amplitude: {offsetMv:+0.###;-0.###;0} mV";
    }

    private static string BuildGainDisplay(string gainText)
    {
        if (!int.TryParse(gainText, out var gain) || gain < 0 || gain > 63)
            return "Gain: -";

        var ratio = 6.0 / (1.0 + 5.0 * ((63.0 - gain) / 63.0));
        return $"Gain: {ratio:0.###} V/V";
    }
}
