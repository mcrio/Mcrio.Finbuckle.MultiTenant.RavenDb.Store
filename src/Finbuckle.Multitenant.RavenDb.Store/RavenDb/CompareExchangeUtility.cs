using System;
using System.Threading.Tasks;
using Finbuckle.MultiTenant;
using Raven.Client.Documents;
using Raven.Client.Documents.Operations.CompareExchange;
using Raven.Client.Documents.Session;

namespace Mcrio.Finbuckle.MultiTenant.RavenDb.Store.RavenDb
{
    /// <summary>
    /// Helper class which provides methods to handle RavenDb compare exchange functionality.
    /// </summary>
    public class CompareExchangeUtility
    {
        private readonly IAsyncDocumentSession _documentSession;

        /// <summary>
        /// Initializes a new instance of the <see cref="CompareExchangeUtility"/> class.
        /// </summary>
        /// <param name="documentSession"></param>
        public CompareExchangeUtility(IAsyncDocumentSession documentSession)
        {
            _documentSession = documentSession;
        }

        /// <summary>
        /// Represents different compare exchange reservation types.
        /// </summary>
        public enum ReservationType
        {
            /// <summary>
            /// Tenant identifier reservation type.
            /// </summary>
            Identifier,
        }

        /// <summary>
        /// Creates a compare exchange reservation.
        /// </summary>
        /// <param name="reservationType">Reservation type.</param>
        /// <param name="entity">Entity we are making the reservation for.</param>
        /// <param name="expectedUniqueValue">Compare exchange unique value requested for given reservation type.</param>
        /// <param name="data">Custom data to be stored.</param>
        /// <typeparam name="TValue">Type of data to be stored.</typeparam>
        /// <typeparam name="TTenantInfo">Type of entity we are storing the unique value for.</typeparam>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        internal Task<bool> CreateReservationAsync<TValue, TTenantInfo>(
            ReservationType reservationType,
            TTenantInfo entity,
            string expectedUniqueValue,
            TValue data = default)
            where TTenantInfo : ITenantInfo
        {
            return CreateReservationAsync(
                CreateCompareExchangeKey(reservationType, entity, expectedUniqueValue),
                data
            );
        }

        /// <summary>
        /// Removes an existing compare exchange reservation.
        /// </summary>
        /// <param name="reservationType">Reservation type.</param>
        /// <param name="entity">Entity we are making the reservation for.</param>
        /// <param name="expectedUniqueValue">Unique value requested for given reservation type.</param>
        /// <typeparam name="TTenantInfo">Type of entity we are storing the unique value for.</typeparam>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        internal Task<bool> RemoveReservationAsync<TTenantInfo>(
            ReservationType reservationType,
            TTenantInfo entity,
            string expectedUniqueValue)
            where TTenantInfo : ITenantInfo
        {
            return RemoveReservationAsync(
                CreateCompareExchangeKey(reservationType, entity, expectedUniqueValue)
            );
        }

        /// <summary>
        /// Retrieves an existing compare exchange value if it exists.
        /// </summary>
        /// <param name="reservationType">Reservation type.</param>
        /// <param name="entity">Optional entity we are getting the reservation for.</param>
        /// <param name="expectedUniqueValue">Unique value for the given reservation type.</param>
        /// <typeparam name="TValue">Type of compare exchange data.</typeparam>
        /// <typeparam name="TTenantInfo">Type of entity we are storing the unique value for.</typeparam>
        /// <returns>The <see cref="Task"/> that represents the asynchronous operation.</returns>
        internal Task<CompareExchangeValue<TValue>?> GetReservationAsync<TValue, TTenantInfo>(
            ReservationType reservationType,
            TTenantInfo entity,
            string expectedUniqueValue)
            where TTenantInfo : ITenantInfo?
        {
            return GetReservationAsync<TValue>(
                CreateCompareExchangeKey(reservationType, entity, expectedUniqueValue)
            );
        }

        /// <summary>
        /// Gets the compare exchange reservation key prefix for the given reservation type.
        /// </summary>
        /// <param name="reservationType"></param>
        /// <returns>The compare exchange key prefix for the given reservation type.</returns>
        protected virtual string GetKeyPrefix(ReservationType reservationType)
        {
            return reservationType switch
            {
                ReservationType.Identifier => "tnt-identifier",
                _ => throw new Exception($"Unhandled reservation type {reservationType}")
            };
        }

        /// <summary>
        /// Creates the compare exchange key for th given reservation type, entity and unique value.
        /// </summary>
        /// <param name="reservationType">Type of reservation.</param>
        /// <param name="entity">Optional entity related to the reservation.
        /// Be aware it is optional and may not be available always, like in scenarios when looking up compare
        /// exchange key without knowing the entity upfront.</param>
        /// <param name="expectedUniqueValue">The unique value.</param>
        /// <typeparam name="TTenantInfo">Type of entity we are making the reservation for.</typeparam>
        /// <returns>The complete compare exchange key.</returns>
        protected virtual string CreateCompareExchangeKey<TTenantInfo>(
            ReservationType reservationType,
            TTenantInfo entity,
            string expectedUniqueValue)
            where TTenantInfo : ITenantInfo?
        {
            return GetKeyPrefix(reservationType).TrimEnd('/') + '/' + expectedUniqueValue;
        }

        private async Task<bool> CreateReservationAsync<TValue>(
            string cmpExchangeKey,
            TValue data = default)
        {
            IDocumentStore documentStore = _documentSession.Advanced.DocumentStore;

            CompareExchangeResult<TValue> compareExchangeResult = await documentStore.Operations.SendAsync(
                new PutCompareExchangeValueOperation<TValue>(cmpExchangeKey, data, 0)
            ).ConfigureAwait(false);

            return compareExchangeResult.Successful;
        }

        private Task<CompareExchangeValue<TValue>?> GetReservationAsync<TValue>(string cmpExchangeKey)
        {
            IDocumentStore store = _documentSession.Advanced.DocumentStore;
            return store.Operations.SendAsync(
                new GetCompareExchangeValueOperation<TValue>(cmpExchangeKey)
            );
        }

        private async Task<bool> RemoveReservationAsync(string cmpExchangeKey)
        {
            IDocumentStore documentStore = _documentSession.Advanced.DocumentStore;

            // get existing in order to get the index
            CompareExchangeValue<string>? existingResult = await GetReservationAsync<string>(
                cmpExchangeKey
            ).ConfigureAwait(false);

            if (existingResult is null)
            {
                // it does not exist so return positive result
                return true;
            }

            CompareExchangeResult<string> compareExchangeResult = await documentStore.Operations.SendAsync(
                new DeleteCompareExchangeValueOperation<string>(cmpExchangeKey, existingResult.Index)
            ).ConfigureAwait(false);

            return compareExchangeResult.Successful;
        }
    }
}