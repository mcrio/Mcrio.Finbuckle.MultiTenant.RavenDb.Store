using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Finbuckle.MultiTenant;
using Raven.Client.Documents.Session;

namespace Mcrio.Finbuckle.MultiTenant.RavenDb.Store.RavenDb
{
    /// <summary>
    /// Extension methods that handle document changes.
    /// </summary>
    internal static class DocumentPropertyChangeExtension
    {
        /// <summary>
        /// Checks whether a specific property has changed for given entity.
        /// </summary>
        /// <param name="documentSession">Document session.</param>
        /// <param name="entity">Entity we are checking the property change for.</param>
        /// <param name="changedPropertyName">Name of property we are checking the change for.</param>
        /// <param name="newPropertyValue">Expected new value for changed property.</param>
        /// <param name="propertyChange">Instance of <see cref="PropertyChange{T}"/> when property has changed, NULL otherwise.</param>
        /// <typeparam name="TTenantInfo">Type of entity we are checking the property change for.</typeparam>
        /// <returns>TRUE if property has changed, FALSE otherwise.</returns>
        internal static bool IfPropertyChanged<TTenantInfo>(
            this IAsyncDocumentSession documentSession,
            TTenantInfo entity,
            string changedPropertyName,
            string? newPropertyValue,
            out PropertyChange<string?>? propertyChange)
            where TTenantInfo : ITenantInfo
        {
            Debug.Assert(
                documentSession.Advanced.IsLoaded(entity.Id),
                "Expected the document to be loaded in the unit of work."
            );

            IDictionary<string, DocumentsChanges[]> whatChanged = documentSession.Advanced.WhatChanged();
            string entityId = entity.Id;

#pragma warning disable SA1011
            if (whatChanged.TryGetValue(entityId, out DocumentsChanges[]? documentChanges))
#pragma warning restore SA1011
            {
                DocumentsChanges? change = documentChanges?
                    .FirstOrDefault(changes =>
                        changes.Change == DocumentsChanges.ChangeType.FieldChanged
                        && changes.FieldName == changedPropertyName
                    );

                if (change != null)
                {
                    if (newPropertyValue != change.FieldNewValue?.ToString())
                    {
                        throw new InvalidOperationException(
                            $"User updated {changedPropertyName} property '{newPropertyValue}' should match change "
                            + $"trackers recorded new value '{change.FieldNewValue}'"
                        );
                    }

                    propertyChange = new PropertyChange<string?>(
                        oldPropertyValue: change.FieldOldValue.ToString(),
                        newPropertyValue: newPropertyValue
                    );
                    return true;
                }
            }

            propertyChange = null;
            return false;
        }
    }
}