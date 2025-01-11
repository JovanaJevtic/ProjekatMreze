using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Zauzece;

namespace Server
{
    public class Program
    {
        public const int UDP_PORT = 50001;
        public const int TCP_PORT = 51000;
        public const int BUFFER_SIZE = 256;
        public const int SOMAXCONN = 15;
        private static Dictionary<int, (int UkupnoMjesta, int SlobodnoMjesta, decimal CijenaPoSatu)> parkingInfo = new Dictionary<int, (int, int, decimal)>();
        private static Dictionary<int, Class> zahtjevi = new Dictionary<int, Class>();

        static void Main(string[] args)
        {
            Console.WriteLine("Unesite broj parkinga u gradu: ");
            int brojParkinga;
            while (!int.TryParse(Console.ReadLine(), out brojParkinga) || brojParkinga <= 0)
            {
                Console.WriteLine("Pogrešan unos! Molimo unesite validan broj parkinga (pozitivan broj).");
            }

            for (int i = 1; i <= brojParkinga; i++)
            {
                int ukupnoMjesta;
                Console.WriteLine($"Unesite broj mjesta za parking {i}: ");
                while (!int.TryParse(Console.ReadLine(), out ukupnoMjesta) || ukupnoMjesta <= 0)
                {
                    Console.WriteLine("Pogrešan unos! Molimo unesite validan broj mjesta (pozitivan broj).");
                }

                int slobodnoMjesta;
                do
                {
                    Console.WriteLine($"Unesite broj slobodnih mjesta za parking {i}: ");
                    while (!int.TryParse(Console.ReadLine(), out slobodnoMjesta) || slobodnoMjesta < 0 || slobodnoMjesta > ukupnoMjesta)
                    {
                        Console.WriteLine("Pogrešan unos! Slobodnih mjesta ne može biti više nego ukupno mjesta na parkingu.");
                    }
                } while (slobodnoMjesta > ukupnoMjesta);

                decimal cijenaPoSatu;
                Console.WriteLine($"Unesite cijenu po satu za parking {i}: ");
                while (!decimal.TryParse(Console.ReadLine(), out cijenaPoSatu) || cijenaPoSatu < 0)
                {
                    Console.WriteLine("Pogrešan unos! Molimo unesite validnu cijenu (pozitivan broj).");
                }
                parkingInfo[i] = (ukupnoMjesta, slobodnoMjesta, cijenaPoSatu);
            }

            Socket udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint udpEndPoint = new IPEndPoint(IPAddress.Any, UDP_PORT); // UDP server port
            udpSocket.Bind(udpEndPoint);

            Console.WriteLine($"Server je pokrenut i ceka poruke na : {udpEndPoint}");

            EndPoint posiljaocaEndPoint = new IPEndPoint(IPAddress.Any, 0);
            byte[] prijemnibuffer = new byte[BUFFER_SIZE];


            while (true)
            {
                try
                {
                    int brojBajta = udpSocket.ReceiveFrom(prijemnibuffer, ref posiljaocaEndPoint);
                    string poruka = Encoding.UTF8.GetString(prijemnibuffer, 0, brojBajta);
                    Console.WriteLine($"Stigla je poruka od {posiljaocaEndPoint} : {poruka}");

                    if (poruka.ToLower() == "prijava")
                    {
                        string odgovor = "Klijent se uspiješno prijavio!";
                        byte[] odgovorBajti = Encoding.UTF8.GetBytes(odgovor);
                        udpSocket.SendTo(odgovorBajti, posiljaocaEndPoint);

                        Console.WriteLine($"Potvrda o prijavi poslana klijentu.");
                        string tcpDetails = $"TCP IP: {((IPEndPoint)posiljaocaEndPoint).Address}, TCP Port: {TCP_PORT}";
                        byte[] tcpDetailsBajti = Encoding.UTF8.GetBytes(tcpDetails);
                        udpSocket.SendTo(tcpDetailsBajti, posiljaocaEndPoint);
                        Console.WriteLine($"Informacije o TCP konekciji poslate klijentu.");

                        //TCP
                        Socket listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        listenSocket.Bind(new IPEndPoint(IPAddress.Any, TCP_PORT));
                        listenSocket.Listen(SOMAXCONN); // Možemo čekati do 15 klijenata
                        Console.WriteLine($"TCP server je pokrenut na portu {TCP_PORT}");
                        Socket clientSocket = listenSocket.Accept();
                        // Console.WriteLine($"Klijent povezan - IP:{((IPEndPoint)clientSocket.RemoteEndPoint).Address}, Port: {((IPEndPoint)clientSocket.RemoteEndPoint).Port}");

                        string parkingInfoMessage = "\n------INFORMACIJE O PARKINGU:------\n";
                        foreach (var parking in parkingInfo)
                        {
                            parkingInfoMessage += $"\t----Parking {parking.Key}:----\n \t{parking.Value.SlobodnoMjesta}/{parking.Value.UkupnoMjesta} slobodnih mjesta,\n \tCijena: {parking.Value.CijenaPoSatu:C} po satu\n";
                        }
                        byte[] data = Encoding.UTF8.GetBytes(parkingInfoMessage);
                        clientSocket.Send(data);

                        Console.WriteLine("Informacije o parkingu poslate klijentu.");
                        byte[] zauzeceData = new byte[BUFFER_SIZE];
                        int bytesReceived = clientSocket.Receive(zauzeceData);

                        // Deserijalizacija objekta
                        Class zauzece = Class.FromByteArray(zauzeceData);

                        if (parkingInfo.ContainsKey(zauzece.BrojParkinga))
                        {
                            if (parkingInfo[zauzece.BrojParkinga].SlobodnoMjesta >= zauzece.BrojMjesta)
                            {
                                // Smanjivanje broja slobodnih mjesta
                                parkingInfo[zauzece.BrojParkinga] = (
                                    parkingInfo[zauzece.BrojParkinga].UkupnoMjesta,
                                    parkingInfo[zauzece.BrojParkinga].SlobodnoMjesta - zauzece.BrojMjesta,
                                    parkingInfo[zauzece.BrojParkinga].CijenaPoSatu
                                );

                                Random random = new Random();
                                int idZahtjeva = random.Next(100, 1000);
                                zahtjevi[idZahtjeva] = zauzece;

                                Console.WriteLine($"Zahtev za zauzimanje prihvaćen!\n" +
                                    $"Zauzima se ... Parking {zauzece.BrojParkinga}, Mjesta {zauzece.BrojMjesta}, Sati {zauzece.BrojSati}");

                                // Slanje potvrde sa ID-om zahteva
                                string potvrdaZauzeca = $"Zahtjev prihvaćen. Jedinstveni ID zahtjeva: {idZahtjeva}.";
                                byte[] potvrdaBajti = System.Text.Encoding.UTF8.GetBytes(potvrdaZauzeca);
                                clientSocket.Send(potvrdaBajti);

                                // Ispis stanja parkinga nakon zauzimanja
                                Console.WriteLine("\nStanje parkinga nakon zauzimanja:");
                                parkingInfoMessage = "------INFORMACIJE O PARKINGU:------\n";
                                foreach (var parking in parkingInfo)
                                {
                                    parkingInfoMessage += $"\t----Parking {parking.Key}:----\n \t{parking.Value.SlobodnoMjesta}/{parking.Value.UkupnoMjesta} slobodnih mjesta,\n \tCijena: {parking.Value.CijenaPoSatu:C} po satu\n";
                                }
                                Console.WriteLine(parkingInfoMessage);
                            }
                            else
                            {
                                string greskaZauzeca = "Nema dovoljno slobodnih mjesta za zauzimanje.";
                                byte[] greskaBajti = System.Text.Encoding.UTF8.GetBytes(greskaZauzeca);
                                clientSocket.Send(greskaBajti);
                            }
                        }
                        else
                        {
                            string pomocni = "Parking sa tim brojem ne postoji.";
                            byte[] pomocniBajti = Encoding.UTF8.GetBytes(pomocni);
                            clientSocket.Send(pomocniBajti);
                        }
                    }
                    else if (poruka.StartsWith("oslobadjam:"))
                    {
                        int zahtevId = int.Parse(poruka.Split(':')[1].Trim());

                        if (zahtjevi.ContainsKey(zahtevId))
                        {
                            var zauzeto = zahtjevi[zahtevId];

                            parkingInfo[zauzeto.BrojParkinga] = (
                                parkingInfo[zauzeto.BrojParkinga].UkupnoMjesta,
                                parkingInfo[zauzeto.BrojParkinga].SlobodnoMjesta + zauzeto.BrojMjesta,
                                parkingInfo[zauzeto.BrojParkinga].CijenaPoSatu
                            );

                            zahtjevi.Remove(zahtevId);

                            string odgovor = $"Mesto je uspešno oslobodjeno. Slobodno mesto na parkingu {zauzeto.BrojParkinga}: {parkingInfo[zauzeto.BrojParkinga].SlobodnoMjesta}";
                            byte[] odgovorZahteva = Encoding.UTF8.GetBytes(odgovor);
                            udpSocket.SendTo(odgovorZahteva, posiljaocaEndPoint);
                        }
                        else
                        {
                            string odgovor = "Ne postoji zahtev sa tim brojem.";
                            byte[] odgovorZahteva = Encoding.UTF8.GetBytes(odgovor);
                            udpSocket.SendTo(odgovorZahteva, posiljaocaEndPoint);
                        }
                    }
                    else if(poruka?.ToLower() == "izlaz")
                    {
                        Console.WriteLine("Klijent završava sa radom.");
                        udpSocket.Close();
                        break;

                    }

                    else
                    {
                        Console.WriteLine("Poruka nije odgovarajuća. Očekivana poruka je 'prijava'.");
                    }
                }
                catch (SocketException ex)
                {
                    Console.WriteLine($"Došlo je do greške prilikom prijema poruke: {ex.Message}");
                }
            }

        }
    }
}
