using log4net;
using log4net.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    public class FileSender
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(FileSender));
        public static void SendFilesAndFolders(string path, NetworkStream stream)
        {
            XmlConfigurator.Configure(new FileInfo("log4net.config"));
            try
            {
                if (Directory.Exists(path))
                {
                    string[] files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);

                    // Send file data marker and the number of files first
                    byte[] marker = Encoding.ASCII.GetBytes("FILEDATA");
                    stream.Write(marker, 0, marker.Length);

                    byte[] fileCountBytes = BitConverter.GetBytes(files.Length);
                    stream.Write(fileCountBytes, 0, fileCountBytes.Length);

                    foreach (string filePath in files)
                    {
                        SendFile(filePath, stream, path);
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error("Got an error while sending files and folders: " + ex.Message);
            }
        }

        public static void SendFile(string filePath, NetworkStream stream, string baseFolder = "")
        {
            XmlConfigurator.Configure(new FileInfo("log4net.config"));
            try
            {
                string relativePath = Path.GetRelativePath(baseFolder, filePath);
                byte[] relativePathBytes = Encoding.ASCII.GetBytes(relativePath);
                byte[] relativePathLength = BitConverter.GetBytes(relativePathBytes.Length);
                stream.Write(relativePathLength, 0, relativePathLength.Length);
                stream.Write(relativePathBytes, 0, relativePathBytes.Length);

                long fileLength = new FileInfo(filePath).Length;
                byte[] fileLengthBytes = BitConverter.GetBytes(fileLength);
                stream.Write(fileLengthBytes, 0, fileLengthBytes.Length);

                const int chunkSize = 1024 * 64; // 64 KB chunks
                byte[] buffer = new byte[chunkSize];
                using (FileStream fileStream = File.OpenRead(filePath))
                {
                    int bytesRead;
                    while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        stream.Write(buffer, 0, bytesRead);
                    }
                }
                log.Info($"Sent file: {relativePath}");
                Console.WriteLine($"Sent file: {relativePath}");
            }
            catch (Exception ex)
            {
                log.Error("Got an error while sending file: " + ex.Message);
            }
        }
    }
}
