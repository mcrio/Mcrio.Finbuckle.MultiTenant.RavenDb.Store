using System.Threading.Tasks;
using FluentAssertions;
using Mcrio.Finbuckle.MultiTenant.RavenDb.Store.RavenDb;
using Raven.Client.Documents;
using Raven.Client.Documents.Conventions;
using Raven.Client.Documents.Operations.CompareExchange;
using Raven.TestDriver;

namespace Mcrio.Finbuckle.MultiTenant.RavenDb.Store.Tests.Integration
{
    public class RavenDbTestBase : RavenTestDriver
    {
        protected RavenDbTestBase()
        {
            DocumentStore = GetDocumentStore();
        }

        protected override void PreInitialize(IDocumentStore documentStore)
        {
            documentStore.Conventions.FindCollectionName = type =>
            {
                if (FinbuckleMultiTenantRavenDbConventions.TryGetCollectionName(
                        type,
                        out string? collectionName))
                {
                    return collectionName;
                }

                return DocumentConventions.DefaultGetCollectionName(type);
            };
            documentStore.Conventions.UseOptimisticConcurrency = true;
        }

        /// <summary>
        /// RavenDb document store.
        /// </summary>
        protected IDocumentStore DocumentStore { get; }

        protected async Task AssertCompareExchangeKeyExistsWithValueAsync<TValue>(
            string cmpExchangeKey,
            TValue value,
            string because = "")
        {
            CompareExchangeValue<TValue> result = await GetCompareExchangeAsync<TValue>(DocumentStore, cmpExchangeKey);
            result.Should().NotBeNull($"cmp exchange {cmpExchangeKey} should exist because {because}");
            result.Value.Should().Be(value);
        }

        protected async Task AssertCompareExchangeKeyDoesNotExistAsync(string cmpExchangeKey, string because = "")
        {
            CompareExchangeValue<string> result = await GetCompareExchangeAsync<string>(DocumentStore, cmpExchangeKey);
            result.Should().BeNull($"cmp exchange {cmpExchangeKey} should not exist because {because}");
        }

        protected async Task AssertReservationDocumentExistsWithValueAsync(
            UniqueReservationType uniqueReservationType,
            string expectedUniqueValue,
            string expectedReferenceDocumentId,
            string because = "")
        {
            var uniqueUtility = new UniqueReservationDocumentUtility(
                DocumentStore.OpenAsyncSession(),
                uniqueReservationType,
                expectedUniqueValue
            );
            bool exists = await uniqueUtility.CheckIfUniqueIsTakenAsync();
            exists.Should().BeTrue(because);

            UniqueReservation reservation = await uniqueUtility.LoadReservationAsync();
            reservation.Should().NotBeNull();
            reservation.ReferenceId.Should().Be(expectedReferenceDocumentId);
        }

        protected async Task AssertReservationDocumentDoesNotExistAsync(
            UniqueReservationType uniqueReservationType,
            string expectedUniqueValue,
            string because = "")
        {
            var uniqueUtility = new UniqueReservationDocumentUtility(
                DocumentStore.OpenAsyncSession(),
                uniqueReservationType,
                expectedUniqueValue
            );
            bool exists = await uniqueUtility.CheckIfUniqueIsTakenAsync();
            exists.Should().BeFalse(because);
        }

        private static Task<CompareExchangeValue<TValue>> GetCompareExchangeAsync<TValue>(
            IDocumentStore documentStore,
            string cmpExchangeKey)
        {
            return documentStore.Operations.SendAsync(
                new GetCompareExchangeValueOperation<TValue>(cmpExchangeKey)
            );
        }
    }
}