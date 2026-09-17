using System.Globalization;
using PRISM_Utility.Core.Contracts.Services;
using PRISM_Utility.Core.Helpers;
using PRISM_Utility.Core.Models;

namespace PRISM_Utility.Core.Services;

public class ScanParameterService : IScanParameterService
{
    private readonly IScanProtocolService _protocol;

    private static readonly ScanParameterDefinition ExposureTicksParameter = new("Exposure", "prism.exposure_ticks");
    private static readonly ScanParameterDefinition Adc1OffsetParameter = new("ADC1 Offset", "prism.adc1.offset");
    private static readonly ScanParameterDefinition Adc1GainParameter = new("ADC1 Gain", "prism.adc1.gain");
    private static readonly ScanParameterDefinition Adc2OffsetParameter = new("ADC2 Offset", "prism.adc2.offset");
    private static readonly ScanParameterDefinition Adc2GainParameter = new("ADC2 Gain", "prism.adc2.gain");
    private static readonly ScanParameterDefinition SysClockKhzParameter = new("System Clock", "prism.sys_clock_khz");

    private static readonly ScanParameterDefinition[] _definitions =
    {
        ExposureTicksParameter,
        Adc1OffsetParameter,
        Adc1GainParameter,
        Adc2OffsetParameter,
        Adc2GainParameter,
        SysClockKhzParameter
    };

    public IReadOnlyList<ScanParameterDefinition> Definitions => _definitions;

    public ScanParameterService(IScanProtocolService protocol)
    {
        _protocol = protocol;
    }

    public bool TryParseInput(string exposureMicroseconds, string adc1Offset, string adc1Gain, string adc2Offset, string adc2Gain, string sysClockMhz, out ScanParameterSnapshot snapshot, out string error)
    {
        snapshot = default!;

        if (!TryParseExposureMicroseconds(exposureMicroseconds, ExposureTicksParameter.DisplayName, sysClockMhz, out var exposureTicks, out error)
            || !TryParseOffset(adc1Offset, Adc1OffsetParameter.DisplayName, out var adc1OffsetParsed, out error)
            || !TryParseGain(adc1Gain, Adc1GainParameter.DisplayName, out var adc1GainParsed, out error)
            || !TryParseOffset(adc2Offset, Adc2OffsetParameter.DisplayName, out var adc2OffsetParsed, out error)
            || !TryParseGain(adc2Gain, Adc2GainParameter.DisplayName, out var adc2GainParsed, out error)
            || !TryParseSysClockMhz(sysClockMhz, SysClockKhzParameter.DisplayName, out var sysClockKhzParsed, out error))
        {
            return false;
        }

        snapshot = new ScanParameterSnapshot(exposureTicks, adc1OffsetParsed, adc1GainParsed, adc2OffsetParsed, adc2GainParsed, sysClockKhzParsed);
        error = string.Empty;
        return true;
    }

    public ScanParameterDisplays BuildDisplays(string exposureMicroseconds, string adc1Offset, string adc1Gain, string adc2Offset, string adc2Gain, string sysClockMhz)
    {
        var exposureDisplay = BuildExposureDisplay(exposureMicroseconds, sysClockMhz);
        var adc1OffsetDisplay = BuildOffsetDisplay(adc1Offset);
        var adc2OffsetDisplay = BuildOffsetDisplay(adc2Offset);
        var adc1GainDisplay = BuildGainDisplay(adc1Gain);
        var adc2GainDisplay = BuildGainDisplay(adc2Gain);
        var sysClockDisplay = BuildSysClockDisplay(sysClockMhz);

        return new ScanParameterDisplays(exposureDisplay, adc1OffsetDisplay, adc2OffsetDisplay, adc1GainDisplay, adc2GainDisplay, sysClockDisplay);
    }

    public string FormatOffsetForInput(int offset)
        => offset >= 0 ? $"+{offset}" : offset.ToString();

