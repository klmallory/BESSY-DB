using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace BESSy.Files
{
    public class ManagedFileLock : IDisposable
    {
        static object _syncRoot = new object();

        static Dictionary<string, ManagedFileLock> _locks = new Dictionary<string, ManagedFileLock>();

        public ManagedFileLock(string name)
        {
            Name = name;

            bool isLocked = false;

            lock (_syncRoot)
                isLocked = _locks.ContainsKey(name);

            while (isLocked)
            {
                Thread.Sleep(50);

                lock (_syncRoot)
                {
                    isLocked = _locks.ContainsKey(name);

                    if (!isLocked)
                    {
                        _locks.Add(this.Name, this);
                        return;
                    }
                }
            }
        }

        public string Name { get; protected set; }

        public void Dispose()
        {
            lock (_syncRoot)
            {
                if (!_locks.ContainsKey(Name))
                    return;
                else
                {
                    _locks.Remove(Name);
                }
            }
        }
    }
}
