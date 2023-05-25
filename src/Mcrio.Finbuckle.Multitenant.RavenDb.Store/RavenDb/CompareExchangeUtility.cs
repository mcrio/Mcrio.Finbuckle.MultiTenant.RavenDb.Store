using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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
        /// Creates the compare exchange key for th given reservation type, entity and unique value.
        /// </summary>
        /// <param name="reservationType">Type of reservation.</param>
        /// <param name="expectedUniqueValue">The unique value.</param>
        /// <returns>The complete compare exchange key.</returns>
        public virtual string CreateCompareExchangeKey(
            UniqueReservationType reservationType,
            string expectedUniqueValue)
        {
            var prefix = reservationType switch
            {
                // ReSharper disable once StringLiteralTypo
                UniqueReservationType.Identifier => "tidentifier",
                _ => throw new Exception($"Unhandled reservation type {reservationType}")
            };
            return $"{prefix.TrimEnd('/')}/{expectedUniqueValue}";
        }

        /// <summary>
        /// Prepare reservation deletion as part of a cluster wide transaction.
        /// </summary>
        /// <param name="uniqueReservationType"></param>
        /// <param name="uniqueValueNormalized"></param>
        /// <param name="logger"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>Instance of <see cref="Task"/>.</returns>
        internal async Task PrepareReservationForRemovalAsync(
            UniqueReservationType uniqueReservationType,
            string uniqueValueNormalized,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            Debug.Assert(
                ((AsyncDocumentSession)_documentSession).TransactionMode == TransactionMode.ClusterWide,
                "Expected cluster wide transaction mode"
            );

            if (string.IsNullOrWhiteSpace(uniqueValueNormalized))
            {
                throw new ArgumentException(
                    $"Unexpected empty value for {nameof(uniqueValueNormalized)} in {nameof(PrepareReservationForRemovalAsync)}"
                );
            }

            string compareExchangeKey = CreateCompareExchangeKey(
                uniqueReservationType,
                uniqueValueNormalized
            );
            CompareExchangeValue<string>? compareExchange = await _documentSession
                .Advanced
                .ClusterTransaction
                .GetCompareExchangeValueAsync<string>(compareExchangeKey, cancellationToken)
                .ConfigureAwait(false);
            if (compareExchange != null)
            {
                _documentSession.Advanced.ClusterTransaction.DeleteCompareExchangeValue(
                    compareExchange
                );
            }
            else
            {
                logger.LogWarning(
                    "On {} old reservation delete, unexpectedly missing compare exchange reservation for value {}",
                    uniqueReservationType.ToString(),
                    uniqueValueNormalized
                );
            }
        }

        /// <summary>
        /// Prepare reservation update by checking if provided new value already exists, and if it doesn't
        /// mark old reservation for deletion and add new reservation.
        /// </summary>
        /// <param name="uniqueReservationType"></param>
        /// <param name="newUniqueValueNormalized"></param>
        /// <param name="oldUniqueValueNormalized">Old unique value reservation. If NULL we assume there was no previous reservation.</param>
        /// <param name="compareExchangeValue">Value that will be assigned to the compare exchange.</param>
        /// <param name="logger"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>TRUE if success, FALSE otherwise.</returns>
        internal async Task<bool> PrepareReservationUpdateInUnitOfWorkAsync(
            UniqueReservationType uniqueReservationType,
            string newUniqueValueNormalized,
            string? oldUniqueValueNormalized,
            string compareExchangeValue,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(newUniqueValueNormalized))
            {
                throw new ArgumentException(
                    $"Unexpected empty value for {nameof(newUniqueValueNormalized)} in {nameof(PrepareReservationUpdateInUnitOfWorkAsync)}");
            }

            Debug.Assert(
                ((AsyncDocumentSession)_documentSession).TransactionMode == TransactionMode.ClusterWide,
                "Expected cluster wide transaction mode"
            );

            // check if new value is unique
            string newValueCompareExchangeKey = CreateCompareExchangeKey(
                uniqueReservationType,
                newUniqueValueNormalized
            );
            CompareExchangeValue<string>? existingCompareExchangeWithNewValue = await _documentSession
                .Advanced
                .ClusterTransaction
                .GetCompareExchangeValueAsync<string>(newValueCompareExchangeKey, cancellationToken)
                .ConfigureAwait(false);

            if (existingCompareExchangeWithNewValue != null)
            {
                logger.LogInformation(
                    "Failed reserving {} {} as already exists",
                    uniqueReservationType.ToString(),
                    newUniqueValueNormalized
                );
                return false;
            }

            if (oldUniqueValueNormalized != null)
            {
                await PrepareReservationForRemovalAsync(
                    uniqueReservationType,
                    oldUniqueValueNormalized,
                    logger,
                    cancellationToken
                ).ConfigureAwait(false);
            }

            // prepare new reservation
            _documentSession
                .Advanced
                .ClusterTransaction
                .CreateCompareExchangeValue(
                    newValueCompareExchangeKey,
                    compareExchangeValue
                );

            return true;
        }
    }
}