# JulesClient

A modern Windows desktop GUI application for interacting with the **Jules API** (Google's AI coding agent API). Built with C# and WinUI 3, JulesClient provides a sleek, feature-rich interface for managing connected GitHub repositories, launching AI coding sessions, tracking real-time progress, inspecting rich chat activity feeds, reviewing code diffs, approving AI plans, and managing network/cache settings.

> **Status:** Work in Progress

## Key Features

- **Sources Browser**
  - Browse all connected GitHub repositories in a visual grid layout with interactive Windows Security–style hover effects.
  - Create new AI coding sessions with customizable starting branch, detailed goal prompts, plan approval requirement, and automatic Pull Request creation options.

- **Session Management & Active PR Banner**
  - Two-pane navigation layout displaying active and historical sessions.
  - **Live status indicator** in the session header — *Working…* (with spinner), *Completed*, *Failed*, *Waiting for plan approval*, *Waiting for your reply*, *Paused* — derived from the session state and the newest terminal activity.
  - Header actions to toggle real-time activity polling, copy the raw session JSON to the clipboard, open the active Pull Request, and open the session on `jules.google.com` (for actions the API doesn't expose, such as pausing).

- **Rich Chat & Activity Stream**
  - Interactive activity feed displaying user messages, agent updates, progress steps, bash command execution outputs, media attachments, and code review cards.
  - **Custom Markdown & Syntax Highlighting Engine**: Custom parser handling Markdown formatting (bold, italic, inline code, fenced code blocks with `` ``` `` and `~~~`), and Git conflict markers (`<<<<<<<`, `=======`, `>>>>>>>`). The highlighter (`CodeHighlighter`) uses per-language keyword/type sets for ~15 languages (C/C++, C#, JS/TS, Python, Java, Kotlin, Swift, Go, Rust, Ruby, PHP, shell, SQL) plus function-name and boolean/`null` constant recognition, hex/binary/octal number literals, and case-insensitive SQL.
  - **Collapsible content**: long "thinking out loud" agent messages, long user messages (e.g. a pasted log), and long fenced code blocks fold to a summary with a **Show more** toggle; every code block gets a collapsible header showing its language and line count.
  - **File-change notes**: an *"Updated `file` and `file`"* line for each changeset, computed as a per-step delta (only the files whose diff changed since the previous snapshot) with binary / mode-only files filtered out.
  - **Specialized Code Review Templates**: Prominent, styled cards (light blue theme with accent border and badge) dedicated to AI code reviews.
  - **Clutter reduction**: filters out duplicate patch signatures, previously seen PR links, repeated lifecycle lines ("Plan approved", "Session completed") and the echoed copy of a just-sent message; an optional **Show progress updates** toggle hides Jules' step-by-step narration.
  - **Jump to latest** button appears while the feed is scrolled up; with *Verbose logging* on, each message gains a **Raw JSON** panel showing the exact activity payload.

- **Hierarchical Unified Diff Viewer**
  - Per-file expandable unified diff view aggregating code changes across all session activities.
  - Line-by-line side-by-side style display with color-coded additions (green), deletions (pink), and patch headers (blue), complete with line numbers.

- **Plan Approval System**
  - Review AI-generated multi-step execution plans and approve pending plans directly from the UI.

- **Adaptive Polling & Bandwidth Management**
  - Real-time reactive activity updates powered by Rx.NET (`System.Reactive`).
  - **Bandwidth-Saving Mode**: Latency tracking (5-request sliding window with 2-second threshold) that dynamically adjusts polling intervals (3s vs 30s) and page sizes (20 vs 5) to optimize bandwidth usage.
  - Manual polling toggle in session header.

- **Caching & Offline Support**
  - Response caching via `CachedJulesApiClient` with a 24-hour TTL for sources, sessions, and activities.
  - Disk-backed cache size management with a 500MB cap and manual cache clearing in Settings.

- **Standalone Demo Mode**
  - Built-in Demo Mode (`DemoJulesApiClient`) providing mock sources, sessions, activities, plans, and diffs for offline evaluation without requiring a Jules API key.

- **Flexible Proxy Support**
  - Support for Direct (None), Custom SOCKS5, and System proxy configurations.
  - Custom SOCKS5 implementation (`Socks5Handshaker`) supporting IPv4, IPv6, and Domain target resolution, optional username/password authentication, and error mapping.

- **Modern Windows 11 Design**
  - Native Windows 11 aesthetics with Mica backdrop, dark/light theme awareness, custom title bar, and fluent UI controls.
  - App icon (`Assets/jules.ico` and the MSIX logo PNGs) generated from `jules.svg` by `tools/generate-icons.mjs`.

## Tech Stack

| Component | Technology |
|---|---|
| **Language** | C# (.NET 8.0, nullable reference types) |
| **UI Framework** | WinUI 3 (Windows App SDK 1.6) |
| **MVVM Framework** | CommunityToolkit.Mvvm 8.2.2 |
| **Reactive Programming** | System.Reactive 6.0.0 |
| **Dependency Injection** | Microsoft.Extensions.DependencyInjection 8.0.0 |
| **Testing** | xUnit 2.9.2, Moq 4.20.72 (110 unit tests) |
| **Packaging** | MSIX (Windows App Package) |
| **Target Platform** | Windows 10/11 (x64), minimum build 17763, target 19041 |

## Prerequisites

- Windows 10/11 (x64)
- .NET 8.0 SDK
- Visual Studio 2022 (v17.14+) with the **Windows App SDK** workload
- Jules API key (`jules.googleapis.com`) *(Optional if using Demo Mode)*

## Getting Started

### Build from Command Line

```bash
# Restore dependencies and build solution (Release configuration)
dotnet build JulesClient.sln -c Release

# Build and run unpackaged for local development
dotnet run --project JulesClient.csproj
```

### Run Tests

The test suite includes 110 unit tests validating API communication, caching logic, polling behavior, unified diff parsing (including per-file change detection), fenced-code splitting, Markdown parsing, and code highlighting:

```bash
# Run unit tests (with Windows targeting enabled for cross-platform test execution)
dotnet test -p:EnableWindowsTargeting=true
```

### Code Formatting

Verify code formatting against `.editorconfig` rules:

```bash
dotnet format JulesClient.sln --verify-no-changes --no-restore
```

## Configuration & Settings

All application settings are persisted locally via `Windows.Storage.ApplicationDataContainer`.

### API Key
Set your **Jules API Key** on the Settings page (`SettingsPage.xaml`). Alternatively, toggle **Demo Mode** to test UI capabilities using mock data.

### Proxy Modes
Choose between three proxy modes on the Settings page:
1. **Direct (None)**: Direct connection to `jules.googleapis.com`.
2. **SOCKS5 Proxy**: Custom SOCKS5 client proxy with optional host, port, username, and password.
3. **System Proxy**: Uses system-wide Windows OS proxy settings.

### Bandwidth & Cache
- **Bandwidth-Saving Mode**: Choose **Auto** (dynamic adjustment based on network latency) or **Manual**.
- **Cache Management**: View current disk cache size and clear cached responses on demand.

### Chat Display
Toggles on the Settings page control the activity feed:
- **Show progress updates** — show or hide Jules' step-by-step narration.
- **Collapse long agent messages** / **Collapse long user messages** — fold long messages to their first line with a *Show more* toggle.
- **Collapse long code blocks** — long fenced blocks start collapsed (all blocks keep a collapsible header).
- **Render Markdown**, **Syntax highlight code blocks**, **Show code block language label**, **Show speaker labels**, **Show timestamps**, **Auto-scroll to newest message**, and chat/code font family and size.
- **Verbose logging** (Advanced) — adds a per-message **Raw JSON** panel.

## Project Structure

```
JulesGuiCs/
├── .github/
│   └── workflows/
│       ├── ci.yml                      # CI pipeline: build, test, format check
│       └── OC.yml                      # AI task runner workflow
├── Assets/                             # Application logos, splash screens, and icons
├── Models/
│   └── JulesApi.cs                     # API DTO records (Source, Session, Activity, Plan, ChangeSet, etc.)
├── Services/
│   ├── AppSettings.cs                 # App configuration constants & settings keys
│   ├── CacheService.cs                 # Disk-backed cache manager with size cap (500MB)
│   ├── CachedJulesApiClient.cs         # Decorator client providing response caching (24h TTL)
│   ├── ChatActivityTemplateSelector.cs # DataTemplate selector: message / code review / system event / change note
│   ├── ChatConverters.cs               # Value converters for chat UI & timestamps
│   ├── CodeHighlighter.cs              # Per-language code syntax highlighter (~15 languages)
│   ├── Converters.cs                   # General UI converters (visibility, icons, status-kind → brush)
│   ├── DemoJulesApiClient.cs           # Mock Jules API client for standalone Demo Mode
│   ├── DemoService.cs                  # Demo mode state service implementation
│   ├── IDemoService.cs                 # Interface for demo service management
│   ├── DiffConverters.cs               # Value converters for git unified diff visualization
│   ├── DiffParser.cs                   # Unified diff parser, patch merger, per-file change detection & summaries
│   ├── FencedCode.cs                   # Splits Markdown into prose and fenced ``` / ~~~ code blocks
│   ├── JulesApiClient.cs               # Core HTTP client for Jules API with latency adaptation
│   ├── MarkdownConflict.cs             # Markdown conflict marker parser (<<<<<<< / ======= / >>>>>>>)
│   ├── MarkdownHelper.cs               # Markdown block parser & WinUI inline builder
│   ├── MarkdownInline.cs               # Markdown inline text styling (bold, italic, code)
│   ├── MarkdownPresenter.cs            # Markdown host control with collapsible long-text / code support
│   ├── MarkdownText.cs                 # Markdown block parser
│   ├── PollingService.cs               # Reactive Rx.NET polling manager for activity updates
│   ├── SettingsService.cs              # Local storage settings manager
│   └── Socks5Handshaker.cs             # SOCKS5 TCP client proxy with auth & error mapping
├── ViewModels/
│   ├── DiffViewModels.cs               # View models for diff files, hunks, and lines
│   ├── SessionsViewModel.cs            # View model for chat feed, active PRs, diff tab, & polling
│   ├── SettingsViewModel.cs            # View model for API settings, proxy, demo, & cache options
│   └── SourcesViewModel.cs             # View model for GitHub sources & session creation dialog
├── Views/
│   ├── SessionPage.xaml + .cs          # Main session view (Chat feed, active PR header, Diff tab)
│   ├── SettingsPage.xaml + .cs         # Settings configuration view
│   └── SourcePage.xaml + .cs           # Sources grid view & session creation modal
├── JulesClient.Tests/
│   ├── ActivityReviewTests.cs          # Unit tests for code review detection
│   ├── CachedJulesApiClientTests.cs    # Unit tests for API client caching
│   ├── CodeHighlighterTests.cs         # Unit tests for code syntax highlighting
│   ├── DiffParserTests.cs              # Unit tests for diff parsing, per-file change detection & summaries
│   ├── FencedCodeTests.cs              # Unit tests for fenced code block splitting
│   ├── JulesApiClientTests.cs          # Unit tests for Jules HTTP API client
│   ├── MarkdownConflictParserTests.cs  # Unit tests for Git conflict marker parsing
│   ├── MarkdownInlineTests.cs          # Unit tests for Markdown inline formatting
│   ├── MarkdownTextTests.cs            # Unit tests for Markdown block parsing
│   └── PollingServiceTests.cs          # Unit tests for Rx.NET polling service
├── tools/
│   └── generate-icons.mjs             # Regenerates the app icon assets from jules.svg (Node + sharp)
├── App.xaml + App.xaml.cs              # App entry point, DI container setup, Mica & proxy init
├── MainWindow.xaml + MainWindow.xaml.cs # Main application window with NavigationView
├── GlobalUsings.cs                     # Global using statements
├── JulesClient.csproj                  # Main WinUI 3 project file
├── JulesClient.sln                     # Visual Studio solution file
├── Package.appxmanifest                # MSIX package manifest
└── app.manifest                        # Windows application manifest (DPI awareness)
```

## Packaging and Signing

### App Icon

The window/taskbar icon (`Assets/jules.ico`) and the MSIX logo PNGs are generated from `jules.svg`:

```bash
npx --package sharp node tools/generate-icons.mjs
```

### 1. Build MSIX Package

```bash
msbuild JulesClient.csproj /p:Configuration=Release /p:Platform=x64 /p:AppxPackageDir="AppPackages\\" /p:AppxBundle=Never
```

### 2. Create Self-Signed Certificate (for local development)

Run PowerShell as Administrator:

```powershell
$cert = New-SelfSignedCertificate -Type Custom -Subject "CN=Jules" -KeyUsage DigitalSignature -FriendlyName "JulesClient Dev Cert" -CertStoreLocation "Cert:\LocalMachine\My" -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")
$password = ConvertTo-SecureString -String "Password123" -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath "JulesClient_TemporaryKey.pfx" -Password $password
```

> **Note:** The `Subject` must match the `Publisher` attribute in `Package.appxmanifest` (`CN=Jules`).

### 3. Sign Package

During build:

```bash
msbuild JulesClient.csproj /p:Configuration=Release /p:PackageCertificateKeyFile=JulesClient_TemporaryKey.pfx /p:PackageCertificatePassword=Password123
```

Or using `SignTool.exe`:

```bash
signtool sign /fd SHA256 /a /f JulesClient_TemporaryKey.pfx /p Password123 AppPackages\JulesClient_1.0.0.0_x64.msix
```

## CI/CD

Continuous Integration is powered by GitHub Actions:

- **CI Workflow** (`.github/workflows/ci.yml`): Runs on Pull Requests targeting `main` or `master`. Automatically restores dependencies, builds the solution, executes all 110 unit tests, and verifies C# code formatting.
- **OpenCode Workflow** (`.github/workflows/OC.yml`): Automated AI workflow runner.

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/my-feature`)
3. Commit your changes (`git commit -m 'Add some feature'`)
4. Push to the branch (`git push origin feature/my-feature`)
5. Open a Pull Request

## License

This project is licensed under the [Apache License 2.0](LICENSE).
