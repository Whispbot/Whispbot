using Npgsql;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Dynamic;
using System.Reflection;
using YellowMacaroni.Discord.Extentions;

namespace Whispbot.Extensions
{
    public static class PostgresExtensions
    {
        /// <summary>
        /// Converts all rows from a data reader to a list of objects of type T
        /// </summary>
        /// <typeparam name="T">The type to convert to</typeparam>
        /// <param name="reader">The data reader containing the results</param>
        /// <returns>A list of objects of type T</returns>
        public static List<T> ToList<T>(this NpgsqlDataReader reader) where T : new()
        {
            var result = new List<T>();
            var columnNames = GetColumnNames(reader);
            var mappings = GetMappings<T>();

            while (reader.Read())
            {
                var item = new T();
                MapReaderToObject(reader, item, columnNames, mappings);
                result.Add(item);
            }

            return result;
        }

        /// <summary>
        /// Converts the first row of a data reader to an object of type T
        /// </summary>
        /// <typeparam name="T">The type to convert to</typeparam>
        /// <param name="reader">The data reader containing the results</param>
        /// <returns>An object of type T, or default(T) if no rows exist</returns>
        public static T? FirstOrDefault<T>(this NpgsqlDataReader reader) where T : new()
        {
            if (!reader.Read())
                return default;

            var item = new T();
            var columnNames = GetColumnNames(reader);
            var mappings = GetMappings<T>();

            MapReaderToObject(reader, item, columnNames, mappings);

            return item;
        }

        public static List<object> ToDynamicList(this NpgsqlDataReader reader)
        {
            var result = new List<object>();
            var columnNames = new string[reader.FieldCount];
            for (int i = 0; i < reader.FieldCount; i++)
            {
                columnNames[i] = reader.GetName(i);
            }

            while (reader.Read())
            {
                IDictionary<string, object?> expando = new ExpandoObject();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    expando[columnNames[i]] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }
                result.Add(expando);
            }

            return result;
        }

        /// <summary>
        /// Gets the column names from the data reader
        /// </summary>
        private static string[] GetColumnNames(IDataReader reader)
        {
            var columnNames = new string[reader.FieldCount];
            for (int i = 0; i < reader.FieldCount; i++)
            {
                columnNames[i] = reader.GetName(i);
            }
            return columnNames;
        }

        /// <summary>
        /// Gets mappings for properties and fields of type T
        /// </summary>
        private static (Dictionary<string, PropertyInfo> Properties, Dictionary<string, FieldInfo> Fields) GetMappings<T>()
        {
            var properties = typeof(T).GetProperties();
            var fields = typeof(T).GetFields();

            var propertyMap = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);
            var fieldMap = new Dictionary<string, FieldInfo>(StringComparer.OrdinalIgnoreCase);

            foreach (var property in properties)
            {
                propertyMap[property.Name] = property;
            }

            foreach (var field in fields)
            {
                fieldMap[field.Name] = field;
            }

