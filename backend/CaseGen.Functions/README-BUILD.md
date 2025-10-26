# Build Configuration for Cross-Platform Support

## Problem Solved

This project includes build configuration files to resolve the `WorkerExtensions` build error that occurs on **macOS Silicon (ARM64)** due to incompatibility with the .NET Core 3.1 x86_64 metadata generator.

## Files Added

### 1. `Directory.Build.props`
- Detects the operating system at build time
- Disables Azure Functions metadata generation **only on macOS**
- Windows and Linux builds work normally with full metadata generation

### 2. `Directory.Build.targets`
- Provides empty target implementations for macOS
- Overrides the problematic metadata generation targets
- Conditional execution based on OS detection

### 3. `build-macos.sh` (Optional)
- Convenience script for macOS users
- Provides clear feedback during build process
- Can be used as an alternative to `dotnet build`

## How It Works

The solution uses MSBuild's OS detection capabilities:

```xml
<IsMacOS Condition="'$([System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform($([System.Runtime.InteropServices.OSPlatform]::OSX)))' == 'true'">true</IsMacOS>
```

All workarounds are **conditionally applied only on macOS**, ensuring:
- ✅ **macOS Silicon**: Builds successfully by skipping incompatible metadata generation
- ✅ **Windows**: Full metadata generation with `extensions.json` created at build time
- ✅ **Linux**: Full metadata generation works normally
- ✅ **Runtime**: Azure Functions runtime generates missing metadata automatically when needed

## Building the Project

### On macOS
```bash
dotnet build
# or
./build-macos.sh
```

### On Windows
```powershell
dotnet build
```

### On Linux
```bash
dotnet build
```

## Important Notes

1. **The metadata file (`extensions.json`) is optional for isolated worker process Azure Functions**
   - It's an optimization, not a requirement
   - The runtime can discover extensions without it

2. **No changes needed when switching platforms**
   - The same source code works on all platforms
   - No manual configuration required

3. **CI/CD Pipelines**
   - These files are safe for CI/CD
   - Each platform will use the appropriate configuration automatically

## Technical Details

The issue occurs because:
- Azure Functions SDK generates a sub-project called `WorkerExtensions`
- This project uses .NET Core 3.1 metadata generator
- The 3.1 runtime on macOS is x86_64 (Intel) only
- Apple Silicon Macs (M1/M2/M3) are ARM64 architecture
- Rosetta 2 emulation doesn't work for this specific build tool

The solution bypasses metadata generation on macOS while keeping it functional on other platforms.

## Verification

After building, you can verify the configuration is working:

### On macOS
```bash
dotnet build -v detailed | grep -i metadata
```
You should see: `✓ Geração de metadados ignorada para macOS Silicon (ARM64)`

### On Windows
The build will generate `bin/Debug/net9.0/bin/extensions.json` normally.

## Questions?

If you encounter build issues:
1. Clean the build artifacts: `rm -rf bin/ obj/` (macOS/Linux) or `rmdir /s bin obj` (Windows)
2. Restore packages: `dotnet restore`
3. Build again: `dotnet build`

For more information, see the [Azure Functions documentation](https://learn.microsoft.com/azure/azure-functions/).
