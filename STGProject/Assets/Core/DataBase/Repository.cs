using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace GenjitsuLAB.Data
{
    /// <summary>Serialized database asset; authoring belongs to the Editor module.</summary>
    public abstract class Repository : ScriptableObject
    {
        [SerializeField, FormerlySerializedAs("m_key")] private string m_spreadsheetId = string.Empty;
        [SerializeField, FormerlySerializedAs("m_gid")] private string m_sheetGid = "0";
        /// <summary>Public spreadsheet identifier, not a credential.</summary>
        public string SpreadsheetId => m_spreadsheetId;
        /// <summary>Worksheet numeric identifier.</summary>
        public string SheetGid => m_sheetGid;
        /// <summary>Concrete record type.</summary>
        public abstract Type DataType
        {
            get;
        }
        /// <summary>Number of serialized records.</summary>
        public abstract int Count
        {
            get;
        }
        /// <summary>Returns a record without copying the collection.</summary>
        public abstract Data GetRecord(int index);
        internal abstract IDatabase CreateIndex(IReadOnlyList<Repository> repositories);
    }

    /// <summary>Typed asset container whose records are sub-assets.</summary>
    public abstract class Repository<T> : Repository where T : Data
    {
        [SerializeField, HideInInspector] private List<T> m_datas = new List<T>();
        /// <inheritdoc/>
        public override Type DataType => typeof(T);
        /// <inheritdoc/>
        public override int Count => m_datas.Count;
        /// <inheritdoc/>
        public override Data GetRecord(int index)
        {
            return m_datas[index];
        }
        /// <summary>Serialized records without an array copy.</summary>
        public IReadOnlyList<T> Records => m_datas;
        /// <summary>Returns a snapshot for tools requiring an array.</summary>
        public T[] GetDatas()
        {
            return m_datas.ToArray();
        }

        internal override IDatabase CreateIndex(IReadOnlyList<Repository> repositories)
        {
            List<T> records = new List<T>();
            for (int i = 0; i < repositories.Count; i++)
            {
                if (repositories[i] is Repository<T> typed)
                {
                    records.AddRange(typed.m_datas);
                }
            }
            return new DatabaseRepository<T>(records);
        }
    }
}
