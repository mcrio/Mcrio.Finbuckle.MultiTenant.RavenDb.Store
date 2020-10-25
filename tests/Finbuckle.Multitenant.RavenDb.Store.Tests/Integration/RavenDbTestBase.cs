using System.Threading.Tasks;
using FluentAssertions;
using Raven.Client.Documents;
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