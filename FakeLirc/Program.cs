using System;
using System.Net;
using System.Net.Sockets;

namespace FakeLirc
{
    class Program
    {
        static TcpListener _serverSocket;
        static Socket _clientSocket;

        static void Main()
        {
            Console.WriteLine("Welcome to lirc fake");
            Console.WriteLine("Every line will be put on the socket");
            Console.WriteLine("Type Exit to stop");
            OpenSocket();
            while (true)
            {
                var line = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (line.StartsWith("Exit", StringComparison.Ordinal))
                {
                    break;
                }
                SendOverSocket(line);
            }
            CloseSocket();
        }

        static void SendOverSocket(string line)
        {
            var bytes = System.Text.Encoding.Default.GetBytes(line);
            _clientSocket.Send(bytes);
        }

        static void OpenSocket()
        {
            _serverSocket = new TcpListener(IPAddress.Any, 8888);
            _serverSocket.Start();
            _clientSocket = _serverSocket.AcceptSocket();
        }

        static void CloseSocket()
        {
            _clientSocket.Close();
            _clientSocket.Dispose();
            _serverSocket.Stop();
        }
    }
}
