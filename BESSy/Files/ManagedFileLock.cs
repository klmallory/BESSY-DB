using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using BESSy.Extensions;

namespace BESSy.Files
{
    public class ManagedFileLock : IDisposable
    {
        Mutex _mutex;
        string _name;

        public ManagedFileLock(string name)
        {
            _name = name;
            var mutexName = String.Format(@"Global\{0}", name);
            
            if (!FileExtensions.TryOpenExistingMutex(mutexName, out _mutex));
            {
                bool isNew = true;
                MutexSecurity mSec = new MutexSecurity();
                SecurityIdentifier sid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
                MutexAccessRule rule = new MutexAccessRule(sid, MutexRights.FullControl, AccessControlType.Allow);
                
                mSec.AddAccessRule(rule);
                _mutex = new Mutex(false, mutexName, out isNew, mSec);
            }

            if (!_mutex.WaitOne(10000))
                throw new InvalidOperationException(string.Format("File Locked {0}", _name));
        }


        public void Dispose()
        {
            if (_mutex != null)
            {
                _mutex.ReleaseMutex();
                _mutex.Dispose();
            }
        }
    }
}
