# Maintenance rules

Target .NET Framework 4.8, C# 5, Windows 7 SP1, x86 and x64. No modern .NET-only APIs or runtime NuGet dependencies. Keep native declarations in Interop. Never log note titles/content. Preserve corrupt originals and refuse unknown schema versions. All writes use same-directory temporary files, Flush(true), atomic replacement and a previous-good backup. Capture snapshots on the dispatcher; serialize saves through the storage gate. User-requested display mode must survive fallback unchanged.

Build both platforms with MSBuild /restore and run the console regression suite. Use docs/compatibility.md for manual OS tests; never equate compilation with Windows 7 validation. Desktop Shell integration is undocumented: document limitations and keep a usable Normal fallback. Do not kill Explorer or change system configuration for automated testing. No external DI container or unnecessary abstraction.
