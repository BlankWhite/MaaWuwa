using MaaFramework.Binding;
using MaaWuwa.Agent.Actions;
using MaaWuwa.Agent.Actions.Daily;

var socketId = args.LastOrDefault();
if (string.IsNullOrWhiteSpace(socketId))
{
    Console.Error.WriteLine("Usage: MaaWuwa.Agent [--native-dir <path>] <socket_id>");
    return 2;
}

var nativeDirs = CollectNativeLibraryDirs(args).ToArray();
Console.WriteLine($"MaaWuwa.Agent starting. socket_id={socketId}");
if (nativeDirs.Length > 0)
{
    Console.WriteLine($"Native search paths: {string.Join(", ", nativeDirs)}");
}

try
{
    MaaAgentServer.Current
        .WithNativeLibrary(nativeDirs)
        .WithIdentifier(socketId)
        .SetLogDirectory(".cache")
        .Register(new AutoCombatAction())
        .Register(new AutoCombatTickRecognition())
        .Register(new DailyTickRecognition())
        .Register(new DebugFInteractTickRecognition())
        .StartUp()
        .Join()
        .ShutDown();

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    return 1;
}

static IEnumerable<string> CollectNativeLibraryDirs(string[] args)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == "--native-dir" && i + 1 < args.Length)
        {
            yield return Path.GetFullPath(args[i + 1]);
            i++;
        }
    }

    foreach (var candidate in new[]
    {
        "libs/MaaAgentBinary",
        "MaaAgentBinary",
        "runtimes/linux-x64/native",
        "runtimes/linux-arm64/native",
        "."
    })
    {
        var fullPath = Path.GetFullPath(candidate);
        if (Directory.Exists(fullPath))
        {
            yield return fullPath;
        }
    }
}
