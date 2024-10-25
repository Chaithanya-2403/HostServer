using log4net;
using log4net.Config;
using Newtonsoft.Json;
using Server;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public class HostServer
{
    private static readonly ILog log = LogManager.GetLogger(typeof(HostServer));
    ConfigVM config;
    public static void Main()
    {
        // Configure log4net
        XmlConfigurator.Configure(new FileInfo("log4net.config"));
        //ConfigVM config;
        //try
        //{
        ConfigVM config = LoadConfiguration(@"D:\Linux\Server\config.json");
        //}
        //catch (Exception ex)
        //{
        //    log.Error("Exception while loading the config data: " + ex.Message);
        //}

        try
        {
            TcpListener server = new TcpListener(IPAddress.Any, config.Port);
            server.Start();
            log.Info("Server is running and waiting for connections...");
            Console.WriteLine("Server is running and waiting for connections...");

            while (true)
            {
                try
                {
                    TcpClient client = server.AcceptTcpClient();
                    log.Info("Client connected.");
                    Console.WriteLine("Client connected.");

                    // Create a new thread for each client to handle requests
                    Thread clientThread = new Thread(() => HandleClient(client, config));
                    clientThread.Start();
                }
                catch (Exception ex)
                {
                    log.Error("Error accepting client connection: " + ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            log.Error("Server encountered an error: " + ex.Message);
        }
    }

    private static void HandleClient(TcpClient client, ConfigVM config)
    {
        NetworkStream stream = client.GetStream();
        try
        {
            // Heartbeat handling
            while (true)
            {
                // Read heartbeat
                byte[] heartbeatBuffer = new byte[16];
                int bytesRead = stream.Read(heartbeatBuffer, 0, heartbeatBuffer.Length);
                string heartbeat = Encoding.ASCII.GetString(heartbeatBuffer, 0, bytesRead);

                if (heartbeat == "HEARTBEAT")
                {
                    // Send ACK back to client
                    byte[] ackResponse = Encoding.ASCII.GetBytes("ACK");
                    stream.Write(ackResponse, 0, ackResponse.Length);
                    log.Info("ACK sent to client.");
                }

                // Check if we should send files
                if (stream.DataAvailable) // Only if data is available, check for file data marker
                {
                    byte[] markerBuffer = new byte[8];
                    stream.Read(markerBuffer, 0, markerBuffer.Length);
                    string marker = Encoding.ASCII.GetString(markerBuffer);

                    if (marker == "FILEDATA")
                    {
                        // Start sending files
                        string path = config.FilePath; //@"D:\Linux\TCP"; // Path to folder
                        SendFilesAndFolders(path, stream);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            log.Error("Error handling client: " + ex.Message);
        }
        finally
        {
            client.Close();
            log.Info("Client connection closed.");
            Console.WriteLine("Client connection closed.");
        }
    }

    public static void SendFilesAndFolders(string path, NetworkStream stream)
    {
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

    public static ConfigVM LoadConfiguration(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Configuration file not found.", filePath);
        }

        string json = File.ReadAllText(filePath);
        return JsonConvert.DeserializeObject<ConfigVM>(json);
    }

}
