namespace PRISM_Utility.Core.Models;

public sealed record ScanMotorMechanicalSettings(uint StepsPerRevolution, uint Microsteps, double LeadLengthMm)
{
    public static ScanMotorMechanicalSettings CreateDefault()
        => new(200, 16, 8.0);

    public ScanMotorMechanicalSettings Normalize()
    {
        var defaults = CreateDefault();
        return new ScanMotorMechanicalSettings(
            StepsPerRevolution > 0 ? StepsPerRevolution : defaults.StepsPerRevolution,
            Microsteps > 0 ? Microsteps : defaults.Microsteps,
            double.IsFinite(LeadLengthMm) && LeadLengthMm > 0.0 ? LeadLengthMm : defaults.LeadLengthMm);
    }
}

public sealed record ScanDeviceSettings(
    ScanMotorMechanicalSettings? Motor1,
    ScanMotorMechanicalSettings? Motor2,
    ScanMotorMechanicalSettings? Motor3)
{
    public static ScanDeviceSettings CreateDefault()
    {
        var motorDefaults = ScanMotorMechanicalSettings.CreateDefault();
        return new ScanDeviceSettings(motorDefaults, motorDefaults, motorDefaults);
    }

    public ScanDeviceSettings Normalize()
    {
        var defaults = CreateDefault();
        return new ScanDeviceSettings(
            (Motor1 ?? defaults.Motor1!).Normalize(),
            (Motor2 ?? defaults.Motor2!).Normalize(),
            (Motor3 ?? defaults.Motor3!).Normalize());
    }

    public ScanMotorMechanicalSettings GetMotorSettings(byte motorId)
        => motorId switch
        {
            0 => (Motor1 ?? ScanMotorMechanicalSettings.CreateDefault()).Normalize(),
            1 => (Motor2 ?? ScanMotorMechanicalSettings.CreateDefault()).Normalize(),
            2 => (Motor3 ?? ScanMotorMechanicalSettings.CreateDefault()).Normalize(),
            _ => throw new ArgumentOutOfRangeException(nameof(motorId))
        };
}
