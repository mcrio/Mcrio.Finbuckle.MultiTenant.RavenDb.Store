using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Finbuckle.MultiTenant;
using Raven.Client.Documents.Session;

namespace Mcrio.Finbuckle.MultiTenant.RavenDb.Store.RavenDb
{
    /// <summary>
    /// Extension methods that handle unique value reservations for changed properties.
    /// </summary>
    internal static class StoreUniquePropertyChangeExtension
    {
        /// <summary>
        /// If there is a property change that requires uniqueness check, make a new compare exchange
        /// reservation or throw if unique value already exists.
        /// </summary>
        /// <param name="documentSession">Document session.</param>
        /// <param name="compareExchangeUtility">Compare exchange utility.</param>
        /// <param name="entity">Entity we are checking the property change for.</param>
        /// <param name="changedPropertyName">Name of property we are checking the change for.</param>
        /// <param name="newPropertyValue">Expected new value for changed property.</param>
        /// <param name="newCompareExchangeUniqueValue">New unique value we want to reserve.</param>
        /// <param name="cmpExchangeReservationType">Compare exchange reservation type.</param>
        /// <param name="data">Optional data to save into the compare exchange.</param>
        /// <typeparam name="TTenantInfo">Type of entity we are checking the property change for.</typeparam>
        /// <returns>Optional property change data if there was a property change and
        /// a successful new compare exchange reservation made.</returns>
        /// <exception cref="UniqueValueExistsException">If new unique value already exists.</exception>
        internal static async Task<PropertyChange<string>?> ReserveIfPropertyChangedAsync<TTenantInfo>(
            this IAsyncDocumentSession documentSession,
            CompareExchangeUtility compareExchangeUtility,
            TTenantInfo entity,
            string changedPropertyName,
            string newPropertyValue,
            string newCompareExchangeUniqueValue,
            CompareExchangeUtility.ReservationType cmpExchangeReservationType,
            string? data = null)
            where TTenantInfo : ITenantInfo
        {
            IDictionary<string, DocumentsChanges[]> whatChanged = documentSession.Advanced.WhatChanged();
            string entityId = entity.Id;

            if (whatChanged.ContainsKey(entityId))
            {
                DocumentsChanges? change = whatChanged[entityId]
                    .FirstOrDefault(changes =>
                        changes.Change == DocumentsChanges.ChangeType.FieldChanged
                        && changes.FieldName == changedPropertyName
                    );
                if (change != null)
                {
                    if (newPropertyValue != change.FieldNewValue.ToString())
                    {
                        throw new InvalidOperationException(
                            $"User updated {changedPropertyName} property '{newPropertyValue}' should match change "
                            + $"trackers recorded new value '{change.FieldNewValue}'"
                        );
                    }

                    bool reserved = await compareExchangeUtility
                        .CreateReservationAsync<string?, TTenantInfo>(
                            cmpExchangeReservationType,
                            entity,
                            newCompareExchangeUniqueValue,
                            data
                        ).ConfigureAwait(false);
                    if (!reserved)
                    {
                        throw new UniqueValueExistsException(
                            $"Compare exchange unique value {newCompareExchangeUniqueValue} already exists."
                        );
                    }

                    return new PropertyChange<string>(
                        change.FieldOldValue.ToString()!,
                        newPropertyValue
                    );
                }
            }

            return null;
        }
    }
}