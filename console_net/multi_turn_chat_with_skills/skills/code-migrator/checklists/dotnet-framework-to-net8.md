# .NET Framework to .NET 8 Migration Checklist

## Pre-Migration

- [ ] Analyze with .NET Upgrade Assistant
- [ ] Review [Microsoft's porting guide](https://docs.microsoft.com/en-us/dotnet/core/porting/)
- [ ] Check NuGet package compatibility
- [ ] Identify unsupported APIs

## Project File Changes

### Convert to SDK-style csproj

**Before (.NET Framework):**
```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="15.0" xmlns="...">
  <Import Project="$(MSBuildExtensionsPath)\..." />
  <PropertyGroup>
    <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="System" />
    <Reference Include="System.Core" />
    <!-- hundreds of lines -->
  </ItemGroup>
</Project>
```

**After (.NET 8):**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
```

## Common API Changes

| .NET Framework | .NET 8 |
|---------------|--------|
| `HttpWebRequest` | `HttpClient` |
| `WebClient` | `HttpClient` |
| `ConfigurationManager` | `IConfiguration` |
| `Thread.Abort()` | `CancellationToken` |
| `Remoting` | gRPC or REST |
| `AppDomain.CreateDomain` | `AssemblyLoadContext` |

## Package Replacements

| Old Package | New Package |
|-------------|-------------|
| `System.Web` | `Microsoft.AspNetCore.*` |
| `System.Web.Mvc` | `Microsoft.AspNetCore.Mvc` |
| `EntityFramework` | `Microsoft.EntityFrameworkCore` |
| `Newtonsoft.Json` | `System.Text.Json` (or keep) |

## ASP.NET to ASP.NET Core

- [ ] Replace `Global.asax` with `Program.cs`
- [ ] Replace `Web.config` with `appsettings.json`
- [ ] Update `HttpContext.Current` to DI
- [ ] Migrate authentication to Identity/JWT
- [ ] Update routing to attribute routing
- [ ] Replace `HttpModules` with middleware

## Entity Framework to EF Core

- [ ] Update `DbContext` configuration
- [ ] Replace `ObjectContext` patterns
- [ ] Update lazy loading setup
- [ ] Review query translation differences
- [ ] Migrate migrations

## Testing

- [ ] All unit tests pass
- [ ] Integration tests updated
- [ ] Manual smoke testing
- [ ] Performance comparison
- [ ] Memory usage comparison

## Deployment

- [ ] Update CI/CD pipelines
- [ ] Update Docker base images
- [ ] Review hosting requirements
- [ ] Update monitoring/APM tools
