using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SRT2Speech.Cache
{
    public interface IMemCacheService
    {
        T Get<T>(string key);
        void Set<T>(string key, T value, TimeSpan? expiration = null);
        bool Contains(string key);
        void Remove(string key);
        void Clear();
    }
}
