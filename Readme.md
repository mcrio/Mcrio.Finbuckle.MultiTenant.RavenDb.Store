<img src="https://github.com/mcrio/Mcrio.Finbuckle.MultiTenant.RavenDb.Store/raw/master/ravendb-logo.png" height="100px" alt="RavenDB" />
<img src="https://github.com/mcrio/Mcrio.Finbuckle.MultiTenant.RavenDb.Store/raw/master/finbuckle-multitenant-logo.png" height="130px" alt="Finbuckle Multitenant" />

# Finbuckle Multi-Tenant on RavenDB

[![Build status](https://dev.azure.com/midnight-creative/Mcrio.Finbuckle.MultiTenant.RavenDb.Store/_apis/build/status/Build)](https://dev.azure.com/midnight-creative/Mcrio.Finbuckle.MultiTenant.RavenDb.Store/_build/latest?definitionId=7)
![Nuget](https://img.shields.io/nuget/v/Mcrio.Finbuckle.MultiTenant.RavenDb.Store)

RavenDB implementation of the Finbuckle.MultiTenant store.

#### What is Finbuckle Multi-Tenant

Finbuckle.MultiTenant is open source multi-tenancy middleware for .NET.
For more information about Finbuckle.MultiTenant visit the [official website](https://www.finbuckle.com).

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
    // adds RavenDb store by providing Tenant info type and RavenDb store type.
    // both can be extended to suit your requirements
    .WithRavenDbStore<TenantInfo, FinbuckleRavenDbStore<TenantInfo>>(
        // define how IAsyncDocumentSession is resolved from DI
        // as library does NOT directly inject IAsyncDocumentSession
        provider => provider.GetRequiredService<IAsyncDocumentSession>().Session
    )
```

### Custom Tenant info data

Extend `Finbuckle.MultiTenant.TenantInfo` or `Finbuckle.MultiTenant.ITenantInfo` and provide types when 
adding Finbuckle.MultiTenant and the RavenDb store.

### Paginated results

`FinbuckleRavenDbStore` implements `Mcrio.Finbuckle.MultiTenant.RavenDb.Store.Interfaces.IHavePaginatedMultiTenantStore<TTenantInfo>`
which can be used to retrieve a paginated tenant list.  

### Compare Exchange key prefixes

Unique tenant identifiers are handled by the compare exchange. 
Compare exchange key prefixes can be modified by following:
- Extend `FinbuckleRavenDbStore` and override `protected virtual CompareExchangeUtility CreateCompareExchangeUtility()` to return
  an extended `CompareExchangeUtility` that will override the functionality for generating
  compare exchange key prefixes. See `CompareExchangeUtility.GetKeyPrefix` for predefined compare exchange key prefixes.

### ID generation

If using the provided `Finbuckle.MultiTenant.TenantInfo` class, `Id`  can be changed after object construction.
By default set to `null` which implies HiLo identifier generation.
Refer to official [RavenDB document](https://ravendb.net/docs/article-page/5.2/working-with-document-identifiers/client-api/document-identifiers/working-with-document-identifiers) about identifier generation strategies.


## Release History

- **1.0.0**
  Stable version.

## Meta

Nikola Josipović

This project is licensed under the MIT License. See [License.md](License.md) for more information.

## Do you like this library?

<img src="https://img.shields.io/badge/%E2%82%B3%20%2F%20ADA-Buy%20me%20a%20coffee%20or%20two%20%3A)-green" alt="₳ ADA | Buy me a coffee or two :)" /> <br /><small> addr1q87dhpq4wkm5gucymxkwcatu2et5enl9z8dal4c0fj98fxznraxyxtx5lf597gunnxn3tewwr6x2y588ttdkdlgaz79spp3avz </small><br />

<img src="https://img.shields.io/badge/%CE%9E%20%2F%20ETH-...a%20nice%20cold%20beer%20%3A)-yellowgreen" alt="Ξ ETH | ...a nice cold beer :)" /> <br /> <small> 0xae0B28c1fCb707e1908706aAd65156b61aC6Ff0A </small><br />

<img src="https://img.shields.io/badge/%E0%B8%BF%20%2F%20BTC-...or%20maybe%20a%20good%20read%20%3A)-yellow" alt="฿ BTC | ...or maybe a good read :)" /> <br /> <small> bc1q3s8qjx59f4wu7tvz7qj9qx8w6ktcje5ktseq68 </small><br />

<img src="https://img.shields.io/badge/ADA%20POOL-Happy if you %20stake%20%E2%82%B3%20with%20Pale%20Blue%20Dot%20%5BPBD%5D%20%3A)-8a8a8a" alt="Happy if you stake ADA with Pale Blue Dot [PBD]" /> <br /> <small> <a href="https://palebluedotpool.org">https://palebluedotpool.org</a> </small>
<br />&nbsp;