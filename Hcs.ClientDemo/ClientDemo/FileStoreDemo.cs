
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;

using Hcs.ClientApi;
using System.Net.Sockets;

namespace Hcs.ClientDemo
{
    public class FileStoreDemo
    {
        public static void DemoDownloadFile(HcsClient hcsClient)
        {
            //Guid fileGuid = new Guid("33ddc355-60bd-4537-adf6-fbb31322e16f");
            //Guid fileGuid = new Guid("33ddc355-60bd-4537-adf6-fbb31322e16e"); // неверный

            //Guid fileGuid = new Guid("c36f040d-e71c-4365-a277-1654d82e2b08"); // Large file договор ТСЖ Онежский берег
            //Guid fileGuid = new Guid("ac88c321-c362-11ef-bc5e-0242ac120002"); // Large file договор ТСЖ Онежский берег
            //Guid fileGuid = new Guid("134188e0-c370-11ef-8e83-0242ac120002");
            //Guid fileGuid = new Guid("573bda1a-bfa8-4c33-837b-aaac325d5dbb"); // bereg8new manual upload
            //Guid fileGuid = new Guid("0e87dedf-c2b1-11ef-bc8e-0242ac120002");

            //Guid fileGuid = new Guid("71958244-c461-11ef-8e83-0242ac120002"); // bereg27
            Guid fileGuid = new Guid("e4c3b39f-ad59-11ef-bc8e-0242ac120002"); // teplo0
            //Guid fileGuid = new Guid("822b06ff-c4e4-11ef-bc5e-0242ac120002"); // teplo4

            var file = hcsClient.FileStoreService.DownloadFile(
                fileGuid, HcsFileStoreContext.homemanagement, CancellationToken.None).Result;
            Console.WriteLine("\nContent len=" + file.Length + 
                " type=" + file.ContentType + " streamLength=" + file.Stream.Length + 
                " hash=" + hcsClient.ComputeGost94Hash(file.Stream));

            using (var s = new FileStream(@"d:\temp\teplo0.pdf", FileMode.CreateNew, FileAccess.Write)) {
                file.Stream.Seek(0, SeekOrigin.Begin);
                file.Stream.CopyTo(s);
                s.Close();
            }

        }

        public static void DemoUploadFile(HcsClient hcsClient)
        {
            //string sourceFileName = @"d:\temp\LargeFile.pdf";
            //string sourceFileName = @"d:\temp\SmallFile.pdf";
            //string sourceFileName = @"d:\temp\Договор_390.1.pdf";
            string sourceFileName = @"d:\temp\Проект договора.docx";

            var contentType = HcsFile.GetMimeContentTypeForFileName(sourceFileName);
            if (contentType == null) throw new HcsException("Не найден тип mime для файла");

            Console.WriteLine("Uploading file: " + sourceFileName);
            using (var stream = new FileStream(sourceFileName, FileMode.Open, FileAccess.Read)) { 
                var file = new HcsFile(Path.GetFileName(sourceFileName), contentType, stream);
                var guid = hcsClient.FileStoreService.UploadFile(
                    file, HcsFileStoreContext.homemanagement, CancellationToken.None).Result;
                Console.WriteLine("Result file GUID=" + guid);
            }
        }

        public static void DemoGetFileLength(HcsClient hcsClient)
        {
            Guid fileGuid = new Guid("33ddc355-60bd-4537-adf6-fbb31322e16f");
            var length = hcsClient.FileStoreService.GetFileLength(
                HcsFileStoreContext.homemanagement, fileGuid, CancellationToken.None).Result;
            Console.WriteLine($"\nFile length={length} for file with GUID " + fileGuid);
        }

        public static void DemoGostHash(HcsClient hcsClient)
        {
            PrintFileHash(hcsClient, @"d:\temp\teplo0.pdf");
            PrintFileHash(hcsClient, @"d:\temp\teplo1.pdf");
            PrintFileHash(hcsClient, @"d:\temp\teplo3.pdf");
            PrintFileHash(hcsClient, @"d:\temp\teplo4.pdf");

            /*
            ComputeFileHash(hcsClient, @"d:\temp\bereg6gis.pdf");
            ComputeFileHash(hcsClient, @"d:\temp\bereg7loc.pdf");
            ComputeFileHash(hcsClient, @"d:\temp\bereg8new.pdf");
            ComputeFileHash(hcsClient, @"d:\temp\bereg9raw.pdf");
            ComputeFileHash(hcsClient, @"d:\temp\bereg1dow.pdf");
            ComputeFileHash(hcsClient, @"d:\temp\100-1-41-22830-02.pdf");
            */

            //ComputeFileHash(hcsClient, @"d:\temp\avers0.docx");
            //ComputeFileHash(hcsClient, @"d:\temp\avers1.docx");
        }

        public static void PrintFileHash(HcsClient hcsClient, string fileName)
        {
            using (var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read)) {
                var hash = hcsClient.ComputeGost94Hash(stream);
                Console.WriteLine($"{fileName} hash=" + hash + " len=" + new FileInfo(fileName).Length);
            }
        }
    }
}
