using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Finbuckle.MultiTenant;
using FluentAssertions;
using Mcrio.Finbuckle.MultiTenant.RavenDb.Store.Model;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Mcrio.Finbuckle.MultiTenant.RavenDb.Store.Tests.Integration
{
    public class FinbuckleRavenDbStoreTest : RavenDbTestBase
    {
        [Fact]
        public async Task ShouldCreateTenantWithUniqueIdentifier()
        {
            {
                FinbuckleRavenDbStore<TenantInfo> store = CreateTenantStore();
                var tenantA = new TenantInfo
                {
                    Id = "id123",
                    Identifier = "tenant-a",
                    Name = "Tenant A",
                };

                (await store.TryAddAsync(tenantA)).Should().BeTrue();
                await AssertCompareExchangeKeyExistsWithValueAsync(
                    $"tidentifier/{tenantA.Identifier}",
                    tenantA.Id
                );
            }

            {
                FinbuckleRavenDbStore<TenantInfo> store = CreateTenantStore();
                var tenantB = new TenantInfo
                {
                    Id = "id987",
                    Identifier = "tenant-b",
                    Name = "Tenant B",
                };
                (await store.TryAddAsync(tenantB)).Should().BeTrue();
                await AssertCompareExchangeKeyExistsWithValueAsync(
                    $"tidentifier/{tenantB.Identifier}",
                    tenantB.Id
                );

                var tenantC = new TenantInfo
                {
                    Id = "id000",
                    Identifier = "tenant-c",
                    Name = "Tenant C",
                };
                (await store.TryAddAsync(tenantC)).Should().BeTrue();
                await AssertCompareExchangeKeyExistsWithValueAsync(
                    $"tidentifier/{tenantC.Identifier}",
                    tenantC.Id
                );
            }

            {
                FinbuckleRavenDbStore<TenantInfo> store = CreateTenantStore();
                IEnumerable<TenantInfo> tenants = await store.GetAllAsync();
                tenants.Count().Should().Be(3);
            }

            WaitForUserToContinueTheTest(DocumentStore);
        }

        [Fact]
        public async Task ShouldNotCreateTenantIfAnotherWithSameIdExists()
        {
            const string id = "123456789";

            {
                FinbuckleRavenDbStore<TenantInfo> store = CreateTenantStore();
                var tenantA = new TenantInfo
                {
                    Id = id,
                    Identifier = "tenant111",
                    Name = "Tenant 1111",
                };
                (await store.TryAddAsync(tenantA)).Should().BeTrue();
            }

            {
                FinbuckleRavenDbStore<TenantInfo> store = CreateTenantStore();
                var tenantA = new TenantInfo
                {
                    Id = id,
                    Identifier = "tenant999",
                    Name = "Tenant 9999",
                };
                (await store.TryAddAsync(tenantA)).Should().BeFalse();
            }
        }

        [Fact]
        public async Task ShouldNotCreateTenantIfAnotherWithSameIdentifierExists()
        {
            const string identifier = "identifier";

            {
                FinbuckleRavenDbStore<TenantInfo> store = CreateTenantStore();
                var tenantA = new TenantInfo
                {
                    Id = "11111",
                    Identifier = identifier,
                    Name = "Tenant 1111",
                };
                (await store.TryAddAsync(tenantA)).Should().BeTrue();
            }

            {
                FinbuckleRavenDbStore<TenantInfo> store = CreateTenantStore();
                var tenantA = new TenantInfo
                {
                    Id = "999999",
                    Identifier = identifier,
                    Name = "Tenant 9999",
                };
                (await store.TryAddAsync(tenantA)).Should().BeFalse();
            }
        }

        [Fact]
        public async Task ShouldUpdateTenantDataWhichAlsoHasNewIdentifier()
        {
            await Seed3TenantsAsync();

            {
                FinbuckleRavenDbStore<TenantInfo> store = CreateTenantStore();
                var tenantA = new TenantInfo
                {
                    Id = "abc",
                    Identifier = "tenant-abc",
                    Name = "Tenant ABC",
                };
                (await store.TryAddAsync(tenantA)).Should().BeTrue();
            }

            {
                FinbuckleRavenDbStore<TenantInfo> store = CreateTenantStore();
                TenantInfo tenant = await store.TryGetAsync("abc");
                tenant.Identifier.Should().Be("tenant-abc");
                tenant.Name.Should().Be("Tenant ABC");
                await AssertCompareExchangeKeyExistsWithValueAsync(
                    "tidentifier/tenant-abc",
                    tenant.Id
                );

                tenant.Identifier = "new-identifier";
                tenant.Name = "New Name";

                (await store.TryUpdateAsync(tenant)).Should().BeTrue();

                WaitForUserToContinueTheTest(DocumentStore);

                await AssertCompareExchangeKeyExistsWithValueAsync(
                    "tidentifier/new-identifier",
                    tenant.Id
                );
                await AssertCompareExchangeKeyDoesNotExistAsync(
                    "tidentifier/tenant-abc",
                    "old identifier was deleted"
                );
            }

            {
                FinbuckleRavenDbStore<TenantInfo> store = CreateTenantStore();
                TenantInfo tenant = await store.TryGetAsync("abc");
                tenant.Identifier.Should().Be("new-identifier");
                tenant.Name.Should().Be("New Name");
            }
        }

        [Fact]
        public async Task ShouldNotUpdateIfTenantModifiedInAnotherSession()
        {
            await Seed3TenantsAsync();

            {
                FinbuckleRavenDbStore<TenantInfo> storeA = CreateTenantStore();
                var tenantA = new TenantInfo
                {
                    Id = "abc",
                    Identifier = "tenant-abc",
                    Name = "Tenant ABC",
                };
                (await storeA.TryAddAsync(tenantA)).Should().BeTrue();
            }

            {
                FinbuckleRavenDbStore<TenantInfo> storeA = CreateTenantStore();

                TenantInfo tenantFromStoreA = await storeA.TryGetAsync("abc");
                tenantFromStoreA.Identifier = "new-identifier";
                tenantFromStoreA.Name = "New Name";

                FinbuckleRavenDbStore<TenantInfo> storeB = CreateTenantStore();
                TenantInfo tenantFromStoreB = await storeB.TryGetAsync("abc");
                tenantFromStoreB.Identifier = "new-identifier22222";
                tenantFromStoreB.Name = "New Name22222";

                // update from store B
                (await storeB.TryUpdateAsync(tenantFromStoreB)).Should().BeTrue();

                // try to update from store A
                (await storeA.TryUpdateAsync(tenantFromStoreA)).Should().BeFalse();

                await AssertCompareExchangeKeyDoesNotExistAsync(
                    "tidentifier/tenant-abc",
                    "was modified from storeB."
                );
                await AssertCompareExchangeKeyDoesNotExistAsync(
                    "tidentifier/new-identifier",
                    "modification from storeA failed due to concurrency."
                );
                await AssertCompareExchangeKeyExistsWithValueAsync(
                    "tidentifier/new-identifier22222",
                    tenantFromStoreA.Id,
                    "storeB successfully modified the tenant."
                );

                WaitForUserToContinueTheTest(DocumentStore);
            }
        }

        [Fact]
        public async Task ShouldNotUpdateTenantIfNewIdentifierIsNotUnique()
        {
            await Seed3TenantsAsync();

            {
                FinbuckleRavenDbStore<TenantInfo> storeA = CreateTenantStore();
                var tenantA = new TenantInfo
                {
                    Id = "abc",
                    Identifier = "tenant-abc",
                    Name = "Tenant ABC",
                };
                (await storeA.TryAddAsync(tenantA)).Should().BeTrue();
                var tenantB = new TenantInfo
                {
                    Id = "foobar",
                    Identifier = "tenant-foobar",
                    Name = "Tenant FooBar",
                };
                (await storeA.TryAddAsync(tenantB)).Should().BeTrue();
            }

            {
                FinbuckleRavenDbStore<TenantInfo> store = CreateTenantStore();
                TenantInfo tenant = await store.TryGetAsync("foobar");

                tenant.Identifier = "tenant-abc";
                tenant.Name = "New Name";

                (await store.TryUpdateAsync(tenant)).Should().BeFalse();

                WaitForUserToContinueTheTest(DocumentStore);

                await AssertCompareExchangeKeyExistsWithValueAsync(
                    "tidentifier/tenant-foobar",
                    "foobar"
                );
                await AssertCompareExchangeKeyExistsWithValueAsync(
                    "tidentifier/tenant-abc",
                    "abc"
                );
            }
        }

        [Fact]
        public async Task ShouldRemoveExistingTenant()
        {
            await Seed3TenantsAsync();

            {
                FinbuckleRavenDbStore<TenantInfo> storeA = CreateTenantStore();
                var tenantA = new TenantInfo
                {
                    Id = "abc",
                    Identifier = "tenant-abc",
                    Name = "Tenant ABC",
                };
                (await storeA.TryAddAsync(tenantA)).Should().BeTrue();
            }

            {
                FinbuckleRavenDbStore<TenantInfo> store = CreateTenantStore();
                (await store.TryRemoveAsync("abc")).Should().BeTrue();
                await AssertCompareExchangeKeyDoesNotExistAsync(
                    "tidentifier/tenant-abc",
                    "we removed the tenant"
                );
                (await store.TryGetAsync("abc")).Should().BeNull();
            }
        }

        [Fact]
        public async Task ShouldNotRemoveIfTenantDoesNotExist()
        {
            await Seed3TenantsAsync();

            FinbuckleRavenDbStore<TenantInfo> store = CreateTenantStore();
            (await store.TryRemoveAsync("some-non-existing-id")).Should().BeFalse();
            (await store.TryGetAsync("some-non-existing-id")).Should().BeNull();
        }

        [Fact]
        public async Task ShouldNotRemoveIfTenantModifiedInAnotherSession()
        {
            await Seed3TenantsAsync();

            {
                FinbuckleRavenDbStore<TenantInfo> storeA = CreateTenantStore();
                var tenantA = new TenantInfo
                {
                    Id = "abc",
                    Identifier = "tenant-abc",
                    Name = "Tenant ABC",
                };
                (await storeA.TryAddAsync(tenantA)).Should().BeTrue();
            }

            {
                FinbuckleRavenDbStore<TenantInfo> storeA = CreateTenantStore();

                // get tenant into storeA
                await storeA.TryGetAsync("abc");

                FinbuckleRavenDbStore<TenantInfo> storeB = CreateTenantStore();
                TenantInfo tenantFromStoreB = await storeB.TryGetAsync("abc");
                tenantFromStoreB.Identifier = "new-identifier22222";
                tenantFromStoreB.Name = "New Name22222";

                // update from store B
                (await storeB.TryUpdateAsync(tenantFromStoreB)).Should().BeTrue();

                // try to delete from store A
                (await storeA.TryRemoveAsync("abc")).Should().BeFalse();
                await AssertCompareExchangeKeyExistsWithValueAsync(
                    "tidentifier/new-identifier22222",
                    "abc",
                    "storeB updated the tenant"
                );
            }

            WaitForUserToContinueTheTest(DocumentStore);

            FinbuckleRavenDbStore<TenantInfo> storeC = CreateTenantStore();
            (await storeC.TryGetAsync("abc")).Should().NotBeNull();
        }

        [Fact]
        public async Task ShouldGetSingleTenantByIdentifier()
        {
            await Seed3TenantsAsync();

            FinbuckleRavenDbStore<TenantInfo> store = CreateTenantStore();

            TenantInfo tenant = await store.TryGetByIdentifierAsync("tenant3");
            tenant.Should().NotBeNull();
            tenant.Name.Should().Be("Tenant 3");
        }

        [Fact]
        public async Task ShouldNotGetTenantIfNotExistsByIdentifier()
        {
            await Seed3TenantsAsync();

            FinbuckleRavenDbStore<TenantInfo> store = CreateTenantStore();

            TenantInfo tenant = await store.TryGetByIdentifierAsync(Guid.NewGuid().ToString());
            tenant.Should().BeNull();
        }

        [Fact]
        public async Task ShouldGetTenantById()
        {
            await Seed3TenantsAsync();

            {
                var tenant = new TenantInfo
                {
                    Id = "id123",
                    Identifier = "tenant-a",
                    Name = "Tenant A",
                };
                (await CreateTenantStore().TryAddAsync(tenant)).Should().BeTrue();
            }

            FinbuckleRavenDbStore<TenantInfo> store = CreateTenantStore();
            TenantInfo fromDb = await store.TryGetAsync("id123");
            fromDb.Should().NotBeNull();
            fromDb.Name.Should().Be("Tenant A");
        }

        [Fact]
        public async Task ShouldNotGetTenantByIdIfNotExists()
        {
            await Seed3TenantsAsync();

            FinbuckleRavenDbStore<TenantInfo> store = CreateTenantStore();

            TenantInfo tenant = await store.TryGetAsync(Guid.NewGuid().ToString());
            tenant.Should().BeNull();
        }

        [Fact]
        public async Task ShouldGetAllTenants()
        {
            await Seed3TenantsAsync();

            FinbuckleRavenDbStore<TenantInfo> store = CreateTenantStore();

            IEnumerable<TenantInfo> tenants = (await store.GetAllAsync()).ToList();
            tenants.Count().Should().Be(3);
            tenants.Select(t => t.Id).Distinct().ToList().Count.Should().Be(3);
            tenants.Select(t => t.Identifier).Distinct().ToList().Count.Should().Be(3);

            tenants.SingleOrDefault(tenant => tenant.Identifier == "tenant1").Should().NotBeNull();
            tenants.SingleOrDefault(tenant => tenant.Identifier == "tenant2").Should().NotBeNull();
            tenants.SingleOrDefault(tenant => tenant.Identifier == "tenant3").Should().NotBeNull();
        }

        [Fact]
        public async Task ShouldGetAllTenantsWithPagination()
        {
            await Seed3TenantsAsync();
            FinbuckleRavenDbStore<TenantInfo> store = CreateTenantStore();

            {
                // per page is higher than tenants count so get all tenants
                PaginatedResult<TenantInfo> resultAll = await store.GetAllPaginatedAsync(1, 10);
                resultAll.TotalItemsCount.Should().Be(3);
                resultAll.Items.Count().Should().Be(3);
            }

            {
                // get sliced result
                PaginatedResult<TenantInfo> resultSliced = await store.GetAllPaginatedAsync(1, 2);
                resultSliced.TotalItemsCount.Should().Be(3);
                resultSliced.Items.Count().Should().Be(2);
            }

            {
                // if out of bounds have empty result
                PaginatedResult<TenantInfo> resultOutOfBounds = await store.GetAllPaginatedAsync(10, 2);
                resultOutOfBounds.TotalItemsCount.Should().Be(3);
                resultOutOfBounds.Items.Count().Should().Be(0);
            }
        }

        private FinbuckleRavenDbStore<TenantInfo> CreateTenantStore()
        {
            var logger = new Mock<ILogger<FinbuckleRavenDbStore<TenantInfo>>>();
            return new FinbuckleRavenDbStore<TenantInfo>(
                () => DocumentStore.OpenAsyncSession(),
                logger.Object
            );
        }

        private async Task Seed3TenantsAsync()
        {
            TenantInfo[] tenants =
            {
                new TenantInfo
                {
                    Id = "305kfkjg4f0",
                    Identifier = "tenant1",
                    Name = "Tenant 1",
                },
                new TenantInfo
                {
                    Id = "94rijgijdgkjo4",
                    Identifier = "tenant2",
                    Name = "Tenant 2",
                },
                new TenantInfo
                {
                    Id = "838hgh38dh8g",
                    Identifier = "tenant3",
                    Name = "Tenant 3",
                },
            };

            FinbuckleRavenDbStore<TenantInfo> store = CreateTenantStore();
            foreach (TenantInfo tenant in tenants)
            {
                await store.TryAddAsync(tenant);
            }
        }
    }
}