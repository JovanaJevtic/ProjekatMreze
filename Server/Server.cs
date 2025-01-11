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
                Console.WriteLine($"\nUnesite broj mjesta za parking {i}: ");
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

            Console.WriteLine($"\nServer je pokrenut i ceka poruke na : {udpEndPoint}");

            EndPoint posiljaocaEndPoint = new IPEndPoint(IPAddress.Any, 0);
            byte[] prijemnibuffer = new byte[BUFFER_SIZE];

            while (true)
            {
                try
                {
                    int brojBajta = udpSocket.ReceiveFrom(prijemnibuffer, ref posiljaocaEndPoint);
                    string poruka = Encoding.UTF8.GetString(prijemnibuffer, 0, brojBajta);
                    Console.WriteLine($"\nStigla je poruka od {posiljaocaEndPoint} : {poruka}");

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

                        // TCP server uspostavlja vezu sa klijentom
                        var tcpListener = new TcpListener(IPAddress.Any, TCP_PORT);
                        tcpListener.Start();
                        Console.WriteLine($"TCP server je pokrenut na portu {TCP_PORT}");

                        // Čeka TCP konekciju
                        Socket tcpClientSocket = tcpListener.AcceptSocket();
                        Console.WriteLine($"TCP konekcija uspostavljena sa {tcpClientSocket.RemoteEndPoint}");

                        string parkingInfoMessage = "\n------INFORMACIJE O PARKINGU:------\n";
                        foreach (var parking in parkingInfo)
                        {
                            parkingInfoMessage += $"\n\t----Parking {parking.Key}:----\n \t{parking.Value.SlobodnoMjesta}/{parking.Value.UkupnoMjesta} slobodnih mjesta,\n \tCijena: {parking.Value.CijenaPoSatu:C} po satu\n";
                        }
                        byte[] data = Encoding.UTF8.GetBytes(parkingInfoMessage);
                        tcpClientSocket.Send(data);

                        Console.WriteLine("Informacije o parkingu poslate klijentu.");

                        byte[] zauzeceData = new byte[BUFFER_SIZE];
                        int bytesReceived = tcpClientSocket.Receive(zauzeceData);

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

                                Console.WriteLine($"\nZahtev za zauzimanje prihvaćen!\n" +
                                    $"Zauzima se ... Parking {zauzece.BrojParkinga}, Mjesta {zauzece.BrojMjesta}, Sati {zauzece.BrojSati}");

                                // Slanje potvrde sa ID-om zahteva
                                string potvrdaZauzeca = $"Zahtjev prihvaćen. Jedinstveni ID zahtjeva: {idZahtjeva}.";
                                byte[] potvrdaBajti = Encoding.UTF8.GetBytes(potvrdaZauzeca);
                                tcpClientSocket.Send(potvrdaBajti);

                                // Ispis stanja parkinga nakon zauzimanja
                                Console.WriteLine("\nStanje parkinga nakon zauzimanja:");
                                parkingInfoMessage = "------INFORMACIJE O PARKINGU:------\n";
                                foreach (var parking in parkingInfo)
                                {
                                    parkingInfoMessage += $"\n\t----Parking {parking.Key}:----\n \t{parking.Value.SlobodnoMjesta}/{parking.Value.UkupnoMjesta} slobodnih mjesta,\n \tCijena: {parking.Value.CijenaPoSatu:C} po satu\n";
                                }
                                Console.WriteLine(parkingInfoMessage);

                                byte[] updatedParkingInfo = Encoding.UTF8.GetBytes(parkingInfoMessage);
                                tcpClientSocket.Send(updatedParkingInfo);
                            }
                            else
                            {
                                string greskaZauzeca = "Nema dovoljno slobodnih mjesta za zauzimanje.";
                                byte[] greskaBajti = Encoding.UTF8.GetBytes(greskaZauzeca);
                                tcpClientSocket.Send(greskaBajti);
                            }
                        }
                        else
                        {
                            string pomocni = "Parking sa tim brojem ne postoji.";
                            byte[] pomocniBajti = Encoding.UTF8.GetBytes(pomocni);
                            tcpClientSocket.Send(pomocniBajti);
                        }

                        tcpClientSocket.Close();
                        tcpListener.Stop();
                    }

                    else if (poruka.ToLower().Contains("oslobadjam:"))
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
                    else if (poruka?.ToLower() == "izlaz")
                    {
                        udpSocket.Close();
                        break;
                    }

                    else
                    {
                        // Pogresan unos
                        string greskaPoruka = "Poruka nije odgovarajuća. Pokušajte ponovo.";
                        Console.WriteLine($"- Nepoznata komanda od {posiljaocaEndPoint} : {poruka}");

                        byte[] greskaBajti = Encoding.UTF8.GetBytes(greskaPoruka);
                        udpSocket.SendTo(greskaBajti, posiljaocaEndPoint);

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