    public async Task<ScanParameterSnapshot> LoadAsync(IScanSessionService session, CancellationToken ct)
    {
        var loadedU16 = new Dictionary<string, ushort>(_definitions.Length);
        uint sysClockKhz = 0;
        foreach (var parameter in _definitions)
        {
            var keyHash = _protocol.ComputeParamKeyHash(parameter.Key);
            var command = _protocol.BuildGetParamByHashCommand(keyHash);
            var response = await session.SendControlCommandAndWaitAckAsync(command, ScanDebugConstants.UsbCmdGetParamByHash, ScanDebugConstants.AckTimeoutMs, ct, true);
            if (response.Status != 0x00)
                throw new IOException($"GET_PARAM '{parameter.DisplayName}' failed: {_protocol.MapStatus(response.Status)} (0x{response.Status:X2})");

            if (parameter == SysClockKhzParameter)
                sysClockKhz = _protocol.ParseU32ParamPayload(response.Payload, keyHash, parameter.DisplayName);
            else
                loadedU16[parameter.Key] = _protocol.ParseU16ParamPayload(response.Payload, keyHash, parameter.DisplayName);
        }

        return new ScanParameterSnapshot(
            loadedU16[ExposureTicksParameter.Key],
            DecodeSignedOffset(loadedU16[Adc1OffsetParameter.Key]),
            loadedU16[Adc1GainParameter.Key],
            DecodeSignedOffset(loadedU16[Adc2OffsetParameter.Key]),
            loadedU16[Adc2GainParameter.Key],
            sysClockKhz);
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

    private async Task SetParameterAsync(IScanSessionService session, ScanParameterDefinition parameter, uint value, CancellationToken ct)
    {
        var keyHash = _protocol.ComputeParamKeyHash(parameter.Key);
        var command = _protocol.BuildSetParamByHashCommand(keyHash, value);
        var response = await session.SendControlCommandAndWaitAckAsync(command, ScanDebugConstants.UsbCmdSetParamByHash, ScanDebugConstants.AckTimeoutMs, ct, true);
        if (response.Status != 0x00)
            throw new IOException($"SET_PARAM '{parameter.DisplayName}' failed: {_protocol.MapStatus(response.Status)} (0x{response.Status:X2})");

        var echoed = _protocol.ParseU32ParamPayload(response.Payload, keyHash, parameter.DisplayName);
        if (echoed != value)
            throw new IOException($"SET_PARAM '{parameter.DisplayName}' verify mismatch: expected {value}, echoed {echoed}");
    }

    private static bool TryParseExposureMicroseconds(string text, string fieldName, string sysClockMhzText, out ushort exposureTicks, out string error)
    {
        exposureTicks = 0;
        if (!TryParseSysClockMhz(sysClockMhzText, SysClockKhzParameter.DisplayName, out var sysClockKhz, out error))
            return false;

        var trimmed = text?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed)
            || !double.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out var exposureMicroseconds)
            || !ScanTimingMath.TryConvertMicrosecondsToExposureTicks(exposureMicroseconds, sysClockKhz, out exposureTicks))
        {
            error = $"{fieldName} must be a finite exposure in microseconds that maps to a valid tick value.";
            exposureTicks = 0;
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

    private static bool TryParseSysClockMhz(string text, string fieldName, out uint value, out string error)
    {
        value = 0;
        var trimmed = text?.Trim();
        var fractionalDigits = trimmed is null ? string.Empty : GetFractionalDigits(trimmed);
        if (string.IsNullOrWhiteSpace(trimmed)
            || fractionalDigits.Length > 3
            || !decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out var mhz)
            || mhz < 30m
            || mhz > 200m)
        {
            error = $"{fieldName} must be a decimal in [30, 200] MHz with integral kHz precision.";
            return false;
        }

        var khz = mhz * 1000m;
        if (decimal.Truncate(khz) != khz)
        {
            error = $"{fieldName} must be a decimal in [30, 200] MHz with integral kHz precision.";
            return false;
        }

        if (khz < ScanDebugConstants.MinSysClockKhz || khz > ScanDebugConstants.MaxSysClockKhz)
        {
            error = $"{fieldName} must be a decimal in [30, 200] MHz with integral kHz precision.";
            return false;
        }

        value = (uint)khz;
        error = string.Empty;
        return true;
    }

    private static string GetFractionalDigits(string text)
    {
        var decimalSeparatorIndex = text.IndexOf('.');
        if (decimalSeparatorIndex < 0 || decimalSeparatorIndex == text.Length - 1)
            return string.Empty;

        return text[(decimalSeparatorIndex + 1)..];
    }

    public async Task ApplyGlobalClockAsync(IScanSessionService session, uint sysClockKhz, CancellationToken ct)
    {
        if (sysClockKhz is < ScanDebugConstants.MinSysClockKhz or > ScanDebugConstants.MaxSysClockKhz)
            throw new ArgumentOutOfRangeException(nameof(sysClockKhz), sysClockKhz, $"System clock must be in [{ScanDebugConstants.MinSysClockKhz}, {ScanDebugConstants.MaxSysClockKhz}] kHz.");

        await SetParameterAsync(session, SysClockKhzParameter, sysClockKhz, ct);
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

    private static string BuildExposureDisplay(string exposureMicrosecondsText, string sysClockMhzText)
    {
        if (!TryParseSysClockMhz(sysClockMhzText, SysClockKhzParameter.DisplayName, out var sysClockKhz, out _)
            || !double.TryParse(exposureMicrosecondsText, NumberStyles.Number, CultureInfo.InvariantCulture, out var exposureMicroseconds)
            || !ScanTimingMath.TryConvertMicrosecondsToExposureTicks(exposureMicroseconds, sysClockKhz, out var exposureTicks))
            return "-";

        var exposureNs = ScanTimingMath.ExposureTicksToNanoseconds(exposureTicks, sysClockKhz);
        var exposureUs = ScanTimingMath.ExposureTicksToMicroseconds(exposureTicks, sysClockKhz);
        var reciprocalSeconds = ScanTimingMath.ExposureNanosecondsToReciprocalSeconds(exposureNs);
        return $"{exposureNs:0.##} ns ({exposureUs:0.###} us, 1/{reciprocalSeconds:0.###} s)";
    }

    private static string BuildOffsetDisplay(string offsetText)
    {
        if (!int.TryParse(offsetText, out var offset) || offset < -255 || offset > 255)
            return "-";

        var offsetMv = offset * 300.0 / 256.0;
        return $"{offsetMv:+0.###;-0.###;0} mV";
    }

    private static string BuildGainDisplay(string gainText)
    {
        if (!int.TryParse(gainText, out var gain) || gain < 0 || gain > 63)
            return "-";

        var ratio = 6.0 / (1.0 + 5.0 * ((63.0 - gain) / 63.0));
        return $"{ratio:0.###} V/V";
    }

    private static string BuildSysClockDisplay(string sysClockMhzText)
    {
        if (!TryParseSysClockMhz(sysClockMhzText, SysClockKhzParameter.DisplayName, out var khz, out _))
            return "-";

        var mhz = khz / 1000.0;
        return $"{mhz:0.###} MHz";
    }
}
