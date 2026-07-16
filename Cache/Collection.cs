using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Whispbot.Extensions;

namespace Whispbot.Cache
{
    public class Collection<K, T> where T : class where K : notnull
    {
        private readonly Dictionary<K, T> _cache = [];

        private readonly Func<K, Task<T?>> _fetchFunc;

        /// <summary>
        /// A 'collection' which stores cached items which can be searched through and added to.
        /// </summary>
        /// <typeparam name="T">The type to assign the values to.</typeparam>
        /// <param name="client">The parent client.</param>
        /// <param name="fetchEndpoint">The endpoint to send requests to when a fetch is initiated.</param>
        /// <param name="fetchMethod">The method to use when sending the request, null for the default (GET).</param>"
        /// <param name="fetchHeaders">Headers to send along side a fetch.</param>
        public Collection(Func<K, Task<T?>> func, Dictionary<K, T>? cache = null)
        {
            _fetchFunc = func;
            if (cache is not null)
            {
                _cache = cache;
            }
        }

        /// <summary>
        /// Get the <see cref="T"/> from the cache with the given key.
        /// </summary>
        /// <param name="key">The key to get the value from.</param>
        /// <returns><see cref="T"/> represented by the given key.</returns>
        public T? FromCache(K key)
        {
            return _cache.GetValueOrDefault(key);
        }

        /// <summary>
        /// Fetch the <see cref="T"/> from the given <see cref="fetchEndpoint"/> using a GET request and then given headers.
        /// </summary>
        /// <param name="key">The key to assign the value to.</param>
        /// <param name="cache">Whether the value should be cached.</param>
        /// <returns><see cref="T"/> represented by the given key.</returns>
        public async Task<T?> Fetch(K key, bool cache = true)
        {
            try
            {
                T? value = await _fetchFunc(key);
                if (value is not null && cache)
                {
                    Insert(key, value);
                }
                return value;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the <see cref="T"/> from the cache with the given key, otherwise, make a fetch request to get and cache the <see cref="T"/>.
        /// </summary>
        /// <param name="key">The key to get the value from or assign to.</param>
        /// <returns><see cref="T"/> represented by the given key</returns>
        public async Task<T?> Get(K key)
        {

            T? cachedValue = FromCache(key);
            if (cachedValue is not null) return cachedValue;

            return await Fetch(key);
        }

        /// <summary>
        /// Find a <see cref="T"/> from the cache using the given predicate.
        /// </summary>
        /// <param name="predicate">A function which takes an input of a <see cref="T"/> and its key which returns a boolean value of whether it should be returned.</param>
        /// <returns><see cref="T"/> which matches the predicate.</returns>
        public T? Find(Func<T, K, bool> predicate)
        {
            T? value = null;
            foreach (KeyValuePair<K, T> KVP in _cache)
            {
                if (predicate(KVP.Value, KVP.Key))
                {
                    value = KVP.Value;
                    break;
                }
            }
            return value;
        }

        /// <summary>
        /// Find multiple <see cref="T"/> from the cache using the given predicate.
        /// </summary>
        /// <param name="predicate">A function which takes an input of a <see cref="T"/> and its key which returns a boolean value of whether it should be returned.</param>
        /// <returns>A list of <see cref="T"/>s which match the predicate.</returns>
        public List<T> FindMany(Func<T, K, bool> predicate)
        {
            List<T> values = [];
            foreach (KeyValuePair<K, T> KVP in _cache)
            {
                if (predicate(KVP.Value, KVP.Key))
                {
                    values.Add(KVP.Value);
                }
            }
            return values;
        }

        /// <summary>
        /// Find multiple <see cref="T"/> from the cache using the given predicate.
        /// </summary>
        /// <param name="predicate">A function which takes an input of a <see cref="T"/> and its key which returns a boolean value of whether it should be returned.</param>
        /// <returns>A dictionary of key <see cref="K"/> and value <see cref="T"/>s which match the predicate.</returns>
        public Dictionary<K, T> FindManyDict(Func<T, K, bool> predicate)
        {
            Dictionary<K, T> values = [];
            foreach (KeyValuePair<K, T> KVP in _cache)
            {
                if (predicate(KVP.Value, KVP.Key))
                {
                    values.Add(KVP.Key, KVP.Value);
                }
            }
            return values;
        }

        /// <summary>
        /// Insert a <see cref="T"/> with the given key into the cache.
        /// </summary>
        /// <param name="key">The key to use.</param>
        /// <param name="value">The value to use.</param>
        public void Insert(K key, T value)
        {
            _cache[key] = value;
        }

        public void UpdateOrInsert(K key, T value, bool updateWhenNull = false)
        {
            if (_cache.TryGetValue(key, out T? oldValue))
            {
                if (oldValue is null) return;

                PropertyInfo[] properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

                foreach (PropertyInfo property in properties)
                {
                    if (!property.CanWrite || !property.CanRead) continue;

                    var newValue = property.GetValue(value);
                    if (newValue is not null || updateWhenNull)
                    {
                        property.SetValue(oldValue, newValue);
                    }
                }
            }
            else
            {
                _cache.TryAdd(key, value);
            }
        }

        /// <summary>
        /// Remove a <see cref="T"/> from the cache with the given key.
        /// </summary>
        /// <param name="key">The key to use.</param>
        public void Remove(K key)
        {
            _cache.Remove(key);
        }

        /// <summary>
        /// Remove all keys and <see cref="T"/> from the cache.
        /// </summary>
        public void Clear()
        {
            _cache.Clear();
        }

        public int Count => _cache.Count;
    }
}
