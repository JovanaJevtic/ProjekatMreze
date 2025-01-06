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
        // Dictionary za praćenje zahteva sa njihovim ID-ovima
        private static Dictionary<int, Class1> zahtjevi = new Dictionary<int, Class1>();
        private static int zahtevId = 1000; // Jedinstveni ID za svaki zahtev
        static void Main(string[] args)
        {
            
            Console.WriteLine("Unesite broj parkinga u gradu: ");
            int brojParkinga = int.Parse(Console.ReadLine());


         //  var parkingInfo = new Dictionary<int, (int UkupnoMjesta, int SlobodnoMjesta, decimal CijenaPoSatu)>();

            for (int i = 1; i <= brojParkinga; i++)
            {
                Console.WriteLine($"Unesite broj mjesta za parking {i}: ");
                int ukupnoMjesta = int.Parse(Console.ReadLine());

                Console.WriteLine($"Unesite broj slobodnih mjesta za parking {i}: ");
                int slobodnoMjesta = int.Parse(Console.ReadLine());

                Console.WriteLine($"Unesite cijenu po satu za parking {i}: ");
                decimal cijenaPoSatu = decimal.Parse(Console.ReadLine());

                parkingInfo[i] = (ukupnoMjesta, slobodnoMjesta, cijenaPoSatu);
            }

           

            // UDP socket
            Socket udpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint udpEndPoint = new IPEndPoint(IPAddress.Any, UDP_PORT); // UDP server port
            udpSocket.Bind(udpEndPoint);

            Console.WriteLine($"Server je pokrenut i ceka poruke na : {udpEndPoint}");

            EndPoint posiljaocaEndPoint = new IPEndPoint(IPAddress.Any, 0); // Endpoint za prijem UDP poruka
            byte[] prijemnibuffer = new byte[BUFFER_SIZE];

            try
            {
                while (true)
                {
                    int brojBajta = udpSocket.ReceiveFrom(prijemnibuffer, ref posiljaocaEndPoint);
                    string poruka = Encoding.UTF8.GetString(prijemnibuffer, 0, brojBajta);
                    Console.WriteLine($"Stigla je poruka od {posiljaocaEndPoint} : {poruka}");

                    if (poruka.ToLower() == "prijava")
                    {
                        string odgovor = "Klijent se uspiješno prijavio!";
                        byte[] odgovorBajti = Encoding.UTF8.GetBytes(odgovor);
                        udpSocket.SendTo(odgovorBajti, posiljaocaEndPoint); // Šaljemo potvrdu klijentu

                        Console.WriteLine($"Potvrda o prijavi poslana klijentu.");

                        string tcpDetails = $"TCP IP: {((IPEndPoint)posiljaocaEndPoint).Address}, TCP Port: {TCP_PORT}";
                        byte[] tcpDetailsBajti = Encoding.UTF8.GetBytes(tcpDetails);
                        udpSocket.SendTo(tcpDetailsBajti, posiljaocaEndPoint); // Šaljemo TCP informacije klijentu
                        Console.WriteLine($"Informacije o TCP konekciji poslate klijentu.");

                        //TCP
                        Socket listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        listenSocket.Bind(new IPEndPoint(IPAddress.Any, TCP_PORT));
                        listenSocket.Listen(SOMAXCONN); // Možemo čekati do 15 klijenata

                        Console.WriteLine($"TCP server je pokrenut na portu {TCP_PORT}");

                        // Prihvatamo TCP konekciju
                        Socket clientSocket = listenSocket.Accept();
                        Console.WriteLine($"Klijent povezan - IP:{((IPEndPoint)clientSocket.RemoteEndPoint).Address}, Port: {((IPEndPoint)clientSocket.RemoteEndPoint).Port}");


                        string parkingInfoMessage = "------INFORMACIJE O PARKINGU:------\n";
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
                        Class1 zauzece = Class1.FromByteArray(zauzeceData);

                        // Obrada zahteva: Provera dostupnosti mesta
                        if (parkingInfo.ContainsKey(zauzece.BrojParkinga) &&
                            parkingInfo[zauzece.BrojParkinga].SlobodnoMjesta >= zauzece.BrojMjesta)
                        {
                            // Smanjivanje broja slobodnih mesta
                            parkingInfo[zauzece.BrojParkinga] = (
                                parkingInfo[zauzece.BrojParkinga].UkupnoMjesta,
                                parkingInfo[zauzece.BrojParkinga].SlobodnoMjesta - zauzece.BrojMjesta,
                                parkingInfo[zauzece.BrojParkinga].CijenaPoSatu
                            );

                            int idZahtjeva = zahtevId++;
                            zahtjevi[idZahtjeva] = zauzece;
                           

                            Console.WriteLine($"Zahtev za zauzimanje prihvaćen: Parking {zauzece.BrojParkinga}, Mjesta {zauzece.BrojMjesta}, Sati {zauzece.BrojSati}");

                            // Slanje potvrde sa ID-om zahteva
                            string potvrdaZauzeca = $"Zahtjev prihvaćen. Jedinstveni ID zahtjeva: {idZahtjeva}.";
                            byte[] potvrdaBajti = System.Text.Encoding.UTF8.GetBytes(potvrdaZauzeca);
                            clientSocket.Send(potvrdaBajti);
                        }
                        else
                        {
                            // Nema dovoljno slobodnih mesta
                            string greskaZauzeca = "Nema dovoljno slobodnih mesta za zauzimanje.";
                            byte[] greskaBajti = System.Text.Encoding.UTF8.GetBytes(greskaZauzeca);
                            clientSocket.Send(greskaBajti);
                        }

                        // Zatvori TCP konekciju
                        clientSocket.Close();
                        listenSocket.Close();
                    }
                    else
                    {
                        Console.WriteLine("Poruka nije odgovarajuća. Očekivana poruka je 'prijava'.");
                    }
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Došlo je do greške prilikom prijema poruke: {ex.Message}");
            }

            udpSocket.Close();
            Console.WriteLine("Server završava sa radom.");

        }
    }
}
