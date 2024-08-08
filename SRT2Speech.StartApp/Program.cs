using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

// Set the console encoding to UTF-8
Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

string ngrokPath = @"START_NGROK.bat";
string apiPath = @"RelaseApi\SRT2Speech.AppAPI.exe";
string appPath = @"RelaseWindowApp\SRT2Speech.AppWindow.exe";
int port = 5000;
string subdomain = "heroic-pleasantly-hagfish.ngrok-free.app";
string ngrokToken = "2kI6civqbvOqIuwupePqMipwa9g_65spmLhVWGZBq7KaojpdF";
string batFilePath = @"START_WINDOW.bat";

//ngrok http 5000 --domain heroic-pleasantly-hagfish.ngrok-free.app
try
{
    WriteLineInfo("Đang khởi động API...");
    StartService(apiPath);
    Thread.Sleep(1000);
    WriteLineInfo("Đang khởi động Window...");
    Process process = new Process();
    process.StartInfo.FileName = "cmd.exe";
    process.StartInfo.Arguments = $"/c \"{batFilePath}\"";
    process.StartInfo.UseShellExecute = false;
    process.StartInfo.RedirectStandardOutput = true;
    process.StartInfo.RedirectStandardError = true;
    process.Start();
    Thread.Sleep(1000);
    WriteLineInfo("Đang khởi động Ngrok");
    StartNgrok(ngrokPath, port, subdomain, ngrokToken);
    Console.ReadLine();
}
catch (Exception ex)
{
    WriteLineError($"Không mở được tool, ex = {ex.Message}");
    Console.ReadLine();
}

static void StartService(string servicePath)
{
    Process.Start(servicePath);
}

static void StartNgrok(string ngrokPath, int port, string subdomain, string token)
{
    Process process = new Process();
    process.StartInfo.FileName = "cmd.exe";
    process.StartInfo.Arguments = $"/c \"{ngrokPath}\"";
    process.StartInfo.UseShellExecute = false;
    process.StartInfo.RedirectStandardOutput = true;
    process.StartInfo.RedirectStandardError = true;
    process.Start();
}

static void SimulateProcess()
{
    int totalSteps = 100;
    int currentStep = 0;

    Console.CursorVisible = false; // Hide the cursor

    while (currentStep < totalSteps)
    {
        // Update the progress bar
        UpdateProgressBar(currentStep, totalSteps);

        // Simulate progress
        currentStep++;
        Thread.Sleep(10); // Simulate a delay
    }
    Console.ForegroundColor = ConsoleColor.White;
    // Finish the process
    UpdateProgressBar(totalSteps, totalSteps);
    Console.CursorVisible = true; // Show the cursor
    Console.WriteLine("\n");
}

static void UpdateProgressBar(int currentStep, int totalSteps)
{
    int progressBarWidth = 50;
    int completedWidth = (int)Math.Round((double)currentStep / totalSteps * progressBarWidth);

    // Clear the previous progress bar
    Console.CursorLeft = 0;
    Console.Write("[{0}] {1}%", new string('#', completedWidth) + new string('.', progressBarWidth - completedWidth), (int)Math.Round((double)currentStep / totalSteps * 100));
}

static string ExtractForwardingUrl(string output)
{
    Regex regex = new Regex(@"Forwarding\s+(?<url>https://[^\s]+)");
    Match match = regex.Match(output);
    return match.Success ? match.Groups["url"].Value : string.Empty;
}

static void WriteLineSuccess(string message)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine($"[SUCCESS] {message}");
    Console.ForegroundColor = ConsoleColor.White;
}

static void WriteLineError(string message)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"[ERROR] {message}");
    Console.ForegroundColor = ConsoleColor.White;
}

static void WriteLineInfo(string message)
{
    Console.ForegroundColor = ConsoleColor.Blue;
    Console.WriteLine($"[INFO] {message}");
    Console.ForegroundColor = ConsoleColor.White;
}

static void WriteLineWarn(string message)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"[WARN] {message}");
    Console.ForegroundColor = ConsoleColor.White;
}
