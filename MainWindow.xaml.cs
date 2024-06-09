using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows;

namespace WpfClient
{
    public partial class MainWindow : Window
    {
        TcpClient client;
        NetworkStream stream;
        string clientName;

        public MainWindow()
        {
            InitializeComponent();
            ConnectToServer();
        }

        private void ConnectToServer()
        {
            try
            {
                int port = 13000;
                client = new TcpClient("127.0.0.1", port);
                stream = client.GetStream();

                // Ввод имени клиента
                clientName = "Client" + new Random().Next(1000, 9999);
                byte[] data = Encoding.ASCII.GetBytes(clientName);
                stream.Write(data, 0, data.Length);

                Thread receiveThread = new Thread(new ThreadStart(ReceiveMessages));
                receiveThread.Start();
            }
            catch (SocketException ex)
            {
                ResponseTextBlock.Text = $"SocketException: {ex}";
            }
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string recipient = ClientComboBox.SelectedItem.ToString();
                string message = MessageTextBox.Text;
                byte[] data = Encoding.ASCII.GetBytes($"{recipient}:{message}");

                stream.Write(data, 0, data.Length);
                ResponseTextBlock.Text += $"\nОтправлено {recipient}: {message}";
            }
            catch (ArgumentNullException ex)
            {
                ResponseTextBlock.Text = $"ArgumentNullException: {ex}";
            }
            catch (SocketException ex)
            {
                ResponseTextBlock.Text = $"SocketException: {ex}";
            }
        }

        private void ReceiveMessages()
        {
            byte[] data = new byte[256];
            string responseData = string.Empty;
            int bytes;

            try
            {
                while ((bytes = stream.Read(data, 0, data.Length)) != 0)
                {
                    responseData = Encoding.ASCII.GetString(data, 0, bytes);
                    Dispatcher.Invoke(() => {
                        if (responseData.StartsWith("clients:"))
                        {
                            string[] clients = responseData.Substring(8).Split(',');
                            ClientComboBox.Items.Clear();
                            foreach (var client in clients)
                            {
                                ClientComboBox.Items.Add(client);
                            }
                        }
                        else if (responseData.StartsWith("file:"))
                        {
                            string[] parts = responseData.Split(':');
                            if (parts.Length < 3)
                            {
                                ResponseTextBlock.Text += "\nНеверный формат данных файла.";
                                return;
                            }

                            string fileName = parts[1];
                            int fileSize = int.Parse(parts[2]);

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

                            // Удаление недопустимых символов из имени файла
                            fileName = string.Concat(fileName.Split(Path.GetInvalidFileNameChars()));

                            File.WriteAllBytes(fileName, fileData);
                            ResponseTextBlock.Text += $"\nФайл получен: {fileName}";
                        }
                        else
                        {
                            ResponseTextBlock.Text += $"\nПолучено: {responseData}";
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => {
                    ResponseTextBlock.Text += $"\nОшибка: {ex.Message}";
                });
            }
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (string file in files)
                {
                    SendFile(file);
                }
            }
        }

        private void SendFile(string filePath)
        {
            try
            {
                string recipient = ClientComboBox.SelectedItem.ToString();
                byte[] fileData = File.ReadAllBytes(filePath);
                byte[] fileNameData = Encoding.ASCII.GetBytes(Path.GetFileName(filePath));
                byte[] data = new byte[4 + fileNameData.Length + fileData.Length];

                BitConverter.GetBytes(fileNameData.Length).CopyTo(data, 0);
                fileNameData.CopyTo(data, 4);
                fileData.CopyTo(data, 4 + fileNameData.Length);

                byte[] header = Encoding.ASCII.GetBytes($"file:{recipient}:{fileNameData.Length}:{fileData.Length}");
                stream.Write(header, 0, header.Length);
                stream.Write(fileNameData, 0, fileNameData.Length);
                stream.Write(fileData, 0, fileData.Length);
                ResponseTextBlock.Text += $"\nФайл отправлен {recipient}: {Path.GetFileName(filePath)}";
            }
            catch (Exception ex)
            {
                ResponseTextBlock.Text += $"\nОшибка при отправке файла: {ex.Message}";
            }
        }
    }
}
