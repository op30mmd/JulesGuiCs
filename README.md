# JulesClient

A Windows desktop GUI application for interacting with the **Jules API** (Google's AI coding agent API). Built with C# and WinUI 3, JulesClient provides a visual interface for browsing connected GitHub repositories, creating AI coding sessions, monitoring progress in real-time, reviewing chat-like activity feeds, approving plans, and inspecting code diffs.

> **Status:** Work in Progress

## Features

- **Sources Browser** -- View all connected GitHub repositories in a grid layout and create new AI sessions with configurable options (title, prompt/goal, branch, plan approval, auto-PR creation)
- **Session Management** -- Two-pane layout with session list and tabbed detail view
- **Chat View** -- Conversation-style activity feed displaying user messages, agent responses, progress updates, generated plans, bash output, code reviews, media/images, and pull request links with markdown rendering support
- **Diff Viewer** -- Aggregate unified diff view of all code changes with expandable file nodes, color-coded added/removed lines, line numbers, and diff prefixes
- **Plan Approval** -- Approve pending AI-generated plans directly from the UI
- **Real-Time Polling** -- Activities are polled every 3 seconds using Rx.NET observables with incremental fetching and pagination support
- **SOCKS5 Proxy Support** -- Full proxy support with optional username/password authentication
- **Settings Persistence** -- API key and proxy settings saved locally via Windows Storage
- **Modern Windows UI** -- Mica backdrop, custom title bar, and Windows 11 design patterns

## Tech Stack

| Component | Technology |
|---|---|
| **Language** | C# (.NET 8.0, nullable reference types) |
| **UI Framework** | WinUI 3 (Windows App SDK 1.6) |
| **MVVM** | CommunityToolkit.Mvvm 8.2.2 |
| **Reactive** | System.Reactive 6.0.0 |
| **DI** | Microsoft.Extensions.DependencyInjection 8.0.0 |
| **Testing** | xUnit 2.9.2, Moq 4.20.72 |
| **Packaging** | MSIX (Windows App Package) |
| **Platform** | Windows 10/11 (x64), minimum build 17763, target 19041 |

## Prerequisites

- Windows 10/11 (x64)
- .NET 8.0 SDK
- Visual Studio 2022 (v17.14+) with Windows App SDK workload
- Jules API access (`jules.googleapis.com`)

## Getting Started

### Build

```bash
# Build the solution (Release configuration)
dotnet build JulesClient.sln -c Release

# Build as unpackaged for debugging
dotnet run --project JulesClient.csproj
```

### Build MSIX Package

```bash
msbuild JulesClient.csproj /p:Configuration=Release /p:Platform=x64 /p:AppxPackageDir="AppPackages\\" /p:AppxBundle=Never
```

### Run Tests

```bash
dotnet test JulesClient.Tests/JulesClient.Tests.csproj --configuration Release
```

### Format Check

```bash
dotnet format JulesClient.sln --verify-no-changes --no-restore
```

## Configuration

### API Key

Set your Jules API key in the Settings page within the app. The key is persisted locally using `Windows.Storage.ApplicationDataContainer`.

### SOCKS5 Proxy

Configure SOCKS5 proxy settings (host, port, optional username/password) in the Settings page. The proxy is integrated into `HttpClient` via a custom `Socks5Handshaker`.

## Project Structure

```
JulesGuiCs/
├── .github/workflows/
│   ├── ci.yml                          # CI: build, test, format check on PR
│   └── OC.yml                          # OpenCode AI task runner workflow
├── Assets/                             # App icons and resources
├── JulesClient.Tests/
│   ├── JulesApiClientTests.cs          # API client unit tests
│   └── PollingServiceTests.cs          # Polling service unit tests
├── Models/
│   └── JulesApi.cs                     # API DTO records (Source, Session, Activity, Plan, etc.)
├── Services/
│   ├── JulesApiClient.cs               # HTTP client for Jules API
│   ├── PollingService.cs               # Reactive polling manager for session activities
│   ├── SettingsService.cs              # Local settings persistence
│   ├── Socks5Handshaker.cs             # SOCKS5 proxy handshake implementation
│   ├── DiffParser.cs                   # Unified diff patch parser
│   ├── DiffConverters.cs               # XAML value converters for diff display
│   ├── ChatConverters.cs               # XAML value converters for chat UI
│   ├── MarkdownHelper.cs               # Lightweight markdown parser
│   └── Converters.cs                   # General XAML value converters
├── ViewModels/
│   ├── SettingsViewModel.cs            # Settings page VM
│   ├── SourcesViewModel.cs             # Sources page VM
│   ├── SessionsViewModel.cs            # Sessions page VM (chat + diff)
│   └── DiffViewModels.cs               # Diff file/hunk/line VMs
├── Views/
│   ├── SourcePage.xaml + .cs           # Sources grid + create session dialog
│   ├── SessionPage.xaml + .cs          # Sessions list + chat/diff tabs
│   └── SettingsPage.xaml + .cs         # API key + proxy settings
├── App.xaml + App.xaml.cs              # App entry, DI container, exception handlers
├── MainWindow.xaml + MainWindow.xaml.cs # Main window with NavigationView
├── GlobalUsings.cs                     # Global using directives
├── JulesClient.csproj                  # Main project file
├── JulesClient.sln                     # Visual Studio solution
├── Package.appxmanifest                # MSIX package manifest
└── app.manifest                        # Windows app manifest (DPI awareness)
```

## Packaging and Signing

### 1. Build the MSIX Package

```bash
msbuild JulesClient.csproj /p:Configuration=Release /p:Platform=x64 /p:AppxPackageDir="AppPackages\\" /p:AppxBundle=Never
```

### 2. Create a Self-Signed Certificate (for local testing)

Run PowerShell as Administrator:

```powershell
$cert = New-SelfSignedCertificate -Type Custom -Subject "CN=Jules" -KeyUsage DigitalSignature -FriendlyName "JulesClient Dev Cert" -CertStoreLocation "Cert:\LocalMachine\My" -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")
$password = ConvertTo-SecureString -String "Password123" -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath "JulesClient_TemporaryKey.pfx" -Password $password
```

> **Note:** The Subject must match the Publisher in `Package.appxmanifest` (`CN=Jules`).

### 3. Sign the Package

During build:

```bash
msbuild JulesClient.csproj /p:Configuration=Release /p:PackageCertificateKeyFile=JulesClient_TemporaryKey.pfx /p:PackageCertificatePassword=Password123
```

Or using `SignTool.exe`:

```bash
signtool sign /fd SHA256 /a /f JulesClient_TemporaryKey.pfx /p Password123 AppPackages\JulesClient_1.0.0.0_x64.msix
```

### 4. Trust the Certificate

1. Right-click `JulesClient_TemporaryKey.pfx` → **Install PFX**
2. Store Location: **Local Machine**
3. Place all certificates in: **Trusted Root Certification Authorities**

## CI/CD

The project uses GitHub Actions for continuous integration:

- **CI Workflow** (`.github/workflows/ci.yml`): Runs on PRs to `main`/`master` -- restores dependencies, builds the solution, runs tests, and verifies code formatting
- **OpenCode Workflow** (`.github/workflows/OC.yml`): Automated AI task runner for code tasks

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/my-feature`)
3. Commit your changes (`git commit -m 'Add some feature'`)
4. Push to the branch (`git push origin feature/my-feature`)
5. Open a Pull Request

## License

This project is licensed under the [Apache License 2.0](LICENSE).
