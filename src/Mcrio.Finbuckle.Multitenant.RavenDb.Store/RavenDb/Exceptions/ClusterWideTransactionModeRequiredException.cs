using System;

namespace Mcrio.Finbuckle.MultiTenant.RavenDb.Store.RavenDb.Exceptions
{
    /// <summary>
    /// RavenDB cluster wide transaction mode required.
    /// </summary>
    public sealed class ClusterWideTransactionModeRequiredException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ClusterWideTransactionModeRequiredException"/> class.
        /// </summary>
        public ClusterWideTransactionModeRequiredException()
            : base("Ravendb Cluster-wide transaction mode required.")
        {
        }
    }
}