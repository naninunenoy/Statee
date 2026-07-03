using ConsoleAppFramework;
using Statee.Cli;

var app = ConsoleApp.Create();
app.Add<StateeCommands>();
app.Run(args);
