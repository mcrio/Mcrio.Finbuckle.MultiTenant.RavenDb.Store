<img src="https://github.com/mcrio/Mcrio.Finbuckle.MultiTenant.RavenDb.Store/raw/master/ravendb-logo.png" height="100px" alt="RavenDB" />
<img src="https://github.com/mcrio/Mcrio.Finbuckle.MultiTenant.RavenDb.Store/raw/master/finbuckle-multitenant-logo.png" height="130px" alt="Finbuckle Multitenant" />

# Finbuckle Multi-Tenant on RavenDB

[![Build status](https://dev.azure.com/midnight-creative/Mcrio.Finbuckle.MultiTenant.RavenDb.Store/_apis/build/status/Build)](https://dev.azure.com/midnight-creative/Mcrio.Finbuckle.MultiTenant.RavenDb.Store/_build/latest?definitionId=7)
![Nuget](https://img.shields.io/nuget/v/Mcrio.Finbuckle.MultiTenant.RavenDb.Store)

RavenDB implementations of the Finbuckle.MultiTenant store.
For more information about Finbuckle visit the [official website](https://www.finbuckle.com).

#### What is Finbuckle Multi-Tenant

Finbuckle.MultiTenant is open source multi-tenancy middleware for .NET.

## Getting Started

### NuGet Package

Using the NuGet package manager install the [Mcrio.Finbuckle.Multitenant.RavenDb.Store](https://www.nuget.org/packages/Mcrio.Finbuckle.MultiTenant.RavenDb.Store) package, or add the following line to the .csproj file:

```xml
<ItemGroup>
    <PackageReference Include="Mcrio.Finbuckle.Multitenant.RavenDb.Store"></PackageReference>
</ItemGroup>
```

## Usage

Add the following lines to Startup.cs.
```c# 
// ConfigureServices(...)
services
    // adds finbuckle support as per official documentation
    .AddMultiTenant<TenantInfo>()
    // adds RavenDb stores by providing Tenant info type and RavenDb store type.
    // both can be extended to suit your requirements
    .WithRavenDbStore<TenantInfo, FinbuckleRavenDbStore<TenantInfo>>(
        // define how IAsyncDocumentSession is resolved from DI
        // as library does NOT directly inject IAsyncDocumentSession
        provider => provider.GetRequiredService<IAsyncDocumentSession>().Session
    )
```

### Custom Tenant info data

Extend `Finbuckle.MultiTenant.TenantInfo` or `Finbuckle.MultiTenant.ITenantInfo` and use when 
registering FinbuckleMultiTenant and the RavenDb stores.

### Paginated results

`FinbuckleRavenDbStore` implements `Mcrio.Finbuckle.MultiTenant.RavenDb.Store.Interfaces.IHavePaginatedMultiTenantStore<TTenantInfo>`
which can be used to retrieve a paginated tenant list.  

### Compare Exchange key prefixes

Unique tenant identifiers are handled by the compare exchange. 
Compare exchange key prefixes can be modified by following:
- Extend `FinbuckleRavenDbStore` and override `protected virtual CompareExchangeUtility CreateCompareExchangeUtility()` to return
  an extended `CompareExchangeUtility` that will override the functionality for generating
  compare exchange key prefixes. See `CompareExchangeUtility.GetKeyPrefix` for predefined compare exchange key prefixes.

## Release History

- **1.0.0**
  Stable version.

## Meta

Nikola Josipović

This project is licensed under the MIT License. See [License.md](License.md) for more information.