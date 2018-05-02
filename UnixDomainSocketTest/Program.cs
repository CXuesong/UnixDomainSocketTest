using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace UnixDomainSocketTest
{
    class Program
    {
        private const byte PROLOGUE = 127;
        private const byte ACKOWLEDGE = 128;

        // Usage
        //      dotnet run [mode [path]]
        // Where
        //      mode        either s, c, or sc, for server-only, client-only, or both. Defaults to sc.
        //      path        the path of the Unix domain socket. Defaults to a random temporary file name.
        static void Main(string[] args)
        {
            var mode = args.Length > 0 ? args[0].ToLowerInvariant() : "sc";
            var path = args.Length > 1 ? args[1] : Path.GetTempFileName();
            var tasks = new List<Task>();
            if (mode.Contains('s')) tasks.Add(RunServerAsync(path));
            if (mode.Contains('c'))
            {
                if (tasks.Count > 0) Task.Delay(1000).Wait();
                tasks.Add(RunClientAsync(path));
            }
            Task.WaitAll(tasks.ToArray());
        }

        static async Task RunServerAsync(string path)
        {
            if (File.Exists(path))
            {
                if (new FileInfo(path).Length > 0)
                {
                    Console.WriteLine("Cannot overwrite {0}. The file exists and has content.", path);
                    return;
                }
                File.Delete(path);
            }
            var endpoint = new UnixDomainSocketEndPoint(path);
            using (var listener = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Unspecified))
            {
                listener.Bind(endpoint);
                listener.Listen(5);
                Console.WriteLine("Server: Listening at {0}.", endpoint);
                using (var handler = await listener.AcceptAsync())
                {
                    Console.WriteLine("Server: Connected at {0}.", handler.RemoteEndPoint);
                    Console.WriteLine("Server: Type something and press Enter. Empty line for exit.");
                    using (var ns = new NetworkStream(handler))
                    using (var writer = new StreamWriter(ns))
                    {
                        var v = ns.ReadByte();
                        Console.WriteLine("Server: Received prologue: {0}.", v);
                        while (true)
                        {
                            Console.Write("SERVER >");
                            var line = Console.ReadLine();
                            await writer.WriteLineAsync(line);
                            await writer.FlushAsync();
                            Console.WriteLine("Server: Sent {0} characters.", line.Length);
                            v = ns.ReadByte();
                            if (v != ACKOWLEDGE)
                            {
                                Console.WriteLine("Server: Client failed to acknowledge the message.");
                            }
                            if (line == "") break;
                        }
                    }
                    Console.WriteLine("Server: Shutting down…");
                    handler.Shutdown(SocketShutdown.Both);
                }
            }
        }

        static async Task RunClientAsync(string path)
        {
            var endpoint = new UnixDomainSocketEndPoint(path);
            using (var client = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Unspecified))
            {
                Console.WriteLine("Client: Connecting to {0}.", endpoint);
                await client.ConnectAsync(endpoint);
                Console.WriteLine("Client: Connected at {0}.", client.RemoteEndPoint);
                using (var ns = new NetworkStream(client))
                using (var reader = new StreamReader(ns))
                {
                    ns.WriteByte(PROLOGUE);
                    Console.WriteLine("Client: Sent prologue.");
                    while (true)
                    {
                        var line = await reader.ReadLineAsync();
                        Console.WriteLine("CLIENT >{0}", line);
                        ns.WriteByte(ACKOWLEDGE);
                        if (line == "") break;
                    }
                }

                Console.WriteLine("Client: Shutting down…");
                client.Shutdown(SocketShutdown.Both);
                client.Close();
            }
        }

    }
}
