using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Client
{
    public class Program
    {
        public const string SERVER_IP = "192.168.1.5";
        public const int UDP_PORT = 50001;
        public const int TCP_PORT = 51000;
        public const int BUFFER_SIZE = 1024;
        static void Main(string[] args)
        {
            Socket udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint serverAddress = new IPEndPoint(IPAddress.Parse(SERVER_IP), UDP_PORT);


            Console.WriteLine("Ako želite da se prijavite na parking, ukucajte 'prijava'");
            string poruka = Console.ReadLine();
            byte[] binarnaPoruka = Encoding.UTF8.GetBytes(poruka);

            try
            {
                int brBajta = udpSocket.SendTo(binarnaPoruka, 0, binarnaPoruka.Length, SocketFlags.None, serverAddress);
                // Console.WriteLine($"Uspješno poslato {brBajta} bajtova ka {serverAddress}");
                byte[] odgovor = new byte[BUFFER_SIZE];
                EndPoint serverEndPoint = new IPEndPoint(IPAddress.Any, 0);
                int brPrijemnihBajta = udpSocket.ReceiveFrom(odgovor, ref serverEndPoint);
                string odgovorPoruka = Encoding.UTF8.GetString(odgovor, 0, brPrijemnihBajta);
                Console.WriteLine($"Server je poslao odgovor: {odgovorPoruka}");
                if (odgovorPoruka.Contains("Klijent se uspiješno prijavio!"))
                {
                    // Kreiraj TCP socket
                    Socket tcpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                    try
                    {
                        tcpSocket.Connect(new IPEndPoint(IPAddress.Parse(SERVER_IP), TCP_PORT));
                        Console.WriteLine("Uspiješno povezivanje sa TCP serverom.");

                        // Primanje podataka o parkingu
                        byte[] tcpBuffer = new byte[BUFFER_SIZE];
                        int bytesReceived = tcpSocket.Receive(tcpBuffer);
                        string parkingInfo = Encoding.UTF8.GetString(tcpBuffer, 0, bytesReceived);
                        Console.WriteLine(parkingInfo);
                    }
                    catch (SocketException ex)
                    {
                        Console.WriteLine($"Greška prilikom slanja UDP poruke: {ex.Message}");
                    }
                    finally
                    {
                        tcpSocket.Close();
                        Console.WriteLine("TCP veza zatvorena.");
                    }
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Greska prilikom slanja UDP poruke:{ex.Message}");

            }
            finally
            {

                udpSocket.Close();
                Console.WriteLine("UDP veza zatvorena.");
            }
            Console.WriteLine("Klijent zavrsava sa radom.");
        }
    }
}
