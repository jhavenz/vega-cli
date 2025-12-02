using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using VegaDevCli.Domain.Project;

namespace VegaDevCli.Domain.Proxy;

public class CharlesProxyManager
{
    private const string CharlesAppPath = "/Applications/Charles.app";
    private const string CharlesConfigPath = "~/.charles";
    private const int DefaultPort = 8888;
    
    private readonly IVegaProjectManager _projectManager;

    public CharlesProxyManager(IVegaProjectManager projectManager)
    {
        _projectManager = projectManager;
    }

    public async Task StartAsync(int port = DefaultPort, bool autoConfig = true)
    {
        Console.WriteLine(" Starting Charles Proxy for Fire TV development...");

        if (IsCharlesRunning())
        {
            Console.WriteLine("WARNING:  Charles Proxy is already running");
            if (autoConfig)
            {
                await ConfigureAsync(port, true, true);
            }
            return;
        }

        if (!File.Exists($"{CharlesAppPath}/Contents/MacOS/Charles"))
        {
            Console.WriteLine("FAILED: Charles Proxy not found at {0}", CharlesAppPath);
            Console.WriteLine(" Install with: brew install --cask charles");
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "open",
            Arguments = $"-a \"{CharlesAppPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        var process = Process.Start(startInfo);
        await process!.WaitForExitAsync();

        Console.WriteLine("Waiting for Charles to initialize...");
        await Task.Delay(5000);

        await CreateProxyEnvironmentFile(port);

        if (autoConfig)
        {
            await ConfigureAsync(port, true, true);
        }

        Console.WriteLine("SUCCESS: Charles Proxy started successfully");
        Console.WriteLine($" Proxy running on port {port}");
        Console.WriteLine(" Environment file created: .env.charles");
    }

    public async Task StopAsync()
    {
        Console.WriteLine("Stopping Charles Proxy...");

        if (!IsCharlesRunning())
        {
            Console.WriteLine("Charles Proxy is not running");
            return;
        }

        var processes = Process.GetProcessesByName("Charles");
        foreach (var process in processes)
        {
            try
            {
                process.Kill();
                await process.WaitForExitAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WARNING:  Error stopping Charles process: {ex.Message}");
            }
        }

        await CleanupProxyEnvironment();

        Console.WriteLine("SUCCESS: Charles Proxy stopped and cleaned up");
    }

    public async Task ConfigureAsync(int port = DefaultPort, bool enableSsl = true, bool transparent = true)
    {
        Console.WriteLine(" Configuring Charles Proxy settings...");

        if (!IsCharlesRunning())
        {
            Console.WriteLine("WARNING:  Charles Proxy is not running. Start it first with: ./vega proxy start");
            return;
        }

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                await ConfigureCharlesViaNativeApi(port, enableSsl, transparent);
            }
            else
            {
                Console.WriteLine("WARNING:  Automatic configuration only supported on macOS");
                Console.WriteLine(" Manually configure Charles with these settings:");
                Console.WriteLine($"   - Port: {port}");
                Console.WriteLine($"   - SSL Proxying: {(enableSsl ? "Enabled" : "Disabled")}");
                Console.WriteLine($"   - Transparent Proxy: {(transparent ? "Enabled" : "Disabled")}");
                Console.WriteLine("   - SSL Location: localhost:8092");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WARNING:  Configuration failed: {ex.Message}");
            Console.WriteLine(" Please configure Charles manually:");
            Console.WriteLine("   1. Open Charles > Proxy > Proxy Settings");
            Console.WriteLine($"   2. Set port to {port}");
            Console.WriteLine("   3. Enable 'Transparent HTTP proxying'");
            Console.WriteLine("   4. Open Charles > Proxy > SSL Proxying Settings");
            Console.WriteLine("   5. Enable 'SSL Proxying'");
            Console.WriteLine("   6. Add location: localhost:8092");
        }

        Console.WriteLine("SUCCESS: Charles configuration complete");
    }

