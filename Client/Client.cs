using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Zauzece;

namespace Client
{
    public class Client
    {
        public const string SERVER_IP = "192.168.56.1";
        public const int UDP_PORT = 50001;
        public const int TCP_PORT = 51000;
        public const int BUFFER_SIZE = 1024;

        static void Main(string[] args)
        {
            Socket udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint serverAddress = new IPEndPoint(IPAddress.Parse(SERVER_IP), UDP_PORT);

            Console.WriteLine("Klijent je pokrenut. Za prijavu unesite 'prijava', ili 'izlaz' za zatvaranje klijenta.");
            Console.WriteLine("Nakon sto ste zauzeli parking, unesite 'oslobadjam: (broj zahteva)', da biste oslobodili parking mesta. ");

            while (true)
            {
                Console.Write("\n\tUnesite komandu: ");
                string poruka = Console.ReadLine()?.Trim();
                byte[] binarnaPoruka = Encoding.UTF8.GetBytes(poruka);

                if (poruka?.ToLower() == "izlaz")
                {
                    Console.WriteLine("Klijent završava sa radom.");
                    udpSocket.Close();
                    break;
                }

                try
                {
                    int brBajta = udpSocket.SendTo(binarnaPoruka, 0, binarnaPoruka.Length, SocketFlags.None, serverAddress);

                    byte[] odgovor = new byte[BUFFER_SIZE];
                    EndPoint serverEndPoint = new IPEndPoint(IPAddress.Any, 0);
                    int brPrijemnihBajta = udpSocket.ReceiveFrom(odgovor, ref serverEndPoint);
                    string odgovorPoruka = Encoding.UTF8.GetString(odgovor, 0, brPrijemnihBajta);

                    Console.WriteLine($"\nServer je poslao odgovor: {odgovorPoruka}");

                    // Provera za neodgovarajuću poruku
                    if (odgovorPoruka.Contains("Poruka nije odgovarajuća"))
                    {
                        Console.WriteLine("Server: Poruka nije odgovarajuća. Molimo vas unesite ponovo. ");
                        continue; // Nastavlja petlju za novi unos
                    }

                    if (odgovorPoruka.Contains("Klijent se uspiješno prijavio!"))
                    {
                        byte[] odgovorTcpInfo = new byte[BUFFER_SIZE];
                        EndPoint serverEndPointTCPInfo = new IPEndPoint(IPAddress.Any, 0);
                        int brPrijemnihBajtaTcp = udpSocket.ReceiveFrom(odgovorTcpInfo, ref serverEndPointTCPInfo);
                        string tcpInfo = Encoding.UTF8.GetString(odgovorTcpInfo, 0, brPrijemnihBajtaTcp);
                        Console.WriteLine($"Server je poslao TCP informacije: {tcpInfo}");

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

                            int brojParkinga;
                            bool isValid;
                            do
                            {
                                Console.WriteLine("Unesite broj parkinga za koji želite da zauzmete mjesto: ");
                                isValid = int.TryParse(Console.ReadLine(), out brojParkinga);
                                if (!isValid)
                                {
                                    Console.WriteLine("Unos nije validan broj parkinga! Pokušajte ponovo.");
                                }
                            } while (!isValid);

                            int brojMjesta;
                            do
                            {
                                Console.WriteLine("Unesite broj mjesta koja želite da zauzmete: ");
                                isValid = int.TryParse(Console.ReadLine(), out brojMjesta);
                                if (!isValid)
                                {
                                    Console.WriteLine("Unos nije validan broj mjesta! Pokušajte ponovo.");
                                }
                            } while (!isValid);

                            int brojSati;
                            do
                            {
                                Console.WriteLine("Unesite broj sati za koje želite da zauzmete mjesto: ");
                                isValid = int.TryParse(Console.ReadLine(), out brojSati);
                                if (!isValid)
                                {
                                    Console.WriteLine("Unos nije validan broj sati! Pokušajte ponovo.");
                                }
                            } while (!isValid);

                            Class zauzece = new Class(brojParkinga, brojMjesta, brojSati);

                            byte[] zauzeceBytes = zauzece.ToByteArray();
                            tcpSocket.Send(zauzeceBytes);
                            byte[] odgovorZauzece = new byte[BUFFER_SIZE];
                            int bytesReceivedZauzece = tcpSocket.Receive(odgovorZauzece);
                            string odgovorZauzecePoruka = Encoding.UTF8.GetString(odgovorZauzece, 0, bytesReceivedZauzece);

                            Console.WriteLine($"Server je poslao odgovor: {odgovorZauzecePoruka}");

                            if (odgovorZauzecePoruka.Contains("Nema dovoljno slobodnih mjesta"))
                            {
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Greška: {ex.Message}");
                        }
                    }

                    else if (odgovorPoruka.Contains("Zahtjev prihvaćen"))
                    {
                        Console.WriteLine("Parking mesto je uspešno oslobodjeno!");
                    }

                    else if (poruka.ToLower().StartsWith("oslobadjam:"))
                    {
                       // Console.WriteLine("Oslobadjate parking mesto...");
                    }

                    else
                    {
                        Console.WriteLine("Neuspešna prijava na parking. Pokušajte ponovo.");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Greška: {ex.Message}");
                }
            }
        }
    }
}
