# Spicetify Auto Updater

A modern WPF application that automatically checks for and installs updates for Spicetify CLI on Windows.

## Features

- **Automatic Spicetify Detection**: Checks if Spicetify is installed on startup and offers to install it if missing.
- **Version Comparison**: Compares your current version with the latest available from GitHub.
- **One-Click Updates**: Simple button to check for and install updates.
- **Real-time Output**: See update progress in a styled console-like interface.
- **Modern UI**: Clean, dark-themed interface.
- **Safe Operation**: Only runs Spicetify commands, no admin required.

## Prerequisites

- **.NET 6.0**: Runtime required to run the application.
- **Windows 10/11**: Designed for Windows.
- **Spicetify CLI**: The app can install this for you if missing.

## Installation


1. **Open** `SpicetifyAutoUpdater.sln` in Visual Studio 2022 or later.
2. **Restore NuGet packages** (should happen automatically).
3. **Build** the solution (`Ctrl+Shift+B`).
4. **Run** the application (`F5`).

## Usage

- **Launch** the app.
- If Spicetify is not installed, you'll be prompted to install it.
- Click **"Check for Updates / Install"** to:
  - Get your current Spicetify version.
  - Fetch the latest version from GitHub.
  - Update if needed.

## How It Works

- On startup, runs `spicetify --version` to check installation.
- If not found, offers to install Spicetify and checks the console output for a successful install.
- For updates, fetches the latest version from GitHub and compares.
- All commands run in background processes with real-time output.

## Troubleshooting

- **"Spicetify is not installed"**: Let the app install it, or install manually and ensure it's in your PATH.
- **Network Issues**: Check your internet connection and firewall.
- **Update Failures**: Check the console output for errors.

## Building from Source

- Visual Studio 2022 or later
- .NET 6.0 SDK

## License

MIT License

## Contributing

Pull requests and issues are welcome!

---
