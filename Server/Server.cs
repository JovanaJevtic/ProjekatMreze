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
        public const int BUFFER_SIZE = 2000;
        public const int SOMAXCONN = 15;

        private static int zahtevID = 1000;

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
            IPEndPoint udpEndPoint = new IPEndPoint(IPAddress.Any, UDP_PORT);
            udpSocket.Bind(udpEndPoint);
            Console.WriteLine($"\nServer je pokrenut i ceka poruke na : {udpEndPoint}");
            EndPoint posiljaocaEndPoint = new IPEndPoint(IPAddress.Any, 0);
            byte[] prijemnibuffer = new byte[BUFFER_SIZE];

            udpSocket.Blocking = false;

            Socket listenSocketTcp = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listenSocketTcp.Bind(new IPEndPoint(IPAddress.Any, TCP_PORT));
            listenSocketTcp.Listen(SOMAXCONN);
            listenSocketTcp.Blocking = false;

            List<Socket> tcpClients = new List<Socket>();
            tcpClients.Add(listenSocketTcp);
            try
            {
                while (true)
                {
                    List<Socket> checkReadUDP = new List<Socket> { udpSocket };
                    List<Socket> checkErrorUDP = new List<Socket> { udpSocket };

                    List<Socket> checkReadTCP = new List<Socket>(tcpClients);
                    List<Socket> checkErrorTCP = new List<Socket>(tcpClients);

                    Socket.Select(checkReadUDP, null, checkErrorUDP, 1000);
                    Socket.Select(checkReadTCP, null, checkErrorTCP, 1000);

                    if (checkReadUDP.Count > 0)
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
                        }
                        else if (poruka.ToLower().Contains("oslobadjam:"))
                        {
                            if (int.TryParse(poruka.Split(':')[1].Trim(), out int zahtevId))
                            {
                                zahtevId = int.Parse(poruka.Split(':')[1].Trim());

                                if (zahtjevi.ContainsKey(zahtevId))
                                {
                                    var zauzeto = zahtjevi[zahtevId];
                                    decimal racun = zauzeto.BrojSati * zauzeto.BrojMjesta * parkingInfo[zauzeto.BrojParkinga].CijenaPoSatu;

                                    parkingInfo[zauzeto.BrojParkinga] = (
                                        parkingInfo[zauzeto.BrojParkinga].UkupnoMjesta,
                                        parkingInfo[zauzeto.BrojParkinga].SlobodnoMjesta + zauzeto.BrojMjesta,
                                        parkingInfo[zauzeto.BrojParkinga].CijenaPoSatu
                                    );

                                    zahtjevi.Remove(zahtevId);
                                    string odgovor = $"Mjesto je uspiješno oslobodjeno.\n---Račun za zauzeta mjesta iznosi: {racun:C}.---\nSlobodno mjesta na parkingu {zauzeto.BrojParkinga}: {parkingInfo[zauzeto.BrojParkinga].SlobodnoMjesta}";
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
                            else
                            {
                                string odgovor = "Neispravan format zahteva za oslobadjanjem.";
                                byte[] odgovorZahteva = Encoding.UTF8.GetBytes(odgovor);
                                udpSocket.SendTo(odgovorZahteva, posiljaocaEndPoint);
                            }
                        }
                        else if (poruka?.ToLower() == "izlaz")
                        {
                            udpSocket.Close();
                            break;
                        }
                        else if (poruka.ToLower() == "račun potvrdjen.")
                        {
                            Console.WriteLine("Klijent je potvrdio racun.");
                        }
                        else
                        {
                            string greskaPoruka = "Poruka nije odgovarajuća. Pokušajte ponovo.";
                            Console.WriteLine($"- Nepoznata komanda od {posiljaocaEndPoint} : {poruka}");
                            byte[] greskaBajti = Encoding.UTF8.GetBytes(greskaPoruka);
                            udpSocket.SendTo(greskaBajti, posiljaocaEndPoint);
                        }
                    }
                    if (checkErrorUDP.Count > 0)
                    {
                        Console.WriteLine($"Desilo se {checkErrorUDP.Count} gresaka\n");
                        foreach (Socket s in checkErrorUDP)
                        {
                            Console.WriteLine($"Greska na UDP socketu: {s.LocalEndPoint}");
                            Console.WriteLine("Zatvaram socket zbog greske...");
                            s.Close();
                        }
                    }
                    checkErrorUDP.Clear();

                    foreach (Socket s in checkErrorTCP)
                    {
                        Console.WriteLine($"Greska na TCP socketu: {s.LocalEndPoint}");
                        s.Close();
                        tcpClients.Remove(s);
                    }
                    checkErrorTCP.Clear();

                    foreach (Socket clientSocketTcp in checkReadTCP)
                    {
                        if (clientSocketTcp == listenSocketTcp)
                        // da li je socket koji je spreman za citanje zapravo listensockettcp
                        //ako jeste to znaci da novi klijent pokusava da se poveze
                        {
                            try
                            {
                                Socket SocketTcp = listenSocketTcp.Accept();
                                SocketTcp.Blocking = false;
                                tcpClients.Add(SocketTcp);
                                Console.WriteLine($"TCP konekcija uspostavljena sa {SocketTcp.RemoteEndPoint}");

                                string parkingInfoMessage = "\n------ INFORMACIJE O PARKINGU: ------\n";
                                foreach (var parking in parkingInfo)
                                {
                                    parkingInfoMessage += $"\n\t----Parking {parking.Key}:----\n \t{parking.Value.SlobodnoMjesta}/{parking.Value.UkupnoMjesta} slobodnih mjesta,\n \tCijena: {parking.Value.CijenaPoSatu:C} po satu\n";
                                }

                                byte[] data = Encoding.UTF8.GetBytes(parkingInfoMessage);
                                SocketTcp.Send(data);
                                Console.WriteLine("Informacije o parkingu poslate klijentu.");
                            }

                            catch (SocketException ex)
                            {
                                if (ex.SocketErrorCode != SocketError.WouldBlock)
                                {
                                    Console.WriteLine($"TCP Accept greska: {ex.Message}");
                                }
                            }
                        }
                        else
                        {
                            try
                            {
                                byte[] zauzeceData = new byte[BUFFER_SIZE];
                                int bytesReceived = clientSocketTcp.Receive(zauzeceData);

                                if (bytesReceived > 0)
                                {
                                    byte[] primljeniPodaci = new byte[bytesReceived];
                                    Array.Copy(zauzeceData, primljeniPodaci, bytesReceived);

                                    Class zauzece = Class.FromByteArray(primljeniPodaci);

                                    if (parkingInfo.ContainsKey(zauzece.BrojParkinga))
                                    {
                                        var parkinzi = parkingInfo[zauzece.BrojParkinga];
                                        if (parkingInfo[zauzece.BrojParkinga].SlobodnoMjesta >= zauzece.BrojMjesta)
                                        {
                                            parkingInfo[zauzece.BrojParkinga] = (
                                                parkingInfo[zauzece.BrojParkinga].UkupnoMjesta,
                                                parkingInfo[zauzece.BrojParkinga].SlobodnoMjesta - zauzece.BrojMjesta,
                                                parkingInfo[zauzece.BrojParkinga].CijenaPoSatu
                                            );

                                            int idZahtjeva = zahtevID++;
                                            zahtjevi[idZahtjeva] = zauzece;

                                            Console.WriteLine($"\nZahtev za zauzimanje prihvaćen!\n" +
                                            $"Zauzima se ... Parking {zauzece.BrojParkinga}, Mjesta {zauzece.BrojMjesta}, Sati {zauzece.BrojSati}");
                                            string potvrdaZauzeca = $"Zahtjev prihvaćen. Jedinstveni ID zahtjeva: {idZahtjeva}.";
                                            byte[] potvrdaBajti = Encoding.UTF8.GetBytes(potvrdaZauzeca);
                                            clientSocketTcp.Send(potvrdaBajti);

                                            //  šaljemo stvaran broj zauzetih mjesta
                                            byte[] stvarniBrojMjesta = BitConverter.GetBytes(zauzece.BrojMjesta);
                                            clientSocketTcp.Send(stvarniBrojMjesta);

                                            // Ispis stanja parkinga nakon zauzimanja
                                            Console.WriteLine("\nStanje parkinga nakon zauzimanja:");
                                            string parkingInfoMessage = "------ INFORMACIJE O PARKINGU: ------\n";
                                            foreach (var parking in parkingInfo)
                                            {
                                                parkingInfoMessage += $"\n\t----Parking {parking.Key}:----\n \t{parking.Value.SlobodnoMjesta}/{parking.Value.UkupnoMjesta} slobodnih mjesta,\n \tCijena: {parking.Value.CijenaPoSatu:C} po satu\n";
                                            }
                                            Console.WriteLine(parkingInfoMessage);

                                            byte[] updatedParkingInfo = Encoding.UTF8.GetBytes(parkingInfoMessage);
                                            clientSocketTcp.Send(updatedParkingInfo);
                                        }

                                        else if (parkinzi.SlobodnoMjesta == 0)
                                        {
                                            Console.WriteLine($"\nNema slobodnih mjesta na parkingu {zauzece.BrojParkinga}. Zauzimanje nije moguće.");
                                            string porukaNemaMjesta = $"Nema slobodnih mjesta na parkingu {zauzece.BrojParkinga}. Nije moguće zauzeti mjesto. Navratite kasnije.";
                                            byte[] porukaBajti = Encoding.UTF8.GetBytes(porukaNemaMjesta);
                                            clientSocketTcp.Send(porukaBajti);
                                            continue;
                                        }
                                        else
                                        {
                                            // Ako nema dovoljno slobodnih mjesta, zauzima se onoliko mjesta koliko je moguce
                                            int zauzetaMjesta = parkinzi.SlobodnoMjesta;
                                            parkingInfo[zauzece.BrojParkinga] = (
                                                parkinzi.UkupnoMjesta,
                                            0,
                                                parkinzi.CijenaPoSatu
                                            );
                                            zauzece.BrojMjesta = zauzetaMjesta;

                                            int idZahtjeva = zahtevID++;
                                            zahtjevi[idZahtjeva] = zauzece;

                                            Console.WriteLine($"\nZahtjev djelimično prihvacen!\n" +
                                                              $"Zauzima se ... Parking {zauzece.BrojParkinga}, Mjesta {zauzetaMjesta}, Sati {zauzece.BrojSati}");
                                            string potvrdaZauzeca = $"Zahtjev djelimično prihvaćen. Jedinstveni ID zahtjeva: {idZahtjeva}.\n " +
                                                                    $"Zauzeta mjesta: {zauzetaMjesta}. Nema više slobodnih mjesta na parkingu {zauzece.BrojParkinga}.";
                                            byte[] potvrdaBajti = Encoding.UTF8.GetBytes(potvrdaZauzeca);
                                            clientSocketTcp.Send(potvrdaBajti);

                                            //saljemo stvaran broj zauzetih mjesta 
                                            byte[] stvarniBrojMjesta = BitConverter.GetBytes(zauzetaMjesta);
                                            clientSocketTcp.Send(stvarniBrojMjesta);

                                            // Dodajemo slanje ažuriranih informacija o parkingu
                                            string parkingInfoMessage = "------ INFORMACIJE O PARKINGU: ------\n";
                                            foreach (var parking in parkingInfo)
                                            {
                                                parkingInfoMessage += $"\n\t----Parking {parking.Key}:----\n \t{parking.Value.SlobodnoMjesta}/{parking.Value.UkupnoMjesta} slobodnih mjesta,\n \tCijena: {parking.Value.CijenaPoSatu:C} po satu\n";
                                            }
                                            byte[] updatedParkingInfo = Encoding.UTF8.GetBytes(parkingInfoMessage);
                                            clientSocketTcp.Send(updatedParkingInfo);
                                        }
                                    }
                                    else
                                    {
                                        string pomocni = "Parking sa tim brojem ne postoji.";
                                        byte[] pomocniBajti = Encoding.UTF8.GetBytes(pomocni);
                                        clientSocketTcp.Send(pomocniBajti);
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"TCP klijent {clientSocketTcp.RemoteEndPoint} se odjavio.");
                                    clientSocketTcp.Close();
                                    tcpClients.Remove(clientSocketTcp);
                                }
                            }
                            catch (SocketException ex)
                            {
                                if (ex.SocketErrorCode != SocketError.WouldBlock)
                                {
                                    // Console.WriteLine($"TCP socket greska od {clientSocketTcp.RemoteEndPoint}: {ex.Message}");
                                    clientSocketTcp.Close();
                                    tcpClients.Remove(clientSocketTcp);
                                }
                            }
                        }
                    }
                    checkReadTCP.Clear();
                }
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Došlo je do greške prilikom prijema poruke: {ex.Message}");
            }
        }
    }
}
