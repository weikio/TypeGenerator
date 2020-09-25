using System;
using System.Collections.Generic;
using System.Reflection;

namespace Weikio.TypeGenerator.Types
{
    public class TypeCache
    {
        private static Dictionary<Guid, Func<object>> Cache = new Dictionary<Guid, Func<object>>();
        private static Dictionary<Guid, TypeCacheItem> Cache2 = new Dictionary<Guid, TypeCacheItem>();

        public static Guid Add(Type type, TypeToTypeWrapperOptions options)
        {
            var id = Guid.NewGuid();

            var factory = options.Factory;

            object Create()
            {
                return factory(options, type);
            }

            void OnConstructor(object instance)
            {
                if (options.OnConstructor == null)
                {
                    return;
                }
                    
                options.OnConstructor.Invoke(options, type, instance);
            }
            
            void OnBeforeMethod(object instance, MethodInfo methodInfo)
            {
                if (options.OnBeforeMethod == null)
                {
                    return;
                }
                    
                options.OnBeforeMethod.Invoke(options, type, instance, methodInfo);
            }
            
            void OnAfterMethod(object instance, MethodInfo methodInfo)
            {
                if (options.OnAfterMethod == null)
                {
                    return;
                }
                    
                options.OnAfterMethod.Invoke(options, type, instance, methodInfo);
            }
            
            var item = new TypeCacheItem()
            {
                Factory = Create,
                OnConstructor = OnConstructor,
                OnBeforeMethod = OnBeforeMethod,
                OnAfterMethod = OnAfterMethod
            };
            
            Cache.Add(id, Create);
            Cache2.Add(id, item);
            return id;
        }

        public static object Get(Guid id)
        {
            var factory = Cache[id];

            return factory();
        }
        
        public static TypeCacheItem Details(Guid id)
        {
            var item = Cache2[id];

            return item;
        }
    }

    public class TypeCacheItem
    {
        public Func<object> Factory { get; set; }
        public Action<object> OnConstructor { get; set; }
        public Action<object, MethodInfo> OnBeforeMethod { get; set; }
        public Action<object, MethodInfo> OnAfterMethod { get; set; }
    }
}
