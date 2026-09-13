using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using GenjitsuLAB.Core;
using UnityEngine;

namespace GenjitsuLAB.Data.Editor
{
    /// <summary>Maps decoded CSV columns to supported Unity serialized fields.</summary>
    public static class DatabaseCSV
    {
        private static readonly Dictionary<Type, FieldInfo[]> s_fields = new Dictionary<Type, FieldInfo[]>();

        /// <summary>Gets inherited serialized fields in deterministic base-to-derived order.</summary>
        public static FieldInfo[] GetFields(Type type)
        {
            if (s_fields.TryGetValue(type, out FieldInfo[] cached))
            {
                return cached;
            }
            List<Type> hierarchy = new List<Type>();
            for (Type current = type; current != null && current != typeof(ScriptableObject); current = current.BaseType)
            {
                hierarchy.Add(current);
            }
            List<FieldInfo> fields = new List<FieldInfo>();
            for (int i = hierarchy.Count - 1; i >= 0; i--)
            {
                FieldInfo[] declared = hierarchy[i].GetFields(BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                for (int j = 0; j < declared.Length; j++)
                {
                    FieldInfo field = declared[j];
                    if (!field.IsStatic && !field.IsInitOnly && !field.IsNotSerialized &&
                        (field.IsPublic || field.IsDefined(typeof(SerializeField), true)))
                    {
                        fields.Add(field);
                    }
                }
            }
            cached = fields.ToArray();
            s_fields.Add(type, cached);
            return cached;
        }

        /// <summary>Tests whether a field is owned by Unity rather than CSV.</summary>
        public static bool IsObjectField(Type type)
        {
            return typeof(UnityEngine.Object).IsAssignableFrom(type) ||
                (type.IsArray && typeof(UnityEngine.Object).IsAssignableFrom(type.GetElementType()));
        }

        /// <summary>Tests supported scalar or one-dimensional array types.</summary>
        public static bool IsSupported(Type type)
        {
            if (type.IsArray)
            {
                return type.GetArrayRank() == 1 && !type.GetElementType().IsArray && IsSupported(type.GetElementType());
            }
            return type == typeof(string) || type == typeof(bool) || type == typeof(byte) ||
                type == typeof(sbyte) || type == typeof(short) || type == typeof(ushort) ||
                type == typeof(int) || type == typeof(uint) || type == typeof(long) ||
                type == typeof(ulong) || type == typeof(float) || type == typeof(double) ||
                type.IsEnum || type == typeof(Vector2) || type == typeof(Vector3) || type == typeof(Vector4) ||
                (typeof(DataReference).IsAssignableFrom(type) && !type.IsAbstract && type.GetConstructor(Type.EmptyTypes) != null);
        }

        /// <summary>Converts one already-unescaped cell to its authored type.</summary>
        public static object ParseCell(Type type, string text)
        {
            if (!IsSupported(type))
            {
                throw new FormatException("Unsupported CSV type: " + type.Name);
            }
            if (type.IsArray)
            {
                string[] parts = text.Length == 0 ? Array.Empty<string>() : text.Split(CSVUtils.ARRAY_SEPARATOR);
                Type element = type.GetElementType();
                Array values = Array.CreateInstance(element, parts.Length);
                for (int i = 0; i < parts.Length; i++)
                {
                    values.SetValue(ParseCell(element, parts[i]), i);
                }
                return values;
            }
            if (typeof(DataReference).IsAssignableFrom(type))
            {
                DataReference reference = (DataReference)Activator.CreateInstance(type);
                reference.FromCSV(text);
                return reference;
            }
            object result = CSVUtils.Parse(type, text);
            if (result is float single && (float.IsNaN(single) || float.IsInfinity(single)) ||
                result is double real && (double.IsNaN(real) || double.IsInfinity(real)))
            {
                throw new FormatException("Non-finite numeric value.");
            }
            return result;
        }

        /// <summary>Exports only CSV-owned fields; asset references stay in Unity.</summary>
        public static string ToCSV<T>(T[] records) where T : Data
        {
            return ToCSV(typeof(T), records);
        }

        /// <summary>Exports a repository with header-only output for empty assets.</summary>
        public static string ToCSV(Repository repository)
        {
            Data[] records = new Data[repository.Count];
            for (int i = 0; i < records.Length; i++)
            {
                records[i] = repository.GetRecord(i);
            }
            return ToCSV(repository.DataType, records);
        }

        private static string ToCSV(Type type, IReadOnlyList<Data> records)
        {
            List<FieldInfo> fields = new List<FieldInfo>();
            foreach (FieldInfo field in GetFields(type))
            {
                if (IsSupported(field.FieldType))
                {
                    fields.Add(field);
                }
            }
            StringBuilder output = new StringBuilder();
            for (int i = 0; i < fields.Count; i++)
            {
                if (i > 0)
                {
                    output.Append(',');
                }
                output.Append(CSVUtils.Escape(fields[i].Name));
            }
            output.Append(CSVUtils.NEW_LINE);
            for (int row = 0; row < records.Count; row++)
            {
                for (int col = 0; col < fields.Count; col++)
                {
                    if (col > 0)
                    {
                        output.Append(',');
                    }
                    output.Append(CSVUtils.Escape(FormatCell(fields[col].GetValue(records[row]), fields[col].FieldType)));
                }
                output.Append(CSVUtils.NEW_LINE);
            }
            return output.ToString();
        }

        private static string FormatCell(object value, Type type)
        {
            if (value == null)
            {
                return string.Empty;
            }
            if (type.IsArray)
            {
                Array array = (Array)value;
                StringBuilder output = new StringBuilder();
                for (int i = 0; i < array.Length; i++)
                {
                    string element = FormatCell(array.GetValue(i), type.GetElementType());
                    if (element.Contains(";") || (array.Length == 1 && element.Length == 0))
                    {
                        throw new FormatException("Array element cannot round-trip with the semicolon format.");
                    }
                    if (i > 0)
                    {
                        output.Append(';');
                    }
                    output.Append(element);
                }
                return output.ToString();
            }
            if (value is DataReference reference)
            {
                return reference.GetKey();
            }
            if (value is Vector2 v2)
            {
                return v2.x.ToString("R", CultureInfo.InvariantCulture) + "," + v2.y.ToString("R", CultureInfo.InvariantCulture);
            }
            if (value is Vector3 v3)
            {
                return v3.x.ToString("R", CultureInfo.InvariantCulture) + "," + v3.y.ToString("R", CultureInfo.InvariantCulture) + "," + v3.z.ToString("R", CultureInfo.InvariantCulture);
            }
            if (value is Vector4 v4)
            {
                return v4.x.ToString("R", CultureInfo.InvariantCulture) + "," + v4.y.ToString("R", CultureInfo.InvariantCulture) + "," + v4.z.ToString("R", CultureInfo.InvariantCulture) + "," + v4.w.ToString("R", CultureInfo.InvariantCulture);
            }
            return value is IFormattable formatted ? formatted.ToString(null, CultureInfo.InvariantCulture) : value.ToString();
        }
    }
}