    public async Task ShowStatusAsync()
    {
        Console.WriteLine(" Charles Proxy Status");
        Console.WriteLine("======================");

        bool isRunning = IsCharlesRunning();
        Console.WriteLine($"Status: {(isRunning ? "SUCCESS: Running" : "FAILED: Stopped")}");

        if (isRunning)
        {
            var processes = Process.GetProcessesByName("Charles");
            foreach (var process in processes)
            {
                Console.WriteLine($"PID: {process.Id}");
            }
        }

        var envFile = _projectManager.GetProjectPath(".env.charles");
        bool hasEnvFile = File.Exists(envFile);
        Console.WriteLine($"Environment file: {(hasEnvFile ? "SUCCESS: Present" : "FAILED: Missing")}");

        if (hasEnvFile)
        {
            var content = await File.ReadAllTextAsync(envFile);
            var lines = content.Split('\n').Where(l => !l.StartsWith("#") && l.Contains("="));
            foreach (var line in lines)
            {
                var parts = line.Split('=', 2);
                if (parts.Length == 2)
                {
                    Console.WriteLine($"  {parts[0]}: {parts[1]}");
                }
            }
        }

        var httpProxy = Environment.GetEnvironmentVariable("HTTP_PROXY");
        var httpsProxy = Environment.GetEnvironmentVariable("HTTPS_PROXY");
        
        Console.WriteLine($"System HTTP_PROXY: {httpProxy ?? "Not set"}");
        Console.WriteLine($"System HTTPS_PROXY: {httpsProxy ?? "Not set"}");

        if (isRunning && !hasEnvFile)
        {
            Console.WriteLine("\n Tip: Run './vega proxy configure' to set up environment");
        }
    }

    private bool IsCharlesRunning()
    {
        var processes = Process.GetProcessesByName("Charles");
        return processes.Length > 0;
    }

    private async Task CreateProxyEnvironmentFile(int port)
    {
        var envContent = $"""
            # Charles Proxy Configuration for Fire TV Development
            # Generated by VegaDevCli on {DateTime.Now:yyyy-MM-dd HH:mm:ss}
            HTTP_PROXY=http://127.0.0.1:{port}
            HTTPS_PROXY=http://127.0.0.1:{port}
            NO_PROXY=127.0.0.1,localhost
            CHARLES_PROXY_ENABLED=true
            CHARLES_PROXY_PORT={port}
            """;

        var envFile = _projectManager.GetProjectPath(".env.charles");
        await File.WriteAllTextAsync(envFile, envContent);

        var gitignorePath = _projectManager.GetProjectPath(".gitignore");
        if (File.Exists(gitignorePath))
        {
            var gitignoreContent = await File.ReadAllTextAsync(gitignorePath);
            if (!gitignoreContent.Contains(".env.charles"))
            {
                await File.AppendAllTextAsync(gitignorePath, "\n.env.charles\n");
                Console.WriteLine("➕ Added .env.charles to .gitignore");
            }
        }
    }

    private async Task CleanupProxyEnvironment()
    {
        var envFile = _projectManager.GetProjectPath(".env.charles");
        if (File.Exists(envFile))
        {
            File.Delete(envFile);
            Console.WriteLine("Removed .env.charles");
        }
    }

    private async Task ConfigureCharlesViaNativeApi(int port, bool enableSsl, bool transparent)
    {
        var appleScript = $"""
            tell application "Charles"
                activate
                delay 2
                
                tell application "System Events"
                    tell process "Charles"
                        -- Configure proxy settings
                        try
                            click menu item "Proxy Settings..." of menu "Proxy" of menu bar 1
                            delay 1
                            
                            set value of text field 1 of group 1 of sheet 1 of window 1 to "{port}"
                            
                            if {transparent.ToString().ToLower()} then
                                click checkbox "Enable transparent HTTP proxying" of group 1 of sheet 1 of window 1
                            end if
                            
                            click button "OK" of sheet 1 of window 1
                            delay 1
                        on error
                            -- Proxy settings dialog might already be configured
                        end try
                        
                        -- Configure SSL settings if requested
                        if {enableSsl.ToString().ToLower()} then
                            try
                                click menu item "SSL Proxying Settings..." of menu "Proxy" of menu bar 1
                                delay 1
                                
                                click checkbox "Enable SSL Proxying" of group 1 of sheet 1 of window 1
                                
                                click button "Add" of group 2 of sheet 1 of window 1
                                delay 1
                                
                                set value of text field 1 of sheet 1 of sheet 1 of window 1 to "localhost"
                                set value of text field 2 of sheet 1 of sheet 1 of window 1 to "8092"
                                click button "OK" of sheet 1 of sheet 1 of window 1
                                
                                click button "OK" of sheet 1 of window 1
                            on error
                                -- SSL settings might already be configured
                            end try
                        end if
                    end tell
                end tell
            end tell
            """;

        var startInfo = new ProcessStartInfo
        {
            FileName = "osascript",
            Arguments = $"-e \"{appleScript.Replace("\"", "\\\"")}\"",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        var process = Process.Start(startInfo);
        await process!.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var error = await process.StandardError.ReadToEndAsync();
            throw new Exception($"AppleScript configuration failed: {error}");
        }
    }
}