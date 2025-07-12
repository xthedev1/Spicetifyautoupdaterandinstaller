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

        public MainWindow()
        {
            InitializeComponent();
            httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "SpicetifyAutoUpdater/1.0");
            
            // Initialize console as hidden
            panelConsole.Visibility = Visibility.Collapsed;
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

        private async Task CheckSpicetifyInstallation()
        {
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
                ModernMessageBox.Show(
                    $"Error checking Spicetify installation: {ex.Message}",
                    "Error",
                    MessageBoxImage.Error);
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
                    // Show console
                    if (!isConsoleVisible)
                    {
                        btnConsole_Click(this, new RoutedEventArgs());
                    }
                    txtOutput.AppendText("Installing Spicetify...\r\n");
                    string installOutput = await InstallSpicetify();
                    // Check for success message in output
                    if (!string.IsNullOrEmpty(installOutput) && installOutput.ToLower().Contains("spicetify was successfully installed!"))
                    {
                        txtOutput.AppendText("\r\nSpicetify installed successfully! Installing Spicetify Marketplace...\r\n");
                        bool marketplaceInstalled = false;
                        
                        try
                        {
                            string marketplaceOutput = await InstallSpicetifyMarketplace();
                            txtOutput.AppendText("Spicetify Marketplace installed successfully!\r\n");
                            marketplaceInstalled = true;
                        }
                        catch (Exception ex)
                        {
                            txtOutput.AppendText($"\r\nWarning: Spicetify Marketplace installation failed: {ex.Message}\r\n");
                            txtOutput.AppendText("Spicetify is still installed and functional.\r\n");
                        }
                        
                        btnCheckUpdates.IsEnabled = false;
                        btnCheckUpdates.Content = "Spicetify Installed!";
                        
                        string message = marketplaceInstalled 
                            ? "Spicetify and Spicetify Marketplace were successfully installed! Would you like to close this app and open Spotify now?"
                            : "Spicetify was successfully installed! (Marketplace installation failed)\r\nWould you like to close this app and open Spotify now?";
                            
                        var openSpotify = MessageBox.Show(message, "Installation Complete", MessageBoxButton.YesNo, MessageBoxImage.Question);
                        
                        if (openSpotify == MessageBoxResult.Yes)
                        {
                            try
                            {
                                Process.Start(new ProcessStartInfo
                                {
                                    FileName = "spotify",
                                    UseShellExecute = true
                                });
                            }
                            catch (Exception ex)
                            {
                                ModernMessageBox.Show($"Failed to open Spotify: {ex.Message}", "Error", MessageBoxImage.Error);
                            }
                            this.Close();
                        }
                    }
                    else
                    {
                        ModernMessageBox.Show("Spicetify installation did not complete successfully. Please check the console output for details.", "Install Failed", MessageBoxImage.Error);
                    }
                    // Re-check installation
                    await CheckSpicetifyInstallation();
                    return;
                }
                else
                {
                    btnCheckUpdates.IsEnabled = false;
                    btnCheckUpdates.Content = "Spicetify Not Found";
                }
            }
        }

        private async Task<string> InstallSpicetify()
        {
            // Run the PowerShell install command
            string psCmd = "iwr -useb https://raw.githubusercontent.com/spicetify/cli/main/install.ps1 | iex";
            return await ExecuteCommandAsync("powershell", $"-NoProfile -ExecutionPolicy Bypass -Command \"{psCmd}\"", true);
        }

        private async Task<string> InstallSpicetifyMarketplace()
        {
            // Try multiple approaches to install Spicetify Marketplace
            try
            {
                // First, try using curl (available on Windows 10+)
                string curlCmd = "curl -fsSL https://raw.githubusercontent.com/spicetify/marketplace/main/resources/install.sh | sh";
                return await ExecuteCommandAsync("cmd", $"/c {curlCmd}", true);
            }
            catch (Exception ex1)
            {
                try
                {
                    // Fallback: try using PowerShell to download and execute
                    string psCmd = "Invoke-WebRequest -Uri 'https://raw.githubusercontent.com/spicetify/marketplace/main/resources/install.sh' -OutFile '$env:TEMP\\install_marketplace.sh'; if (Get-Command bash -ErrorAction SilentlyContinue) { bash '$env:TEMP\\install_marketplace.sh' } else { Write-Host 'Bash not available, marketplace installation skipped' }";
                    return await ExecuteCommandAsync("powershell", $"-NoProfile -ExecutionPolicy Bypass -Command \"{psCmd}\"", true);
                }
                catch (Exception ex2)
                {
                    throw new Exception($"Marketplace installation failed. Curl error: {ex1.Message}. PowerShell error: {ex2.Message}");
                }
            }
        }

        private void btnConsole_Click(object sender, RoutedEventArgs e)
        {
            isConsoleVisible = !isConsoleVisible;

            if (isConsoleVisible)
            {
                btnConsole.ToolTip = "Hide Console";
                AnimateWindowHeight(500); // Expand window smoothly
                panelConsole.Visibility = Visibility.Visible;
            }
            else
            {
                btnConsole.ToolTip = "Show Console";
                AnimateWindowHeight(300); // Collapse window smoothly
                panelConsole.Visibility = Visibility.Collapsed;
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
                if (e.Data != null) {
                    outputBuilder.AppendLine(e.Data);
                    this.Dispatcher.Invoke(() => txtOutput.AppendText(e.Data + "\r\n"));
                }
            };
            process.ErrorDataReceived += (s, e) => {
                if (e.Data != null) {
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
            httpClient.Dispose();
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
