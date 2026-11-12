using System.IO;
using System.Runtime.Loader;

if (args.Length != 1)
{
    Console.WriteLine("Usage: dotnet run --project UiProbe -- <Paint.NET directory>");
    return 2;
}

// The SDK supplies compile-time references only. Resolve the public runtime
// assemblies from an installed paint.net for this standalone developer check.
// No members are discovered or invoked through reflection.
string pdnDirectory = Path.GetFullPath(args[0]);
AssemblyLoadContext.Default.Resolving += (_, name) =>
{
    string path = Path.Combine(pdnDirectory, name.Name + ".dll");
    return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
};

int controlResult = ControlChecks.Run();
int bitmapResult = BitmapChecks.Run();
return controlResult != 0 ? controlResult : bitmapResult;
