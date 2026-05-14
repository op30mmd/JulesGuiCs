# JulesGuiCs
A GUI for Jules API endpoint written in C# and WinUI 3 for Windows

# Status
Work in progress

## Packaging and Signing

This project is configured for MSIX packaging. To build and sign the package, follow these steps:

### 1. Build the MSIX package
You can build the package using MSBuild or the Visual Studio UI.
```bash
msbuild JulesClient.csproj /p:Configuration=Release /p:Platform=x64 /p:AppxPackageDir="AppPackages\\" /p:AppxBundle=Never
```

### 2. Create a Self-Signed Certificate (for local testing)
If you don't have a code-signing certificate, you can create a self-signed one using PowerShell (run as Administrator):
```powershell
$cert = New-SelfSignedCertificate -Type Custom -Subject "CN=Jules" -KeyUsage DigitalSignature -FriendlyName "JulesClient Dev Cert" -CertStoreLocation "Cert:\LocalMachine\My" -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")
$password = ConvertTo-SecureString -String "Password123" -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath "JulesClient_TemporaryKey.pfx" -Password $password
```
*Note: The Subject must match the Publisher attribute in `Package.appxmanifest` (`CN=Jules`).*

### 3. Sign the Package
You can sign the package during the build by passing the certificate properties:
```bash
msbuild JulesClient.csproj /p:Configuration=Release /p:PackageCertificateKeyFile=JulesClient_TemporaryKey.pfx /p:PackageCertificatePassword=Password123
```
Alternatively, use `SignTool.exe` from the Windows SDK:
```bash
signtool sign /fd SHA256 /a /f JulesClient_TemporaryKey.pfx /p Password123 AppPackages\JulesClient_1.0.0.0_x64.msix
```

### 4. Trust the Certificate
To install the signed MSIX locally, you must trust the certificate:
1. Right-click `JulesClient_TemporaryKey.pfx` -> **Install PFX**.
2. Store Location: **Local Machine**.
3. Place all certificates in the following store: **Trusted Root Certification Authorities**.
