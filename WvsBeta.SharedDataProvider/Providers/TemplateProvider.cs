using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using WzTools.FileSystem;
using WzTools.Objects;

namespace WvsBeta.SharedDataProvider.Providers
{
    public abstract class TemplateProvider<TTemplateKey, TValue> : TemplateProvider
    {
        protected TemplateProvider(WzFileSystem fileSystem) : base(fileSystem) {}
        public abstract IDictionary<TTemplateKey, TValue> LoadAll();
    }

    public abstract class TemplateProvider
    {
        protected static ILog _log = LogManager.GetLogger("TemplateProvider");
        
        public WzFileSystem FileSystem { get; }

        protected TemplateProvider(WzFileSystem fileSystem)
        {
            FileSystem = fileSystem;
        }

        public delegate void HandleIndexedProp(int index, WzProperty prop);
        public delegate T HandleIndexedProp<T>(int index, WzProperty prop);
        public delegate void HandleIndexed<T>(int index, T prop);

        /// <summary>
        /// This function will loop from 0 to infinity, and check if the node exist. If it is, cb is called with cb(i, prop).
        /// If it doesnt, this function will return.
        /// </summary>
        /// <param name="prop">The property where the indexed nodes are in</param>
        /// <param name="cb">Callback function</param>
        public static void IterateOverIndexed(WzProperty prop, HandleIndexedProp cb) =>
            IterateOverIndexed<WzProperty>(prop, (a, b) => cb(a, b));
        
        public static void IterateOverIndexed<T>(WzProperty prop, HandleIndexed<T> cb)
        {
            if (prop == null) return;

            for (var i = 0;; i++)
            {
                var subProp = prop.Get<T>(i);
                if (subProp == null) return;
                cb(i, subProp);
            }
        }

        public static IEnumerable<T> SelectOverIndexed<T>(WzProperty prop, HandleIndexedProp<T> cb)
        {
            if (prop == null) yield break;

            for (var i = 0; ; i++)
            {
                var subProp = prop.GetProperty(i);
                if (subProp == null) yield break;
                yield return cb(i, subProp);
            }
        }

        /// <summary>
        /// Load all props from 0..n as T, and return them as a list
        /// </summary>
        /// <param name="prop"></param>
        /// <returns></returns>
        public static IEnumerable<T> LoadArgs<T>(WzProperty prop)
        {
            for (var i = 0; ; i++)
            {
                var x = prop.Get("" + i);
                if (x == null) yield break;

                yield return (T)x;
            }
        }

        public static IEnumerable<object> LoadArgs(WzProperty prop) => LoadArgs<object>(prop);

        /// <summary>
        /// Iterate over each element (Parallel in non-debug mode) and make a Dictionary as result
        /// </summary>
        /// <typeparam name="TIn">Input type of iterator</typeparam>
        /// <typeparam name="TOut">Iterations output type</typeparam>
        /// <typeparam name="TKey">Dictionary Key type</typeparam>
        /// <param name="elements">Elements to iterate over</param>
        /// <param name="func">Function to run per iteration</param>
        /// <param name="outToKey">Function to convert the output to a key</param>
        /// <returns>A regular Dictionary with the key/value pairs</returns>
        protected static Dictionary<TKey, TOut> IterateAllToDict<TIn, TOut, TKey>(
            IEnumerable<TIn> elements,
            Func<TIn, TOut> func,
            Func<TOut, TKey> outToKey) =>
            IterateAllToDict(elements, func, outToKey, x => x);

        protected static Dictionary<TKey, TVal> IterateAllToDict<TIn, TOut, TKey, TVal>(IEnumerable<TIn> elements, Func<TIn, TOut> func, Func<TOut, TKey> outToKey, Func<TOut, TVal> outToVal)
        {
#if DEBUG && NEVER
            var dict = new Dictionary<TKey, TVal>();
            foreach (var nxNode in elements)
            {
                var ret = func(nxNode);
                if (ret == null) continue;
                dict[outToKey(ret)] = outToVal(ret);
            }
            return dict;
#endif
            return elements
                .AsParallel()
                .Select(func)
                .Where(x => x != null)
                .ToDictionary(outToKey, outToVal);
        }
    }

    public abstract class TemplateProvider<TValue> : TemplateProvider<int, TValue>
    {
        protected TemplateProvider(WzFileSystem fileSystem) : base(fileSystem)
        {
        }
    }
}