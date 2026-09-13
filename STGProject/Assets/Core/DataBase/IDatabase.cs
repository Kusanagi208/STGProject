using System;
namespace GenjitsuLAB.Data
{
    /// <summary>Read-only runtime view of a concrete record type.</summary>
    public interface IDatabase
    {
        /// <summary>Concrete record type.</summary>
        Type DataType
        {
            get;
        }
        /// <summary>Number of indexed records.</summary>
        int Count
        {
            get;
        }
        /// <summary>Returns a record or null without allocating.</summary>
        Data GetData(string key);
    }
}
