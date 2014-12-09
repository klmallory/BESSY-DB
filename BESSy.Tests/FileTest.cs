using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using BESSy.Extensions;

namespace BESSy.Tests
{
    public abstract class FileTest
    {
        protected string _testName;

        protected virtual void Cleanup()
        {
            var fi = new FileInfo(_testName + ".xml");
            if (fi.Exists)
                while (fi.IsFileLocked())
                    Thread.Sleep(100);

            fi.Delete();

            fi = new FileInfo(_testName + ".index");
            if (fi.Exists)
                while (fi.IsFileLocked())
                    Thread.Sleep(100);

            fi.Delete();

            fi = new FileInfo(_testName + ".test.index");
            if (fi.Exists)
                while (fi.IsFileLocked())
                    Thread.Sleep(100);

            fi.Delete();

            fi = new FileInfo(_testName + ".database");
            if (fi.Exists)
                while (fi.IsFileLocked())
                    Thread.Sleep(100);

            fi.Delete();

            fi = new FileInfo(_testName + ".database.index");
            if (fi.Exists)
                while (fi.IsFileLocked())
                    Thread.Sleep(100);

            fi.Delete();

            fi = new FileInfo(_testName + ".subscriber.database");
            if (fi.Exists)
                while (fi.IsFileLocked())
                    Thread.Sleep(100);

            fi.Delete();

            fi = new FileInfo(_testName + ".subscriber.database.index");
            if (fi.Exists)
                while (fi.IsFileLocked())
                    Thread.Sleep(100);

            fi.Delete();

            fi = new FileInfo(_testName + ".subscriber.database");
            if (fi.Exists)
                while (fi.IsFileLocked())
                    Thread.Sleep(100);

            fi.Delete();

            fi = new FileInfo(_testName + ".subscriber.database.index");
            if (fi.Exists)
                while (fi.IsFileLocked())
                    Thread.Sleep(100);

            fi.Delete();

            fi = new FileInfo(_testName + ".catalog.indexUpdate");
            if (fi.Exists)
                while (fi.IsFileLocked())
                    Thread.Sleep(100);

            fi.Delete();

            fi = new FileInfo(_testName + ".scenario");
            if (fi.Exists)
                while (fi.IsFileLocked())
                    Thread.Sleep(100);

            fi.Delete();

            fi = new FileInfo(_testName + ".database.catIndex.index");
            if (fi.Exists)
                while (fi.IsFileLocked())
                    Thread.Sleep(100);

            fi.Delete();

            fi = new FileInfo(_testName + ".database.cascade.index");
            if (fi.Exists)
                while (fi.IsFileLocked())
                    Thread.Sleep(100);

            fi.Delete();
        }
    }
}
