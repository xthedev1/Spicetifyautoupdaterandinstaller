using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Diagnostics;
using Newtonsoft.Json;
using System.Windows.Media;
using System.ComponentModel;
using System.Collections.Generic;

namespace SpicetifyAutoUpdater
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly HttpClient httpClient;
        private bool isSpicetifyInstalled = false;
        private bool isConsoleVisible = false;
        private readonly List<Process> startedPowerShellProcesses = new List<Process>();
        private bool isInstalling = false;
        private bool installCompleted = false;
        private bool isClosing = false;

        public MainWindow()
        {
            InitializeComponent();
            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "SpicetifyAutoUpdater/1.0");

            // Initialize console as hidden
            panelConsole.Visibility = Visibility.Collapsed;
            panelProgress.Visibility = Visibility.Collapsed;
            isConsoleVisible = false;
            this.Height = 300; // Initial height
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void btnSettings_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implement settings functionality
            ModernMessageBox.Show("Settings functionality coming soon!", "Settings", MessageBoxImage.Information);
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await CheckSpicetifyInstallation();
        }

        private void ShowProgress(string message = "Installing...")
        {
            Dispatcher.Invoke(() => {
                panelProgress.Visibility = Visibility.Visible;
                lblProgress.Text = message;
                progressBar.IsIndeterminate = true;
                panelConsole.Visibility = Visibility.Collapsed;
                isConsoleVisible = false;
                // Hide main content
                btnCheckUpdates.Visibility = Visibility.Collapsed;
            });
        }

        private void HideProgress()
        {
            Dispatcher.Invoke(() => {
                panelProgress.Visibility = Visibility.Collapsed;
                if (!isConsoleVisible)
                {
                    btnCheckUpdates.Visibility = Visibility.Visible;
                }
            });
        }

        private void ShowConsole()
        {
            Dispatcher.Invoke(() => {
                panelConsole.Visibility = Visibility.Visible;
                panelProgress.Visibility = Visibility.Collapsed;
                isConsoleVisible = true;
                btnCheckUpdates.Visibility = Visibility.Collapsed;
            });
        }

        private void HideConsole()
        {
            Dispatcher.Invoke(() => {
                panelConsole.Visibility = Visibility.Collapsed;
                isConsoleVisible = false;
                if (panelProgress.Visibility != Visibility.Visible)
                {
                    btnCheckUpdates.Visibility = Visibility.Visible;
                }
            });
        }

        private void ScrollConsoleToEnd()
        {
            Dispatcher.Invoke(() => {
                txtOutput.CaretIndex = txtOutput.Text.Length;
                txtOutput.ScrollToEnd();
            });
        }

        private async Task CheckSpicetifyInstallation()
        {
            if (isInstalling || installCompleted || isClosing)
            {
                return;
            }
            try
            {
                var result = await ExecuteCommandAsync("spicetify", "--version", false);
                isSpicetifyInstalled = !string.IsNullOrEmpty(result) && !result.Contains("is not recognized");
            }
            catch (Win32Exception)
            {
                isSpicetifyInstalled = false;
            }
            catch (Exception ex)
            {
                if (!isClosing)
                {
                    ModernMessageBox.Show(
                        $"Error checking Spicetify installation: {ex.Message}",
                        "Error",
                        MessageBoxImage.Error);
                }
                isSpicetifyInstalled = false; // Ensure prompt is shown
            }

            if (!isSpicetifyInstalled)
            {
                var res = MessageBox.Show(
                    "Spicetify is not installed. Would you like to install it now?",
                    "Spicetify Not Found",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (res == MessageBoxResult.Yes)
                {
                    isInstalling = true;
                    ShowProgress("Installing Spicetify and Marketplace...");
                    txtOutput.Clear();
                    string installOutput = await InstallSpicetifyAndMarketplace();
                    // Check for success message in output
                    if (!string.IsNullOrEmpty(installOutput) && installOutput.ToLower().Contains("run spicetify -h to get started"))
                    {
                        btnCheckUpdates.IsEnabled = false;
                        btnCheckUpdates.Content = "Spicetify Installed!";
                        installCompleted = true;
                        isInstalling = false;
                        HideProgress();
                        if (!isConsoleVisible)
                        {
                            btnCheckUpdates.Visibility = Visibility.Collapsed;
                        }
                        // Show installed message in main content
                        ShowInstalledMessage();
                    }
                    else
                    {
                        isInstalling = false;
                        HideProgress();
                        ModernMessageBox.Show("Spicetify installation did not complete successfully. Please check the console output for details.", "Install Failed", MessageBoxImage.Error);
                    }
                    // Re-check installation only if not completed or closing
                    if (!installCompleted && !isClosing)
                    {
                        await CheckSpicetifyInstallation();
                    }
                    return;
                }
                else
                {
                    btnCheckUpdates.IsEnabled = false;
                    btnCheckUpdates.Content = "Spicetify Not Found";
                }
            }
        }

        private void ShowInstalledMessage()
        {
            Dispatcher.Invoke(() => {
                // Hide the install button
                btnCheckUpdates.Visibility = Visibility.Collapsed;
                // Add or show a label in the main content area
                var parent = btnCheckUpdates.Parent as Panel;
                if (parent != null)
                {
                    // Remove any previous installed label
                    foreach (var child in parent.Children)
                    {
                        if (child is Label lbl && lbl.Name == "lblInstalledSuccess")
                        {
                            parent.Children.Remove(lbl);
                            break;
                        }
                    }
                    // Add new label
                    var installedLabel = new Label
                    {
                        Name = "lblInstalledSuccess",
                        Content = "Installed Successfully!",
                        Foreground = new SolidColorBrush(Color.FromRgb(29, 185, 84)), // Spotify green
                        FontSize = 20,
                        FontWeight = FontWeights.Bold,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 20, 0, 0)
                    };
                    parent.Children.Add(installedLabel);
                }
            });
        }

        // Sequential install: Spicetify, wait for message, then Marketplace
        private async Task<string> InstallSpicetifyAndMarketplace()
        {
            var outputBuilder = new StringBuilder();
            bool sawSuccess = false;
            try
            {
                this.Dispatcher.Invoke(() => txtOutput.AppendText("[DEBUG] Starting Spicetify install...\r\n"));
                string psCmd = "iwr -useb https://raw.githubusercontent.com/spicetify/cli/main/install.ps1 | iex";
                bool sawSuccessMessage = false;
                Process? spicetifyProcess = null;
                await ExecuteCommandAsyncWithCallback(
                    "powershell",
                    $"-NoProfile -ExecutionPolicy Bypass -Command \"{psCmd}\"",
                    true,
                    (line) =>
                    {
                        if (line != null)
                        {
                            outputBuilder.AppendLine("[SPICETIFY OUT] " + line);
                            this.Dispatcher.Invoke(() => txtOutput.AppendText("[SPICETIFY OUT] " + line + "\r\n"));
                            if (line.ToLower().Contains("run spicetify -h to get started"))
                            {
                                this.Dispatcher.Invoke(() => txtOutput.AppendText("[DEBUG] Detected success message in Spicetify output.\r\n"));
                                sawSuccessMessage = true;
                                if (spicetifyProcess != null && !spicetifyProcess.HasExited)
                                {
                                    try
                                    {
                                        spicetifyProcess.Kill(true);
                                        this.Dispatcher.Invoke(() => txtOutput.AppendText("[DEBUG] Killed Spicetify PowerShell process after success message.\r\n"));
                                    }
                                    catch (Exception ex)
                                    {
                                        this.Dispatcher.Invoke(() => txtOutput.AppendText($"[DEBUG] Failed to kill process: {ex.Message}\r\n"));
                                    }
                                }
                            }
                        }
                    },
                    (proc) => spicetifyProcess = proc,
                    ignoreExitCodeIfSuccessMessage: true,
                    wasSuccessMessageSeen: () => sawSuccessMessage
                );
                this.Dispatcher.Invoke(() => txtOutput.AppendText("[DEBUG] Finished Spicetify process.\r\n"));
                if (sawSuccessMessage)
                {
                    sawSuccess = true;
                    this.Dispatcher.Invoke(() => txtOutput.AppendText("[DEBUG] About to start Marketplace install...\r\n"));
                }
                else
                {
                    this.Dispatcher.Invoke(() => txtOutput.AppendText("[DEBUG] ERROR: Spicetify install did not complete as expected (missing message).\r\n"));
                }
                this.Dispatcher.Invoke(() => txtOutput.AppendText("[DEBUG] Spicetify install finished.\r\n"));
            }
            catch (Exception ex)
            {
                this.Dispatcher.Invoke(() => txtOutput.AppendText($"[DEBUG] ERROR during Spicetify install: {ex.Message}\r\n"));
            }

            // 2. Install Marketplace if Spicetify install succeeded
            if (sawSuccess)
            {
                try
                {
                    this.Dispatcher.Invoke(() => txtOutput.AppendText("[DEBUG] Starting Marketplace install...\r\n"));
                    string marketOutput = await InstallSpicetifyMarketplaceWithDebug();
                    outputBuilder.AppendLine(marketOutput);
                    this.Dispatcher.Invoke(() => txtOutput.AppendText("[DEBUG] Marketplace install finished.\r\n"));
                }
                catch (Exception ex)
                {
                    this.Dispatcher.Invoke(() => txtOutput.AppendText($"[DEBUG] ERROR: Marketplace install failed: {ex.Message}\r\n"));
                }
            }
            return outputBuilder.ToString();
        }

        // Modified to accept a process callback and ignore exit code if success message seen
        private async Task<string?> ExecuteCommandAsyncWithCallback(
            string command,
            string arguments,
            bool showOutput,
            Action<string?>? onOutputLine,
            Action<Process>? onProcessCreated = null,
            bool ignoreExitCodeIfSuccessMessage = false,
            Func<bool>? wasSuccessMessageSeen = null)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            // Kill any previous PowerShell processes started by this app if running a new PowerShell command
            if (command.ToLower().Contains("powershell"))
            {
                KillStartedPowerShells();
            }

            var process = new Process { StartInfo = startInfo };
            if (command.ToLower().Contains("powershell"))
            {
                startedPowerShellProcesses.Add(process);
            }
            onProcessCreated?.Invoke(process);
            var outputBuilder = new StringBuilder();

            process.OutputDataReceived += (s, e) => {
                if (e.Data != null)
                {
                    outputBuilder.AppendLine(e.Data);
                    this.Dispatcher.Invoke(() => {
                        txtOutput.AppendText(e.Data + "\r\n");
                        ScrollConsoleToEnd();
                    });
                    onOutputLine?.Invoke(e.Data);
                }
            };
            process.ErrorDataReceived += (s, e) => {
                if (e.Data != null)
                {
                    outputBuilder.AppendLine(e.Data);
                    this.Dispatcher.Invoke(() => {
                        txtOutput.AppendText("ERROR: " + e.Data + "\r\n");
                        ScrollConsoleToEnd();
                    });
                    onOutputLine?.Invoke(e.Data);
                }
            };

            this.Dispatcher.Invoke(() => {
                txtOutput.AppendText($"[DEBUG] Starting process: {command} {arguments}\r\n");
                ScrollConsoleToEnd();
            });
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();
            this.Dispatcher.Invoke(() => {
                txtOutput.AppendText($"[DEBUG] Process exited: {command} {arguments} (ExitCode: {process.ExitCode})\r\n");
                ScrollConsoleToEnd();
            });

            if (process.ExitCode != 0 && !outputBuilder.ToString().Contains("is not recognized"))
            {
                if (!(ignoreExitCodeIfSuccessMessage && wasSuccessMessageSeen != null && wasSuccessMessageSeen()))
                {
                    throw new Exception($"Command failed with exit code {process.ExitCode}.");
                }
            }

            return outputBuilder.ToString();
        }

        private void KillStartedPowerShells()
        {
            foreach (var proc in startedPowerShellProcesses.ToArray())
            {
                try
                {
                    if (!proc.HasExited)
                    {
                        proc.Kill(true);
                        this.Dispatcher.Invoke(() => txtOutput.AppendText("[DEBUG] Killed old PowerShell process.\r\n"));
                    }
                }
                catch { /* ignore */ }
                startedPowerShellProcesses.Remove(proc);
            }
        }

        private async Task<string> InstallSpicetifyMarketplaceWithDebug()
        {
            this.Dispatcher.Invoke(() => txtOutput.AppendText("[DEBUG] Entered InstallSpicetifyMarketplaceWithDebug\r\n"));
            // Merge registry PATH with current process PATH before running the marketplace install
            try
            {
                var regKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment", false);
                string? regPath = null;
                if (regKey != null)
                {
                    regPath = regKey.GetValue("Path", "", Microsoft.Win32.RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
                }
                var processPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process) ?? "";
                var systemPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine) ?? "";
                var userPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";
                var allPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                void AddPaths(string? pathStr)
                {
                    if (!string.IsNullOrEmpty(pathStr))
                    {
                        foreach (var p in pathStr.Split(';'))
                        {
                            var trimmed = p.Trim();
                            if (!string.IsNullOrEmpty(trimmed)) allPaths.Add(trimmed);
                        }
                    }
                }
                AddPaths(processPath);
                AddPaths(regPath);
                AddPaths(systemPath);
                AddPaths(userPath);
                var mergedPath = string.Join(";", allPaths);
                Environment.SetEnvironmentVariable("PATH", mergedPath, EnvironmentVariableTarget.Process);
                this.Dispatcher.Invoke(() => txtOutput.AppendText("[DEBUG] Merged and refreshed PATH before Marketplace install.\r\n"));
            }
            catch (Exception ex)
            {
                this.Dispatcher.Invoke(() => txtOutput.AppendText($"[DEBUG] Failed to merge/refresh PATH: {ex.Message}\r\n"));
            }
            var psCmd = "iwr -useb https://raw.githubusercontent.com/spicetify/marketplace/main/resources/install.ps1 | iex";
            var outputBuilder = new StringBuilder();
            bool success = false;
            try
            {
                this.Dispatcher.Invoke(() => txtOutput.AppendText("[DEBUG] Running Marketplace install with PowerShell...\r\n"));
                string psOutput = await ExecuteCommandAsyncWithCallback(
                    "powershell",
                    $"-NoProfile -ExecutionPolicy Bypass -Command \"{psCmd}\"",
                    true,
                    (line) =>
                    {
                        if (line != null)
                        {
                            outputBuilder.AppendLine("[MARKET OUT] " + line);
                            this.Dispatcher.Invoke(() => txtOutput.AppendText("[MARKET OUT] " + line + "\r\n"));
                        }
                    }
                ) ?? string.Empty;
                outputBuilder.AppendLine("--- PowerShell Output ---\r\n" + psOutput);
                success = true;
            }
            catch (Exception ex)
            {
                outputBuilder.AppendLine($"[MARKET ERR] PowerShell install failed: {ex.Message}");
                this.Dispatcher.Invoke(() => txtOutput.AppendText($"[MARKET ERR] PowerShell install failed: {ex.Message}\r\n"));
            }
            if (!success)
            {
                throw new Exception(outputBuilder.ToString());
            }
            return outputBuilder.ToString();
        }

        private void btnConsole_Click(object sender, RoutedEventArgs e)
        {
            isConsoleVisible = !isConsoleVisible;

            if (isConsoleVisible)
            {
                btnConsole.ToolTip = "Hide Console";
                AnimateWindowHeight(500); // Expand window smoothly
                ShowConsole();
                ScrollConsoleToEnd();
            }
            else
            {
                btnConsole.ToolTip = "Show Console";
                AnimateWindowHeight(300); // Collapse window smoothly
                HideConsole();
            }
        }

        private void AnimateWindowHeight(double newHeight)
        {
            var animation = new DoubleAnimation
            {
                From = this.Height,
                To = newHeight,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };

            this.BeginAnimation(HeightProperty, animation);
        }

        private async void btnCheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            if (!isSpicetifyInstalled)
            {
                ModernMessageBox.Show(
                    "Spicetify is not installed. Please install it first.",
                    "Spicetify Not Found",
                    MessageBoxImage.Warning);
                return;
            }

            btnCheckUpdates.IsEnabled = false;
            btnCheckUpdates.Content = "Checking...";

            try
            {
                await CheckForUpdates();
            }
            catch (Exception ex)
            {
                ModernMessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxImage.Error);
            }
            finally
            {
                btnCheckUpdates.IsEnabled = true;
                btnCheckUpdates.Content = "Check for Updates";
            }
        }

        private async Task CheckForUpdates()
        {
            string? currentVersion = (await ExecuteCommandAsync("spicetify", "--version", false))?.Trim();
            if (string.IsNullOrEmpty(currentVersion))
            {
                ModernMessageBox.Show("Could not determine current Spicetify version.", "Error", MessageBoxImage.Error);
                return;
            }

            string latestVersion = await GetLatestVersionFromGitHub();
            if (string.IsNullOrEmpty(latestVersion))
            {
                ModernMessageBox.Show("Could not fetch the latest version from GitHub.", "Error", MessageBoxImage.Error);
                return;
            }

            if (currentVersion == latestVersion)
            {
                ModernMessageBox.Show($"You are using the latest version: {currentVersion}", "Up to Date", MessageBoxImage.Information);
            }
            else
            {
                // Show update panel and perform update
                if (!isConsoleVisible)
                {
                    btnConsole_Click(this, new RoutedEventArgs()); // Programmatically click the console button
                }
                txtOutput.Text = $"New version available!\r\nCurrent: {currentVersion}\r\nLatest:  {latestVersion}\r\n\r\nUpdating Spicetify...\r\n";
                await PerformUpdate();
            }
        }

        private async Task<string> GetLatestVersionFromGitHub()
        {
            try
            {
                string url = "https://api.github.com/repos/spicetify/cli/releases/latest";
                string json = await httpClient.GetStringAsync(url);
                var release = JsonConvert.DeserializeObject<GitHubRelease>(json);
                return release?.tag_name?.TrimStart('v');
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"Network error fetching version: {ex.Message}");
            }
        }

        private async Task PerformUpdate()
        {
            try
            {
                string updateOutput = await ExecuteCommandAsync("spicetify", "update", true);
                txtOutput.AppendText("\r\nUpdate completed successfully!\r\n");
            }
            catch (Exception ex)
            {
                txtOutput.AppendText($"\r\nError during update: {ex.Message}\r\n");
            }
        }

        private async Task<string?> ExecuteCommandAsync(string command, string arguments, bool showOutput)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = new Process { StartInfo = startInfo };
            var outputBuilder = new StringBuilder();

            process.OutputDataReceived += (s, e) => {
                if (e.Data != null)
                {
                    outputBuilder.AppendLine(e.Data);
                    this.Dispatcher.Invoke(() => txtOutput.AppendText(e.Data + "\r\n"));
                }
            };
            process.ErrorDataReceived += (s, e) => {
                if (e.Data != null)
                {
                    outputBuilder.AppendLine(e.Data);
                    this.Dispatcher.Invoke(() => txtOutput.AppendText("ERROR: " + e.Data + "\r\n"));
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0 && !outputBuilder.ToString().Contains("is not recognized"))
            {
                throw new Exception($"Command failed with exit code {process.ExitCode}.");
            }

            return outputBuilder.ToString();
        }

        protected override void OnClosed(EventArgs e)
        {
            isClosing = true;
            httpClient.Dispose();
            KillStartedPowerShells();
            base.OnClosed(e);
        }

        private void ConsoleImage_ImageFailed(object sender, ExceptionRoutedEventArgs e)
        {
            MessageBox.Show($"Failed to load console.png: {e.ErrorException?.Message}", "Image Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public class GitHubRelease
    {
        [JsonProperty("tag_name")]
        public string? tag_name { get; set; }
    }

    // Modern Message Box for WPF
    public static class ModernMessageBox
    {
        public static void Show(string message, string title, MessageBoxImage icon)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, icon);
        }
    }
}
