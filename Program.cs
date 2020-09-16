using Axinom.Toolkit;
using Mono.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace TlsConnectStressTest
{
    public sealed class Program
    {
        static void Main(string[] args)
        {
            var showHelp = false;
            var loadTarget = 4;

            var options = new OptionSet
            {
                "Usage: TlsConnectStressTest.exe --load 4",
                { "h|?|help", "Displays usage instructions.", val => showHelp = val != null },
                { "load=", "Amount of load to generate, from 1 to N", (int x) => loadTarget = x}
            };

            var remainingOptions = options.Parse(args);

            if (showHelp)
            {
                options.WriteOptionDescriptions(Console.Out);
                return;
            }

            if (remainingOptions.Count != 0)
            {
                Console.WriteLine("Unknown command line parameters: {0}", string.Join(" ", remainingOptions.ToArray()));
                Console.WriteLine("For usage instructions, use the --help command line parameter.");
                return;
            }

            var targetUrl = new Uri("https://localhost:443");
            var payload = Helpers.Random.GetBytes(1 * 1024 * 1024);

            var iterations = 0L;

            var clientRange = IPNetwork.Parse("127.0.0.0/16");
            var clientAddresses = new List<IPAddress>(256 * 256 * 256);
            foreach (var address in clientRange.ListIPAddress(FilterEnum.Usable).ToList())
                clientAddresses.Add(address);

            var serverEndpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 443);

            var threadCount = loadTarget;
            var threads = new Thread[threadCount];

            for (var i = 0; i < threadCount; i++)
            {
                bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors) => true;

                void ThreadEntryPoint()
                {
                    while (true)
                    {
                        try
                        {
                            // We exhaust local dynamic port pool per IP address, so just hop addresses.
                            var clientAddress = Helpers.Random.GetRandomItem(clientAddresses);

                            using var client = new TcpClient(new IPEndPoint(clientAddress, 0));

                            // We want to send small packets, trickled one by one.
                            client.NoDelay = true;

                            // One would hope that this prevents the client port from hanging around for long.
                            // One would be wrong but we might as well try.
                            client.LingerState.Enabled = false;
                            client.LingerState.LingerTime = 1;

                            // Just in case.
                            client.SendTimeout = 1000;
                            client.ReceiveTimeout = 1000;

                            client.Connect(serverEndpoint);

                            using var rawStream = client.GetStream();
                            using var slicer = new SlicerStream(rawStream);
                            using var stream = new SslStream(slicer, false, ValidateServerCertificate, null);

                            stream.AuthenticateAsClient("127.0.0.1");

                            stream.Write(payload);
                            stream.Flush();
                            stream.ReadByte();

                            rawStream.Close(100);
                        }
                        catch
                        {
                            // Don't really care about errors.
                        }
                        finally
                        {
                            Interlocked.Increment(ref iterations);
                        }
                    }
                }

                threads[i] = new Thread(ThreadEntryPoint)
                {
                    IsBackground = true,
                    Name = $"Worker #{i}"
                };

                threads[i].Start();
            }

            Console.WriteLine($"Starting https://127.0.0.1:443 with {threadCount} threads.");

            while (true)
            {
                Thread.Sleep(TimeSpan.FromSeconds(5));

                Console.WriteLine($"Iteration {iterations:N0}");
            }
        }
    }
}
