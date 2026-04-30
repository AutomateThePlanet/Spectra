using System.CommandLine;

namespace Spectra.CLI.Commands.Doctor;

/// <summary>
/// Spec 040: parent command for diagnostic and repair operations.
/// Subcommands: <c>ids</c>.
/// </summary>
public sealed class DoctorCommand : Command
{
    public DoctorCommand() : base("doctor", "Diagnose and repair workspace health issues")
    {
        AddCommand(DoctorIdsCommand.Create());
    }
}
