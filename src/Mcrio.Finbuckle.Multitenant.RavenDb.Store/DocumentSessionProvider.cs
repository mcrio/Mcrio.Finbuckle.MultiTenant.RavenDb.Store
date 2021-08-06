using Raven.Client.Documents.Session;

namespace Mcrio.Finbuckle.MultiTenant.RavenDb.Store
{
    /// <summary>
    /// Provides the async document session.
    /// </summary>
    /// <returns>RavenDB async document session.</returns>
    public delegate IAsyncDocumentSession DocumentSessionProvider();
}