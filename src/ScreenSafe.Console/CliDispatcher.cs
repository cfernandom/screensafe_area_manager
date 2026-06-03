using ScreenSafe.Application;

namespace ScreenSafe.Console;

/// <summary>
/// Parses command-line arguments and dispatches to the appropriate use case.
/// </summary>
public class CliDispatcher
{
    private readonly ApplyUseCase _applyUseCase;
    private readonly RestoreUseCase _restoreUseCase;
    private readonly StatusUseCase _statusUseCase;

    public CliDispatcher(ApplyUseCase applyUseCase, RestoreUseCase restoreUseCase, StatusUseCase statusUseCase)
    {
        _applyUseCase = applyUseCase;
        _restoreUseCase = restoreUseCase;
        _statusUseCase = statusUseCase;
    }

    /// <summary>
    /// Executes the command specified by the given arguments.
    /// </summary>
    /// <param name="args">Command-line arguments. First argument is the command name.</param>
    /// <returns>0 on success, 1 on error or unknown command.</returns>
    public int Execute(string[] args)
    {
        if (args.Length == 0)
        {
            System.Console.WriteLine("Usage: ScreenSafe.Console.exe <command>");
            System.Console.WriteLine("Commands: apply, restore, status");
            return 1;
        }

        switch (args[0].ToLowerInvariant())
        {
            case "apply":
                return _applyUseCase.Execute();
            case "restore":
                return _restoreUseCase.Execute();
            case "status":
                return _statusUseCase.Execute();
            default:
                System.Console.WriteLine($"Unknown command: {args[0]}");
                System.Console.WriteLine("Usage: ScreenSafe.Console.exe <command>");
                System.Console.WriteLine("Commands: apply, restore, status");
                return 1;
        }
    }
}
