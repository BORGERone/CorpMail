using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class Server
{
    static Dictionary<string, TcpClient> clients = new Dictionary<string, TcpClient>();
    static Dictionary<string, List<string>> offlineMessages = new Dictionary<string, List<string>>();
    static Dictionary<string, List<byte[]>> offlineFiles = new Dictionary<string, List<byte[]>>();

    static void Main(string[] args)
    {
        TcpListener server = null;
        try
        {
            int port = 13000;
            IPAddress localAddr = IPAddress.Parse("127.0.0.1");
            server = new TcpListener(localAddr, port);
            server.Start();

            Console.WriteLine("Ожидание подключения...");

            while (true)
            {
                TcpClient client = server.AcceptTcpClient();
                Thread clientThread = new Thread(() => HandleClient(client));
                clientThread.Start();
            }
        }
        catch (SocketException e)
        {
            Console.WriteLine("SocketException: {0}", e);
        }
        finally
        {
            server.Stop();
        }

        Console.WriteLine("\nНажмите Enter для завершения...");
        Console.Read();
    }

    static void HandleClient(TcpClient client)
    {
        NetworkStream stream = client.GetStream();
        byte[] bytes = new byte[256];
        int i;

        // Получение имени клиента
        i = stream.Read(bytes, 0, bytes.Length);
        string clientName = Encoding.ASCII.GetString(bytes, 0, i).Trim();
        clients[clientName] = client;
        Console.WriteLine($"{clientName} подключен!");

        // Отправка списка клиентов
        SendClientList();

        // Отправка оффлайн сообщений и файлов
        if (offlineMessages.ContainsKey(clientName))
        {
            foreach (var message in offlineMessages[clientName])
            {
                byte[] msg = Encoding.ASCII.GetBytes(message);
                stream.Write(msg, 0, msg.Length);
            }
            offlineMessages[clientName].Clear();
        }

        if (offlineFiles.ContainsKey(clientName))
        {
            foreach (var file in offlineFiles[clientName])
            {
                stream.Write(file, 0, file.Length);
            }
            offlineFiles[clientName].Clear();
        }

        while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
        {
            string data = Encoding.ASCII.GetString(bytes, 0, i).Trim();
            Console.WriteLine($"{clientName}: {data}");

            if (data.StartsWith("file:"))
            {
                string[] parts = data.Split(':');
                if (parts.Length < 4)
                {
                    Console.WriteLine("Неверный формат данных файла.");
                    continue;
                }

                string recipient = parts[1];
                int fileNameLength = int.Parse(parts[2]);
                int fileSize = int.Parse(parts[3]);

                byte[] fileNameData = new byte[fileNameLength];
                stream.Read(fileNameData, 0, fileNameLength);
                string fileName = Encoding.ASCII.GetString(fileNameData).Trim();

                byte[] fileData = new byte[fileSize];
                int totalBytesRead = 0;
                while (totalBytesRead < fileSize)
                {
                    int bytesRead = stream.Read(fileData, totalBytesRead, fileSize - totalBytesRead);
                    if (bytesRead == 0)
                    {
                        break;
                    }
                    totalBytesRead += bytesRead;
                }

                if (clients.ContainsKey(recipient))
                {
                    NetworkStream recipientStream = clients[recipient].GetStream();
                    byte[] header = Encoding.ASCII.GetBytes($"file:{fileName}:{fileData.Length}");
                    recipientStream.Write(header, 0, header.Length);
                    recipientStream.Write(fileData, 0, fileData.Length);
                    Console.WriteLine($"Файл отправлен {recipient}");
                }
                else
                {
                    if (!offlineFiles.ContainsKey(recipient))
                    {
                        offlineFiles[recipient] = new List<byte[]>();
                    }
                    offlineFiles[recipient].Add(fileData);
                    Console.WriteLine($"Файл для {recipient} сохранен.");
                }
            }
            else
            {
                string[] parts = data.Split(':');
                if (parts.Length < 2)
                {
                    Console.WriteLine("Неверный формат данных сообщения.");
                    continue;
                }

                string recipient = parts[0];
                string message = parts[1];

                if (clients.ContainsKey(recipient))
                {
                    NetworkStream recipientStream = clients[recipient].GetStream();
                    byte[] msg = Encoding.ASCII.GetBytes($"{clientName}: {message}");
                    recipientStream.Write(msg, 0, msg.Length);
                    Console.WriteLine($"Отправлено {recipient}: {message}");
                }
                else
                {
                    if (!offlineMessages.ContainsKey(recipient))
                    {
                        offlineMessages[recipient] = new List<string>();
                    }
                    offlineMessages[recipient].Add($"{clientName}: {message}");
                    Console.WriteLine($"Сообщение для {recipient} сохранено.");
                }
            }
        }

        clients.Remove(clientName);
        SendClientList();
        client.Close();
    }

    static void SendClientList()
    {
        string clientList = string.Join(",", clients.Keys);
        byte[] msg = Encoding.ASCII.GetBytes($"clients:{clientList}");
        foreach (var client in clients.Values)
        {
            NetworkStream stream = client.GetStream();
            stream.Write(msg, 0, msg.Length);
        }
    }
}
