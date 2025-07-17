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
        private bool isDebugMode = false;
        private bool isUpdating = false;

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
                // Reset button state if no operations are running
                if (!isUpdating && !isInstalling)
                {
                    btnCheckUpdates.IsEnabled = true;
                    btnCheckUpdates.Content = "Check for Updates";
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
                
                // Add welcome message if console is empty
                if (string.IsNullOrWhiteSpace(txtOutput.Text))
                {
                    txtOutput.AppendText("=== Spicetify Auto Updater Console ===\r\n");
                    txtOutput.AppendText("Type 'debug' to enter debug mode for advanced commands.\r\n");
                    txtOutput.AppendText("Debug commands: help, force update, status, clear, exit\r\n\r\n");
                }
                
                txtDebugInput.Focus();
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

        private void txtDebugInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var input = txtDebugInput.Text.Trim().ToLower();
                if (input == "debug")
                {
                    Dispatcher.Invoke(() => {
                        isDebugMode = true;
                        txtOutput.AppendText("[DEBUG] Debug mode activated!\r\n");
                        txtOutput.AppendText("[DEBUG] Available commands: help, force update, force install, status, clear, exit\r\n");
                        txtDebugInput.Text = "";
                        ScrollConsoleToEnd();
                    });
                }
                else if (input == "exit" || input == "quit")
                {
                    Dispatcher.Invoke(() => {
                        isDebugMode = false;
                        txtOutput.AppendText("[DEBUG] Debug mode deactivated.\r\n");
                        txtDebugInput.Text = "";
                        ScrollConsoleToEnd();
                    });
                }
                else if (isDebugMode && input == "force update")
                {
                    Dispatcher.Invoke(() => {
                        txtOutput.AppendText("[DEBUG] Force update command received.\r\n");
                        txtDebugInput.Text = "";
                        ScrollConsoleToEnd();
                    });
                    _ = ForceUpdateInDebugMode();
                }
                else if (isDebugMode && input == "force install")
                {
                    Dispatcher.Invoke(() => {
                        txtOutput.AppendText("[DEBUG] Force install command received.\r\n");
                        txtDebugInput.Text = "";
                        ScrollConsoleToEnd();
                    });
                    _ = ForceInstallInDebugMode();
                }
                else if (isDebugMode && input == "help")
                {
                    Dispatcher.Invoke(() => {
                        txtOutput.AppendText("[DEBUG] Available commands:\r\n");
                        txtOutput.AppendText("[DEBUG]   help - Show this help message\r\n");
                        txtOutput.AppendText("[DEBUG]   force update - Force update Spicetify regardless of version\r\n");
                        txtOutput.AppendText("[DEBUG]   force install - Force install Spicetify and Marketplace\r\n");
                        txtOutput.AppendText("[DEBUG]   status - Show current debug and installation status\r\n");
                        txtOutput.AppendText("[DEBUG]   clear - Clear console output\r\n");
                        txtOutput.AppendText("[DEBUG]   exit/quit - Exit debug mode\r\n");
                        txtDebugInput.Text = "";
                        ScrollConsoleToEnd();
                    });
                }
                else if (isDebugMode && input == "status")
                {
                    Dispatcher.Invoke(() => {
                        txtOutput.AppendText($"[DEBUG] Debug mode: {(isDebugMode ? "ON" : "OFF")}\r\n");
                        txtOutput.AppendText($"[DEBUG] Spicetify installed: {(isSpicetifyInstalled ? "YES" : "NO")}\r\n");
                        txtOutput.AppendText($"[DEBUG] Currently installing: {(isInstalling ? "YES" : "NO")}\r\n");
                        txtOutput.AppendText($"[DEBUG] Currently updating: {(isUpdating ? "YES" : "NO")}\r\n");
                        txtOutput.AppendText($"[DEBUG] Install completed: {(installCompleted ? "YES" : "NO")}\r\n");
                        txtDebugInput.Text = "";
                        ScrollConsoleToEnd();
                    });
                }
                else if (isDebugMode && input == "clear")
                {
                    Dispatcher.Invoke(() => {
                        txtOutput.Clear();
                        txtDebugInput.Text = "";
                        ScrollConsoleToEnd();
                    });
                }
                else if (isDebugMode)
                {
                    Dispatcher.Invoke(() => {
                        txtOutput.AppendText($"[DEBUG] Unknown command: {input}\r\n");
                        txtOutput.AppendText("[DEBUG] Type 'help' for available commands.\r\n");
                        txtDebugInput.Text = "";
                        ScrollConsoleToEnd();
                    });
                }
                else
                {
                    Dispatcher.Invoke(() => {
                        txtOutput.AppendText($"[CONSOLE] Type 'debug' to enter debug mode.\r\n");
                        txtDebugInput.Text = "";
                        ScrollConsoleToEnd();
                    });
                }
            }
        }

        private async Task ForceUpdateInDebugMode()
        {
            if (!isDebugMode) return;

            isUpdating = true;
            ShowProgress("Force updating Spicetify...");
            txtOutput.Clear();
            txtOutput.AppendText("[DEBUG] Starting force update process...\r\n");
            
            try
            {
                string updateOutput = await ExecuteCommandAsyncWithCallback(
                    "spicetify",
                    "update",
                    true,
                    (line) =>
                    {
                        if (line != null)
                        {
                            txtOutput.AppendText("[UPDATE] " + line + "\r\n");
                            ScrollConsoleToEnd();
                        }
                    }
                ) ?? string.Empty;

                txtOutput.AppendText("\r\n[DEBUG] Force update completed!\r\n");
                ModernMessageBox.Show("Force update completed successfully!", "Update Complete", MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                txtOutput.AppendText($"\r\n[DEBUG] Error during force update: {ex.Message}\r\n");
                ModernMessageBox.Show($"Force update failed: {ex.Message}", "Update Failed", MessageBoxImage.Error);
            }
            finally
            {
                isUpdating = false;
                HideProgress();
            }
        }

        private async Task ForceInstallInDebugMode()
        {
            if (!isDebugMode) return;

            isInstalling = true;
            ShowProgress("Force installing Spicetify and Marketplace...");
            txtOutput.Clear();
            txtOutput.AppendText("[DEBUG] Starting force install process...\r\n");
            try
            {
                string installOutput = await InstallSpicetifyAndMarketplace();
                txtOutput.AppendText("\r\n[DEBUG] Force install completed!\r\n");
                ModernMessageBox.Show("Force install completed!", "Install Complete", MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                txtOutput.AppendText($"\r\n[DEBUG] Error during force install: {ex.Message}\r\n");
                ModernMessageBox.Show($"Force install failed: {ex.Message}", "Install Failed", MessageBoxImage.Error);
            }
            finally
            {
                isInstalling = false;
                HideProgress();
            }
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
                    if (onOutputLine != null)
                    {
                        this.Dispatcher.Invoke(() => onOutputLine(e.Data));
                    }
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
                    if (onOutputLine != null)
                    {
                        this.Dispatcher.Invoke(() => onOutputLine(e.Data));
                    }
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

            if (isUpdating || isInstalling)
            {
                ModernMessageBox.Show("An operation is already in progress. Please wait.", "Operation in Progress", MessageBoxImage.Warning);
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
                if (!isUpdating && !isInstalling)
                {
                    btnCheckUpdates.IsEnabled = true;
                    btnCheckUpdates.Content = "Check for Updates";
                }
            }
        }

        private async Task CheckForUpdates()
        {
            if (isUpdating || isInstalling)
            {
                ModernMessageBox.Show("An operation is already in progress. Please wait.", "Operation in Progress", MessageBoxImage.Warning);
                return;
            }

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

            if (currentVersion == latestVersion && !isDebugMode)
            {
                ModernMessageBox.Show($"You are using the latest version: {currentVersion}", "Up to Date", MessageBoxImage.Information);
            }
            else
            {
                // Show update panel and perform update with progress bar
                if (!isConsoleVisible)
                {
                    btnConsole_Click(this, new RoutedEventArgs()); // Programmatically click the console button
                }
                
                isUpdating = true;
                ShowProgress("Checking for updates...");
                txtOutput.Clear();
                txtOutput.AppendText($"[UPDATE] Current version: {currentVersion}\r\n");
                txtOutput.AppendText($"[UPDATE] Latest version: {latestVersion}\r\n");
                
                if (currentVersion == latestVersion && isDebugMode)
                {
                    txtOutput.AppendText("[DEBUG] Force update requested despite being on latest version.\r\n");
                }
                else
                {
                    txtOutput.AppendText("[UPDATE] New version available! Starting update...\r\n");
                }
                
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
                ShowProgress("Updating Spicetify...");
                txtOutput.AppendText("[UPDATE] Starting Spicetify update process...\r\n");
                
                string updateOutput = await ExecuteCommandAsyncWithCallback(
                    "spicetify",
                    "update",
                    true,
                    (line) =>
                    {
                        if (line != null)
                        {
                            this.Dispatcher.Invoke(() => {
                                txtOutput.AppendText("[UPDATE] " + line + "\r\n");
                                ScrollConsoleToEnd();
                            });
                        }
                    }
                ) ?? string.Empty;

                txtOutput.AppendText("\r\n[UPDATE] Update completed successfully!\r\n");
                ModernMessageBox.Show("Spicetify has been updated successfully!", "Update Complete", MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                txtOutput.AppendText($"\r\n[UPDATE] Error during update: {ex.Message}\r\n");
                ModernMessageBox.Show($"Update failed: {ex.Message}", "Update Failed", MessageBoxImage.Error);
            }
            finally
            {
                HideProgress();
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
