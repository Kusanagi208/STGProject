using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace GenjitsuLAB.Core
{
    /// <summary>CSV parsing and invariant scalar conversion for authoring tools.</summary>
    public class CSVUtils
    {
        /// <summary>CSV export line ending.</summary>
        public const string NEW_LINE = "\r\n";
        /// <summary>Column delimiter.</summary>
        public const char SEPARATOR = ',';
        /// <summary>One-dimensional array delimiter inside a cell.</summary>
        public const char ARRAY_SEPARATOR = ';';

        /// <summary>A parsed table; each row is decoded exactly once.</summary>
        public sealed class Table
        {
            /// <summary>Header cells.</summary>
            public string[] Fields
            {
                get;
            }
            /// <summary>Data records, preserving empty cells.</summary>
            public IReadOnlyList<string[]> Rows
            {
                get;
            }
            /// <summary>Physical source line of each data record.</summary>
            public IReadOnlyList<int> Lines
            {
                get;
            }
            internal Table(string[] fields, List<string[]> rows, List<int> lines)
            {
                Fields = fields;
                Rows = rows;
                Lines = lines;
            }
        }

        /// <summary>Parses RFC-style quoted fields, CR/LF and embedded newlines.</summary>
        public static Table ParseTable(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }
            List<string[]> records = new List<string[]>();
            List<int> lines = new List<int>();
            List<string> cells = new List<string>();
            StringBuilder cell = new StringBuilder();
            bool quoted = false;
            bool closed = false;
            bool touched = false;
            int line = 1;
            int rowLine = 1;
            int start = text.Length > 0 && text[0] == '\uFEFF' ? 1 : 0;
            for (int i = start; i < text.Length; i++)
            {
                char ch = text[i];
                if (quoted)
                {
                    if (ch == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            cell.Append('"');
                            i++;
                        }
                        else
                        {
                            quoted = false;
                            closed = true;
                        }
                    }
                    else
                    {
                        cell.Append(ch);
                        if (ch == '\n' || (ch == '\r' && (i + 1 >= text.Length || text[i + 1] != '\n')))
                        {
                            line++;
                        }
                    }
                    continue;
                }
                if (ch == ',' || ch == '\r' || ch == '\n')
                {
                    cells.Add(cell.ToString());
                    cell.Clear();
                    closed = false;
                    if (ch == ',')
                    {
                        touched = true;
                        continue;
                    }
                    if (touched || cells.Count > 1 || cells[0].Length > 0)
                    {
                        records.Add(cells.ToArray());
                        lines.Add(rowLine);
                    }
                    cells.Clear();
                    touched = false;
                    if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    {
                        i++;
                    }
                    line++;
                    rowLine = line;
                    continue;
                }
                if (closed)
                {
                    throw new FormatException("Unexpected character after closing quote at line " + line);
                }
                if (ch == '"')
                {
                    if (cell.Length != 0)
                    {
                        throw new FormatException("Quote inside unquoted cell at line " + line);
                    }
                    quoted = true;
                }
                else
                {
                    cell.Append(ch);
                }
                touched = true;
            }
            if (quoted)
            {
                throw new FormatException("Unclosed quoted cell at line " + rowLine);
            }
            if (touched || cells.Count > 0 || cell.Length > 0)
            {
                cells.Add(cell.ToString());
                records.Add(cells.ToArray());
                lines.Add(rowLine);
            }
            if (records.Count == 0)
            {
                throw new FormatException("CSV requires a header.");
            }
            string[] fields = records[0];
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < fields.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(fields[i]) || !names.Add(fields[i]))
                {
                    throw new FormatException("Empty or duplicate CSV column: " + fields[i]);
                }
            }
            records.RemoveAt(0);
            lines.RemoveAt(0);
            for (int i = 0; i < records.Count; i++)
            {
                if (records[i].Length != fields.Length)
                {
                    throw new FormatException("Column count mismatch at line " + lines[i]);
                }
            }
            return new Table(fields, records, lines);
        }

        /// <summary>Returns decoded header names.</summary>
        public static string[] GetFieldNames(string text)
        {
            return ParseTable(text).Fields;
        }
        /// <summary>Returns the data row count (legacy method name).</summary>
        public static int GetColumnLength(string text)
        {
            return ParseTable(text).Rows.Count;
        }
        /// <summary>Legacy cell conversion; batch importers should use ParseTable once.</summary>
        public static object Parse(Type fieldType, string csv, int col, string fieldName)
        {
            return Parse(fieldType, GetFieldValue(csv, col, fieldName));
        }
        /// <summary>Converts a scalar using invariant culture. Invalid numeric values throw.</summary>
        public static object Parse(Type fieldType, string value)
        {
            if (fieldType == typeof(string))
            {
                return value;
            }
            if (fieldType == typeof(Vector2) || fieldType == typeof(Vector3) || fieldType == typeof(Vector4))
            {
                string[] parts = value.Trim().Trim('"').Trim('(', ')').Split(',');
                int count = fieldType == typeof(Vector2) ? 2 : fieldType == typeof(Vector3) ? 3 : 4;
                if (parts.Length != count)
                {
                    throw new FormatException("Vector requires " + count + " components.");
                }
                float x = float.Parse(parts[0], CultureInfo.InvariantCulture);
                float y = float.Parse(parts[1], CultureInfo.InvariantCulture);
                if (count == 2)
                {
                    return new Vector2(x, y);
                }
                float z = float.Parse(parts[2], CultureInfo.InvariantCulture);
                if (count == 3)
                {
                    return new Vector3(x, y, z);
                }
                return new Vector4(x, y, z, float.Parse(parts[3], CultureInfo.InvariantCulture));
            }
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new FormatException("Empty " + fieldType.Name + " value.");
            }
            if (fieldType.IsEnum)
            {
                object result = Enum.Parse(fieldType, value, false);
                if (!Enum.IsDefined(fieldType, result))
                {
                    throw new FormatException("Undefined " + fieldType.Name + " value: " + value);
                }
                return result;
            }
            TypeConverter converter = TypeDescriptor.GetConverter(fieldType);
            return converter.ConvertFromInvariantString(value);
        }
        /// <summary>Parses a legacy semicolon-separated array cell.</summary>
        public static object ParseArray(Type fieldType, string csv, int col, string fieldName)
        {
            string value = GetFieldValue(csv, col, fieldName);
            string[] parts = value.Length == 0 ? Array.Empty<string>() : value.Split(ARRAY_SEPARATOR);
            Array array = Array.CreateInstance(fieldType, parts.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                array.SetValue(Parse(fieldType, parts[i]), i);
            }
            return array;
        }
        /// <summary>Escapes one decoded CSV cell, including embedded double quotes.</summary>
        public static string Escape(string value)
        {
            value = value ?? string.Empty;
            if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
            {
                return value;
            }
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        private static string GetFieldValue(string text, int col, string fieldName)
        {
            Table table = ParseTable(text);
            int index = Array.IndexOf(table.Fields, fieldName);
            if (index < 0)
            {
                throw new FormatException("Missing column: " + fieldName);
            }
            return table.Rows[col][index];
        }
    }
}
