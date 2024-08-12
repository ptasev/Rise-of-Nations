using ConsoleAppFramework;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using RoNFileConverter;

using RoNLibrary.Formats.Gltf;

using ZLogger;

var services = new ServiceCollection();
services.AddLogging(x =>
{
    x.ClearProviders();
    x.SetMinimumLevel(LogLevel.Trace);
    x.AddZLoggerConsole();
});
services.AddSingleton<Bh3GltfConverter>();
services.AddSingleton<GltfBh3Converter>();

using var serviceProvider = services.BuildServiceProvider();
ConsoleApp.ServiceProvider = serviceProvider;

var app = ConsoleApp.Create();
app.UseFilter<LogRunningTimeFilter>();
app.Add<Commands>();
app.Run(args);
