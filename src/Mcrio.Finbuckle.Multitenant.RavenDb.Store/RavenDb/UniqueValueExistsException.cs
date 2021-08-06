using System;

namespace Mcrio.Finbuckle.MultiTenant.RavenDb.Store.RavenDb
{
    internal class UniqueValueExistsException : Exception
    {
        internal UniqueValueExistsException(string message)
            : base(message)
        {
        }
    }
}