            return (propertyMap, fieldMap);
        }

        /// <summary>
        /// Maps data from a reader to an object
        /// </summary>
        private static void MapReaderToObject<T>(
            NpgsqlDataReader reader,
            T item,
            string[] columnNames,
            (Dictionary<string, PropertyInfo> Properties, Dictionary<string, FieldInfo> Fields) mappings)
        {
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var columnName = columnNames[i];

                if (reader.IsDBNull(i))
                    continue;

                var value = reader.GetValue(i);

                if (mappings.Properties.TryGetValue(columnName, out var property))
                {
                    try
                    {
                        SetPropertyValue(property, item!, value);
                    }
                    catch { }
                    continue;
                }

                if (mappings.Fields.TryGetValue(columnName, out var field))
                {
                    try
                    {
                        SetFieldValue(field, item!, value);
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// Sets a property value with appropriate type conversion
        /// </summary>
        private static void SetPropertyValue(PropertyInfo property, object item, object value)
        {
            Type propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

            if (propertyType == typeof(Guid) && value is string stringValue)
            {
                property.SetValue(item, Guid.Parse(stringValue));
            }
            else if (propertyType == typeof(Guid) && value is byte[] guidBytes)
            {
                property.SetValue(item, new Guid(guidBytes));
            }
            else if (propertyType == typeof(DateTimeOffset) && value is DateTime dateTimeValue)
            {
                property.SetValue(item, new DateTimeOffset(dateTimeValue));
            }
            else if (propertyType.IsEnum && value is string enumString)
            {
                property.SetValue(item, Enum.Parse(property.PropertyType, enumString));
            }
            else if (propertyType.IsEnum && value is int enumInt)
            {
                property.SetValue(item, Enum.ToObject(property.PropertyType, enumInt));
            }
            else if (value is Array array)
            {
                if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    var elementType = propertyType.GetGenericArguments()[0];
                    var listType = typeof(List<>).MakeGenericType(elementType);
                    var list = Activator.CreateInstance(listType);
                    var addMethod = listType.GetMethod("Add");
                    
                    foreach (var arrayElement in array)
                    {
                        if (arrayElement != null)
                        {
                            var convertedItem = elementType == typeof(string) ? arrayElement.ToString() : Convert.ChangeType(arrayElement, elementType);
                            addMethod?.Invoke(list, [convertedItem]);
                        }
                    }
                    
                    property.SetValue(item, list);
                }
                else
                {
                    property.SetValue(item, array);
                }
            }
            else
            {
                var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                var convertedValue = Convert.ChangeType(value, targetType);
                property.SetValue(item, convertedValue);
            }
        }

        /// <summary>
        /// Sets a field value with appropriate type conversion
        /// </summary>
        private static void SetFieldValue(FieldInfo field, object item, object value)
        {
            Type fieldType = Nullable.GetUnderlyingType(field.FieldType) ?? field.FieldType;

            if (fieldType == typeof(Guid) && value is string stringValue)
            {
                field.SetValue(item, Guid.Parse(stringValue));
            }
            else if (fieldType == typeof(Guid) && value is byte[] guidBytes)
            {
                field.SetValue(item, new Guid(guidBytes));
            }
            else if (fieldType == typeof(DateTimeOffset) && value is DateTime dateTimeValue)
            {
                field.SetValue(item, new DateTimeOffset(dateTimeValue));
            }
            else if (fieldType.IsEnum && value is string enumString)
            {
                field.SetValue(item, Enum.Parse(field.FieldType, enumString));
            }
            else if (fieldType.IsEnum && value is int enumInt)
            {
                field.SetValue(item, Enum.ToObject(field.FieldType, enumInt));
            }
            else if (value is Array array)
            {
                if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    var elementType = fieldType.GetGenericArguments()[0];
                    var listType = typeof(List<>).MakeGenericType(elementType);
                    var list = Activator.CreateInstance(listType);
                    var addMethod = listType.GetMethod("Add");
                    
                    foreach (var arrayElement in array)
                    {
                        if (arrayElement != null)
                        {
                            var convertedItem = elementType == typeof(string) ? arrayElement.ToString() : Convert.ChangeType(arrayElement, elementType);
                            addMethod?.Invoke(list, [convertedItem]);
                        }
                    }
                    
                    field.SetValue(item, list);
                }
                else
                {
                    field.SetValue(item, array);
                }
            }
            else
            {
                var targetType = Nullable.GetUnderlyingType(field.FieldType) ?? field.FieldType;
                var convertedValue = Convert.ChangeType(value, targetType);
                field.SetValue(item, convertedValue);
            }
        }

        public static void OpenWithRetry(this NpgsqlConnection connection, int maxRetries, TimeSpan delay)
        {
            int retries = 0;
            while (true)
            {
                try
                {
                    connection.Open();
                    return;
                }
                catch (NpgsqlException)
                {
                    if (++retries > maxRetries)
                        throw;

                    Thread.Sleep(delay);
                }
            }
        }
    }
}