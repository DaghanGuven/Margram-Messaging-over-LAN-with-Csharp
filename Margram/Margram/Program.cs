using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LanChat
{
    class Program
    {
        // List to keep track of connected clients
        static List<TcpClient> clients = new List<TcpClient>();
        static TcpListener listener;
        static bool isServer = false;
        static string userName;

        static async Task Main(string[] args)
        {
            Console.Write("Enter your name: ");
            userName = Console.ReadLine();

            Console.Write("Do you want to host the chat? (y/n): ");
            string hostChoice = Console.ReadLine();

            if (hostChoice.ToLower() == "y")
            {
                isServer = true;
                StartServer();
            }

            Console.Write("Enter the IP address to connect to (leave empty if hosting): ");
            string ipInput = Console.ReadLine();

            if (!isServer && !string.IsNullOrEmpty(ipInput))
            {
                await ConnectToServer(ipInput, 5000);
            }

            // Start listening for incoming messages
            Task.Run(() => ListenForMessages());

            // Start sending messages
            while (true)
            {
                Console.Write(userName + ":");
                string message = Console.ReadLine();
                if (isServer)
                {
                    
                    BroadcastMessage($"{userName}: {message}");
                }
                else
                {
                    SendMessageToServer($"{userName}: {message}");
                }
            }
        }

        // Start the server to accept incoming connections
        static void StartServer()
        {
            try
            {
                listener = new TcpListener(IPAddress.Any, 5000);
                listener.Start();
                Console.WriteLine("Server started. Waiting for clients...");

                Task.Run(async () =>
                {
                    while (true)
                    {
                        TcpClient client = await listener.AcceptTcpClientAsync();
                        lock (clients)
                        {
                            clients.Add(client);
                        }
                        Console.WriteLine("Client connected.");
                        HandleClient(client);
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Server error: {ex.Message}");
            }
        }

        // Handle incoming messages from a client
        static async void HandleClient(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            byte[] buffer = new byte[1024];
            int byteCount;

            try
            {
                while ((byteCount = await stream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                {
                    string message = Encoding.UTF8.GetString(buffer, 0, byteCount);
                    Console.WriteLine(message);
                    BroadcastMessage(message, client);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Client error: {ex.Message}");
            }
            finally
            {
                lock (clients)
                {
                    clients.Remove(client);
                }
                client.Close();
                Console.WriteLine("Client disconnected.");
            }
        }

        // Connect to a server
        static async Task ConnectToServer(string ip, int port)
        {
            try
            {
                TcpClient client = new TcpClient();
                await client.ConnectAsync(IPAddress.Parse(ip), port);
                Console.WriteLine("Connected to the server.");
                clients.Add(client);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Connection error: {ex.Message}");
            }
        }

        // Listen for incoming messages (for client)
        static async Task ListenForMessages()
        {
            while (true)
            {
                if (!isServer && clients.Count > 0)
                {
                    TcpClient client = clients[0];
                    NetworkStream stream = client.GetStream();
                    byte[] buffer = new byte[1024];
                    int byteCount;

                    try
                    {
                        byteCount = await stream.ReadAsync(buffer, 0, buffer.Length);
                        if (byteCount != 0)
                        {
                            string message = Encoding.UTF8.GetString(buffer, 0, byteCount);
                            Console.WriteLine(message);
                        }
                        else
                        {
                            // Server disconnected
                            Console.WriteLine("Server disconnected.");
                            clients.RemoveAt(0);
                            client.Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Listening error: {ex.Message}");
                        clients.RemoveAt(0);
                        client.Close();
                    }
                }
                await Task.Delay(100);
            }
        }

        // Broadcast message to all clients except the sender
        static void BroadcastMessage(string message, TcpClient excludeClient = null)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            lock (clients)
            {
                foreach (var client in clients)
                {
                    if (client != excludeClient)
                    {
                        try
                        {
                            NetworkStream stream = client.GetStream();
                            stream.WriteAsync(data, 0, data.Length);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Broadcast error: {ex.Message}");
                        }
                    }
                }
            }
        }

        // Send message to the server (for client)
        static void SendMessageToServer(string message)
        {
            if (clients.Count > 0)
            {
                TcpClient client = clients[0];
                try
                {
                    NetworkStream stream = client.GetStream();
                    byte[] data = Encoding.UTF8.GetBytes(message);
                    stream.WriteAsync(data, 0, data.Length);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Send error: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("Not connected to any server.");
            }
        }
    }
}
