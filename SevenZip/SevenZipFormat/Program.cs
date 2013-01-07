using System;
using System.Collections.Generic;
using System.Text;
using Nomad.Archive.SevenZip;
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection;

namespace SevenZip
{
    class Program
    {
        private static void ShowHelp()
        {
            Console.WriteLine("SevenZip");
            Console.WriteLine("SevenZip l {ArchiveName}");
            Console.WriteLine("SevenZip e {ArchiveName} {FileNumber}");
        }

        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                ShowHelp();
                return;
            }

            try
            {
                string ArchiveName;
                uint FileNumber = 0xFFFFFFFF;
                bool Extract;

                switch (args[0])
                {
                    case "l":
                        ArchiveName = args[1];
                        Extract = false;
                        break;
                    case "e":
                        ArchiveName = args[1];
                        Extract = true;
                        if ((args.Length < 3) || !uint.TryParse(args[2], out FileNumber))
                        {
                            ShowHelp();
                            return;
                        }
                        break;
                    default:
                        ShowHelp();
                        return;
                }

                using (SevenZipFormat Format = new SevenZipFormat(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "7z.dll")))
                {
                    IInArchive Archive = Format.CreateInArchive(SevenZipFormat.GetClassIdFromKnownFormat(KnownSevenZipFormat.SevenZip));
                    if (Archive == null)
                    {
                        ShowHelp();
                        return;
                    }

                    try
                    {
                        using (InStreamWrapper ArchiveStream = new InStreamWrapper(File.OpenRead(ArchiveName)))
                        {
                            ulong CheckPos = 32 * 1024;
                            if (Archive.Open(ArchiveStream, ref CheckPos, null) != 0)
                                ShowHelp();

                            Console.Write("Archive: ");
                            Console.WriteLine(ArchiveName);

                            if (Extract)
                            {
                                PropVariant Name = new PropVariant();
                                Archive.GetProperty(FileNumber, ItemPropId.kpidPath, ref Name);
                                string FileName = (string)Name.GetObject();

                                Console.Write("Extracting: ");
                                Console.Write(FileName);
                                Console.Write(' ');

                                Archive.Extract(new uint[] { FileNumber }, 1, 0, new ArchiveCallback(FileNumber, FileName));
                            }
                            else
                            {
                                Console.WriteLine("List:");
                                uint Count = Archive.GetNumberOfItems();
                                for (uint I = 0; I < Count; I++)
                                {
                                    PropVariant Name = new PropVariant();
                                    Archive.GetProperty(I, ItemPropId.kpidPath, ref Name);
                                    Console.Write(I);
                                    Console.Write(' ');
                                    Console.WriteLine(Name.GetObject());
                                }
                            }
                        }
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(Archive);
                    }
                }
            }
            catch (Exception e)
            {
                Console.Write("Error: ");
                Console.WriteLine(e.Message);
            }
        }
    }

    class ArchiveCallback : IArchiveExtractCallback
    {
        private uint FileNumber;
        private string FileName;
        private OutStreamWrapper FileStream;

        public ArchiveCallback(uint fileNumber, string fileName)
        {
            this.FileNumber = fileNumber;
            this.FileName = fileName;
        }

        #region IArchiveExtractCallback Members

        public void SetTotal(ulong total)
        {
        }

        public void SetCompleted(ref ulong completeValue)
        {
        }

        public int GetStream(uint index, out ISequentialOutStream outStream, AskMode askExtractMode)
        {
            if ((index == FileNumber) && (askExtractMode == AskMode.kExtract))
            {
                string FileDir = Path.GetDirectoryName(FileName);
                if (!string.IsNullOrEmpty(FileDir))
                    Directory.CreateDirectory(FileDir);
                FileStream = new OutStreamWrapper(File.Create(FileName));

                outStream = FileStream;
            }
            else
                outStream = null;

            return 0;
        }

        public void PrepareOperation(AskMode askExtractMode)
        {
        }

        public void SetOperationResult(OperationResult resultEOperationResult)
        {
            FileStream.Dispose();
            Console.WriteLine(resultEOperationResult);
        }

        #endregion
    }
}
