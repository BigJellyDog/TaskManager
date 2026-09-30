using System.Diagnostics;
using System.Runtime.InteropServices;

var folder = Path.Combine(AppContext.BaseDirectory, "app");
var exe = Path.Combine(folder, "TaskManager.App.exe");

if (!File.Exists(exe))
{
    _ = MessageBox(IntPtr.Zero,
        "The program files were not found. Extract the whole zip and keep the \"app\" folder next to this file.",
        "Task Manager",
        0x10);
    return 1;
}

var start = new ProcessStartInfo(exe) { WorkingDirectory = folder, UseShellExecute = false };

foreach (var argument in args)
    start.ArgumentList.Add(argument);

Process.Start(start);
return 0;

[DllImport("user32.dll", CharSet = CharSet.Unicode)]
static extern int MessageBox(IntPtr window, string text, string caption, uint type);
