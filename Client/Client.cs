using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Zauzece;

namespace Client
{
    public class Client
    {
        public const string SERVER_IP = "192.168.1.5";
        public const int UDP_PORT = 50001;
        public const int TCP_PORT = 51000;
        public const int BUFFER_SIZE = 2000;
        private static Dictionary<int, int> statistikaParkinga = new Dictionary<int, int>();
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

                    if (odgovorPoruka.Contains("Poruka nije odgovarajuća"))
                    {
                        continue;
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

                            string proizvodjac = " ";
                            string model = " ";
                            string boja = " ";
                            string regBroj = " ";

                            if (brojMjesta == 1)
                            {
                                Console.WriteLine("\nDa li zelite da unesete informacije o svom automobilu? (da/ne)");
                                string odgovorInfo = Console.ReadLine()?.Trim();

                                if (odgovorInfo.ToLower() == "da")
                                {
                                    Console.WriteLine("Proizvodjac: ");
                                    proizvodjac = Console.ReadLine()?.Trim();
                                    Console.WriteLine("Model: ");
                                    model = Console.ReadLine()?.Trim();
                                    Console.WriteLine("Boja: ");
                                    boja = Console.ReadLine()?.Trim();
                                    Console.WriteLine("Registarski broj: ");
                                    regBroj = Console.ReadLine()?.Trim();
                                }
                            }
                            Class zauzece = new Class(brojParkinga, brojMjesta, brojSati, proizvodjac, model, boja, regBroj);
                            byte[] zauzeceBytes = zauzece.ToByteArray();
                            tcpSocket.Send(zauzeceBytes);

                            byte[] odgovorZauzece = new byte[BUFFER_SIZE];
                            int bytesReceivedZauzece = tcpSocket.Receive(odgovorZauzece);
                            string odgovorZauzecePoruka = Encoding.UTF8.GetString(odgovorZauzece, 0, bytesReceivedZauzece);
                            Console.WriteLine($"Server je poslao odgovor: {odgovorZauzecePoruka}");

                            if (odgovorZauzecePoruka.Contains("Nema dovoljno slobodnih mjesta") ||
                               odgovorZauzecePoruka.Contains("Nije moguće zauzeti mjesto") ||
                               odgovorZauzecePoruka.Contains("Zauzeta mjesta: 0"))
                            {
                                continue;
                            }
                            //  PRIMAMO STVARNI BROJ MIJESTA KOJI SU ZAUZETI
                            byte[] stvarniBrojBytes = new byte[4];
                            int primljeno = tcpSocket.Receive(stvarniBrojBytes);
                            int stvarnoZauzeto = BitConverter.ToInt32(stvarniBrojBytes, 0);

                            if (!odgovorZauzecePoruka.Contains("Parking sa tim brojem ne postoji"))
                            {
                                //statistika
                                if (statistikaParkinga.ContainsKey(brojParkinga))
                                {
                                    statistikaParkinga[brojParkinga] += stvarnoZauzeto;
                                }
                                else
                                {
                                    statistikaParkinga[brojParkinga] = stvarnoZauzeto;
                                }
                                string statistikaInfo = "\n------ STATISTIKA O PARKINGU: ------";
                                foreach (var stat in statistikaParkinga)
                                {
                                    statistikaInfo += $"\n\tParking {stat.Key}: {stat.Value} vozila.";
                                }
                                Console.WriteLine(statistikaInfo);
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

                    else if (odgovorPoruka.Contains("Mjesto je uspiješno oslobodjeno"))
                    {
                        Console.WriteLine("\nDa li želite da potvrdite račun? (Da/Ne)");
                        string potvrda = Console.ReadLine()?.Trim().ToLower();
                        if (potvrda == "da")
                        {
                            // Pošaljite potvrdu serveru
                            string potvrdaPoruka = "Račun potvrdjen.";
                            byte[] binarnaPotvrda = Encoding.UTF8.GetBytes(potvrdaPoruka);
                            udpSocket.SendTo(binarnaPotvrda, 0, binarnaPotvrda.Length, SocketFlags.None, serverAddress);

                            Console.WriteLine("Račun potvrdjen. Hvala što ste koristili parking!");
                        }
                        else
                        {
                            Console.WriteLine("Račun nije potvrdjen.");
                        }
                    }
                    else if (poruka.ToLower().StartsWith("oslobadjam:"))
                    {
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